using CommunityToolkit.Mvvm.Messaging;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Services
{
    /// <summary>
    /// The bookkeeping behind the detached panes: how many windows there may be, what key each one gets,
    /// and what the workspace looks like once one goes away.
    /// </summary>
    public class PopoutHostTests
    {
        private readonly FakePopoutWindowFactory _windows = new();
        private readonly WorkspaceState _state;
        private readonly PopoutHost _host;

        public PopoutHostTests()
        {
            _state = new WorkspaceState(new WeakReferenceMessenger(), new FakeSpellModelService(), new FakeTableCatalog());
            _host  = new PopoutHost(_windows, _state);
        }

        [Fact]
        public void Popping_a_view_out_opens_a_window_and_registers_it()
        {
            string key = _host.Popout(PaneDescriptor.Detail.Id);

            Assert.NotNull(key);
            Assert.Equal(1, _host.OpenCount);
            Assert.True(_windows.Windows[key].IsShown);
            Assert.Contains(_state.Popouts, p => p.Key == key && p.ViewId == PaneDescriptor.Detail.Id);
        }

        [Fact]
        public void Every_pop_out_gets_its_own_key_so_two_of_a_view_can_coexist()
        {
            string first = _host.Popout(PaneDescriptor.Detail.Id);
            string second = _host.Popout(PaneDescriptor.Detail.Id);

            Assert.NotEqual(first, second);
            Assert.StartsWith(PaneDescriptor.Detail.Id, first);
            Assert.Equal(2, _host.OpenCount);
        }

        [Fact]
        public void There_is_a_cap_because_each_window_is_a_browser_of_its_own()
        {
            // Each pop-out is a WebView2 instance costing tens of megabytes, so the cap is a real limit and
            // not a formality - and going over it has to refuse cleanly rather than half-register a window.
            for (int i = 0; i < _host.Cap; i++)
                Assert.NotNull(_host.Popout(PaneDescriptor.Detail.Id));

            Assert.Null(_host.Popout(PaneDescriptor.Detail.Id));

            Assert.Equal(_host.Cap, _host.OpenCount);
            Assert.Equal(_host.Cap, _state.Popouts.Count);
            Assert.Equal(_host.Cap, _windows.Popped.Count);
        }

        [Fact]
        public void A_view_that_is_not_there_is_refused_rather_than_opening_an_empty_window()
        {
            Assert.Null(_host.Popout(null));

            Assert.Equal(0, _host.OpenCount);
            Assert.Empty(_state.Popouts);
            Assert.Empty(_windows.Popped);
        }

        [Fact]
        public void Windows_cascade_so_several_pop_outs_do_not_land_on_top_of_each_other()
        {
            _windows.Anchor = (400, 200);

            string first = _host.Popout(PaneDescriptor.Detail.Id);
            string second = _host.Popout(PaneDescriptor.Effects.Id);

            Assert.Equal(640, _windows.Windows[first].Left);
            Assert.Equal(330, _windows.Windows[first].Top);

            Assert.True(_windows.Windows[second].Left > _windows.Windows[first].Left);
            Assert.True(_windows.Windows[second].Top > _windows.Windows[first].Top);
        }

        // ------------------------------------------------------------------ going away again

        [Fact]
        public void Docking_closes_the_window_and_returns_the_view_to_the_main_one()
        {
            string key = _host.Popout(PaneDescriptor.Detail.Id);

            _host.Dock(key);

            Assert.True(_windows.Windows[key].IsClosed);
            Assert.Equal(0, _host.OpenCount);
            Assert.Empty(_state.Popouts);
            Assert.Contains(PaneDescriptor.Detail.Id, _state.Open);
        }

        [Fact]
        public void Docking_a_key_that_is_not_open_does_nothing()
        {
            _host.Popout(PaneDescriptor.Detail.Id);

            _host.Dock("not-a-window");

            Assert.Equal(1, _host.OpenCount);
            Assert.Single(_state.Popouts);
        }

        [Fact]
        public void Closing_a_pop_out_drops_the_view_rather_than_docking_it_back()
        {
            // Closing says "I am done with this", which is the difference from docking - the view does not
            // reappear as a tab in the main window.
            string key = _host.Popout(PaneDescriptor.Detail.Id);

            _host.Close(key);

            Assert.True(_windows.Windows[key].IsClosed);
            Assert.Equal(0, _host.OpenCount);
            Assert.Empty(_state.Popouts);
            Assert.DoesNotContain(PaneDescriptor.Detail.Id, _state.Open);
        }

        [Fact]
        public void Closing_a_key_that_is_not_open_does_nothing()
        {
            _host.Close("not-a-window");

            Assert.Equal(0, _host.OpenCount);
        }

        [Fact]
        public void A_window_the_user_closes_themselves_unregisters_itself()
        {
            // The close button on the window's own chrome never goes through the host, so the host has to
            // learn about it from the window - otherwise the workspace keeps a pop-out that is not there
            // and the cap counts a window nobody can see.
            string key = _host.Popout(PaneDescriptor.Detail.Id);

            _windows.Windows[key].Close();

            Assert.Equal(0, _host.OpenCount);
            Assert.Empty(_state.Popouts);
        }

        [Fact]
        public void Closing_every_window_leaves_nothing_open()
        {
            _host.Popout(PaneDescriptor.Detail.Id);
            _host.Popout(PaneDescriptor.Effects.Id);

            _host.CloseAll();

            Assert.Equal(0, _host.OpenCount);
            Assert.All(_windows.Windows.Values, w => Assert.True(w.IsClosed));
        }

        [Fact]
        public void Closing_every_window_for_shutdown_keeps_them_in_the_workspace_to_restore()
        {
            // CloseAll is how the app quits with pop-outs open. Each window raising Closed on the way must
            // not unregister it as though the user had closed it, or the save on exit writes no pop-outs
            // and "Restore windows" has nothing to restore.
            _host.Popout(PaneDescriptor.Detail.Id);
            _host.Popout(PaneDescriptor.Effects.Id);

            _host.CloseAll();

            Assert.Equal([PaneDescriptor.Detail.Id, PaneDescriptor.Effects.Id], _state.Popouts.Select(p => p.ViewId));
        }

        [Fact]
        public void A_closed_window_leaves_room_under_the_cap_for_another()
        {
            for (int i = 0; i < _host.Cap; i++)
                _host.Popout(PaneDescriptor.Detail.Id);

            _host.Dock(_state.Popouts[0].Key);

            Assert.NotNull(_host.Popout(PaneDescriptor.Detail.Id));
            Assert.Equal(_host.Cap, _host.OpenCount);
        }
    }
}
