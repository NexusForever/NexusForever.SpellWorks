using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Components;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Components
{
    /// <summary>
    /// One pane's own chrome: the Data/Filter toggle, the pop-out and close buttons, and the Detail
    /// view's sub-tabs.
    /// </summary>
    public class PaneTests : ComponentTestContext
    {
        private ShellContext _ctx;

        private IRenderedComponent<Pane> Render(PaneDescriptor descriptor, bool isPopout = false)
        {
            _ctx = Context(isPopout);

            return RenderUnderContext<Pane>(_ctx, p => p
                .Add(c => c.Descriptor, descriptor)
                .Add(c => c.Scope, descriptor.Id)
                .Add(c => c.Generation, 0));
        }

        [Fact]
        public void A_pane_shows_its_title_and_meta()
        {
            IRenderedComponent<Pane> cut = Render(PaneDescriptor.Spell4);

            Assert.Contains("Spell4 browser", cut.Markup);
        }

        [Fact]
        public void A_popped_out_pane_drops_its_own_chrome_because_the_window_carries_it()
        {
            IRenderedComponent<Pane> cut = Render(PaneDescriptor.Spell4, isPopout: true);

            Assert.Empty(cut.FindAll("div.pane-head"));
        }

        [Fact]
        public void Middle_clicking_the_header_closes_the_view()
        {
            // The header is what a view has instead of a tab in a split layout, and its x is the only way
            // to close one there - so it answers the same gesture the tab strip does.
            // "spells" is one of the two views open by default.
            IRenderedComponent<Pane> cut = Render(PaneDescriptor.Spell4);

            On(cut, "div.pane-head", e => e.MouseUp(new MouseEventArgs { Button = 1 }));

            Assert.DoesNotContain(PaneDescriptor.Spell4.Id, State.Open);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void Any_other_button_on_the_header_leaves_the_view_open(long button)
        {
            IRenderedComponent<Pane> cut = Render(PaneDescriptor.Spell4);

            On(cut, "div.pane-head", e => e.MouseUp(new MouseEventArgs { Button = button }));

            Assert.Contains(PaneDescriptor.Spell4.Id, State.Open);
        }

        [Fact]
        public void The_toggle_switches_between_the_grid_and_the_filter_form()
        {
            IRenderedComponent<Pane> cut = Render(PaneDescriptor.Spell4);

            OnNth(cut, "div.pane-head .segmented button", 1, e => e.Click());
            Assert.Equal(PaneMode.Filter, State.PaneStateFor(PaneDescriptor.Spell4.Id).Mode);

            OnNth(cut, "div.pane-head .segmented button", 0, e => e.Click());
            Assert.Equal(PaneMode.Rows, State.PaneStateFor(PaneDescriptor.Spell4.Id).Mode);
        }

        [Fact]
        public void The_setup_view_has_nothing_to_filter()
        {
            IRenderedComponent<Pane> cut = Render(PaneDescriptor.Setup);

            Assert.Empty(cut.FindAll("div.pane-head .segmented"));
        }

        [Fact]
        public void The_filter_button_carries_a_badge_once_a_constraint_is_set()
        {
            State.PaneStateFor(PaneDescriptor.Spell4.Id).Filters
                .Set(FilterFields.HasProcs, "", FilterOperator.IsSet);

            IRenderedComponent<Pane> cut = Render(PaneDescriptor.Spell4);

            Assert.Contains("badge", cut.Find("div.pane-head .segmented").InnerHtml);
        }

        [Fact]
        public void The_pop_out_button_opens_the_view_in_its_own_window()
        {
            IRenderedComponent<Pane> cut = Render(PaneDescriptor.Spell4);

            On(cut, "button.icon-btn[title='Pop out as window']", e => e.Click());

            Assert.Equal([PaneDescriptor.Spell4.Id], Windows.Popped);
        }

        [Fact]
        public void The_close_button_closes_the_view()
        {
            State.SelectView(PaneDescriptor.Spell4.Id);
            IRenderedComponent<Pane> cut = Render(PaneDescriptor.Spell4);

            On(cut, "button.icon-btn[title='Close view']", e => e.Click());

            Assert.DoesNotContain(PaneDescriptor.Spell4.Id, State.Open);
        }

        // ------------------------------------------------------------------ detail sub-tabs

        [Fact]
        public void The_detail_view_offers_a_sub_tab_per_section()
        {
            IRenderedComponent<Pane> cut = Render(PaneDescriptor.Detail);

            Assert.Equal(Enum.GetValues<DetailSubTab>().Length, cut.FindAll("button.subtab").Count);
        }

        [Fact]
        public void Choosing_a_sub_tab_switches_the_section()
        {
            IRenderedComponent<Pane> cut = Render(PaneDescriptor.Detail);

            OnNth(cut, "button.subtab", 0, e => e.Click());

            Assert.Equal(DetailSubTab.Spell, State.PaneStateFor(PaneDescriptor.Detail.Id).SubTab);
        }

        [Fact]
        public void Back_is_disabled_until_there_is_somewhere_to_go_back_to()
        {
            IRenderedComponent<Pane> cut = Render(PaneDescriptor.Detail);

            Assert.True(cut.Find("button.icon-btn[title='Back to previously viewed spell']").HasAttribute("disabled"));
        }

        [Fact]
        public void Back_returns_to_the_previously_viewed_spell()
        {
            State.Select(1);
            State.Select(2);
            IRenderedComponent<Pane> cut = Render(PaneDescriptor.Detail);

            On(cut, "button.icon-btn[title='Back to previously viewed spell']", e => e.Click());

            Assert.Equal(1u, State.SelectedSpellId);
        }

        [Fact]
        public void Locking_a_pane_holds_it_on_the_current_spell()
        {
            State.Select(10);
            IRenderedComponent<Pane> cut = Render(PaneDescriptor.Detail);

            On(cut, "button.icon-btn[title='Lock this pane to the current spell']", e => e.Click());

            Assert.Equal(10u, State.PaneStateFor(PaneDescriptor.Detail.Id).LockedSpellId);
        }

        // ------------------------------------------------------------------ effect type panes

        [Fact]
        public void An_effect_type_pane_carries_a_lock_but_no_sub_tabs()
        {
            // It is one grid, not three sections - but it still has to be holdable while the browser moves on.
            IRenderedComponent<Pane> cut = Render(PaneDescriptor.EffectTypeSpells);

            Assert.Empty(cut.FindAll("button.subtab"));
            Assert.NotNull(cut.Find("button.icon-btn[title='Lock this pane to the current effect type']"));
        }

        [Fact]
        public void Locking_an_effect_type_pane_holds_it_on_the_current_type()
        {
            State.SelectEffectType(SpellEffectType.Damage);
            IRenderedComponent<Pane> cut = Render(PaneDescriptor.EffectTypeSpells);

            On(cut, "button.icon-btn[title='Lock this pane to the current effect type']", e => e.Click());

            Assert.Equal(SpellEffectType.Damage,
                State.PaneStateFor(PaneDescriptor.EffectTypeSpells.Id).LockedEffectType);
        }

        [Fact]
        public void An_effect_type_pane_says_what_it_is_showing()
        {
            Models.EffectTypeUsages[SpellEffectType.Damage] = new EffectTypeUsage
            {
                Type = SpellEffectType.Damage, SpellIds = [1, 2], EffectRowCount = 5
            };

            Assert.Contains("no effect type selected", Render(PaneDescriptor.EffectTypeSpells).Markup);

            State.SelectEffectType(SpellEffectType.Damage);
            Assert.Contains("2 spells", Render(PaneDescriptor.EffectTypeSpells).Markup);

            // A type the index has never heard of must still render rather than throw.
            State.SelectEffectType(SpellEffectType.Heal);
            Assert.Contains("unused", Render(PaneDescriptor.EffectTypeSpells).Markup);
        }

        [Fact]
        public void The_pane_renders_the_view_its_kind_calls_for()
        {
            Assert.Contains("setup-body", Render(PaneDescriptor.Setup).Markup);
            Assert.Contains("table-view", Render(PaneDescriptor.Spell4).Markup);

            State.PaneStateFor(PaneDescriptor.Spell4.Id).Mode = PaneMode.Filter;
            Assert.Contains("filter-body", Render(PaneDescriptor.Spell4).Markup);
        }
    }

    /// <summary>
    /// The command palette: search, keyboard cursor, and what opening a hit does.
    /// </summary>
    public class PaletteTests : ComponentTestContext
    {
        private int _cursor;
        private int _closed;

        private IRenderedComponent<Palette> Open()
        {
            Palette.Rebuild();

            return RenderUnderContext<Palette>(Context(), p => p
                .Add(c => c.Index, Palette)
                .Add(c => c.Cursor, _cursor)
                .Add(c => c.CursorChanged, c => _cursor = c)
                .Add(c => c.Closed, () => _closed++));
        }

        [Fact]
        public void Lists_the_views_before_anything_is_typed()
        {
            IRenderedComponent<Palette> cut = Open();

            Assert.NotEmpty(cut.FindAll("button.palette-result"));
        }

        [Fact]
        public void Typing_narrows_the_results()
        {
            Catalog.With("Creature2", ["Id"], []);
            IRenderedComponent<Palette> cut = Open();

            On(cut, ".palette-input input", e => e.Input("creature"));

            cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button.palette-result")));
            Assert.Contains("Creature2.tbl", cut.Markup);
        }

        [Fact]
        public void A_query_that_matches_nothing_says_so()
        {
            IRenderedComponent<Palette> cut = Open();

            On(cut, ".palette-input input", e => e.Input("zzzz-nothing"));

            cut.WaitForAssertion(() => Assert.Contains("Nothing matches", cut.Markup));
            Assert.Empty(cut.FindAll("button.palette-result"));
        }

        [Fact]
        public void Opening_a_hit_switches_to_that_view_and_closes_the_palette()
        {
            IRenderedComponent<Palette> cut = Open();
            On(cut, ".palette-input input", e => e.Input("game tables"));
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button.palette-result")));

            // The debounce re-renders, so find and click together or the handler id goes stale.
            On(cut, "button.palette-result", e => e.Click(new MouseEventArgs()));

            cut.WaitForAssertion(() => Assert.Equal(PaneDescriptor.Tables.Id, State.Active));
            Assert.Equal(1, _closed);
        }

        [Fact]
        public void Shift_opening_a_hit_pops_it_out_instead()
        {
            IRenderedComponent<Palette> cut = Open();
            On(cut, ".palette-input input", e => e.Input("game tables"));
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button.palette-result")));

            On(cut, "button.palette-result", e => e.Click(new MouseEventArgs { ShiftKey = true }));

            cut.WaitForAssertion(() => Assert.Equal([PaneDescriptor.Tables.Id], Windows.Popped));
        }

        [Fact]
        public void Opening_an_effect_type_hit_shows_the_spells_behind_it()
        {
            Models.EffectTypeUsages[SpellEffectType.Damage] = new EffectTypeUsage
            {
                Type = SpellEffectType.Damage, SpellIds = [1], EffectRowCount = 1
            };

            IRenderedComponent<Palette> cut = Open();
            On(cut, ".palette-input input", e => e.Input(nameof(SpellEffectType.Damage)));
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button.palette-result")));

            On(cut, "button.palette-result", e => e.Click(new MouseEventArgs()));

            cut.WaitForAssertion(() => Assert.Equal(PaneDescriptor.EffectTypeSpells.Id, State.Active));
            Assert.Equal(SpellEffectType.Damage, State.SelectedEffectType);
        }

        [Fact]
        public void Hovering_a_hit_moves_the_keyboard_cursor_onto_it()
        {
            IRenderedComponent<Palette> cut = Open();

            OnNth(cut, "button.palette-result", 2, e => e.MouseEnter());

            Assert.Equal(2, _cursor);
        }

        [Fact]
        public void The_cursor_is_marked_so_the_keyboard_selection_is_visible()
        {
            _cursor = 1;
            IRenderedComponent<Palette> cut = Open();

            Assert.Contains("cursor", cut.FindAll("button.palette-result")[1].ClassName);
        }

        [Fact]
        public void Pressing_the_backdrop_closes_the_palette()
        {
            IRenderedComponent<Palette> cut = Open();

            On(cut, "div.scrim", e => e.MouseDown());

            Assert.Equal(1, _closed);
        }

        [Fact]
        public void A_press_inside_the_palette_never_reaches_the_backdrop()
        {
            IRenderedComponent<Palette> cut = Open();

            // The body carries only a stopPropagation modifier, so the press is swallowed before the
            // backdrop's close handler can see it.
            Assert.Contains("onmousedown:stoppropagation", cut.Find("div.palette").OuterHtml);
            Assert.Equal(0, _closed);
        }

        [Fact]
        public void The_input_is_focused_so_typing_goes_straight_in()
        {
            Open();

            Assert.Equal(1, InvocationCount("focus"));
        }

        // ------------------------------------------------------------------ spells

        [Fact]
        public void Every_spell_is_in_the_index_by_id_and_by_description()
        {
            Models.SpellModels[7157] = Spell(7157, "Arcane Missile");
            Palette.Rebuild();

            PaletteEntry byId = Assert.Single(Palette.Search("7157"));
            PaletteEntry byDescription = Assert.Single(Palette.Search("arcane missile"));

            Assert.Same(byId, byDescription);
            Assert.Equal("spell", byId.Kind);
            Assert.Equal("7157", byId.Label);
            Assert.Equal(7157u, byId.SpellId);
            Assert.Equal(PaneDescriptor.Detail.Id, byId.ViewId);
        }

        [Fact]
        public void Opening_a_spell_hit_selects_it_and_shows_the_detail_view()
        {
            Models.SpellModels[7157] = Spell(7157, "Arcane Missile");

            IRenderedComponent<Palette> cut = Open();
            On(cut, ".palette-input input", e => e.Input("7157"));
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button.palette-result")));

            On(cut, "button.palette-result", e => e.Click(new MouseEventArgs()));

            cut.WaitForAssertion(() => Assert.Equal(PaneDescriptor.Detail.Id, State.Active));
            Assert.Equal(7157u, State.SelectedSpellId);
            Assert.Equal(1, _closed);
        }

        // ------------------------------------------------------------------ the debounce

        [Fact]
        public async Task Opening_nothing_does_nothing()
        {
            // Enter with an empty result list reaches Run with no entry; it must not close the palette on
            // the way to dereferencing a null.
            IRenderedComponent<Palette> cut = Open();
            string active = State.Active;

            await cut.InvokeAsync(() => cut.Instance.Run(null, false));

            Assert.Equal(0, _closed);
            Assert.Equal(active, State.Active);
            Assert.Empty(Windows.Popped);
        }

        [Fact]
        public void A_search_that_is_overtaken_never_shows_its_results()
        {
            // Each keystroke cancels the one in flight. The abandoned search must not land afterwards and
            // replace what the newer query found - whether it is cancelled inside the delay (which throws)
            // or after it (which returns).
            Catalog.With("Creature2", ["Id"], []);
            IRenderedComponent<Palette> cut = Open();

            On(cut, ".palette-input input", e => e.Input("creature"));
            On(cut, ".palette-input input", e => e.Input("game tables"));

            cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button.palette-result")));
            Assert.Contains("Game tables", cut.Markup);
            Assert.DoesNotContain("Creature2.tbl", cut.Markup);
        }

        [Fact]
        public void A_search_still_in_flight_when_the_palette_closes_is_abandoned()
        {
            IRenderedComponent<Palette> cut = Open();

            On(cut, ".palette-input input", e => e.Input("game tables"));
            cut.InvokeAsync(() => cut.Instance.Dispose()).GetAwaiter().GetResult();

            // Disposing cancels the debounce, so the search never gets as far as moving the cursor.
            Assert.Equal(0, _cursor);
        }

        private static ISpellModel Spell(uint id, string description) => new TestSpell
        {
            Entry          = new Spell4Entry { Id = id },
            Description    = description,
            SpellBaseModel = new TestBase { Entry = new Spell4BaseEntry() }
        };
    }

    /// <summary>
    /// The context menu surface itself.
    /// </summary>
    public class ContextMenuTests : ComponentTestContext
    {
        private int _closed;

        private IRenderedComponent<ContextMenu> Menu(ContextMenuModel model) =>
            RenderUnderContext<ContextMenu>(Context(), p => p
                .Add(c => c.Model, model)
                .Add(c => c.Closed, () => _closed++));

        private static ContextMenuModel Model(params MenuItem[] items) =>
            new() { Title = "Spell", Sub = "Spell4 · 1", Items = [.. items] };

        [Fact]
        public void Nothing_renders_until_a_menu_is_open()
        {
            IRenderedComponent<ContextMenu> cut = Menu(null);

            Assert.Empty(cut.Markup.Trim());
        }

        [Fact]
        public void Renders_a_heading_and_an_item_per_action()
        {
            IRenderedComponent<ContextMenu> cut = Menu(Model(
                new MenuItem("icon", "Open in Detail", "dbl-click", () => { }),
                new MenuItem("icon", "Copy spell id", "1", () => { })));

            Assert.Contains("Spell", cut.Find(".menu-head .title").TextContent);
            Assert.Equal(2, cut.FindAll("button.menu-item").Count);
            Assert.Contains("dbl-click", cut.Markup);
        }

        [Fact]
        public void Choosing_an_item_runs_it_and_closes_the_menu()
        {
            var invoked = false;
            IRenderedComponent<ContextMenu> cut = Menu(Model(
                new MenuItem("icon", "Open in Detail", "", () => invoked = true)));

            On(cut, "button.menu-item", e => e.Click());

            Assert.True(invoked);
            Assert.Equal(1, _closed);
        }

        [Fact]
        public void Hovering_an_item_moves_the_keyboard_cursor_onto_it()
        {
            ContextMenuModel model = Model(
                new MenuItem("icon", "First", "", () => { }),
                new MenuItem("icon", "Second", "", () => { }));

            IRenderedComponent<ContextMenu> cut = Menu(model);

            OnNth(cut, "button.menu-item", 1, e => e.MouseEnter());

            Assert.Equal(1, model.Cursor);
        }

        [Fact]
        public void The_menu_is_measured_and_clamped_inside_the_viewport()
        {
            Menu(Model(new MenuItem("icon", "Only", "", () => { })));

            Assert.Equal(1, InvocationCount("measure"));
            Assert.Equal(1, InvocationCount("menuPosition"));
        }

        [Fact]
        public void A_menu_dismissed_while_it_is_being_measured_does_not_take_the_window_with_it()
        {
            // Placing a menu is two JS round trips - measure, then clamp - so the model the clamped position
            // is written back to has to be captured up front. Escape, a click anywhere, a menu item, or the
            // window losing focus all null the parameter out in between, and reading it afterwards would be
            // a null reference inside OnAfterRenderAsync - a fatal render-tree exception, not a swallowed one.
            var measuring = ShellJs.Setup<Size>("measure", _ => true);

            ContextMenuModel model = Model(new MenuItem("icon", "Only", "", () => { }));
            IRenderedComponent<ContextMenu> cut = Menu(model);

            cut.WaitForState(() => InvocationCount("measure") > 0);

            // The menu is dismissed while the measurement is still in flight.
            cut.Render(p => p.Add(c => c.Model, null).Add(c => c.Closed, () => _closed++));

            measuring.SetResult(new Size(120, 90));

            // The placement simply does not happen: there is nothing left to place.
            cut.WaitForAssertion(() => Assert.False(model.Placed));
        }

        [Fact]
        public void A_menu_that_replaces_one_still_being_measured_is_measured_itself()
        {
            // The other half of capturing the model: abandoning the round trip that no longer matches must
            // not abandon the menu that now does. A second right-click while the first menu is in flight
            // has to end up measured, clamped and on screen.
            var measuring = ShellJs.Setup<Size>("measure", _ => true);

            IRenderedComponent<ContextMenu> cut = Menu(Model(new MenuItem("icon", "First", "", () => { })));
            cut.WaitForState(() => InvocationCount("measure") > 0);

            ContextMenuModel second = Model(new MenuItem("icon", "Second", "", () => { }));
            cut.Render(p => p.Add(c => c.Model, second).Add(c => c.Closed, () => _closed++));

            measuring.SetResult(new Size(120, 90));

            cut.WaitForAssertion(() => Assert.True(second.Placed));
        }

        [Fact]
        public void The_cursor_is_marked_so_the_keyboard_selection_is_visible()
        {
            ContextMenuModel model = Model(
                new MenuItem("icon", "First", "", () => { }),
                new MenuItem("icon", "Second", "", () => { }));
            model.Cursor = 1;

            IRenderedComponent<ContextMenu> cut = Menu(model);

            Assert.Contains("cursor", cut.FindAll("button.menu-item")[1].ClassName);
        }
    }
}
