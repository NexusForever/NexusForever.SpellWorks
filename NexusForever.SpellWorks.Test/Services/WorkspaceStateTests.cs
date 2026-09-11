using CommunityToolkit.Mvvm.Messaging;
using NexusForever.Game.Static.Spell;
using NexusForever.SpellWorks.Core.Messages;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Services
{
    /// <summary>
    /// The workspace every window agrees on: which views are open, how they are laid out, and what is
    /// selected. Every mutation raises <c>Changed</c>; the no-op paths deliberately do not.
    /// </summary>
    public class WorkspaceStateTests
    {
        private readonly FakeTableCatalog _catalog = new();
        private readonly FakeSpellModelService _models = new();
        private readonly IMessenger _messenger = new WeakReferenceMessenger();

        private WorkspaceState State() => new(_messenger, _models, _catalog);

        private static int CountChanges(WorkspaceState state, Action act)
        {
            var raised = 0;
            void Handler() => raised++;

            state.Changed += Handler;
            act();
            state.Changed -= Handler;

            return raised;
        }

        // ------------------------------------------------------------------ open views

        [Fact]
        public void Opens_with_the_spell_browser_and_the_detail_view()
        {
            WorkspaceState state = State();

            Assert.Equal(["spells", "detail"], state.Open);
            Assert.Equal("spells", state.Active);
            Assert.Equal(LayoutMode.Tabbed, state.Layout);
        }

        [Fact]
        public void Selecting_a_closed_view_opens_and_activates_it()
        {
            WorkspaceState state = State();

            state.SelectView("tables");

            Assert.Contains("tables", state.Open);
            Assert.Equal("tables", state.Active);
        }

        [Fact]
        public void Selecting_an_open_view_activates_it_without_opening_it_twice()
        {
            WorkspaceState state = State();

            state.SelectView("detail");

            Assert.Equal(2, state.Open.Count);
            Assert.Equal("detail", state.Active);
        }

        [Fact]
        public void Activating_a_view_does_not_open_it()
        {
            WorkspaceState state = State();

            state.ActivateOnly("tables");

            Assert.Equal("tables", state.Active);
            Assert.DoesNotContain("tables", state.Open);
        }

        [Fact]
        public void Closing_the_active_view_falls_back_to_the_first_still_open()
        {
            WorkspaceState state = State();

            state.CloseView("spells");

            Assert.Equal("detail", state.Active);
        }

        [Fact]
        public void Closing_an_inactive_view_leaves_the_active_one_alone()
        {
            WorkspaceState state = State();

            state.CloseView("detail");

            Assert.Equal("spells", state.Active);
        }

        [Fact]
        public void Closing_the_last_view_leaves_nothing_active()
        {
            WorkspaceState state = State();

            state.CloseView("spells");
            state.CloseView("detail");

            Assert.Empty(state.Open);
            Assert.Null(state.Active);
        }

        // ------------------------------------------------------------------ reordering

        [Fact]
        public void Moving_a_view_later_accounts_for_its_own_removal()
        {
            WorkspaceState state = State();
            state.SelectView("tables");

            // Dropping "spells" into the slot after "detail" lands it in the middle, not at the end.
            state.MoveView("spells", 2);

            Assert.Equal(["detail", "spells", "tables"], state.Open);
        }

        [Fact]
        public void Moving_a_view_earlier_inserts_it_at_the_slot()
        {
            WorkspaceState state = State();
            state.SelectView("tables");

            state.MoveView("tables", 0);

            Assert.Equal(["tables", "spells", "detail"], state.Open);
        }

        [Fact]
        public void Dropping_a_view_back_where_it_started_changes_nothing()
        {
            WorkspaceState state = State();

            Assert.Equal(0, CountChanges(state, () => state.MoveView("spells", 0)));
            Assert.Equal(0, CountChanges(state, () => state.MoveView("spells", 1)));
            Assert.Equal(["spells", "detail"], state.Open);
        }

        [Fact]
        public void Moving_a_view_that_is_not_open_does_nothing()
        {
            WorkspaceState state = State();

            Assert.Equal(0, CountChanges(state, () => state.MoveView("nope", 0)));
        }

        [Fact]
        public void A_drop_slot_past_the_end_clamps_to_the_last_position()
        {
            WorkspaceState state = State();

            state.MoveView("spells", 99);

            Assert.Equal(["detail", "spells"], state.Open);
        }

        // ------------------------------------------------------------------ layout

        [Fact]
        public void Layout_can_be_switched()
        {
            WorkspaceState state = State();

            state.SetLayout(LayoutMode.SplitPanes);

            Assert.Equal(LayoutMode.SplitPanes, state.Layout);
        }

        [Fact]
        public void Panes_are_evenly_weighted_until_a_seam_is_dragged()
        {
            WorkspaceState state = State();

            Assert.Equal(1d, state.FlexOf("spells"));
        }

        [Fact]
        public void Dragging_a_seam_stores_a_weight_per_pane()
        {
            WorkspaceState state = State();

            state.SetFlexes(["spells", "detail"], [2.5, 0.5]);

            Assert.Equal(2.5, state.FlexOf("spells"));
            Assert.Equal(0.5, state.FlexOf("detail"));
        }

        [Fact]
        public void Extra_weights_beyond_the_open_panes_are_ignored()
        {
            WorkspaceState state = State();

            state.SetFlexes(["spells"], [2.0, 9.0]);

            Assert.Equal(2.0, state.FlexOf("spells"));
            Assert.Equal(1d, state.FlexOf("detail"));
        }

        // ------------------------------------------------------------------ columns

        [Fact]
        public void A_column_keeps_its_projected_width_until_it_is_resized()
        {
            WorkspaceState state = State();
            var column = new GridColumn("Description", "", 300);

            Assert.Equal(300, state.ColumnWidth("spells", column));
        }

        [Fact]
        public void A_resized_column_keeps_its_new_width()
        {
            WorkspaceState state = State();
            var column = new GridColumn("Description", "", 300);

            state.SetColumnWidth("spells", "Description", 560);

            Assert.Equal(560, state.ColumnWidth("spells", column));
        }

        [Fact]
        public void Column_widths_are_kept_per_view()
        {
            WorkspaceState state = State();
            var column = new GridColumn("Id", "id", 84);

            state.SetColumnWidth("spells", "Id", 200);

            Assert.Equal(200, state.ColumnWidth("spells", column));
            Assert.Equal(84, state.ColumnWidth("effects", column));
        }

        [Theory]
        [InlineData(10, WorkspaceState.MinColumnWidth)]
        [InlineData(5000, WorkspaceState.MaxColumnWidth)]
        public void A_column_width_is_clamped_to_something_usable(int requested, int expected)
        {
            WorkspaceState state = State();

            state.SetColumnWidth("spells", "Id", requested);

            Assert.Equal(expected, state.ColumnWidth("spells", new GridColumn("Id", "id", 84)));
        }

        [Fact]
        public void Resetting_a_column_returns_it_to_its_projected_width()
        {
            WorkspaceState state = State();
            var column = new GridColumn("Id", "id", 84);
            state.SetColumnWidth("spells", "Id", 200);

            state.ResetColumnWidth("spells", "Id");

            Assert.Equal(84, state.ColumnWidth("spells", column));
        }

        [Fact]
        public void Resetting_a_column_that_was_never_resized_changes_nothing()
        {
            WorkspaceState state = State();

            Assert.Equal(0, CountChanges(state, () => state.ResetColumnWidth("spells", "Id")));
            Assert.Equal(0, CountChanges(state, () => state.ResetColumnWidth("nope", "Id")));
        }

        // ------------------------------------------------------------------ pinning

        [Fact]
        public void A_view_can_be_pinned_and_unpinned()
        {
            WorkspaceState state = State();

            state.Pin("tbl:Spell4Effects");
            Assert.Equal(["tbl:Spell4Effects"], state.Pinned);

            state.Unpin("tbl:Spell4Effects");
            Assert.Empty(state.Pinned);
        }

        [Fact]
        public void Pinning_the_same_view_twice_pins_it_once()
        {
            WorkspaceState state = State();

            state.Pin("tbl:Spell4Effects");
            state.Pin("tbl:Spell4Effects");

            Assert.Single(state.Pinned);
        }

        // ------------------------------------------------------------------ selection

        [Fact]
        public void Selecting_a_spell_announces_it()
        {
            WorkspaceState state = State();
            uint announced = 0;
            _messenger.Register<SpellSelectedMessage>(this, (_, m) => announced = m.Spell?.Id ?? uint.MaxValue);

            state.Select(42);

            Assert.Equal(42u, state.SelectedSpellId);
            Assert.Equal(uint.MaxValue, announced);
        }

        [Fact]
        public void Reselecting_the_same_spell_is_a_no_op()
        {
            WorkspaceState state = State();
            state.Select(42);

            Assert.Equal(0, CountChanges(state, () => state.Select(42)));
        }

        [Fact]
        public void Selecting_pushes_the_previous_spell_onto_the_history()
        {
            WorkspaceState state = State();

            state.Select(1);
            state.Select(2);

            Assert.Equal([1u], state.History);
        }

        [Fact]
        public void The_first_selection_has_no_history_to_push()
        {
            WorkspaceState state = State();

            state.Select(1);

            Assert.Empty(state.History);
        }

        [Fact]
        public void History_is_capped_so_it_cannot_grow_without_bound()
        {
            WorkspaceState state = State();

            for (uint i = 1; i <= 30; i++)
                state.Select(i);

            Assert.Equal(25, state.History.Count);
            Assert.Equal(29u, state.History[^1]);
        }

        [Fact]
        public void Going_back_returns_to_the_previous_spell()
        {
            WorkspaceState state = State();
            state.Select(1);
            state.Select(2);

            state.Back();

            Assert.Equal(1u, state.SelectedSpellId);
            Assert.Empty(state.History);
        }

        [Fact]
        public void Going_back_with_no_history_does_nothing()
        {
            WorkspaceState state = State();

            Assert.Equal(0, CountChanges(state, state.Back));
        }

        [Fact]
        public void Following_a_hyperlink_selects_without_announcing_a_plain_selection()
        {
            WorkspaceState state = State();
            var followed = 0;
            _messenger.Register<SpellHyperlinkClicked>(this, (_, _) => followed++);

            state.FollowHyperlink(null, PaneDescriptor.Detail.Id);
            Assert.Equal(0, followed);
        }

        // ------------------------------------------------------------------ per-pane state

        [Fact]
        public void A_pane_keeps_its_own_state_per_scope()
        {
            WorkspaceState state = State();

            state.PaneStateFor("spells").Mode = PaneMode.Filter;

            Assert.Equal(PaneMode.Filter, state.PaneStateFor("spells").Mode);
            Assert.Equal(PaneMode.Rows, state.PaneStateFor("effects").Mode);
        }

        [Fact]
        public void The_same_scope_always_gets_the_same_pane_state()
        {
            WorkspaceState state = State();

            Assert.Same(state.PaneStateFor("spells"), state.PaneStateFor("spells"));
        }

        [Fact]
        public void A_locked_pane_keeps_its_spell_while_the_shared_selection_moves_on()
        {
            WorkspaceState state = State();
            state.Select(10);

            state.ToggleLock("spells");
            state.Select(20);

            Assert.Equal(10u, state.SelectedIn("spells"));
            Assert.Equal(20u, state.SelectedIn("detail"));
        }

        [Fact]
        public void Unlocking_a_pane_returns_it_to_the_shared_selection()
        {
            WorkspaceState state = State();
            state.Select(10);
            state.ToggleLock("spells");
            state.Select(20);

            state.ToggleLock("spells");

            Assert.Equal(20u, state.SelectedIn("spells"));
        }

        [Fact]
        public void Opening_the_detail_view_selects_the_spell_and_shows_the_sub_tab()
        {
            WorkspaceState state = State();

            state.OpenDetail(7, DetailSubTab.Procs);

            Assert.Equal(7u, state.SelectedSpellId);
            Assert.Equal("detail", state.Active);
            Assert.Equal(DetailSubTab.Procs, state.PaneStateFor("detail").SubTab);
            Assert.Equal(PaneMode.Rows, state.PaneStateFor("detail").Mode);
        }

        [Fact]
        public void Opening_the_detail_view_without_a_sub_tab_leaves_it_where_it_was()
        {
            WorkspaceState state = State();
            state.PaneStateFor("detail").SubTab = DetailSubTab.Spell;

            state.OpenDetail(7);

            Assert.Equal(DetailSubTab.Spell, state.PaneStateFor("detail").SubTab);
        }

        // ------------------------------------------------------------------ routing past a lock

        [Fact]
        public void Opening_a_spell_while_the_detail_pane_is_locked_gives_it_a_pane_of_its_own()
        {
            // Switching to the locked pane would show the spell it is locked to, so the double-click would
            // look like it had done nothing.
            WorkspaceState state = State();
            state.Select(7);
            state.ToggleLock("detail");

            state.OpenDetail(9, DetailSubTab.Effects);

            Assert.Equal("detail:2", state.Active);
            Assert.Contains("detail:2", state.Open);
            Assert.Equal(9u, state.SelectedIn("detail:2"));
            Assert.Equal(7u, state.SelectedIn("detail"));
            Assert.Equal(DetailSubTab.Effects, state.PaneStateFor("detail:2").SubTab);
        }

        [Fact]
        public void The_pane_that_was_spawned_is_reused_rather_than_spawning_another()
        {
            WorkspaceState state = State();
            state.Select(7);
            state.ToggleLock("detail");

            state.OpenDetail(9);
            state.OpenDetail(11);

            Assert.Equal(["spells", "detail", "detail:2"], state.Open);
            Assert.Equal(11u, state.SelectedIn("detail:2"));
        }

        [Fact]
        public void A_pane_locked_to_the_very_spell_being_opened_is_the_right_pane_for_it()
        {
            WorkspaceState state = State();
            state.Select(7);
            state.ToggleLock("detail");

            state.OpenDetail(7);

            Assert.Equal("detail", state.Active);
            Assert.DoesNotContain("detail:2", state.Open);
        }

        [Fact]
        public void Each_locked_pane_pushes_the_spell_on_to_the_next_number()
        {
            WorkspaceState state = State();
            state.Select(7);
            state.ToggleLock("detail");
            state.OpenDetail(9);
            state.ToggleLock("detail:2");

            state.OpenDetail(11);

            Assert.Equal("detail:3", state.Active);
            Assert.Equal(9u, state.SelectedIn("detail:2"));
            Assert.Equal(11u, state.SelectedIn("detail:3"));
        }

        [Fact]
        public void With_the_primary_pane_closed_an_open_one_is_preferred_over_reopening_it()
        {
            WorkspaceState state = State();
            state.Select(7);
            state.ToggleLock("detail");
            state.OpenDetail(9);
            state.CloseView("detail");

            state.OpenDetail(11);

            Assert.Equal("detail:2", state.Active);
            Assert.DoesNotContain("detail", state.Open);
        }

        [Fact]
        public void Closing_a_locked_pane_releases_its_lock()
        {
            // A lock that outlived its pane would push every later spell into a fresh pane rather than into
            // the one the user has just freed.
            WorkspaceState state = State();
            state.Select(7);
            state.ToggleLock("detail");

            state.CloseView("detail");
            state.OpenDetail(9);

            Assert.Equal("detail", state.Active);
            Assert.DoesNotContain("detail:2", state.Open);
            Assert.Equal(9u, state.SelectedIn("detail"));
        }

        [Fact]
        public void A_spawned_pane_that_was_closed_comes_back_free_of_what_it_used_to_hold()
        {
            // Its id goes back in the pool, so a reopened detail:2 is a different pane and must not
            // inherit the lock, the sub-tab or the filters of the one that was closed.
            WorkspaceState state = State();
            state.Select(7);
            state.ToggleLock("detail");
            state.OpenDetail(9, DetailSubTab.Procs);
            state.ToggleLock("detail:2");
            state.PaneStateFor("detail:2").Filters.Set(FilterFields.Id, "9", FilterOperator.StartsWith);

            state.CloseView("detail:2");
            state.OpenDetail(11, DetailSubTab.Effects);

            Assert.Equal("detail:2", state.Active);
            Assert.DoesNotContain("detail:3", state.Open);
            Assert.Equal(11u, state.SelectedIn("detail:2"));
            Assert.Equal(DetailSubTab.Effects, state.PaneStateFor("detail:2").SubTab);
            Assert.True(string.IsNullOrEmpty(state.PaneStateFor("detail:2").Filters.ValueOf(FilterFields.Id)));
        }

        [Fact]
        public void Closing_a_view_keeps_the_filters_it_was_set_up_with()
        {
            // Only the lock is released - a fixed view reopened is the same view, and losing its filters
            // on every close would be its own annoyance.
            WorkspaceState state = State();
            state.PaneStateFor("spells").Filters.Set(FilterFields.Id, "42", FilterOperator.StartsWith);

            state.CloseView("spells");
            state.SelectView("spells");

            Assert.Equal("42", state.PaneStateFor("spells").Filters.ValueOf(FilterFields.Id));
        }

        [Fact]
        public void Closing_a_popped_out_window_takes_its_pane_state_with_it()
        {
            WorkspaceState state = State();
            state.Select(7);
            state.RegisterPopout("key", "detail");
            state.ToggleLock("key");

            state.UnregisterPopout("key", dockBack: false);

            Assert.Null(state.PaneStateFor("key").LockedSpellId);
        }

        [Fact]
        public void Following_a_hyperlink_moves_the_pane_it_was_clicked_in()
        {
            WorkspaceState state = State();
            state.Select(7);

            state.FollowHyperlink(new TestSpell { Entry = new Spell4Entry { Id = 9 } }, "detail");

            Assert.Equal(9u, state.SelectedSpellId);
            Assert.DoesNotContain("detail:2", state.Open);
        }

        [Fact]
        public void Following_a_hyperlink_out_of_a_locked_pane_gives_the_spell_a_pane_of_its_own()
        {
            // The proxy "open" button, clicked in a pane that cannot move.
            WorkspaceState state = State();
            state.Select(7);
            state.ToggleLock("detail");
            state.PaneStateFor("detail").SubTab = DetailSubTab.Effects;

            state.FollowHyperlink(new TestSpell { Entry = new Spell4Entry { Id = 9 } }, "detail");

            Assert.Equal("detail:2", state.Active);
            Assert.Equal(9u, state.SelectedIn("detail:2"));
            Assert.Equal(7u, state.SelectedIn("detail"));

            // The link was on the Effects tab, so that is where it lands.
            Assert.Equal(DetailSubTab.Effects, state.PaneStateFor("detail:2").SubTab);
        }

        [Fact]
        public void A_hyperlink_followed_out_of_a_locked_popout_still_lands_in_the_main_window()
        {
            WorkspaceState state = State();
            state.Select(7);
            state.RegisterPopout("key", "detail");
            state.ToggleLock("key");

            state.FollowHyperlink(new TestSpell { Entry = new Spell4Entry { Id = 9 } }, "key");

            Assert.Equal(7u, state.SelectedIn("key"));
            Assert.Equal(9u, state.SelectedIn("detail"));
            Assert.Equal("detail", state.Active);
        }

        // ------------------------------------------------------------------ descriptors

        [Fact]
        public void Describes_the_fixed_views()
        {
            WorkspaceState state = State();

            Assert.Equal(PaneDescriptor.Spell4, state.Describe("spells"));
            Assert.Equal(PaneDescriptor.Setup, state.Describe("setup"));
        }

        [Fact]
        public void Describes_a_spawned_detail_pane_by_its_number()
        {
            PaneDescriptor descriptor = State().Describe("detail:2");

            Assert.Equal("detail:2", descriptor.Id);
            Assert.Equal(PaneKind.Detail, descriptor.Kind);
            Assert.Equal("Spell detail 2", descriptor.Title);
            Assert.Equal("Detail 2", descriptor.Label);
            Assert.False(descriptor.CanPin);
        }

        [Fact]
        public void Describes_a_game_table_from_the_catalog()
        {
            _catalog.With("Spell4Effects", ["Id", "SpellId"], [["1", "100"]]);
            WorkspaceState state = State();

            PaneDescriptor descriptor = state.Describe("tbl:Spell4Effects");

            Assert.Equal(PaneKind.GameTable, descriptor.Kind);
            Assert.Equal("Spell4Effects.tbl", descriptor.Title);
            Assert.Equal("Spell4Effects", descriptor.TableName);
            Assert.Contains("1 rows", descriptor.Meta);
        }

        [Fact]
        public void Describes_a_game_table_that_is_not_loaded_yet()
        {
            WorkspaceState state = State();

            Assert.Equal("generic table", state.Describe("tbl:NotLoaded").Meta);
        }

        [Fact]
        public void A_described_game_table_picks_up_a_reload()
        {
            WorkspaceState state = State();
            Assert.Equal("generic table", state.Describe("tbl:Spell4Effects").Meta);

            _catalog.With("Spell4Effects", ["Id"], [["1"]]);
            state.InvalidateDescriptors();

            Assert.Contains("1 rows", state.Describe("tbl:Spell4Effects").Meta);
        }

        [Fact]
        public void An_unknown_view_falls_back_to_the_spell_browser()
        {
            WorkspaceState state = State();

            Assert.Null(state.Describe(null));
            Assert.Equal(PaneDescriptor.Spell4, state.Describe("nonsense"));
        }

        // ------------------------------------------------------------------ effect types

        [Fact]
        public void Picking_an_effect_type_moves_every_unlocked_pane_to_it()
        {
            WorkspaceState state = State();

            Assert.Null(state.EffectTypeIn("effecttype"));
            Assert.Equal(1, CountChanges(state, () => state.SelectEffectType(SpellEffectType.Damage)));

            Assert.Equal(SpellEffectType.Damage, state.SelectedEffectType);
            Assert.Equal(SpellEffectType.Damage, state.EffectTypeIn("effecttype"));

            // Re-picking the same type changes nothing, so it must not wake every pane up.
            Assert.Equal(0, CountChanges(state, () => state.SelectEffectType(SpellEffectType.Damage)));
        }

        [Fact]
        public void A_locked_pane_keeps_its_effect_type_while_the_shared_selection_moves_on()
        {
            WorkspaceState state = State();

            state.SelectEffectType(SpellEffectType.Damage);
            state.ToggleEffectTypeLock("effecttype");
            state.SelectEffectType(SpellEffectType.Heal);

            Assert.Equal(SpellEffectType.Damage, state.EffectTypeIn("effecttype"));
            Assert.Equal(SpellEffectType.Heal, state.EffectTypeIn("effecttype:2"));

            state.ToggleEffectTypeLock("effecttype");

            Assert.Equal(SpellEffectType.Heal, state.EffectTypeIn("effecttype"));
        }

        [Fact]
        public void Opening_an_effect_type_uses_the_primary_pane_when_nothing_is_locked()
        {
            WorkspaceState state = State();

            state.OpenEffectTypeSpells(SpellEffectType.Damage);

            Assert.Equal("effecttype", state.Active);
            Assert.Contains("effecttype", state.Open);
            Assert.Equal(PaneMode.Rows, state.PaneStateFor("effecttype").Mode);
        }

        [Fact]
        public void Opening_an_effect_type_while_the_pane_is_locked_gives_it_a_pane_of_its_own()
        {
            // As with the detail panes: without this the user is switched to a locked pane still showing
            // the type it was locked to, so the double-click looks like it did nothing.
            WorkspaceState state = State();

            state.OpenEffectTypeSpells(SpellEffectType.Damage);
            state.ToggleEffectTypeLock("effecttype");

            state.OpenEffectTypeSpells(SpellEffectType.Heal);

            Assert.Equal("effecttype:2", state.Active);
            Assert.Equal(SpellEffectType.Damage, state.EffectTypeIn("effecttype"));
            Assert.Equal(SpellEffectType.Heal, state.EffectTypeIn("effecttype:2"));
        }

        [Fact]
        public void A_pane_locked_to_the_very_type_being_opened_is_the_right_pane_for_it()
        {
            WorkspaceState state = State();

            state.OpenEffectTypeSpells(SpellEffectType.Damage);
            state.ToggleEffectTypeLock("effecttype");

            state.OpenEffectTypeSpells(SpellEffectType.Damage);

            Assert.Equal("effecttype", state.Active);
            Assert.DoesNotContain("effecttype:2", state.Open);
        }

        [Fact]
        public void Each_locked_pane_pushes_the_effect_type_on_to_the_next_number()
        {
            WorkspaceState state = State();

            state.OpenEffectTypeSpells(SpellEffectType.Damage);
            state.ToggleEffectTypeLock("effecttype");

            state.OpenEffectTypeSpells(SpellEffectType.Heal);
            state.ToggleEffectTypeLock("effecttype:2");

            state.OpenEffectTypeSpells(SpellEffectType.Proxy);

            Assert.Equal("effecttype:3", state.Active);
            Assert.Equal(SpellEffectType.Proxy, state.EffectTypeIn("effecttype:3"));
        }

        [Fact]
        public void Closing_a_locked_effect_type_pane_releases_its_lock()
        {
            WorkspaceState state = State();

            state.OpenEffectTypeSpells(SpellEffectType.Damage);
            state.ToggleEffectTypeLock("effecttype");
            state.CloseView("effecttype");

            // A lock nobody can reach would route every later type around the pane forever.
            Assert.Null(state.PaneStateFor("effecttype").LockedEffectType);
        }

        [Fact]
        public void A_spawned_effect_type_pane_starts_clean_when_its_id_comes_round_again()
        {
            WorkspaceState state = State();

            state.OpenEffectTypeSpells(SpellEffectType.Damage);
            state.ToggleEffectTypeLock("effecttype");
            state.OpenEffectTypeSpells(SpellEffectType.Heal);

            state.PaneStateFor("effecttype:2").Filters.Set(FilterFields.Id, "42", FilterOperator.StartsWith);
            state.CloseView("effecttype:2");

            // The id went back to the pool, so whatever reuses it is a different pane.
            Assert.Equal("", state.PaneStateFor("effecttype:2").Filters.ValueOf(FilterFields.Id));
        }

        [Fact]
        public void A_spawned_effect_type_pane_is_numbered_in_its_title()
        {
            PaneDescriptor descriptor = State().Describe("effecttype:2");

            Assert.Equal("effecttype:2", descriptor.Id);
            Assert.Equal(PaneKind.EffectTypeSpells, descriptor.Kind);
            Assert.Equal("Effect Type spells 2", descriptor.Title);
            Assert.Equal("Type spells 2", descriptor.Label);
        }

        [Fact]
        public void The_primary_effect_type_pane_is_the_fixed_descriptor()
        {
            Assert.Equal(PaneDescriptor.EffectTypeSpells, State().Describe("effecttype"));
            Assert.Equal(PaneDescriptor.EffectTypes, State().Describe("effecttypes"));
        }

        // ------------------------------------------------------------------ pop-outs

        [Fact]
        public void Popping_a_view_out_takes_it_out_of_the_tab_strip()
        {
            WorkspaceState state = State();

            state.RegisterPopout("key", "spells");

            Assert.DoesNotContain("spells", state.Open);
            Assert.Equal("detail", state.Active);
            Assert.Single(state.Popouts);
        }

        [Fact]
        public void Docking_a_popped_out_view_puts_it_back_and_activates_it()
        {
            WorkspaceState state = State();
            state.RegisterPopout("key", "spells");

            state.UnregisterPopout("key", dockBack: true);

            Assert.Contains("spells", state.Open);
            Assert.Equal("spells", state.Active);
            Assert.Empty(state.Popouts);
        }

        [Fact]
        public void Closing_a_popped_out_window_does_not_bring_the_view_back()
        {
            WorkspaceState state = State();
            state.RegisterPopout("key", "spells");

            state.UnregisterPopout("key", dockBack: false);

            Assert.DoesNotContain("spells", state.Open);
            Assert.Empty(state.Popouts);
        }

        [Fact]
        public void Unregistering_a_window_that_was_never_registered_does_nothing()
        {
            WorkspaceState state = State();

            Assert.Equal(0, CountChanges(state, () => state.UnregisterPopout("nope", true)));
        }

        // ------------------------------------------------------------------ notification

        [Fact]
        public void Every_mutation_tells_the_windows_to_re_render()
        {
            WorkspaceState state = State();

            Assert.Equal(1, CountChanges(state, () => state.SelectView("tables")));
            Assert.Equal(1, CountChanges(state, () => state.ActivateOnly("spells")));
            Assert.Equal(1, CountChanges(state, () => state.SetLayout(LayoutMode.SplitPanes)));
            Assert.Equal(1, CountChanges(state, () => state.Pin("tbl:X")));
            Assert.Equal(1, CountChanges(state, () => state.Unpin("tbl:X")));
            Assert.Equal(1, CountChanges(state, () => state.SetColumnWidth("spells", "Id", 120)));
            Assert.Equal(1, CountChanges(state, () => state.ResetColumnWidth("spells", "Id")));
            Assert.Equal(1, CountChanges(state, () => state.CloseView("tables")));
            Assert.Equal(1, CountChanges(state, state.Notify));
        }
    }
}
