using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NexusForever.SpellWorks.Components;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Components
{
    /// <summary>
    /// Drives every binding on the shell the way a user reaches it - a click, a pointer press, a context
    /// menu - and asserts on what the workspace looks like afterwards.
    /// </summary>
    public class ShellTests : ComponentTestContext
    {
        private IRenderedComponent<Shell> Shell() => RenderRoot<Shell>(p => p.Add(c => c.Bridge, Bridge));

        // ------------------------------------------------------------------ window chrome

        [Fact]
        public void Minimize_button_minimises_the_window()
        {
            IRenderedComponent<Shell> cut = Shell();

            On(cut, "button.window-btn[title='Minimize']", e => e.Click());

            Assert.Equal(1, Bridge.MinimizeCount);
        }

        [Fact]
        public void Maximize_button_toggles_maximised()
        {
            IRenderedComponent<Shell> cut = Shell();

            On(cut, "button.window-btn[title='Maximize']", e => e.Click());

            Assert.Equal(1, Bridge.MaximizeCount);
        }

        [Fact]
        public void Close_button_closes_the_window()
        {
            IRenderedComponent<Shell> cut = Shell();

            On(cut, "button.window-btn.close", e => e.Click());

            Assert.Equal(1, Bridge.CloseCount);
        }

        [Fact]
        public void Pressing_the_header_starts_a_window_drag()
        {
            IRenderedComponent<Shell> cut = Shell();

            On(cut, "header.header", e => e.MouseDown(new MouseEventArgs { Detail = 1 }));

            Assert.Equal(1, Bridge.DragCount);
            Assert.Equal(0, Bridge.MaximizeCount);
        }

        [Fact]
        public void Double_pressing_the_header_maximises_instead_of_dragging()
        {
            // DragMove runs a nested WPF message loop that swallows the following click, so the second press
            // is recognised from the event's own click count rather than from a dblclick that never arrives.
            IRenderedComponent<Shell> cut = Shell();

            On(cut, "header.header", e => e.MouseDown(new MouseEventArgs { Detail = 2 }));

            Assert.Equal(1, Bridge.MaximizeCount);
            Assert.Equal(0, Bridge.DragCount);
        }

        // ------------------------------------------------------------------ layout

        [Fact]
        public void Segmented_control_switches_between_tabbed_and_split_panes()
        {
            IRenderedComponent<Shell> cut = Shell();

            OnNth(cut, "div.segmented button", 1, e => e.Click());
            Assert.Equal(LayoutMode.SplitPanes, State.Layout);

            OnNth(cut, "div.segmented button", 0, e => e.Click());
            Assert.Equal(LayoutMode.Tabbed, State.Layout);
        }

        [Fact]
        public void Split_panes_renders_a_seam_between_each_pair_of_panes()
        {
            IRenderedComponent<Shell> cut = Shell();

            OnNth(cut, "div.segmented button", 1, e => e.Click());

            // Two views are open by default, so there is exactly one seam between them.
            Assert.Equal(State.Open.Count - 1, cut.FindAll("div.pane-splitter").Count);
        }

        [Fact]
        public void Tabbed_layout_shows_one_tab_per_open_view()
        {
            IRenderedComponent<Shell> cut = Shell();

            Assert.Equal(State.Open.Count, cut.FindAll("div.tab").Count);
        }

        // ------------------------------------------------------------------ tabs

        [Fact]
        public void Clicking_a_tab_activates_only_that_view()
        {
            IRenderedComponent<Shell> cut = Shell();

            OnNth(cut, "div.tab button.tab-select", 1, e => e.Click());

            Assert.Equal(State.Open[1], State.Active);
        }

        [Fact]
        public void Closing_a_tab_removes_the_view()
        {
            IRenderedComponent<Shell> cut = Shell();
            string closing = State.Open[0];

            OnNth(cut, "div.tab button.tab-close", 0, e => e.Click());

            Assert.DoesNotContain(closing, State.Open);
            Assert.Equal(State.Open.Count, cut.FindAll("div.tab").Count);
        }

        [Fact]
        public void Pressing_a_tab_hands_the_reorder_gesture_to_the_module()
        {
            IRenderedComponent<Shell> cut = Shell();

            OnNth(cut, "div.tab", 1, e => e.PointerDown(new PointerEventArgs { Button = 0, ClientX = 140, ClientY = 30 }));

            IReadOnlyList<object> args = InvocationArgs("beginTabDrag");
            Assert.Equal(State.Open[1], args[1]);
        }

        [Fact]
        public void Middle_clicking_a_tab_closes_it()
        {
            // Anywhere on the tab, not on its × - the × is already one click away and needs aiming.
            IRenderedComponent<Shell> cut = Shell();
            string closing = State.Open[0];

            OnNth(cut, "div.tab", 0, e => e.MouseUp(new MouseEventArgs { Button = 1 }));

            Assert.DoesNotContain(closing, State.Open);
            Assert.Equal(State.Open.Count, cut.FindAll("div.tab").Count);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void Any_other_button_leaves_the_tab_open(long button)
        {
            // The left button selects and drags it; the right one opens its menu.
            IRenderedComponent<Shell> cut = Shell();
            int before = State.Open.Count;

            OnNth(cut, "div.tab", 0, e => e.MouseUp(new MouseEventArgs { Button = button }));

            Assert.Equal(before, State.Open.Count);
        }

        [Fact]
        public void A_non_primary_press_on_a_tab_starts_no_gesture()
        {
            IRenderedComponent<Shell> cut = Shell();

            OnNth(cut, "div.tab", 0, e => e.PointerDown(new PointerEventArgs { Button = 2 }));

            Assert.Equal(0, InvocationCount("beginTabDrag"));
        }

        [Fact]
        public void Right_clicking_a_tab_opens_its_menu()
        {
            IRenderedComponent<Shell> cut = Shell();

            OnNth(cut, "div.tab", 0, e => e.ContextMenu(new MouseEventArgs { ClientX = 40, ClientY = 60 }));

            // ShowMenu refreshes the window through a fire-and-forget InvokeAsync, so the menu reaches
            // the DOM a beat after the event.
            cut.WaitForAssertion(() => Assert.Contains("menu-items", cut.Markup));
        }

        // ------------------------------------------------------------------ rail

        [Fact]
        public void Clicking_a_rail_item_opens_and_activates_that_view()
        {
            IRenderedComponent<Shell> cut = Shell();

            On(cut, "button.rail-item[title='Game tables']", e => e.Click());

            Assert.Equal(PaneDescriptor.Tables.Id, State.Active);
            Assert.Contains(PaneDescriptor.Tables.Id, State.Open);
        }

        [Fact]
        public void Dragging_an_already_pinned_rail_item_offers_to_unpin_it()
        {
            // Only pinned tables reach the rail, so the gesture a rail item starts is always the unpin one.
            Catalog.With("Spell4Effects", "Id");
            State.Pin(PaneDescriptor.GameTableId("Spell4Effects"));

            IRenderedComponent<Shell> cut = Shell();

            On(cut, "button.rail-item[title='Spell4Effects.tbl']",
                e => e.PointerDown(new PointerEventArgs { Button = 0 }));

            IReadOnlyList<object> args = InvocationArgs("beginPinDrag");
            Assert.Equal(PaneDescriptor.GameTableId("Spell4Effects"), args[0]);
            Assert.Equal("unpin", args[1]);
        }

        [Fact]
        public void A_game_table_tab_may_be_dragged_onto_the_rail_to_pin_it()
        {
            Catalog.With("Spell4Effects", "Id");
            State.SelectView(PaneDescriptor.GameTableId("Spell4Effects"));

            IRenderedComponent<Shell> cut = Shell();
            int index = State.Open.IndexOf(PaneDescriptor.GameTableId("Spell4Effects"));

            OnNth(cut, "div.tab", index, e => e.PointerDown(new PointerEventArgs { Button = 0 }));

            // canPin is the third argument, and decides whether a drop on the rail is accepted.
            Assert.Equal(true, InvocationArgs("beginTabDrag")[2]);
        }

        [Fact]
        public void A_fixed_view_tab_may_not_be_pinned()
        {
            IRenderedComponent<Shell> cut = Shell();

            OnNth(cut, "div.tab", 0, e => e.PointerDown(new PointerEventArgs { Button = 0 }));

            Assert.Equal(false, InvocationArgs("beginTabDrag")[2]);
        }

        [Fact]
        public void A_fixed_rail_view_cannot_be_pinned_so_no_gesture_starts()
        {
            IRenderedComponent<Shell> cut = Shell();

            On(cut, "button.rail-item[title='Spell4 browser']", e => e.PointerDown(new PointerEventArgs { Button = 0 }));

            Assert.Equal(0, InvocationCount("beginPinDrag"));
        }

        [Fact]
        public void Right_clicking_a_rail_item_opens_its_menu()
        {
            IRenderedComponent<Shell> cut = Shell();

            On(cut, "button.rail-item[title='Spell detail']",
                e => e.ContextMenu(new MouseEventArgs { ClientX = 10, ClientY = 10 }));

            cut.WaitForAssertion(() => Assert.Contains("menu-items", cut.Markup));
        }

        [Fact]
        public void Pressing_the_shell_dismisses_an_open_menu()
        {
            IRenderedComponent<Shell> cut = Shell();
            OnNth(cut, "div.tab", 0, e => e.ContextMenu(new MouseEventArgs()));
            cut.WaitForAssertion(() => Assert.Contains("menu-items", cut.Markup));

            On(cut, "div.app", e => e.MouseDown(new MouseEventArgs()));

            cut.WaitForAssertion(() => Assert.DoesNotContain("menu-items", cut.Markup));
        }

        // ------------------------------------------------------------------ pop-out and palette

        [Fact]
        public void Pop_out_button_pops_out_the_active_view()
        {
            IRenderedComponent<Shell> cut = Shell();
            string active = State.Active;

            On(cut, "button.accent-btn", e => e.Click());

            // Popping out takes the view off the strip, so the active view moves on afterwards.
            Assert.Equal([active], Windows.Popped);
            Assert.NotEqual(active, State.Active);
        }

        [Fact]
        public void Jump_button_opens_the_command_palette()
        {
            IRenderedComponent<Shell> cut = Shell();

            On(cut, "button.jump-btn", e => e.Click());

            Assert.Contains("palette", cut.Markup);
        }

        // ------------------------------------------------------------------ splitter

        [Fact]
        public void Pressing_a_seam_hands_the_resize_gesture_to_the_module()
        {
            IRenderedComponent<Shell> cut = Shell();
            OnNth(cut, "div.segmented button", 1, e => e.Click());

            On(cut, "div.pane-splitter", e => e.PointerDown(new PointerEventArgs { ClientX = 500 }));

            IReadOnlyList<object> args = InvocationArgs("beginSplitDrag");
            Assert.Equal(0, args[1]);
        }

        // ------------------------------------------------------------------ callbacks from shell.js

        [Fact]
        public void OnSplitDragged_stores_the_new_pane_widths()
        {
            IRenderedComponent<Shell> cut = Shell();
            OnNth(cut, "div.segmented button", 1, e => e.Click());

            cut.Instance.OnSplitDragged([2.5, 0.5]);

            Assert.Equal(2.5, State.FlexOf(State.Open[0]));
            Assert.Equal(0.5, State.FlexOf(State.Open[1]));
        }

        [Fact]
        public void OnTabReordered_moves_the_tab_and_persists_the_order()
        {
            IRenderedComponent<Shell> cut = Shell();
            string first = State.Open[0];

            cut.Instance.OnTabReordered(first, 2);

            Assert.Equal(first, State.Open[1]);
            Assert.True(File.Exists(Path.Combine(StoreDirectory, "Workspace.json")));
        }

        [Fact]
        public void OnPinDropped_pins_and_unpins_a_view()
        {
            IRenderedComponent<Shell> cut = Shell();
            string table = PaneDescriptor.GameTableId("Spell4Effects");

            cut.Instance.OnPinDropped(table, "pin");
            Assert.Contains(table, State.Pinned);

            cut.Instance.OnPinDropped(table, "unpin");
            Assert.DoesNotContain(table, State.Pinned);
        }

        [Fact]
        public async Task OnHotkey_toggles_the_palette_and_escape_closes_it()
        {
            IRenderedComponent<Shell> cut = Shell();

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("palette", false));
            Assert.Contains("palette", cut.Markup);

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("escape", false));
            Assert.DoesNotContain("palette-input", cut.Markup);
        }

        [Fact]
        public async Task OnHotkey_ignores_a_key_it_does_not_handle()
        {
            IRenderedComponent<Shell> cut = Shell();

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("unhandled", false));

            Assert.DoesNotContain("palette-input", cut.Markup);
        }

        [Fact]
        public void OnWindowBlur_dismisses_an_open_menu()
        {
            IRenderedComponent<Shell> cut = Shell();
            OnNth(cut, "div.tab", 0, e => e.ContextMenu(new MouseEventArgs()));
            cut.WaitForAssertion(() => Assert.Contains("menu-items", cut.Markup));

            cut.InvokeAsync(() => cut.Instance.OnWindowBlur()).GetAwaiter().GetResult();

            cut.WaitForAssertion(() => Assert.DoesNotContain("menu-items", cut.Markup));
        }

        [Fact]
        public void OnWindowBlur_with_no_menu_open_does_nothing()
        {
            IRenderedComponent<Shell> cut = Shell();

            cut.InvokeAsync(() => cut.Instance.OnWindowBlur()).GetAwaiter().GetResult();

            Assert.DoesNotContain("menu-items", cut.Markup);
        }

        [Fact]
        public async Task Escape_dismisses_an_open_menu()
        {
            IRenderedComponent<Shell> cut = Shell();
            OnNth(cut, "div.tab", 0, e => e.ContextMenu(new MouseEventArgs()));
            cut.WaitForAssertion(() => Assert.Contains("menu-items", cut.Markup));

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("escape", false));

            cut.WaitForAssertion(() => Assert.DoesNotContain("menu-items", cut.Markup));
        }

        [Fact]
        public async Task The_palette_hotkey_closes_the_palette_it_opened()
        {
            IRenderedComponent<Shell> cut = Shell();

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("palette", false));
            Assert.Contains("palette-input", cut.Markup);

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("palette", false));

            Assert.DoesNotContain("palette-input", cut.Markup);
        }

        [Fact]
        public void Choosing_a_menu_item_closes_the_menu_behind_it()
        {
            // The menu reports its own close back to the shell, which has to re-render without it.
            IRenderedComponent<Shell> cut = Shell();
            OnNth(cut, "div.tab", 0, e => e.ContextMenu(new MouseEventArgs()));
            cut.WaitForAssertion(() => Assert.Contains("menu-items", cut.Markup));

            On(cut, "button.menu-item", e => e.Click());

            cut.WaitForAssertion(() => Assert.DoesNotContain("menu-items", cut.Markup));
        }

        // ------------------------------------------------------------------ chrome content

        [Fact]
        public void Header_reports_the_mounted_archive_and_table_count()
        {
            Catalog.With("Spell4", "Id").With("Spell4Base", "Id");
            Engine.PatchPath = @"C:\WildStar\Patch";

            IRenderedComponent<Shell> cut = Shell();

            Assert.Contains("2 tables", cut.Markup);
        }

        [Fact]
        public void Footer_reports_how_many_windows_are_popped_out()
        {
            IRenderedComponent<Shell> cut = Shell();

            On(cut, "button.accent-btn", e => e.Click());

            Assert.Contains("1 popped-out window(s)", cut.Markup);
        }
    }
}
