using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Services
{
    /// <summary>
    /// Flex filtering: a card per linked game table row, a column picked rather than declared, and the
    /// correlation that makes several constraints on one row mean one row.
    /// </summary>
    public class FilterFlexTests
    {
        private readonly FakeSpellModelService _models = new();
        private readonly FakeTableCatalog _catalog = new();

        /// <summary>The live preferences the schemas read their float tolerance from.</summary>
        private readonly Preferences _preferences = new();

        private FilterSchema<T> Schema<T>(PaneDescriptor descriptor) =>
            new FilterSchemaRegistry(_models, _catalog, _preferences).For<T>(descriptor);

        private FilterSchema<ISpellModel> Spells() => Schema<ISpellModel>(PaneDescriptor.Spell4);

        private static string Key(string source, string column) => FilterFields.Flex(source, column);

        // ------------------------------------------------------------------ the schema

        [Theory]
        [InlineData(FilterFields.SpellSource)]
        [InlineData(FilterFields.BaseSource)]
        [InlineData(FilterFields.EffectsSource)]
        [InlineData(FilterFields.HitResultSource)]
        [InlineData(FilterFields.TargetMechanicsSource)]
        [InlineData(FilterFields.TargetAngleSource)]
        [InlineData(FilterFields.PrerequisitesSource)]
        [InlineData(FilterFields.ValidTargetsSource)]
        [InlineData(FilterFields.PrerequisiteSpellSource)]
        [InlineData(FilterFields.SpellTypeSource)]
        public void The_spell_pane_offers_a_card_per_linked_row(string source)
        {
            FilterFlexSource<ISpellModel> flex = Spells().FlexSource(source);

            Assert.NotNull(flex);
            Assert.NotEmpty(flex.Columns);
            Assert.False(string.IsNullOrWhiteSpace(flex.Title));
        }

        [Fact]
        public void The_effects_and_procs_panes_offer_the_effect_row()
        {
            Assert.NotNull(Schema<ISpellEffectModel>(PaneDescriptor.Effects)
                .FlexSource(FilterFields.EffectRowSource));

            Assert.NotNull(Schema<ISpellProcModel>(PaneDescriptor.Procs)
                .FlexSource(FilterFields.ProcRowSource));
        }

        [Fact]
        public void The_effect_type_spells_pane_inherits_the_spell_cards()
        {
            // It lists Spell4 rows, so narrowing a reverse lookup is the same job as narrowing the forward
            // one - flex cards included.
            Assert.Equal(
                Spells().Flex.Select(f => f.Key),
                Schema<ISpellModel>(PaneDescriptor.EffectTypeSpells).Flex.Select(f => f.Key));
        }

        [Fact]
        public void A_source_the_pane_does_not_offer_resolves_to_nothing()
        {
            Assert.Null(Spells().FlexSource("no.such.row"));
            Assert.Null(Spells().FlexSource(null));
        }

        [Fact]
        public void A_column_is_keyed_by_its_source_and_its_name()
        {
            FilterFieldSchema field = ((FilterSchema)Spells()).Field(Key(FilterFields.BaseSource, "CastMethod"));

            FilterColumnFieldSchema column = Assert.IsType<FilterColumnFieldSchema>(field);
            Assert.Equal("CastMethod", column.Label);
            Assert.Equal(FilterFields.BaseSource, column.Source);
        }

        [Fact]
        public void A_number_column_offers_the_comparisons_and_a_text_column_the_two_readings()
        {
            Assert.Equal(
                [FilterOperator.Equals, FilterOperator.AtLeast, FilterOperator.AtMost,
                 FilterOperator.MaskAll, FilterOperator.MaskAny],
                Column(FilterFields.SpellSource, "Id").AllowedOperators);

            Assert.Equal(
                [FilterOperator.Contains, FilterOperator.Equals],
                Column(FilterFields.SpellSource, "Description").AllowedOperators);
        }

        [Fact]
        public void A_fractional_column_is_offered_no_mask()
        {
            // There are no bits in a float to mean anything, so it gets the thresholds and nothing else.
            FilterColumnFieldSchema column = Spells()
                .FlexSource(FilterFields.BaseSource).Columns
                .First(c => FieldType(c) == typeof(float));

            Assert.Equal(
                [FilterOperator.Equals, FilterOperator.AtLeast, FilterOperator.AtMost],
                column.AllowedOperators);
        }

        [Fact]
        public void Flex_columns_are_kept_out_of_the_hand_written_cards()
        {
            // Hundreds of them, drawn in every OR block: listing them beside the curated fields is the very
            // thing the flex card exists to avoid.
            Assert.DoesNotContain(Spells().Cards.SelectMany(c => c), f => f is FilterColumnFieldSchema);

            // They are still fields, or nothing could resolve a saved condition by key.
            Assert.Contains(Spells().Fields, f => f is FilterColumnFieldSchema);
        }

        [Fact]
        public void The_form_is_given_one_card_per_source_in_declaration_order()
        {
            Assert.Equal(Spells().Flex.Select(f => f.Key), Spells().FlexCards.Select(c => c.Key));

            Assert.Equal(
                Spells().Flex.Select(f => f.Columns.Count),
                Spells().FlexCards.Select(c => c.Columns.Count));
        }

        [Fact]
        public void The_typed_field_lookup_does_not_claim_a_flex_column()
        {
            // A column predicate is untyped in the element, so the typed lookup has to say "not one of
            // mine" rather than cast and throw.
            Assert.Null(Spells().Field(Key(FilterFields.BaseSource, "CastMethod")));
        }

        // ------------------------------------------------------------------ compiling

        [Fact]
        public void A_constraint_on_the_spells_own_row_reads_that_row()
        {
            Assert.True(Matches(Spell(tier: 3), Condition(FilterFields.SpellSource, "TierIndex", "3")));
            Assert.False(Matches(Spell(tier: 3), Condition(FilterFields.SpellSource, "TierIndex", "4")));
        }

        [Fact]
        public void A_constraint_on_the_base_reads_the_linked_row()
        {
            ISpellModel spell = Spell(castMethod: 2);

            Assert.True(Matches(spell, Condition(FilterFields.BaseSource, "CastMethod", "2")));
            Assert.False(Matches(spell, Condition(FilterFields.BaseSource, "CastMethod", "5")));
        }

        [Theory]
        [InlineData(FilterFields.TargetAngleSource)]
        [InlineData(FilterFields.PrerequisitesSource)]
        [InlineData(FilterFields.ValidTargetsSource)]
        [InlineData(FilterFields.PrerequisiteSpellSource)]
        [InlineData(FilterFields.SpellTypeSource)]
        public void A_spell_whose_link_is_unresolved_fails_the_constraint_rather_than_passing_it(string source)
        {
            // TestBase leaves these null, as SpellBaseModel does for a spell that points at no such row.
            Assert.False(Matches(Spell(), Condition(source, "Id", "0")));
        }

        [Theory]
        [InlineData(FilterFields.HitResultSource)]
        [InlineData(FilterFields.TargetMechanicsSource)]
        public void A_link_that_does_resolve_is_read_like_any_other_row(string source)
        {
            // The other half of the theory above: the accessor reaches the row rather than merely failing
            // to. TestBase gives these a default entry, whose Id is zero.
            Assert.True(Matches(Spell(), Condition(source, "Id", "0")));
            Assert.False(Matches(Spell(), Condition(source, "Id", "1")));
        }

        [Fact]
        public void A_spell_with_no_base_row_at_all_cannot_answer_a_question_about_one()
        {
            Assert.False(Matches(new TestSpell { Entry = new Spell4Entry() },
                Condition(FilterFields.BaseSource, "CastMethod", "0")));
        }

        [Fact]
        public void The_effects_pane_reads_the_effect_row_it_lists()
        {
            var effect = new TestEffect { Entry = new Spell4EffectsEntry { DataBits00 = 5 } };

            Assert.True(Matches<ISpellEffectModel>(PaneDescriptor.Effects, effect,
                Condition(FilterFields.EffectRowSource, "DataBits00", "5")));

            Assert.False(Matches<ISpellEffectModel>(PaneDescriptor.Effects, effect,
                Condition(FilterFields.EffectRowSource, "DataBits00", "6")));
        }

        [Fact]
        public void The_procs_pane_reads_the_effect_row_behind_the_proc()
        {
            var proc = new TestProc { Entry = new Spell4EffectsEntry { DataBits01 = 7161 } };

            Assert.True(Matches<ISpellProcModel>(PaneDescriptor.Procs, proc,
                Condition(FilterFields.ProcRowSource, "DataBits01", "7161")));

            Assert.False(Matches<ISpellProcModel>(PaneDescriptor.Procs, proc,
                Condition(FilterFields.ProcRowSource, "DataBits01", "1")));
        }

        [Theory]
        [InlineData(FilterOperator.Equals,  "7", true)]
        [InlineData(FilterOperator.Equals,  "8", false)]
        [InlineData(FilterOperator.AtLeast, "7", true)]
        [InlineData(FilterOperator.AtLeast, "8", false)]
        [InlineData(FilterOperator.AtMost,  "7", true)]
        [InlineData(FilterOperator.AtMost,  "6", false)]
        [InlineData(FilterOperator.MaskAll, "0x06", true)]
        [InlineData(FilterOperator.MaskAll, "0x08", false)]
        [InlineData(FilterOperator.MaskAny, "0x0C", true)]
        [InlineData(FilterOperator.MaskAny, "0x08", false)]
        [InlineData(FilterOperator.Equals,  "0x07", true)]
        [InlineData(FilterOperator.AtMost,  "7.5", true)]
        [InlineData(FilterOperator.AtLeast, "7.5", false)]
        public void Every_operator_a_numeric_column_offers_is_wired_to_its_reading(
            FilterOperator op, string value, bool expected)
        {
            // 7 is 0x07, so the thresholds and both mask readings all have something to say about it.
            Assert.Equal(expected,
                Matches(Spell(tier: 7), Condition(FilterFields.SpellSource, "TierIndex", value, op)));
        }

        [Fact]
        public void Two_constraints_on_one_effect_row_are_answered_by_one_effect()
        {
            ISpellModel spell = Spell(effects:
            [
                new Spell4EffectsEntry { EffectType = SpellEffectType.Damage, DataBits00 = 1 },
                new Spell4EffectsEntry { EffectType = SpellEffectType.Heal,   DataBits00 = 9 }
            ]);

            // Neither row is a damage effect with a large DataBits00, so the block matches nothing - asking
            // the two conditions independently would wrongly call this a hit.
            Assert.False(Matches(spell, Damage(), Data00AtLeast5()));

            ISpellModel both = Spell(effects:
                [new Spell4EffectsEntry { EffectType = SpellEffectType.Damage, DataBits00 = 9 }]);

            Assert.True(Matches(both, Damage(), Data00AtLeast5()));
        }

        [Fact]
        public void Constraints_in_separate_or_blocks_are_not_correlated()
        {
            // The correlation is scoped to the group, which is where the AND lives. Split across blocks the
            // same two conditions are an OR, and either row on its own is enough.
            var query = new FilterQuery();
            query.AddGroup().Conditions.Add(Damage());
            query.AddGroup().Conditions.Add(Data00AtLeast5());

            Assert.True(Compile(query).Filter(SplitAcrossTwoEffects()));
        }

        [Fact]
        public void The_common_band_correlates_within_itself_rather_than_into_the_blocks()
        {
            // The band is AND-ed once around the whole disjunction, so it is its own group - and its own row
            // match. A pinned condition therefore never has to be met by the same row as a block's.
            var query = new FilterQuery();
            query.Common.Conditions.Add(Damage());
            query.AddGroup().Conditions.Add(Data00AtLeast5());

            Assert.True(Compile(query).Filter(SplitAcrossTwoEffects()));
        }

        [Fact]
        public void Constraints_on_different_rows_are_each_answered_by_their_own()
        {
            ISpellModel spell = Spell(tier: 3, castMethod: 2);

            Assert.True(Matches(spell,
                Condition(FilterFields.SpellSource, "TierIndex", "3"),
                Condition(FilterFields.BaseSource, "CastMethod", "2")));

            Assert.False(Matches(spell,
                Condition(FilterFields.SpellSource, "TierIndex", "3"),
                Condition(FilterFields.BaseSource, "CastMethod", "5")));
        }

        [Fact]
        public void Negating_a_flex_condition_asks_for_a_row_that_differs()
        {
            // Not "no damage effect", but "an effect that is not damage" - the reading that keeps the !
            // button meaning the same thing it does on every other row of the form.
            FilterCondition condition = Damage();
            condition.Negate = true;

            Assert.True(Matches(SplitAcrossTwoEffects(), condition));

            ISpellModel damageOnly = Spell(effects:
                [new Spell4EffectsEntry { EffectType = SpellEffectType.Damage }]);

            Assert.False(Matches(damageOnly, condition));
        }

        [Fact]
        public void A_text_column_matches_by_substring_unless_asked_for_the_whole_value()
        {
            ISpellModel spell = Spell(description: "Arcane Missile");

            Assert.True(Matches(spell,
                Condition(FilterFields.SpellSource, "Description", "arcane", FilterOperator.Contains)));

            Assert.False(Matches(spell,
                Condition(FilterFields.SpellSource, "Description", "arcane", FilterOperator.Equals)));

            Assert.True(Matches(spell,
                Condition(FilterFields.SpellSource, "Description", "Arcane Missile", FilterOperator.Equals)));
        }

        [Fact]
        public void A_float_column_is_compared_with_the_tolerance_the_preferences_carry()
        {
            // The setting is only a setting if it reaches the filter the form compiles: 0.1 typed against a
            // column holding 0.1f is the comparison it exists for.
            ISpellModel spell = WithMaxRange(0.1f);
            FilterCondition condition = Condition(FilterFields.SpellSource, "TargetMaxRange", "0.1");

            Assert.True(Matches(spell, condition));

            _preferences.FilterEpsilon = 0d;
            Assert.False(Matches(spell, condition));
        }

        private static ISpellModel WithMaxRange(float range) => new TestSpell
        {
            Entry          = new Spell4Entry { Id = 1, TargetMaxRange = range },
            SpellBaseModel = new TestBase { Entry = new Spell4BaseEntry() }
        };

        [Fact]
        public void A_flex_condition_with_nothing_typed_constrains_nothing()
        {
            // The row exists because a column was picked; it asks nothing until a value is typed, and it is
            // pruned on apply rather than persisted.
            var query = new FilterQuery();
            query.FirstGroup().Conditions.Add(Condition(FilterFields.SpellSource, "TierIndex", ""));

            Assert.True(Compile(query).Filter(Spell(tier: 3)));

            query.Prune(Spells());
            Assert.Equal(0, query.ConditionCount);
        }

        [Fact]
        public void A_value_the_column_cannot_parse_is_reported_and_dropped()
        {
            var query = new FilterQuery();
            query.FirstGroup().Conditions.Add(Condition(FilterFields.SpellSource, "TierIndex", "not a number"));

            FilterQueryCompiler.Compile(query, Spells(), out IReadOnlyList<FilterDiagnostic> diagnostics);

            Assert.Contains(diagnostics, d =>
                d.Kind == FilterDiagnosticKind.UnparseableValue
                && d.Field == Key(FilterFields.SpellSource, "TierIndex"));
        }

        [Fact]
        public void A_column_of_a_row_the_pane_does_not_offer_is_as_unknown_as_a_bad_key()
        {
            // A pane's own flex columns are the only ones its schema resolves, so a column keyed to another
            // pane's source can only arrive from a hand-edited workspace file - and is dropped the way any
            // structurally meaningless key is.
            FilterSchema<ISpellEffectModel> effects = Schema<ISpellEffectModel>(PaneDescriptor.Effects);

            var query = new FilterQuery();
            query.FirstGroup().Conditions.Add(new FilterCondition
            {
                Field = Key(FilterFields.EffectsSource, "DataBits00"),
                Value = "1"
            });

            FilterQueryCompiler.Compile(query, effects, out IReadOnlyList<FilterDiagnostic> diagnostics);

            Assert.Contains(diagnostics, d =>
                d.Kind == FilterDiagnosticKind.UnknownField
                && d.Field == Key(FilterFields.EffectsSource, "DataBits00"));
        }

        [Fact]
        public void A_flex_condition_reads_back_in_the_chips_with_the_row_it_asks_about()
        {
            var query = new FilterQuery();
            query.FirstGroup().Conditions.Add(Data00AtLeast5());

            FilterChip chip = Assert.Single(FilterChips.For(query, Spells()));
            Assert.Equal("effects · databits00 ≥ 5", chip.Label);
        }

        [Fact]
        public void A_flex_condition_survives_a_round_trip_through_the_workspace_file()
        {
            var saved = new FilterQuery();
            saved.FirstGroup().Conditions.Add(Data00AtLeast5());

            var loaded = new FilterQuery();
            FilterQueryDtoMapper.Load(loaded, FilterQueryDtoMapper.ToDto(saved), Spells());

            FilterCondition condition = Assert.Single(loaded.Groups[0].Conditions);
            Assert.Equal(Key(FilterFields.EffectsSource, "DataBits00"), condition.Field);
            Assert.Equal(FilterOperator.AtLeast, condition.Operator);
            Assert.Equal("5", condition.Value);
        }

        [Fact]
        public void A_saved_column_the_archive_no_longer_has_is_dropped_on_load()
        {
            var dto = new FilterQueryDto
            {
                Groups = [[new FilterConditionDto { Field = Key(FilterFields.EffectsSource, "Gone"), Value = "1" }]]
            };

            var loaded = new FilterQuery();
            FilterQueryDtoMapper.Load(loaded, dto, Spells());

            Assert.Equal(0, loaded.ConditionCount);
        }

        // ------------------------------------------------------------------ helpers

        private FilterColumnFieldSchema Column(string source, string name) =>
            Spells().FlexSource(source).Columns.Single(c => c.Label == name);

        private static Type FieldType(FilterColumnFieldSchema column) =>
            typeof(Spell4BaseEntry).GetField(column.Label).FieldType;

        private static FilterCondition Condition(
            string source, string column, string value, FilterOperator op = FilterOperator.Equals) =>
            new() { Field = FilterFields.Flex(source, column), Value = value, Operator = op };

        /// <summary>
        /// An effect row's type column, asked for by name.
        /// </summary>
        /// <remarks>
        /// The column is typed as the enum, so it is offered as text and matched against the name it
        /// renders as - which is also what the grid shows in that column, so the two agree.
        /// </remarks>
        private static FilterCondition Damage() =>
            Condition(FilterFields.EffectsSource, "EffectType", nameof(SpellEffectType.Damage));

        private static FilterCondition Data00AtLeast5() =>
            Condition(FilterFields.EffectsSource, "DataBits00", "5", FilterOperator.AtLeast);

        /// <summary>A spell whose two effect rows each satisfy one half of the pair above.</summary>
        private static ISpellModel SplitAcrossTwoEffects() => Spell(effects:
        [
            new Spell4EffectsEntry { EffectType = SpellEffectType.Damage, DataBits00 = 1 },
            new Spell4EffectsEntry { EffectType = SpellEffectType.Heal,   DataBits00 = 9 }
        ]);

        private IModelFilter<ISpellModel> Compile(FilterQuery query) =>
            FilterQueryCompiler.Compile(query, Spells());

        private bool Matches(ISpellModel spell, params FilterCondition[] conditions) =>
            Matches(PaneDescriptor.Spell4, spell, conditions);

        private bool Matches<T>(PaneDescriptor descriptor, T element, params FilterCondition[] conditions)
        {
            var query = new FilterQuery();
            query.FirstGroup().Conditions.AddRange(conditions);

            return FilterQueryCompiler.Compile(query, Schema<T>(descriptor)).Filter(element);
        }

        private static ISpellModel Spell(
            uint tier = 0, uint castMethod = 0, string description = "", Spell4EffectsEntry[] effects = null)
        {
            var spell = new TestSpell
            {
                Entry          = new Spell4Entry { Id = 1, TierIndex = tier, Description = description },
                SpellBaseModel = new TestBase { Entry = new Spell4BaseEntry { CastMethod = castMethod } }
            };

            foreach (Spell4EffectsEntry entry in effects ?? [])
                spell.Effects.Add(new TestEffect { Entry = entry });

            return spell;
        }
    }
}
