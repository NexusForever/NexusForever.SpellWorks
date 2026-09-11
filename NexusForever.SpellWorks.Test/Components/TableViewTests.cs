using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NexusForever.SpellWorks.Components;
using NexusForever.SpellWorks.Components.Views;
using NexusForever.Game.Static.Spell;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Components
{
    /// <summary>
    /// The grid: its search box, its filter button, its rows, and the column-resize gesture.
    /// </summary>
    public class TableViewTests : ComponentTestContext
    {
        private readonly PaneState _pane = new();
        private ShellContext _ctx;

        private IRenderedComponent<TableView> Grid(PaneDescriptor descriptor = null,
            SpellEffectType? effectType = null, bool expectRows = false)
        {
            descriptor ??= PaneDescriptor.Tables;
            _ctx = Context();

            IRenderedComponent<TableView> cut = RenderUnderContext<TableView>(_ctx, p => p
                .Add(c => c.Descriptor, descriptor)
                .Add(c => c.Scope, descriptor.Id)
                .Add(c => c.Pane, _pane)
                .Add(c => c.SpellId, 0u)
                .Add(c => c.EffectType, effectType)
                .Add(c => c.Generation, 0));

            // The projection runs off the render thread, and the grid shows its empty state until it
            // lands - so waiting for "rows or empty" would return before the rows arrive.
            if (Catalog.Tables.Count > 0 || expectRows)
                cut.WaitForState(() => DataRows(cut).Count > 0);
            else
                cut.WaitForState(() => cut.FindAll("thead th").Count > 0 || cut.FindAll(".empty-state").Count > 0);

            return cut;
        }

        // ------------------------------------------------------------------ rendering

        [Fact]
        public void Renders_a_header_per_column_and_a_row_per_entry()
        {
            Catalog.With("Spell4", ["Id", "Description"], [["1", "Arcane"]]);
            Catalog.With("Spell4Base", ["Id"], []);

            IRenderedComponent<TableView> cut = Grid();

            Assert.Equal(4, cut.FindAll("thead th").Count);
            cut.WaitForAssertion(() => Assert.Equal(2, DataRows(cut).Count));
        }

        [Fact]
        public void The_table_states_its_own_width_so_the_columns_cannot_re_measure()
        {
            // table-layout:fixed is ignored for an auto-width table, which would let the columns move as
            // rows scroll past.
            Catalog.With("Spell4", ["Id"], [["1"]]);

            IRenderedComponent<TableView> cut = Grid();

            Assert.Contains("width:", cut.Find("table.grid").GetAttribute("style"));
        }

        [Fact]
        public void An_empty_result_offers_to_reset_the_filters()
        {
            _pane.Filters.Set(FilterFields.TableName, "no-such-table", FilterOperator.StartsWith);

            IRenderedComponent<TableView> cut = Grid();

            Assert.Contains("No rows match this filter set", cut.Markup);
        }

        [Fact]
        public void Resetting_from_the_empty_state_clears_the_filters()
        {
            _pane.Filters.Set(FilterFields.TableName, "no-such-table", FilterOperator.StartsWith);
            IRenderedComponent<TableView> cut = Grid();

            On(cut, ".empty-state button", e => e.Click());

            Assert.False(_pane.Filters.Has(FilterFields.TableName));
        }

        [Fact]
        public void Resetting_from_the_empty_state_clears_the_search_box_too()
        {
            // The empty state offers itself as the remedy for "nothing matched", and the search box empties
            // the grid as readily as the form does. Clearing only the form left the button doing nothing at
            // all in the case the user is most likely to reach it from.
            _pane.Filters.Search = "no-such-table";
            IRenderedComponent<TableView> cut = Grid();

            Assert.Contains("No rows match this filter set", cut.Find(".empty-state").TextContent);

            On(cut, ".empty-state button", e => e.Click());

            Assert.Equal("", _pane.Filters.Search);

            // And the box itself follows, rather than still showing a term that no longer constrains.
            Assert.Equal("", cut.Find(".search-field.text input").GetAttribute("value"));
        }

        [Fact]
        public void Resetting_from_the_empty_state_clears_the_id_box_too()
        {
            // The id box is a separate question, so the reset has to clear it as well.
            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.Spell4);
            _pane.Filters.IdSearch = "999999";

            On(cut, ".empty-state button", e => e.Click());

            Assert.Equal("", _pane.Filters.IdSearch);
        }

        [Fact]
        public void Reports_how_many_rows_survived_the_filter()
        {
            Catalog.With("Spell4", ["Id"], [["1"]]);
            Catalog.With("Creature2", ["Id"], []);

            IRenderedComponent<TableView> cut = Grid();

            Assert.Contains("2 / 2 rows", cut.Markup);
        }

        // ------------------------------------------------------------------ search and filter

        [Fact]
        public void Typing_in_the_search_box_narrows_the_grid()
        {
            Catalog.With("Spell4", ["Id"], []);
            Catalog.With("Creature2", ["Id"], []);
            IRenderedComponent<TableView> cut = Grid();

            On(cut, ".search-field input", e => e.Input("creature"));

            cut.WaitForAssertion(() => Assert.Contains("1 / 2 rows", cut.Markup));
        }

        [Fact]
        public void A_search_overtaken_by_a_newer_one_does_not_land_on_the_grid()
        {
            // The debounce only ever abandons a reload that has not started yet, so two projections can be
            // in flight at once. The older, slower one must not commit last and leave the grid showing rows
            // the search box is not asking for.
            Catalog.With("Spell4", ["Id"], []);
            Catalog.With("Spell4Effects", ["Id"], []);
            Catalog.With("Creature2", ["Id"], []);

            IRenderedComponent<TableView> cut = Grid();
            int projected = Filtering.Calls;

            // Hold the projection the first search starts, then type a narrower search over the top of it.
            TaskCompletionSource overtaken = Filtering.Hold(projected + 1);

            On(cut, ".search-field input", e => e.Input("spell"));
            Until(() => Filtering.Calls == projected + 1);

            On(cut, ".search-field input", e => e.Input("creature"));
            cut.WaitForAssertion(() => Assert.Contains("1 / 3 rows", cut.Markup));

            // Now let the older one finish, behind the newer one it was overtaken by.
            overtaken.SetResult();
            Until(() => Filtering.Completed == projected + 2);

            // One dispatcher turn, so any commit the stale projection queued has run before this is read.
            cut.InvokeAsync(() => { }).GetAwaiter().GetResult();

            Assert.Contains("1 / 3 rows", cut.Markup);
            Assert.Equal("Creature2", DataRows(cut).Single().QuerySelector("td").TextContent.Trim());
        }


        [Fact]
        public void A_projection_that_fails_leaves_the_rows_that_were_there_and_tries_again()
        {
            // A projection runs on the thread pool against engine state the UI thread can replace, so it
            // can fail for reasons the grid can do nothing about - a reload landing underneath it being
            // the one that actually happened. Blowing up the pane is the wrong answer twice over: the rows
            // on screen were fine, and the reload that broke this projection is about to ask for another.
            Catalog.With("Spell4", ["Id"], []);
            Catalog.With("Creature2", ["Id"], []);

            IRenderedComponent<TableView> cut = Grid();
            cut.WaitForAssertion(() => Assert.Contains("2 / 2 rows", cut.Markup));

            Filtering.FailNext = true;
            On(cut, ".search-field input", e => e.Input("creature"));

            // The grid keeps what it had rather than emptying or throwing.
            Until(() => !Filtering.FailNext);
            cut.InvokeAsync(() => { }).GetAwaiter().GetResult();
            Assert.Contains("2 / 2 rows", cut.Markup);

            // And it must not go on believing it is showing the search it never managed to run: the next
            // render - which is what a reload, a selection or any other notification produces - reprojects
            // and catches up. Otherwise the box says "creature" and the grid shows every row, for good.
            cut.Render();

            cut.WaitForAssertion(() => Assert.Contains("1 / 2 rows", cut.Markup));
        }

        [Fact]
        public void A_projection_that_was_overtaken_and_then_failed_leaves_the_newer_one_standing()
        {
            // The recovery for a failed projection clears the signature so the next render retries. A
            // projection that has already been overtaken must not do that, or it wipes the signature the
            // newer, successful projection set and the next render runs the whole projection again.
            Catalog.With("Spell4", ["Id"], []);
            Catalog.With("Spell4Effects", ["Id"], []);
            Catalog.With("Creature2", ["Id"], []);

            IRenderedComponent<TableView> cut = Grid();
            int projected = Filtering.Calls;

            TaskCompletionSource overtaken = Filtering.Hold(projected + 1);
            Filtering.Fail(projected + 1);

            On(cut, ".search-field input", e => e.Input("spell"));
            Until(() => Filtering.Calls == projected + 1);

            On(cut, ".search-field input", e => e.Input("creature"));
            cut.WaitForAssertion(() => Assert.Contains("1 / 3 rows", cut.Markup));

            overtaken.SetResult();
            Until(() => Filtering.Finished == projected + 2);
            cut.InvokeAsync(() => { }).GetAwaiter().GetResult();

            // A render with nothing changed is a render, not a reprojection.
            cut.Render();
            cut.InvokeAsync(() => { }).GetAwaiter().GetResult();

            Assert.Equal(projected + 2, Filtering.Calls);
            Assert.Contains("1 / 3 rows", cut.Markup);
        }

        [Fact]
        public void Changing_the_locale_reprojects_the_grid()
        {
            // A spell's name comes through the text service, so a name search is a question asked in one
            // language. The locale has to be part of what the grid compares to decide whether to
            // reproject, or switching it leaves the grid holding matches made in the language it has just
            // stopped showing - and a projection in flight across the switch matches half its rows in each.
            Text.With("enUS", 1, "Arcane Missile").With("deDE", 1, "Arkanes Geschoss");
            Text.Locale = "enUS";

            Spell(11, "");
            ((TestBase)Models.SpellModels[11].SpellBaseModel).LocalisedName = () => Text.GetText(1);

            _pane.Filters.Search = "Geschoss";

            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.Spell4);
            cut.WaitForAssertion(() => Assert.Contains("0 / 1 rows", cut.Markup));

            // What Setup does, followed by the render the notification it raises produces.
            Text.Locale = "deDE";
            cut.Render();

            cut.WaitForAssertion(() => Assert.Contains("1 / 1 rows", cut.Markup));
        }

        [Fact]
        public void Changing_the_float_tolerance_reprojects_the_grid()
        {
            // The tolerance is an input to the comparison rather than to the query, so a grid already
            // showing a filter has to answer it again - otherwise Setup changes a number that visibly does
            // nothing until the user retypes their filter.
            Spell(11, "Arcane Missile");
            Models.SpellModels[11].Entry.TargetMaxRange = 30f;
            _pane.Filters.And(FilterFields.Flex(FilterFields.SpellSource, "TargetMaxRange"), "24");

            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.Spell4);
            cut.WaitForAssertion(() => Assert.Contains("0 / 1 rows", cut.Markup));

            // Half the larger of the two, which 30 against 24 is well inside.
            State.Preferences.FilterEpsilon = 0.5d;
            // Stands in for the shell, which re-renders every pane off a WorkspaceState notification - which
            // is what Setup raises when a preference is changed.
            cut.Render();

            cut.WaitForAssertion(() => Assert.Contains("1 / 1 rows", cut.Markup));
        }

        [Fact]
        public void The_search_box_shows_what_was_typed()
        {
            IRenderedComponent<TableView> cut = Grid();

            On(cut, ".search-field input", e => e.Input("spell"));

            Assert.Equal("spell", _pane.Filters.Search);
        }

        [Fact]
        public void The_search_bar_offers_a_text_box_an_id_box_and_an_exact_toggle()
        {
            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.Spell4);

            Assert.NotNull(cut.Find(".search-field.text input"));
            Assert.NotNull(cut.Find(".search-field.id input"));
            Assert.NotNull(cut.Find(".exact-toggle input[type=checkbox]"));
        }

        [Fact]
        public void A_pane_with_nothing_numbered_to_search_shows_no_id_box()
        {
            // A table is named, not numbered, so a second box would have nothing to ask.
            IRenderedComponent<TableView> cut = Grid();

            Assert.Empty(cut.FindAll(".search-field.id"));
        }

        [Fact]
        public void The_two_boxes_write_to_their_own_halves_of_the_query()
        {
            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.Spell4);

            On(cut, ".search-field.text input", e => e.Input("arcane"));
            On(cut, ".search-field.id input", e => e.Input("7157"));

            Assert.Equal("arcane", _pane.Filters.Search);
            Assert.Equal("7157", _pane.Filters.IdSearch);
        }

        [Fact]
        public void The_exact_toggle_switches_both_boxes_from_contains_to_whole_value()
        {
            IRenderedComponent<TableView> cut = Grid();
            Assert.False(_pane.Filters.ExactSearch);

            On(cut, ".exact-toggle input", e => e.Change(true));
            Assert.True(_pane.Filters.ExactSearch);

            On(cut, ".exact-toggle input", e => e.Change(false));
            Assert.False(_pane.Filters.ExactSearch);
        }

        [Fact]
        public void The_exact_toggle_says_when_it_is_on()
        {
            IRenderedComponent<TableView> cut = Grid();

            On(cut, ".exact-toggle input", e => e.Change(true));

            cut.WaitForAssertion(() => Assert.Contains("on", cut.Find(".exact-toggle").ClassName));
        }

        [Fact]
        public void An_exact_search_narrows_the_grid_further_than_a_contains()
        {
            Catalog.With("Spell4", ["Id"], []);
            Catalog.With("Spell4Effects", ["Id"], []);
            IRenderedComponent<TableView> cut = Grid();

            On(cut, ".search-field.text input", e => e.Input("Spell4"));
            cut.WaitForAssertion(() => Assert.Contains("2 / 2 rows", cut.Markup));

            On(cut, ".exact-toggle input", e => e.Change(true));
            cut.WaitForAssertion(() => Assert.Contains("1 / 2 rows", cut.Markup));
        }

        [Fact]
        public void Each_box_echoes_its_own_parse()
        {
            IRenderedComponent<TableView> cut = Grid();

            On(cut, ".search-field.text input", e => e.Input("a || b"));

            cut.WaitForAssertion(() =>
            {
                Assert.NotNull(cut.Find(".search-terms[data-search='text']"));
                Assert.Empty(cut.FindAll(".search-terms[data-search='id']"));
            });
        }

        [Fact]
        public void A_plain_search_shows_no_parse_because_there_is_nothing_to_disambiguate()
        {
            IRenderedComponent<TableView> cut = Grid();

            On(cut, ".search-field input", e => e.Input("fire bolt"));

            // Whitespace is not an implicit AND, so this is one literal phrase and reads as one.
            cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".search-terms")));
        }

        [Fact]
        public void An_or_search_echoes_its_parse_back_under_the_box()
        {
            // The grammar is otherwise invisible - this is the text-side equivalent of the form's OR blocks.
            IRenderedComponent<TableView> cut = Grid();

            On(cut, ".search-field input", e => e.Input("7157 || 7158"));

            cut.WaitForAssertion(() =>
            {
                Assert.Single(cut.FindAll(".search-terms .chip-or"));
                Assert.Equal(["7157", "7158"], cut.FindAll(".search-terms .chip.term").Select(c => c.TextContent.Trim()));
            });
        }

        [Fact]
        public void A_search_the_pane_was_opened_with_echoes_its_parse_without_being_retyped()
        {
            // A query arrives already carrying a search - restored from Workspace.json, or simply because
            // the pane was toggled into the filter form and back. The box shows the text either way, so an
            // echo that only appears on a keystroke says the grammar is off when it is still in force.
            _pane.Filters.Search = "7157 || 7158";

            IRenderedComponent<TableView> cut = Grid();

            cut.WaitForAssertion(() =>
            {
                Assert.Single(cut.FindAll(".search-terms .chip-or"));
                Assert.Equal(["7157", "7158"], cut.FindAll(".search-terms .chip.term").Select(c => c.TextContent.Trim()));
            });
        }

        [Fact]
        public void A_negated_search_term_says_so()
        {
            IRenderedComponent<TableView> cut = Grid();

            On(cut, ".search-field input", e => e.Input("!deprecated"));

            cut.WaitForAssertion(() =>
                Assert.Equal("not deprecated", cut.Find(".search-terms .chip.term").TextContent.Trim()));
        }

        [Fact]
        public void The_search_placeholder_names_what_a_view_searches()
        {
            Assert.Contains("Search table name…", Grid(PaneDescriptor.Tables).Markup);
        }

        [Fact]
        public void The_filter_button_switches_the_pane_into_filter_mode()
        {
            IRenderedComponent<TableView> cut = Grid();

            On(cut, "button.outline-btn", e => e.Click());

            Assert.Equal(PaneMode.Filter, _pane.Mode);
        }

        // ------------------------------------------------------------------ rows

        [Fact]
        public void Clicking_a_table_row_opens_that_table()
        {
            Catalog.With("Spell4", ["Id"], [["1"]]);
            IRenderedComponent<TableView> cut = Grid();

            OnRow(cut, 0, row => row.Click(new MouseEventArgs { Detail = 1 }));

            Assert.Equal(PaneDescriptor.GameTableId("Spell4"), State.Active);
        }

        [Fact]
        public void Double_clicking_a_table_row_opens_it_too()
        {
            Catalog.With("Spell4", ["Id"], [["1"]]);
            IRenderedComponent<TableView> cut = Grid();

            OnRow(cut, 0, row => row.Click(new MouseEventArgs { Detail = 2 }));

            Assert.Equal(PaneDescriptor.GameTableId("Spell4"), State.Active);
        }

        // ------------------------------------------------------------------ effect types

        private void Usage(SpellEffectType type, uint[] spellIds) =>
            Models.EffectTypeUsages[type] = new EffectTypeUsage
            {
                Type = type, SpellIds = spellIds, EffectRowCount = spellIds.Length
            };

        private void Spell(uint id, string description) =>
            Models.SpellModels[id] = new TestSpell
            {
                Entry          = new NexusForever.GameTable.Model.Spell4Entry { Id = id },
                Description    = description,
                SpellBaseModel = new TestBase { Entry = new NexusForever.GameTable.Model.Spell4BaseEntry() }
            };

        [Fact]
        public void Double_clicking_an_effect_type_opens_the_spells_behind_it()
        {
            // The whole point of the browser: the reverse lookup is one gesture away from the type.
            Usage(SpellEffectType.Damage, [11]);
            Spell(11, "Arcane Missile");

            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.EffectTypes, expectRows: true);

            OnRow(cut, 0, row => row.Click(new MouseEventArgs { Detail = 2 }));

            Assert.Equal(SpellEffectType.Damage, State.SelectedEffectType);
            Assert.Equal(PaneDescriptor.EffectTypeSpells.Id, State.Active);
        }

        [Fact]
        public void Single_clicking_an_effect_type_only_selects_it()
        {
            Usage(SpellEffectType.Damage, [11]);

            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.EffectTypes, expectRows: true);

            OnRow(cut, 0, row => row.Click(new MouseEventArgs { Detail = 1 }));

            Assert.Equal(SpellEffectType.Damage, State.SelectedEffectType);
            Assert.DoesNotContain(PaneDescriptor.EffectTypeSpells.Id, State.Open);
        }

        [Fact]
        public void Right_clicking_an_effect_type_names_it_and_says_how_many_spells_use_it()
        {
            Usage(SpellEffectType.Damage, [11, 22]);

            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.EffectTypes, expectRows: true);

            OnRow(cut, 0, row => row.ContextMenu(new MouseEventArgs { ClientX = 20, ClientY = 30 }));

            Assert.Equal(nameof(SpellEffectType.Damage), _ctx.Menu.Title);
            Assert.Contains("2 spells", _ctx.Menu.Sub);
        }

        [Fact]
        public void The_spells_behind_an_effect_type_show_their_description_next_to_their_id()
        {
            // An id alone does not say what a spell does, which is what the reverse lookup is being read for.
            Usage(SpellEffectType.Damage, [11]);
            Spell(11, "Arcane Missile");

            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.EffectTypeSpells, SpellEffectType.Damage, expectRows: true);

            IReadOnlyList<IElement> cells = DataRows(cut)[0].QuerySelectorAll("td");

            Assert.Equal("11", cells[0].TextContent.Trim());
            Assert.Equal("Arcane Missile", cells[1].TextContent.Trim());
        }

        [Fact]
        public void Double_clicking_a_spell_behind_an_effect_type_opens_its_detail_pane()
        {
            Usage(SpellEffectType.Damage, [11]);
            Spell(11, "Arcane Missile");

            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.EffectTypeSpells, SpellEffectType.Damage, expectRows: true);

            OnRow(cut, 0, row => row.Click(new MouseEventArgs { Detail = 2 }));

            // The detail pane is the ordinary one, so it coexists with the pane the spell was reached from.
            Assert.Equal(PaneDescriptor.Detail.Id, State.Active);
            Assert.Equal(11u, State.SelectedSpellId);
        }

        [Fact]
        public void An_effect_type_pane_with_nothing_picked_yet_points_at_the_browser()
        {
            Engine.State = EngineState.Ready;

            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.EffectTypeSpells);

            // Blaming the filters here would be wrong - the user has not chosen a type yet.
            Assert.Contains("No effect type selected", cut.Find(".empty-state").TextContent);

            On(cut, ".empty-state button", e => e.Click());

            Assert.Equal(PaneDescriptor.EffectTypes.Id, State.Active);
        }

        // ------------------------------------------------------------------ nothing to show

        [Fact]
        public void An_empty_grid_blames_the_filters_only_when_the_archive_is_actually_loaded()
        {
            Engine.State = EngineState.Ready;

            IRenderedComponent<TableView> cut = Grid();

            Assert.Contains("No rows match this filter set", cut.Find(".empty-state").TextContent);
            Assert.NotNull(cut.Find(".empty-state button"));
        }

        [Fact]
        public void An_empty_grid_says_so_when_the_archive_could_not_be_read()
        {
            // The two look identical on screen and only one of them is the user's doing.
            Engine.State = EngineState.Failed;

            IRenderedComponent<TableView> cut = Grid();

            Assert.Contains("The client archive could not be read", cut.Find(".empty-state").TextContent);
            Assert.DoesNotContain("filter", cut.Find(".empty-state").TextContent);
        }

        [Fact]
        public void An_empty_grid_says_so_when_no_archive_has_been_loaded_yet()
        {
            Engine.State = EngineState.Idle;

            Assert.Contains("No client archive loaded", Grid().Find(".empty-state").TextContent);
        }

        [Fact]
        public void An_empty_grid_says_so_while_the_archive_is_still_being_read()
        {
            Engine.State = EngineState.Loading;

            Assert.Contains("Reading the client archive", Grid().Find(".empty-state").TextContent);
        }

        [Fact]
        public void The_empty_state_offers_the_way_out_of_a_missing_archive()
        {
            Engine.State = EngineState.Failed;
            IRenderedComponent<TableView> cut = Grid();

            On(cut, ".empty-state button", e => e.Click());

            Assert.Equal(PaneDescriptor.Setup.Id, State.Active);
            Assert.True(State.IsBrowsing);
        }

        [Fact]
        public void Right_clicking_a_row_opens_a_menu_describing_it()
        {
            Catalog.With("Spell4", ["Id", "Description"], [["1", "x"]]);
            IRenderedComponent<TableView> cut = Grid();

            OnRow(cut, 0, row => row.ContextMenu(new MouseEventArgs { ClientX = 20, ClientY = 30 }));

            // The menu is owned by the root component, so a pane raises it on the shared context.
            Assert.NotNull(_ctx.Menu);
            Assert.Equal("Spell4.tbl", _ctx.Menu.Title);
            Assert.NotEmpty(_ctx.Menu.Items);
        }

        // ------------------------------------------------------------------ column resize

        [Fact]
        public void Every_header_carries_a_resize_grip()
        {
            Catalog.With("Spell4", ["Id"], [["1"]]);

            IRenderedComponent<TableView> cut = Grid();

            Assert.Equal(cut.FindAll("thead th").Count, cut.FindAll("th .col-grip").Count);
        }

        [Fact]
        public void Pressing_a_grip_hands_the_resize_gesture_to_the_module()
        {
            Catalog.With("Spell4", ["Id"], [["1"]]);
            IRenderedComponent<TableView> cut = Grid();

            OnNth(cut, "th .col-grip", 1, e => e.PointerDown(new PointerEventArgs { Button = 0, ClientX = 300 }));

            // The position for the DOM to move while the pointer is down, the name for the answer that
            // comes back on release, and the width it started at.
            IReadOnlyList<object> args = InvocationArgs("beginColumnResize");
            Assert.Equal(1, args[1]);
            Assert.Equal("Rows", args[2]);
            Assert.Equal(110, args[3]);
        }

        [Fact]
        public void A_non_primary_press_on_a_grip_starts_no_gesture()
        {
            Catalog.With("Spell4", ["Id"], [["1"]]);
            IRenderedComponent<TableView> cut = Grid();

            OnNth(cut, "th .col-grip", 0, e => e.PointerDown(new PointerEventArgs { Button = 2 }));

            Assert.Equal(0, InvocationCount("beginColumnResize"));
        }

        [Fact]
        public void A_resized_column_keeps_its_width_and_widens_the_table()
        {
            Catalog.With("Spell4", ["Id"], [["1"]]);
            IRenderedComponent<TableView> cut = Grid();

            int before = TableWidth(cut);
            cut.InvokeAsync(() => cut.Instance.OnColumnResized("Table", 400)).GetAwaiter().GetResult();

            Assert.Equal(400, State.ColumnWidth(PaneDescriptor.Tables.Id, new GridColumn("Table", "", 260)));
            Assert.True(TableWidth(cut) > before);
        }

        [Fact]
        public void A_resized_column_is_persisted_so_it_survives_a_restart()
        {
            Catalog.With("Spell4", ["Id"], [["1"]]);
            IRenderedComponent<TableView> cut = Grid();

            cut.InvokeAsync(() => cut.Instance.OnColumnResized("Table", 400)).GetAwaiter().GetResult();

            Assert.True(File.Exists(Path.Combine(StoreDirectory, "Workspace.json")));
        }

        [Fact]
        public void Double_clicking_a_grip_returns_the_column_to_its_projected_width()
        {
            Catalog.With("Spell4", ["Id"], [["1"]]);
            IRenderedComponent<TableView> cut = Grid();
            cut.InvokeAsync(() => cut.Instance.OnColumnResized("Table", 400)).GetAwaiter().GetResult();

            OnNth(cut, "th .col-grip", 0, e => e.DoubleClick());

            Assert.Equal(260, State.ColumnWidth(PaneDescriptor.Tables.Id, new GridColumn("Table", "", 260)));
        }

        [Fact]
        public void A_resize_lands_on_the_column_that_was_dragged_even_if_the_table_changed_meanwhile()
        {
            // The gesture is reported once, on release, and the grid can have reprojected in between - a
            // reload that changes a generic table's columns is the way it happens. An ordinal captured at
            // pointerdown would then name a different column on release, and the width the user dragged
            // would be written under the neighbouring column's name.
            Catalog.With("Spell4", ["Id", "Description"], [["1", "Arcane"]]);

            IRenderedComponent<TableView> cut = Grid(State.Describe("tbl:Spell4"), expectRows: true);

            cut.InvokeAsync(() => cut.Instance.OnColumnResized("Description", 400)).GetAwaiter().GetResult();

            Assert.Equal(400, State.ColumnWidth("tbl:Spell4", new GridColumn("Description", "", 128)));
            Assert.Equal(96, State.ColumnWidth("tbl:Spell4", new GridColumn("Id", "id", 96)));
        }

        [Fact]
        public void A_resize_of_a_column_that_is_not_there_is_ignored()
        {
            IRenderedComponent<TableView> cut = Grid();

            cut.InvokeAsync(() =>
            {
                cut.Instance.OnColumnResized(null, 400);
                cut.Instance.OnColumnResized("no such column", 400);
            }).GetAwaiter().GetResult();

            Assert.Empty(State.ColumnWidths);
        }

        /// <summary>Fire an event on a data row inside one dispatcher turn.</summary>
        private static void OnRow(IRenderedComponent<TableView> cut, int index, Action<IElement> fire) =>
            cut.InvokeAsync(() => fire(DataRows(cut)[index])).GetAwaiter().GetResult();

        /// <summary>The rows carrying cells - Virtualize's spacers are bare tr elements.</summary>
        private static IReadOnlyList<IElement> DataRows(IRenderedComponent<TableView> cut) =>
            cut.FindAll("tbody tr").Where(r => r.QuerySelector("td") != null).ToList();

        private static int TableWidth(IRenderedComponent<TableView> cut)
        {
            string style = cut.Find("table.grid").GetAttribute("style");
            return int.Parse(style.Replace("width:", "").Replace("px", "").Trim());
        }
    }
}
