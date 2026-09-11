using AngleSharp.Dom;
using Bunit;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Components.Views;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Components
{
    /// <summary>
    /// The boolean form: stacking a second value on one field, negating a condition, splitting the query
    /// into OR blocks, and pinning a condition across all of them.
    /// </summary>
    public class FilterViewBooleanTests : ComponentTestContext
    {
        private readonly PaneState _pane = new();

        private IRenderedComponent<FilterView> Form(PaneDescriptor descriptor = null) =>
            RenderUnderContext<FilterView>(Context(), p => p
                .Add(c => c.Descriptor, descriptor ?? PaneDescriptor.Spell4)
                .Add(c => c.Pane, _pane)
                .Add(c => c.SpellId, 0u)
                .Add(c => c.Applied, () => { }));

        private FilterQuery Q => _pane.Filters;

        [Fact]
        public void An_untouched_form_draws_one_block_and_no_or_rule()
        {
            IRenderedComponent<FilterView> cut = Form();

            Assert.Single(cut.FindAll("div.filter-groups"));
            Assert.Empty(cut.FindAll("div.or-divider"));
            Assert.Empty(cut.FindAll("div.common-band"));
        }

        [Fact]
        public void Typing_into_an_untouched_control_is_what_creates_the_condition()
        {
            // Nothing blank is ever stored, so an untouched form saves and signs as empty.
            IRenderedComponent<FilterView> cut = Form();
            Assert.Equal(0, Q.ConditionCount);

            SetField(cut, FilterFields.Id, "7157");

            FilterCondition condition = Assert.Single(Q.Groups[0].Conditions);
            Assert.Equal(FilterFields.Id, condition.Field);
            Assert.Equal("7157", condition.Value);
        }

        [Fact]
        public void Clearing_a_control_drops_the_condition_rather_than_storing_a_blank()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.Id, "7157");

            SetField(cut, FilterFields.Id, "");

            Assert.Equal(0, Q.ConditionCount);
        }

        [Fact]
        public void Adding_a_condition_stacks_a_second_value_on_the_same_field()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.Class, "Esper", control: "select");

            ClickField(cut, FilterFields.Class, "button.add-condition");
            SetField(cut, FilterFields.Class, "Spellslinger", control: "select", index: 1);

            Assert.Equal(["Esper", "Spellslinger"], Q.On(FilterFields.Class).Select(c => c.Value));

            // Both are in one block, so they are AND-ed - which is what the OR block is for instead.
            Assert.Single(Q.Groups);
        }

        [Fact]
        public void Removing_a_stacked_condition_drops_only_that_row()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.Class, "Esper", control: "select");
            ClickField(cut, FilterFields.Class, "button.add-condition");
            SetField(cut, FilterFields.Class, "Spellslinger", control: "select", index: 1);

            // Only the stacked rows carry a remove button; the first row carries the add instead.
            ClickField(cut, FilterFields.Class, "button.remove-condition");

            Assert.Equal(["Esper"], Q.On(FilterFields.Class).Select(c => c.Value));
        }

        [Fact]
        public void Removing_every_condition_leaves_the_query_empty()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.Class, "Esper", control: "select");
            ClickField(cut, FilterFields.Class, "button.add-condition");

            ClickField(cut, FilterFields.Class, "button.remove-condition");
            SetField(cut, FilterFields.Class, FilterFieldSchema.Any, control: "select");

            Assert.True(Q.IsEmpty);
            Assert.Equal(0, Q.ConditionCount);
        }

        [Fact]
        public void A_block_keeps_stacking_conditions_however_many_it_already_holds()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.Class, "Esper", control: "select");

            // A block has no condition limit, so the + button and typing into a field agree with the
            // persistence layer about what a block can hold.
            for (int i = 0; i < 24; i++)
                ClickField(cut, FilterFields.Class, "button.add-condition");

            Assert.Equal(25, Q.Groups[0].Conditions.Count);
        }

        /// <summary>
        /// Every text field the Spell4 form draws, in one block.
        /// </summary>
        private static readonly string[] spell4TextFields =
        [
            FilterFields.Id, FilterFields.Name, FilterFields.Tooltip,
            FilterFields.CastTime, FilterFields.Duration, FilterFields.Cooldown,
            FilterFields.ChannelTime, FilterFields.ChannelPulse,
            FilterFields.Tier, FilterFields.AbilityCharges,
            FilterFields.TargetMinRange, FilterFields.TargetMaxRange,
            FilterFields.TargetVerticalRange, FilterFields.MissileSpeed,
            FilterFields.EffectCount, FilterFields.ProcCount, FilterFields.ProcReferenceCount
        ];

        [Fact]
        public void A_block_holds_a_condition_on_every_field_the_form_offers()
        {
            // Typing into a field creates its condition, so filling in the whole card builds a block with
            // one condition per field.
            IRenderedComponent<FilterView> cut = Form();

            foreach (string field in spell4TextFields)
                SetField(cut, field, "1");

            Assert.Equal(spell4TextFields.Length, Q.Groups[0].Conditions.Count);
            Assert.Equal(spell4TextFields, Q.Groups[0].Conditions.Select(c => c.Field));
        }

        [Fact]
        public void Typing_nothing_into_an_empty_control_stores_nothing()
        {
            // Distinct from clearing a control that had a value: there is no condition here to remove, and
            // a blank one would show up in the chips row as a constraint the user never set.
            IRenderedComponent<FilterView> cut = Form();

            SetField(cut, FilterFields.Id, "   ");

            Assert.Equal(0, Q.ConditionCount);
        }

        [Fact]
        public void Every_operator_a_field_offers_is_labelled_by_its_sign()
        {
            // Data bits offer an exact value as well as both mask readings. Equals is written "=" here as
            // it is in the chips row - a segmented control is too narrow for a word, which is why the flex
            // rows, where Equals and Contains sit side by side, needed signs for both.
            IRenderedComponent<FilterView> cut = Form(PaneDescriptor.Effects);
            SetField(cut, FilterFields.EffectData(0), "0x6");

            string[] labels = cut
                .FindAll($"[data-field='{FilterFields.EffectData(0)}'] div.segmented.op button")
                .Select(b => b.TextContent.Trim())
                .ToArray();

            Assert.Equal(["=", "ALL", "ANY"], labels);
        }

        [Theory]
        [InlineData(FilterFields.Deprecated)]
        [InlineData(FilterFields.TestSpell)]
        public void A_housekeeping_toggle_starts_negated_so_switching_it_on_hides_them(string key)
        {
            // Both are phrased positively - Deprecated selects the deprecated spells - but the reading
            // anybody wants is the negative one, so the form seeds them denied. Getting this backwards
            // makes the toggle select exactly the rows it is meant to hide, which reads as a working
            // filter until the count is looked at.
            IRenderedComponent<FilterView> cut = Form();

            ClickField(cut, key, "button.toggle");

            Assert.True(Q.On(key).Single().Negate);
            Assert.Equal($"not {Label(cut, key)}", cut.Find("button.chip").TextContent.Trim());
        }

        [Fact]
        public void An_ordinary_toggle_starts_asserted()
        {
            IRenderedComponent<FilterView> cut = Form();

            ClickField(cut, FilterFields.HasProcs, "button.toggle");

            Assert.False(Q.On(FilterFields.HasProcs).Single().Negate);
        }

        private static string Label(IRenderedComponent<FilterView> cut, string key) =>
            cut.Find($"label.field[data-field='{key}'] span.label").TextContent.Trim().ToLowerInvariant();

        [Fact]
        public void Negating_a_condition_flips_only_that_one()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.Id, "7157");
            SetField(cut, FilterFields.Class, "Esper", control: "select");

            ClickField(cut, FilterFields.Class, "button.not");

            Assert.False(Q.On(FilterFields.Id).Single().Negate);
            Assert.True(Q.On(FilterFields.Class).Single().Negate);
        }

        [Fact]
        public void A_negated_condition_reads_back_in_the_chips()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.Class, "Esper", control: "select");
            ClickField(cut, FilterFields.Class, "button.not");

            Assert.Equal("not class = Esper", cut.Find("button.chip").TextContent.Trim());
        }

        [Fact]
        public void Adding_an_or_block_splits_the_form_and_draws_the_rule()
        {
            IRenderedComponent<FilterView> cut = Form();

            On(cut, "button.add-or-block", e => e.Click());

            Assert.Equal(2, cut.FindAll("div.filter-groups").Count);
            Assert.Single(cut.FindAll("div.or-divider"));
            Assert.Equal(2, Q.Groups.Count);
        }

        [Fact]
        public void A_second_block_carries_its_own_conditions()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.School, "Magic", control: "select");

            On(cut, "button.add-or-block", e => e.Click());
            SetField(cut, FilterFields.School, "Physical", control: "select", block: 1);

            Assert.Equal("Magic", Q.Groups[0].Conditions.Single().Value);
            Assert.Equal("Physical", Q.Groups[1].Conditions.Single().Value);
        }

        [Fact]
        public void The_chips_row_rules_off_between_blocks()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.School, "Magic", control: "select");
            On(cut, "button.add-or-block", e => e.Click());
            SetField(cut, FilterFields.School, "Physical", control: "select", block: 1);

            Assert.Single(cut.FindAll("span.chip-or"));
            Assert.Equal(2, cut.FindAll("button.chip").Count);
        }

        [Fact]
        public void The_header_states_the_shape_the_badge_cannot()
        {
            // Five conditions across two blocks can return more rows than three in one, so a bare count is
            // not enough on its own.
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.School, "Magic", control: "select");
            On(cut, "button.add-or-block", e => e.Click());
            SetField(cut, FilterFields.School, "Physical", control: "select", block: 1);

            Assert.Equal("2 blocks · 2 conditions", cut.Find("span.shape").TextContent.Trim());
        }

        [Fact]
        public void Removing_the_last_condition_in_a_block_removes_the_block()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.School, "Magic", control: "select");
            On(cut, "button.add-or-block", e => e.Click());
            SetField(cut, FilterFields.School, "Physical", control: "select", block: 1);

            SetField(cut, FilterFields.School, "Any", control: "select", block: 1);

            Assert.Single(Q.Groups);
            Assert.Equal("Magic", Q.Groups[0].Conditions.Single().Value);
        }

        [Fact]
        public void A_block_can_be_removed_outright()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.School, "Magic", control: "select");
            On(cut, "button.add-or-block", e => e.Click());

            On(cut, "button.remove-block", e => e.Click());

            Assert.Single(Q.Groups);
        }

        [Fact]
        public void The_common_band_appears_only_once_there_is_more_than_one_block()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.School, "Magic", control: "select");
            Assert.Empty(cut.FindAll("div.common-band"));

            On(cut, "button.add-or-block", e => e.Click());
            Assert.Single(cut.FindAll("div.common-band"));
        }

        [Fact]
        public void Pinning_a_condition_moves_it_into_the_common_band()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.Id, "7157");
            On(cut, "button.add-or-block", e => e.Click());

            ClickField(cut, FilterFields.Id, "button.pin-common");

            FilterCondition condition = Assert.Single(Q.Common.Conditions);
            Assert.Equal("7157", condition.Value);
            Assert.Contains("all · id = 7157", cut.Markup);
        }

        [Fact]
        public void Unpinning_returns_the_condition_to_the_first_block()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.Id, "7157");
            On(cut, "button.add-or-block", e => e.Click());
            ClickField(cut, FilterFields.Id, "button.pin-common");

            On(cut, "div.common-band button.pin-common", e => e.Click());

            Assert.Empty(Q.Common.Conditions);
            Assert.Equal("7157", Q.Groups[0].Conditions.Single().Value);
        }

        [Fact]
        public void A_named_bit_can_be_picked_instead_of_typed()
        {
            // The bits have names, so the form offers them rather than asking for 0x06.
            IRenderedComponent<FilterView> cut = Form();

            ClickField(cut, FilterFields.TargetMechanicFlags, "button.bit", index: 0);

            Assert.Equal("0x1", Q.ValueOf(FilterFields.TargetMechanicFlags));
        }

        [Fact]
        public void Picked_bits_accumulate_and_clearing_the_last_one_clears_the_constraint()
        {
            IRenderedComponent<FilterView> cut = Form();

            ClickField(cut, FilterFields.TargetMechanicFlags, "button.bit", index: 0);
            ClickField(cut, FilterFields.TargetMechanicFlags, "button.bit", index: 1);
            Assert.Equal("0x3", Q.ValueOf(FilterFields.TargetMechanicFlags));

            ClickField(cut, FilterFields.TargetMechanicFlags, "button.bit", index: 0);
            Assert.Equal("0x2", Q.ValueOf(FilterFields.TargetMechanicFlags));

            ClickField(cut, FilterFields.TargetMechanicFlags, "button.bit", index: 1);
            Assert.False(Q.Has(FilterFields.TargetMechanicFlags));
        }

        [Fact]
        public void A_mask_field_offers_an_all_or_any_choice()
        {
            IRenderedComponent<FilterView> cut = Form(PaneDescriptor.Effects);
            SetField(cut, FilterFields.EffectFlags, "0x06");

            Field(cut, FilterFields.EffectFlags, e => e.Click(),
                control: "div.segmented.op button", index: 1);

            Assert.Equal(FilterOperator.MaskAny, Q.On(FilterFields.EffectFlags).Single().Operator);
        }

        [Fact]
        public void An_unparseable_value_is_marked_rather_than_discarded()
        {
            IRenderedComponent<FilterView> cut = Form(PaneDescriptor.Effects);

            SetField(cut, FilterFields.EffectFlags, "not a mask");

            // Kept, so the user can see and fix what they typed.
            Assert.Equal("not a mask", Q.ValueOf(FilterFields.EffectFlags));
            Assert.Contains("invalid", cut.Find("[data-field='effect.flags'] input").ClassName);
        }

        [Fact]
        public void Applying_prunes_the_query_of_anything_that_constrains_nothing()
        {
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.Id, "7157");
            ClickField(cut, FilterFields.Id, "button.add-condition");

            // The added row was never filled in, so it must not survive the apply.
            Assert.Equal(2, Q.ConditionCount);

            On(cut, "button.apply-btn", e => e.Click());

            Assert.Equal(1, Q.ConditionCount);
        }

        [Fact]
        public void The_preview_counts_what_the_apply_would_return()
        {
            Models.SpellModels[1] = Spell(1, "Arcane Missile");
            Models.SpellModels[2] = Spell(2, "Healing Wave");

            IRenderedComponent<FilterView> cut = Form();
            cut.WaitForAssertion(() => Assert.Contains("show 2 rows", cut.Markup));

            SetField(cut, FilterFields.Id, "1");
            cut.WaitForAssertion(() => Assert.Contains("show 1 rows", cut.Markup));
        }

        [Fact]
        public void The_preview_on_the_effect_type_spells_pane_counts_that_type()
        {
            // The preview has to include the effect type, or this pane promises zero rows whatever the form says.
            Models.SpellModels[1] = Spell(1, "Arcane Missile");
            Models.EffectTypeUsages[SpellEffectType.Damage] = new EffectTypeUsage
            {
                Type = SpellEffectType.Damage, SpellIds = [1], EffectRowCount = 1
            };

            IRenderedComponent<FilterView> cut = RenderUnderContext<FilterView>(Context(), p => p
                .Add(c => c.Descriptor, PaneDescriptor.EffectTypeSpells)
                .Add(c => c.Pane, _pane)
                .Add(c => c.SpellId, 0u)
                .Add(c => c.EffectType, SpellEffectType.Damage)
                .Add(c => c.Applied, () => { }));

            cut.WaitForAssertion(() => Assert.Contains("show 1 rows", cut.Markup));
        }

        // ------------------------------------------------------------------ layout

        [Fact]
        public void Filling_one_field_leaves_its_neighbours_empty()
        {
            // The row markup is built by one method for every field, so without a key the diff matches rows
            // by a shared compiler sequence: a row that grows an operator control is confused with the next
            // field's row and writes its value into it.
            IRenderedComponent<FilterView> cut = Form();

            SetField(cut, FilterFields.CastTime, "1500");

            Assert.Equal("1500", Q.ValueOf(FilterFields.CastTime));
            Assert.Equal(1, Q.ConditionCount);

            foreach (IElement input in cut.FindAll(".filter-body [data-field] input"))
            {
                string field = input.Closest("[data-field]").GetAttribute("data-field");
                string rendered = input.GetAttribute("value") ?? "";

                Assert.Equal(field == FilterFields.CastTime ? "1500" : "", rendered);
            }
        }

        [Fact]
        public void Every_control_sits_in_a_wrapper_it_can_wrap_within()
        {
            // The value box and its buttons share one wrapper, so a narrow card wraps the buttons under the
            // box instead of squeezing the box down to nothing.
            IRenderedComponent<FilterView> cut = Form();
            SetField(cut, FilterFields.CastTime, "1500");

            IElement row = cut.Find($"[data-field='{FilterFields.CastTime}']");

            Assert.Equal(["label", "control"], row.Children.Select(c => c.ClassName));
            Assert.NotNull(row.QuerySelector(".control input"));
            Assert.NotNull(row.QuerySelector(".control .segmented.op"));
            Assert.NotNull(row.QuerySelector(".control button.not"));
        }

        [Fact]
        public void A_card_with_many_fields_lays_them_out_in_columns()
        {
            // A generic table contributes one field per column, and a single vertical list of a hundred of
            // them is several screens of scrolling before the first is reachable.
            Catalog.With("Wide", Enumerable.Range(0, 40).Select(i => "Column" + i).ToArray(), []);

            IRenderedComponent<FilterView> cut = Form(GameTable("Wide"));

            IElement columns = cut.FindAll(".group-card")
                .Single(c => c.QuerySelector(".group-title").TextContent.Contains("by name"));

            Assert.Contains("wide", columns.ClassName);
        }

        [Fact]
        public void A_card_with_few_fields_stays_a_plain_list()
        {
            IRenderedComponent<FilterView> cut = Form();

            IElement housekeeping = cut.FindAll(".group-card")
                .Single(c => c.QuerySelector(".group-title").TextContent.Trim() == "Housekeeping");

            Assert.DoesNotContain("wide", housekeeping.ClassName);
        }

        [Fact]
        public void A_card_says_how_many_fields_it_holds()
        {
            IRenderedComponent<FilterView> cut = Form();

            IElement housekeeping = cut.FindAll(".group-card")
                .Single(c => c.QuerySelector(".group-title").TextContent.Trim() == "Housekeeping");

            // Deprecated, Test and Has procs.
            Assert.Equal("3", housekeeping.QuerySelector(".group-count").TextContent.Trim());
        }

        [Fact]
        public void A_block_is_captioned_only_once_there_is_more_than_one()
        {
            IRenderedComponent<FilterView> cut = Form();
            Assert.Empty(cut.FindAll(".block-head"));

            On(cut, "button.add-or-block", e => e.Click());

            Assert.Equal(2, cut.FindAll(".block-head").Count);
            Assert.Equal(["1", "2"], cut.FindAll(".block-index").Select(e => e.TextContent.Trim()));
        }

        private static PaneDescriptor GameTable(string name) =>
            new(PaneDescriptor.GameTableId(name), PaneKind.GameTable, name + ".tbl", name, "icon", "meta", name);

        private static ISpellModel Spell(uint id, string description) => new TestSpell
        {
            Entry          = new Spell4Entry { Id = id },
            Description    = description,
            SpellBaseModel = new TestBase { Entry = new Spell4BaseEntry() }
        };
    }
}
