using System.IO;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NexusForever.SpellWorks.Components;
using NexusForever.SpellWorks.Components.Views;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Components
{
    /// <summary>
    /// The flex card in the filter form: picking a column is what creates a constraint, and everything a
    /// curated row can do - negate, remove, pin across blocks - a flex row can do too.
    /// </summary>
    public class FilterViewFlexTests : ComponentTestContext
    {
        private readonly PaneState _pane = new();

        /// <summary>The context the form raises its menus on, kept so a test can read them back.</summary>
        private ShellContext _ctx;

        private IRenderedComponent<FilterView> Form(PaneDescriptor descriptor = null)
        {
            _ctx = Context();

            return RenderUnderContext<FilterView>(_ctx, p => p
                .Add(c => c.Descriptor, descriptor ?? PaneDescriptor.Spell4)
                .Add(c => c.Pane, _pane)
                .Add(c => c.SpellId, 0u)
                .Add(c => c.Applied, () => { }));
        }

        private FilterQuery Q => _pane.Filters;

        private const string Source = FilterFields.EffectsSource;

        private static string Column(string name) => FilterFields.Flex(Source, name);

        // ------------------------------------------------------------------ the card

        [Fact]
        public void The_spell_form_draws_a_card_per_linked_row()
        {
            IRenderedComponent<FilterView> cut = Form();

            Assert.NotEmpty(cut.FindAll("div.group-card.flex-card"));
            Assert.Single(cut.FindAll($"div.group-card.flex-card[data-flex='{Source}']"));
        }

        [Fact]
        public void An_untouched_card_offers_one_blank_row_and_stores_nothing()
        {
            IRenderedComponent<FilterView> cut = Form();

            Assert.Single(cut.FindAll($"div.flex-row[data-flex='{Source}']"));
            Assert.Equal(0, Q.ConditionCount);
        }

        [Fact]
        public void A_blank_row_offers_the_columns_but_no_value_box_until_one_is_picked()
        {
            // Nothing to type into before the row has a subject: an operator and a value with no column
            // would be a control that cannot mean anything yet.
            IRenderedComponent<FilterView> cut = Form();

            Assert.Empty(cut.FindAll($"div.flex-row[data-flex='{Source}'] input"));
            Assert.True(cut.FindAll($"div.flex-row[data-flex='{Source}'] select.col option").Count > 1);
        }

        // ------------------------------------------------------------------ editing

        [Fact]
        public void Picking_a_column_is_what_creates_the_constraint()
        {
            IRenderedComponent<FilterView> cut = Form();

            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);

            FilterCondition condition = Assert.Single(Q.Groups[0].Conditions);
            Assert.Equal(Column("DataBits00"), condition.Field);
            Assert.Equal("", condition.Value);

            // And the card grows a second row, so the next constraint has somewhere to go.
            Assert.Equal(2, cut.FindAll($"div.flex-row[data-flex='{Source}']").Count);
        }

        [Fact]
        public void Typing_a_value_into_a_flex_row_narrows_the_query()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);

            SetFlexValue(cut, Source, "5", row: 0);

            Assert.Equal("5", Assert.Single(Q.Groups[0].Conditions).Value);
        }

        [Fact]
        public void Clearing_a_flex_value_keeps_the_column_the_user_picked()
        {
            // Unlike a curated control, whose only identity is its field: here the row was brought into
            // being by picking a column, and retyping a value should not cost that pick.
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            SetFlexValue(cut, Source, "5", row: 0);

            SetFlexValue(cut, Source, "", row: 0);

            Assert.Equal(Column("DataBits00"), Assert.Single(Q.Groups[0].Conditions).Field);
            Assert.Equal("", Q.Groups[0].Conditions[0].Value);
        }

        [Fact]
        public void A_blank_flex_condition_is_pruned_on_apply_rather_than_persisted()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);

            On(cut, "button.apply-btn", e => e.Click());

            Assert.Equal(0, Q.ConditionCount);
        }

        [Fact]
        public void Clearing_the_column_picker_drops_the_constraint()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            SetFlexValue(cut, Source, "5", row: 0);

            SetFlexColumn(cut, Source, "", row: 0);

            Assert.Equal(0, Q.ConditionCount);
        }

        [Fact]
        public void Repointing_a_row_at_another_column_keeps_what_was_typed()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            SetFlexValue(cut, Source, "5", row: 0);

            SetFlexColumn(cut, Source, Column("DataBits01"), row: 0);

            FilterCondition condition = Assert.Single(Q.Groups[0].Conditions);
            Assert.Equal(Column("DataBits01"), condition.Field);
            Assert.Equal("5", condition.Value);
        }

        [Fact]
        public void Repointing_a_row_coerces_an_operator_the_new_column_cannot_answer()
        {
            // A threshold carried onto a text column would persist as an operator that column has no
            // reading for - the same staleness the persistence mapper coerces rather than keeps.
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            SetFlexValue(cut, Source, "5", row: 0);

            ClickFlex(cut, Source, "div.segmented.op button[title='AtLeast']", row: 0);
            Assert.Equal(FilterOperator.AtLeast, Q.Groups[0].Conditions[0].Operator);

            // EffectType is typed as the enum, so it is offered as text: Contains and Equals, no threshold.
            SetFlexColumn(cut, Source, Column("EffectType"), row: 0);

            Assert.Equal(FilterOperator.Contains, Q.Groups[0].Conditions[0].Operator);
        }

        [Fact]
        public void A_column_keeps_its_operator_when_the_new_one_offers_it_too()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            ClickFlex(cut, Source, "div.segmented.op button[title='MaskAny']", row: 0);

            SetFlexColumn(cut, Source, Column("DataBits01"), row: 0);

            Assert.Equal(FilterOperator.MaskAny, Q.Groups[0].Conditions[0].Operator);
        }

        [Fact]
        public void A_numeric_column_offers_the_thresholds_and_a_text_column_the_two_readings()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);

            Assert.Equal(["=", "≥", "≤", "ALL", "ANY"], Operators(cut));

            SetFlexColumn(cut, Source, Column("EffectType"), row: 0);

            Assert.Equal(["HAS", "="], Operators(cut));
        }

        [Fact]
        public void A_value_the_column_cannot_parse_is_marked_rather_than_discarded()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);

            SetFlexValue(cut, Source, "not a number", row: 0);

            Assert.Single(cut.FindAll($"div.flex-row[data-flex='{Source}'] input.invalid"));
            Assert.Equal("not a number", Q.Groups[0].Conditions[0].Value);
        }

        [Fact]
        public void Several_constraints_stack_in_one_card()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            SetFlexColumn(cut, Source, Column("DataBits01"), row: ^1);

            Assert.Equal([Column("DataBits00"), Column("DataBits01")],
                Q.Groups[0].Conditions.Select(c => c.Field));
        }

        [Fact]
        public void A_card_keeps_adding_constraints_however_many_the_block_holds()
        {
            // The card has no limit, the same as the hand-written fields beside it, so no constraint is
            // refused depending on which control the user reaches for.
            IRenderedComponent<FilterView> cut = Form();

            for (int i = 0; i < 20; i++)
                SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);

            Assert.Equal(20, Q.Groups[0].Conditions.Count);
        }

        // ------------------------------------------------------------------ the shared affordances

        [Fact]
        public void A_flex_row_can_be_negated_removed_and_read_back_in_the_chips()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            SetFlexValue(cut, Source, "5", row: 0);

            ClickFlex(cut, Source, "button.not", row: 0);
            Assert.True(Q.Groups[0].Conditions[0].Negate);

            Assert.Equal("not effects · databits00 = 5", cut.Find("button.chip").TextContent.Trim());

            ClickFlex(cut, Source, "button.remove-condition", row: 0);
            Assert.Equal(0, Q.ConditionCount);
        }

        [Fact]
        public void A_flex_row_can_be_pinned_across_every_block()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            SetFlexValue(cut, Source, "5", row: 0);

            // The pin only appears once there is more than one block to apply across.
            On(cut, "button.add-or-block", e => e.Click());
            ClickFlex(cut, Source, "button.pin-common", row: 0);

            FilterCondition condition = Assert.Single(Q.Common.Conditions);
            Assert.Equal(Column("DataBits00"), condition.Field);
        }

        [Fact]
        public void A_pinned_flex_condition_keeps_its_column_picker_in_the_common_band()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            SetFlexValue(cut, Source, "5", row: 0);
            On(cut, "button.add-or-block", e => e.Click());
            ClickFlex(cut, Source, "button.pin-common", row: 0);

            // A bare value box in the band would show a constraint with no way to see which column it asks.
            Assert.Single(cut.FindAll("div.common-band div.flex-row select.col"));

            SetFlexColumn(cut, Source, Column("DataBits02"), row: 0, block: "common");
            Assert.Equal(Column("DataBits02"), Q.Common.Conditions[0].Field);
        }

        [Fact]
        public void Each_or_block_carries_its_own_cards()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            On(cut, "button.add-or-block", e => e.Click());

            SetFlexColumn(cut, Source, Column("DataBits01"), row: ^1, block: "1");

            Assert.Equal(Column("DataBits00"), Assert.Single(Q.Groups[0].Conditions).Field);
            Assert.Equal(Column("DataBits01"), Assert.Single(Q.Groups[1].Conditions).Field);
        }

        [Fact]
        public void The_curated_cards_are_untouched_by_the_flex_ones()
        {
            // The flex card is an addition, not a replacement: the named fields keep their dropdowns and
            // their bit pickers, and none of the hundreds of columns leak into them.
            IRenderedComponent<FilterView> cut = Form();

            SetField(cut, FilterFields.Class, "Esper", control: "select");

            Assert.Equal("Esper", Assert.Single(Q.On(FilterFields.Class)).Value);
            Assert.Empty(cut.FindAll("div.group-card:not(.flex-card) label.field[data-field^='fx:']"));
        }

        [Fact]
        public void The_effects_pane_offers_the_effect_row_it_lists()
        {
            IRenderedComponent<FilterView> cut = Form(PaneDescriptor.Effects);

            Assert.Single(cut.FindAll($"div.group-card.flex-card[data-flex='{FilterFields.EffectRowSource}']"));
        }

        [Fact]
        public void An_empty_card_does_not_claim_a_line_of_its_own()
        {
            // Ten linked rows, one or two of them asked about: an empty card is a single blank picker, and
            // giving each one a full-width line is most of what made this form several screens tall.
            IRenderedComponent<FilterView> cut = Form();

            Assert.Empty(cut.FindAll("div.flex-card.in-use"));
        }

        [Fact]
        public void A_card_being_used_claims_the_line_so_its_constraints_pack_across_it()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);

            Assert.Single(cut.FindAll($"div.flex-card.in-use[data-flex='{Source}']"));

            // And gives the line back when its last constraint goes.
            ClickFlex(cut, Source, "button.remove-condition", row: 0);
            Assert.Empty(cut.FindAll("div.flex-card.in-use"));
        }

        [Fact]
        public void Only_the_block_that_holds_the_constraint_widens_its_card()
        {
            // Each block draws its own cards, so a constraint in one says nothing about the other's.
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            On(cut, "button.add-or-block", e => e.Click());

            Assert.Single(cut.FindAll($"[data-group='0'] div.flex-card.in-use[data-flex='{Source}']"));
            Assert.Empty(cut.FindAll($"[data-group='1'] div.flex-card.in-use[data-flex='{Source}']"));
        }

        // ------------------------------------------------------------------ promoting a column

        /// <summary>Right-click the nth constraint row of the flex card.</summary>
        private static void RightClickFlexRow(IRenderedComponent<FilterView> cut, int row = 0) =>
            OnNth(cut, $"div.flex-row[data-flex='{Source}']", row,
                e => e.ContextMenu(new MouseEventArgs { ClientX = 10, ClientY = 20 }));

        /// <summary>Right-click the row one field draws, promoted or curated.</summary>
        private static void RightClickField(IRenderedComponent<FilterView> cut, string key) =>
            On(cut, $"label.field[data-field='{key}']", e => e.ContextMenu(new MouseEventArgs()));

        /// <summary>
        /// Run the open menu's only item, then re-render.
        /// </summary>
        /// <remarks>
        /// The re-render stands in for the shell. A menu action notifies <c>WorkspaceState</c> and the
        /// root component re-renders the tree off that, which is how every other menu affordance reaches
        /// the form; a view rendered on its own here has no root above it to do the same.
        /// </remarks>
        private void InvokeMenuItem(IRenderedComponent<FilterView> cut)
        {
            cut.InvokeAsync(() => _ctx.Menu.Items[0].Invoke()).GetAwaiter().GetResult();
            cut.Render();
        }

        /// <summary>Promote from the menu, which is the only way a user can.</summary>
        private void PromoteFromMenu(IRenderedComponent<FilterView> cut, int row = 0)
        {
            RightClickFlexRow(cut, row);
            InvokeMenuItem(cut);
        }

        [Fact]
        public void An_untouched_form_promotes_nothing()
        {
            Assert.Empty(Form().FindAll("div.promoted-card"));
        }

        [Fact]
        public void Right_clicking_a_flex_row_offers_to_promote_its_column()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);

            RightClickFlexRow(cut);

            Assert.Equal("Effects \u00b7 DataBits00", _ctx.Menu.Title);
            Assert.Equal("Promote to its own field", Assert.Single(_ctx.Menu.Items).Label);
        }

        [Fact]
        public void Right_clicking_a_curated_field_offers_nothing()
        {
            // Row draws the hand-written fields too, and a curated field has no column to promote - it is
            // already one. Right-clicking one does nothing.
            IRenderedComponent<FilterView> cut = Form();

            RightClickField(cut, FilterFields.Id);

            Assert.Null(_ctx.Menu);
        }

        [Fact]
        public void Promoting_draws_the_column_as_a_field_of_its_own()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);

            PromoteFromMenu(cut);

            Assert.Equal([Column("DataBits00")], _pane.Promoted);

            IElement promoted = cut.Find("div.promoted-card");
            Assert.Equal("Promoted", promoted.QuerySelector(".group-title").TextContent.Trim());
            Assert.Equal("Effects \u00b7 DataBits00",
                promoted.QuerySelector("label.field > span.label").TextContent.Trim());
        }

        [Fact]
        public void A_promoted_field_is_drawn_in_every_block()
        {
            // That is what being a field means: a promotion belongs to the pane, not to one block.
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            PromoteFromMenu(cut);

            On(cut, "button.add-or-block", e => e.Click());

            Assert.Equal(2, cut.FindAll("div.promoted-card").Count);
            Assert.Single(cut.FindAll(
                $"[data-group='1'] div.promoted-card label.field[data-field='{Column("DataBits00")}']"));
        }

        [Fact]
        public void A_promoted_field_still_takes_a_value_and_an_operator()
        {
            // The column and its table are fixed; everything else about the row still edits.
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            PromoteFromMenu(cut);

            SetField(cut, Column("DataBits00"), "5");
            Assert.Equal("5", Assert.Single(Q.Groups[0].Conditions).Value);

            ClickField(cut, Column("DataBits00"), "div.segmented.op button[title='AtLeast']");
            Assert.Equal(FilterOperator.AtLeast, Q.Groups[0].Conditions[0].Operator);
        }

        [Fact]
        public void A_promoted_column_leaves_the_card_it_was_promoted_from()
        {
            // Drawn in both places it would be two controls editing one condition, and the picker would
            // offer a column that lands somewhere else on the form.
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            SetFlexValue(cut, Source, "5", row: 0);

            PromoteFromMenu(cut);

            Assert.Single(cut.FindAll($"div.flex-row[data-flex='{Source}']"));
            Assert.DoesNotContain(Column("DataBits00"),
                cut.FindAll($"div.flex-card[data-flex='{Source}'] select.col option")
                   .Select(o => o.GetAttribute("value")));
        }

        [Fact]
        public void Promoting_keeps_the_constraint_that_was_already_written()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            SetFlexValue(cut, Source, "5", row: 0);

            PromoteFromMenu(cut);

            FilterCondition condition = Assert.Single(Q.Groups[0].Conditions);
            Assert.Equal(Column("DataBits00"), condition.Field);
            Assert.Equal("5", condition.Value);
            Assert.Equal("5", cut.Find("div.promoted-card label.field input").GetAttribute("value"));
        }

        [Fact]
        public void Demoting_hands_the_column_back_with_its_value_intact()
        {
            // Promotion moves nothing, so neither does demotion - both directions are lossless.
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            SetFlexValue(cut, Source, "5", row: 0);
            PromoteFromMenu(cut);

            RightClickField(cut, Column("DataBits00"));
            Assert.Equal("Promoted field", _ctx.Menu.Sub);
            Assert.Equal("Demote to the column picker", Assert.Single(_ctx.Menu.Items).Label);
            InvokeMenuItem(cut);

            Assert.Empty(_pane.Promoted);
            Assert.Empty(cut.FindAll("div.promoted-card"));
            Assert.Equal("5", Assert.Single(Q.Groups[0].Conditions).Value);
            Assert.Equal("5", cut.Find($"div.flex-row[data-flex='{Source}'] input").GetAttribute("value"));
        }

        [Fact]
        public void A_promotion_is_saved_as_soon_as_it_is_made()
        {
            // Alone among the menu actions: a promotion is a standing decision about the shape of the
            // form, and losing it to a crash would be losing a decision rather than a view.
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);

            PromoteFromMenu(cut);

            Assert.True(File.Exists(Path.Combine(StoreDirectory, "Workspace.json")));
        }

        [Fact]
        public void A_pinned_promoted_condition_keeps_no_column_picker()
        {
            // Its column is fixed everywhere else on the form; a picker in the band would be the one
            // place it could still be changed.
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            SetFlexValue(cut, Source, "5", row: 0);
            PromoteFromMenu(cut);

            On(cut, "button.add-or-block", e => e.Click());
            ClickField(cut, Column("DataBits00"), "button.pin-common");

            Assert.Single(cut.FindAll("div.common-band label.field"));
            Assert.Empty(cut.FindAll("div.common-band div.flex-row"));
        }

        [Fact]
        public void A_preview_count_that_cannot_be_taken_leaves_the_button_saying_something_true()
        {
            // The count on the apply button is the same projection the apply itself runs, taken on the
            // thread pool - so it can fail for the same reasons, and it is started fire-and-forget, which
            // means a failure has nowhere to go unless the button handles it. Otherwise it keeps promising a
            // row count from before the form was edited, which is the one thing this button must not do.
            IRenderedComponent<FilterView> cut = Form();

            // The button is promising a number before anything goes wrong.
            cut.WaitForAssertion(() => Assert.Contains("rows", cut.Find("button.apply-btn").TextContent));

            Filtering.FailNext = true;
            SetField(cut, FilterFields.Id, "7157");

            // No number is better than the one from before the edit: the count that could not be taken is
            // not a count, and the button says so rather than repeating the last one it managed.
            cut.WaitForAssertion(() => Assert.Equal("Apply filter", cut.Find("button.apply-btn").TextContent.Trim()));

            // And the next edit counts again.
            SetField(cut, FilterFields.Id, "7");
            cut.WaitForAssertion(() => Assert.Contains("rows", cut.Find("button.apply-btn").TextContent));
        }

        [Fact]
        public void A_count_that_was_overtaken_and_then_failed_does_not_wipe_the_newer_one()
        {
            // The count is debounced, so a newer edit cancels an older count - but only if it has not started.
            // One already projecting runs on, and if it then fails, its exception must not escape the
            // debounce and blank the button over the newer count that has already landed. A superseded
            // run's failure is as irrelevant as its result.
            IRenderedComponent<FilterView> cut = Form();
            cut.WaitForAssertion(() => Assert.Contains("rows", cut.Find("button.apply-btn").TextContent));

            int counted = Filtering.Calls;
            int completed = Filtering.Completed;
            TaskCompletionSource overtaken = Filtering.Hold(counted + 1);
            Filtering.Fail(counted + 1);

            SetField(cut, FilterFields.Id, "7");
            Until(() => Filtering.Calls == counted + 1);

            // The newer count lands while the older one is still held.
            SetField(cut, FilterFields.Id, "71");
            Until(() => Filtering.Completed == completed + 1);
            cut.WaitForAssertion(() => Assert.Contains("rows", cut.Find("button.apply-btn").TextContent));

            overtaken.SetResult();
            Until(() => Filtering.Finished == counted + 2);
            cut.InvokeAsync(() => { }).GetAwaiter().GetResult();

            Assert.Contains("rows", cut.Find("button.apply-btn").TextContent);
        }

        // ------------------------------------------------------------------ the floating apply

        [Fact]
        public void The_form_carries_a_second_apply_that_floats_within_reach()
        {
            // The form is several screens tall once every linked row has a card, so the count on the foot
            // button is out of sight for most of the scroll.
            IRenderedComponent<FilterView> cut = Form();

            Assert.Single(cut.FindAll("div.filter-float button.apply-btn.float-apply"));
        }

        [Fact]
        public void The_floating_apply_says_what_the_foot_one_says()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            SetFlexValue(cut, Source, "5", row: 0);

            string[] labels = cut.FindAll("button.apply-btn").Select(b => b.TextContent.Trim()).ToArray();

            Assert.Equal(2, labels.Length);
            Assert.Equal(labels[0], labels[1]);
        }

        [Fact]
        public void The_floating_apply_applies()
        {
            bool applied = false;

            IRenderedComponent<FilterView> cut = RenderUnderContext<FilterView>(Context(), p => p
                .Add(c => c.Descriptor, PaneDescriptor.Spell4)
                .Add(c => c.Pane, _pane)
                .Add(c => c.SpellId, 0u)
                .Add(c => c.Applied, () => applied = true));

            SetFlexColumn(cut, Source, Column("DataBits00"), row: ^1);
            SetFlexValue(cut, Source, "5", row: 0);

            On(cut, "button.float-apply", e => e.Click());

            Assert.True(applied);
            Assert.Equal(PaneMode.Rows, _pane.Mode);

            // And it prunes as the foot button does, so the two are the same action rather than two that
            // happen to look alike.
            Assert.Equal(1, Q.ConditionCount);
        }

        private static string[] Operators(IRenderedComponent<FilterView> cut) => cut
            .FindAll($"div.flex-row[data-flex='{Source}'] div.segmented.op button")
            .Select(b => b.TextContent.Trim())
            .ToArray();
    }
}
