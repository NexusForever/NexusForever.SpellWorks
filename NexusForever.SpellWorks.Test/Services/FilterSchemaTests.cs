using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Services
{
    /// <summary>
    /// The per-pane schemas and the folding of a query into one Core filter. The exhaustive theories here
    /// are what stop a new pane kind, or a new field, from shipping half-wired.
    /// </summary>
    public class FilterSchemaTests
    {
        private readonly FakeSpellModelService _models = new();
        private readonly FakeTableCatalog _catalog = new();

        private FilterSchemaRegistry Registry() => new(_models, _catalog, new Preferences());

        /// <summary>Every grid kind, which is every kind that offers a filter form.</summary>
        public static TheoryData<PaneKind> GridKinds =>
            new(Enum.GetValues<PaneKind>().Where(k => Descriptor(k).IsTableKind));

        [Theory]
        [MemberData(nameof(GridKinds))]
        public void Every_grid_kind_has_a_schema(PaneKind kind)
        {
            Assert.NotNull(Registry().For(Descriptor(kind)));
        }

        [Theory]
        [MemberData(nameof(GridKinds))]
        public void Every_schema_field_is_renderable(PaneKind kind)
        {
            foreach (FilterFieldSchema field in Registry().For(Descriptor(kind)).Fields)
            {
                Assert.False(string.IsNullOrWhiteSpace(field.Key));
                Assert.False(string.IsNullOrWhiteSpace(field.Label));
                Assert.False(string.IsNullOrWhiteSpace(field.GroupTitle));
                Assert.NotEmpty(field.AllowedOperators);

                // A choice with no options is a dropdown that can only say "Any" - never ship one.
                if (field.Control == FilterControlKind.Choice)
                    Assert.True(field.Options.Count > 1);
                else
                    Assert.Null(field.Options);
            }
        }

        [Theory]
        [MemberData(nameof(GridKinds))]
        public void Every_schema_field_can_actually_build_a_filter(PaneKind kind)
        {
            // The companion to "every field renders a control": a field that draws but whose factory can
            // never produce anything is a control that silently does nothing.
            _catalog.With("Spell4Effects", ["Id", "SpellId", "Flags"], []);

            foreach (FilterFieldSchema field in Registry().For(Descriptor(kind)).Fields)
                Assert.True(Buildable(field), field.Key + " builds no filter for any plausible value");
        }

        /// <summary>Whether the field compiles for at least one value a user could reasonably enter.</summary>
        private static bool Buildable(FilterFieldSchema field)
        {
            List<string> candidates = ["1", "0x06", "1.5", "Spell4"];

            if (field.Options is { Count: > 1 })
                candidates.Insert(0, field.Options[1]);

            if (field.Bits is { Count: > 0 })
                candidates.Insert(0, "0x" + field.Bits[0].Value.ToString("X"));

            foreach (FilterOperator op in field.AllowedOperators)
                foreach (string value in candidates)
                {
                    var condition = new FilterCondition { Field = field.Key, Value = value, Operator = op };
                    if (field.IsValid(condition) && !field.IsBlank(condition))
                        return true;

                    // A toggle carries no value at all; its presence is the constraint.
                    if (field.Control == FilterControlKind.Toggle && field.IsValid(condition))
                        return true;
                }

            return false;
        }

        [Theory]
        [MemberData(nameof(GridKinds))]
        public void Field_keys_are_unique_within_a_schema(PaneKind kind)
        {
            List<string> keys = Registry().For(Descriptor(kind)).Fields.Select(f => f.Key).ToList();

            Assert.Equal(keys.Count, keys.Distinct().Count());
        }

        [Fact]
        public void A_pane_that_is_not_a_grid_has_no_schema()
        {
            Assert.Null(Registry().For(PaneDescriptor.Setup));
            Assert.Null(Registry().For(null));
        }

        [Fact]
        public void The_effect_type_spells_pane_offers_the_spell_schema_verbatim()
        {
            // It lists Spell4 rows, so narrowing a reverse lookup is the same job as narrowing the forward one.
            FilterSchema spells = Registry().For(PaneDescriptor.Spell4);
            FilterSchema reverse = Registry().For(PaneDescriptor.EffectTypeSpells);

            Assert.Equal(spells.Fields.Select(f => f.Key), reverse.Fields.Select(f => f.Key));
        }

        [Fact]
        public void Invalidating_drops_the_cached_schemas()
        {
            // A reload can change which tables exist and what columns they carry.
            FilterSchemaRegistry registry = Registry();
            FilterSchema before = registry.For(PaneDescriptor.Spell4);

            registry.Invalidate();

            Assert.NotSame(before, registry.For(PaneDescriptor.Spell4));
        }

        [Fact]
        public void A_fixed_kind_is_cached_and_a_game_table_is_not()
        {
            FilterSchemaRegistry registry = Registry();

            Assert.Same(registry.For(PaneDescriptor.Spell4), registry.For(PaneDescriptor.Spell4));

            // A generic table's fields are its columns, which are a runtime fact that a reload can change.
            PaneDescriptor table = GameTable("Spell4Effects");
            Assert.NotSame(registry.For(table), registry.For(table));
        }

        // ------------------------------------------------------------------ blank, valid, invalid

        [Theory]
        [InlineData(FilterFields.Id, "", true)]
        [InlineData(FilterFields.Id, "  ", true)]
        [InlineData(FilterFields.Id, "7157", false)]
        [InlineData(FilterFields.School, "Any", true)]
        [InlineData(FilterFields.School, "Magic", false)]
        public void A_blank_control_asks_for_nothing(string key, string value, bool blank)
        {
            FilterFieldSchema field = Registry().For(PaneDescriptor.Spell4).Field(key);

            Assert.Equal(blank, field.IsBlank(Condition(key, value)));
        }

        [Fact]
        public void A_toggle_is_never_blank_because_its_presence_is_the_constraint()
        {
            FilterFieldSchema field = Registry().For(PaneDescriptor.Spell4).Field(FilterFields.HasProcs);

            Assert.False(field.IsBlank(Condition(FilterFields.HasProcs)));
        }

        [Theory]
        [InlineData(FilterFields.School, "Magic", true)]
        [InlineData(FilterFields.School, "NotASchool", false)]
        [InlineData(FilterFields.TargetMechanicFlags, "0x06", true)]
        [InlineData(FilterFields.TargetMechanicFlags, "6", true)]
        [InlineData(FilterFields.TargetMechanicFlags, "not a mask", false)]
        public void An_unparseable_value_is_invalid_rather_than_dropped_quietly(string key, string value, bool valid)
        {
            FilterFieldSchema field = Registry().For(PaneDescriptor.Spell4).Field(key);

            Assert.Equal(valid, field.IsValid(Condition(key, value)));
        }

        // ------------------------------------------------------------------ compilation

        [Fact]
        public void An_empty_query_compiles_to_match_all()
        {
            IModelFilter<ISpellModel> filter = Compile(new FilterQuery());

            Assert.IsType<MatchAllFilter<ISpellModel>>(filter);
        }

        [Fact]
        public void Conditions_in_one_block_are_anded()
        {
            FilterQuery query = new();
            query.Groups.Add(Group(
                Condition(FilterFields.Id, "7"),
                Condition(FilterFields.School, "Magic")));

            IModelFilter<ISpellModel> filter = Compile(query);

            Assert.True(filter.Filter(Spell(7157, school: DamageType.Magic)));
            Assert.False(filter.Filter(Spell(8157, school: DamageType.Magic)));
            Assert.False(filter.Filter(Spell(7157, school: DamageType.Physical)));
        }

        [Fact]
        public void Blocks_are_ored()
        {
            FilterQuery query = new();
            query.Groups.Add(Group(Condition(FilterFields.School, "Magic")));
            query.Groups.Add(Group(Condition(FilterFields.School, "Physical")));

            IModelFilter<ISpellModel> filter = Compile(query);

            Assert.True(filter.Filter(Spell(1, school: DamageType.Magic)));
            Assert.True(filter.Filter(Spell(2, school: DamageType.Physical)));
            Assert.False(filter.Filter(Spell(3, school: DamageType.Fall)));
        }

        [Fact]
        public void A_negated_condition_inverts_only_itself()
        {
            FilterQuery query = new();
            query.Groups.Add(Group(
                Condition(FilterFields.Id, "7"),
                Condition(FilterFields.School, "Magic", negate: true)));

            IModelFilter<ISpellModel> filter = Compile(query);

            Assert.True(filter.Filter(Spell(7157, school: DamageType.Physical)));
            Assert.False(filter.Filter(Spell(7157, school: DamageType.Magic)));
            Assert.False(filter.Filter(Spell(8157, school: DamageType.Physical)));
        }

        [Fact]
        public void The_common_band_narrows_every_block()
        {
            FilterQuery query = new();
            query.Common.Conditions.Add(Condition(FilterFields.Id, "7"));
            query.Groups.Add(Group(Condition(FilterFields.School, "Magic")));
            query.Groups.Add(Group(Condition(FilterFields.School, "Physical")));

            IModelFilter<ISpellModel> filter = Compile(query);

            Assert.True(filter.Filter(Spell(7157, school: DamageType.Physical)));
            Assert.False(filter.Filter(Spell(8157, school: DamageType.Physical)));
        }

        [Fact]
        public void The_search_box_is_anded_across_the_whole_disjunction()
        {
            FilterQuery query = new() { Search = "arcane" };
            query.Groups.Add(Group(Condition(FilterFields.School, "Magic")));
            query.Groups.Add(Group(Condition(FilterFields.School, "Physical")));

            IModelFilter<ISpellModel> filter = Compile(query);

            Assert.True(filter.Filter(Spell(1, "Arcane Missile", DamageType.Physical)));
            Assert.False(filter.Filter(Spell(2, "Healing Wave", DamageType.Physical)));
        }

        [Fact]
        public void A_block_whose_conditions_are_all_invalid_is_dropped_not_made_true()
        {
            // The rule the whole design turns on: one broken term inside an OR must never widen the grid
            // to every row.
            FilterQuery query = new();
            query.Groups.Add(Group(Condition(FilterFields.School, "Magic")));
            query.Groups.Add(Group(Condition(FilterFields.School, "NotASchool")));

            IModelFilter<ISpellModel> filter = Compile(query, out IReadOnlyList<FilterDiagnostic> diagnostics);

            Assert.False(filter.Filter(Spell(1, school: DamageType.Physical)));
            Assert.True(filter.Filter(Spell(2, school: DamageType.Magic)));

            Assert.Contains(diagnostics, d => d.Kind == FilterDiagnosticKind.UnparseableValue);
            Assert.Contains(diagnostics, d => d.Kind == FilterDiagnosticKind.EmptyGroup);
        }

        [Fact]
        public void A_condition_on_an_unknown_field_is_dropped_with_a_diagnostic()
        {
            FilterQuery query = new();
            query.Groups.Add(Group(
                Condition(FilterFields.School, "Magic"),
                Condition("field.that.no.longer.exists", "x")));

            IModelFilter<ISpellModel> filter = Compile(query, out IReadOnlyList<FilterDiagnostic> diagnostics);

            Assert.True(filter.Filter(Spell(1, school: DamageType.Magic)));
            Assert.Contains(diagnostics, d => d.Kind == FilterDiagnosticKind.UnknownField);
        }

        [Fact]
        public void A_blank_condition_neither_constrains_nor_drops_its_block()
        {
            FilterQuery query = new();
            query.Groups.Add(Group(
                Condition(FilterFields.School, "Magic"),
                Condition(FilterFields.Id, "")));

            IModelFilter<ISpellModel> filter = Compile(query, out IReadOnlyList<FilterDiagnostic> diagnostics);

            Assert.True(filter.Filter(Spell(1, school: DamageType.Magic)));
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void The_deprecated_field_carries_its_polarity_in_the_condition()
        {
            FilterQuery hide = new();
            hide.Groups.Add(Group(Condition(FilterFields.Deprecated, negate: true)));

            FilterQuery only = new();
            only.Groups.Add(Group(Condition(FilterFields.Deprecated)));

            ISpellModel old = Spell(1, "[DEPRECATED] old");
            ISpellModel live = Spell(2, "Arcane Missile");

            Assert.False(Compile(hide).Filter(old));
            Assert.True(Compile(hide).Filter(live));
            Assert.True(Compile(only).Filter(old));
            Assert.False(Compile(only).Filter(live));
        }

        // ------------------------------------------------------------------ search

        [Theory]
        [MemberData(nameof(GridKinds))]
        public void Every_grid_kind_says_what_its_search_boxes_match(PaneKind kind)
        {
            FilterSchema schema = Registry().For(Descriptor(kind));

            Assert.False(string.IsNullOrWhiteSpace(schema.TextPlaceholder));

            // Either a pane has an id to search and says so, or it has neither.
            Assert.Equal(schema.HasIdSearch, !string.IsNullOrWhiteSpace(schema.IdPlaceholder));
        }

        [Theory]
        [InlineData(PaneKind.Spell4, true)]
        [InlineData(PaneKind.EffectTypeSpells, true)]
        [InlineData(PaneKind.Effects, true)]
        [InlineData(PaneKind.Procs, true)]
        [InlineData(PaneKind.EffectTypes, true)]
        [InlineData(PaneKind.GameTable, true)]
        [InlineData(PaneKind.Tables, false)]
        public void Only_a_pane_with_something_numbered_offers_an_id_box(PaneKind kind, bool expected)
        {
            // The table list is the one grid whose rows are named rather than numbered.
            Assert.Equal(expected, Registry().For(Descriptor(kind)).HasIdSearch);
        }

        [Fact]
        public void The_text_box_does_not_match_the_id()
        {
            FilterQuery query = new() { Search = "7157" };

            Assert.False(Compile(query).Filter(Spell(7157, "Arcane Missile")));
            Assert.True(Compile(new FilterQuery { IdSearch = "7157" }).Filter(Spell(7157, "Arcane Missile")));
        }

        [Fact]
        public void The_two_boxes_are_anded_rather_than_ored()
        {
            FilterQuery query = new() { Search = "arcane", IdSearch = "7157" };

            Assert.True(Compile(query).Filter(Spell(7157, "Arcane Missile")));
            Assert.False(Compile(query).Filter(Spell(7158, "Arcane Missile")));
            Assert.False(Compile(query).Filter(Spell(7157, "Healing Wave")));
        }

        [Fact]
        public void The_exact_toggle_reaches_both_boxes()
        {
            FilterQuery loose = new() { Search = "arcane", IdSearch = "715" };
            FilterQuery exact = new() { Search = "arcane", IdSearch = "715", ExactSearch = true };

            ISpellModel spell = Spell(7157, "Arcane Missile");

            Assert.True(Compile(loose).Filter(spell));
            Assert.False(Compile(exact).Filter(spell));
            Assert.True(Compile(new FilterQuery
            {
                Search = "Arcane Missile", IdSearch = "7157", ExactSearch = true
            }).Filter(spell));
        }

        [Fact]
        public void The_search_grammar_still_applies_inside_each_box()
        {
            FilterQuery query = new() { IdSearch = "7157 || 7158" };

            Assert.True(Compile(query).Filter(Spell(7157, "")));
            Assert.True(Compile(query).Filter(Spell(7158, "")));
            Assert.False(Compile(query).Filter(Spell(7159, "")));
        }

        // ------------------------------------------------------------------ numeric thresholds

        [Theory]
        [InlineData(FilterFields.CastTime)]
        [InlineData(FilterFields.Duration)]
        [InlineData(FilterFields.Cooldown)]
        [InlineData(FilterFields.Tier)]
        [InlineData(FilterFields.TargetMinRange)]
        [InlineData(FilterFields.TargetMaxRange)]
        [InlineData(FilterFields.TargetVerticalRange)]
        [InlineData(FilterFields.MissileSpeed)]
        [InlineData(FilterFields.ChannelTime)]
        [InlineData(FilterFields.ChannelPulse)]
        [InlineData(FilterFields.AbilityCharges)]
        [InlineData(FilterFields.EffectCount)]
        [InlineData(FilterFields.ProcCount)]
        [InlineData(FilterFields.ProcReferenceCount)]
        public void Every_spell_threshold_offers_both_ends_of_the_range(string key)
        {
            FilterFieldSchema field = Registry().For(PaneDescriptor.Spell4).Field(key);

            Assert.NotNull(field);
            Assert.Equal([FilterOperator.AtLeast, FilterOperator.AtMost], field.AllowedOperators);
            Assert.Equal(FilterOperator.AtLeast, field.DefaultOperator);
        }

        [Fact]
        public void A_threshold_reads_a_decimal_and_rejects_a_word()
        {
            FilterFieldSchema field = Registry().For(PaneDescriptor.Spell4).Field(FilterFields.TargetMaxRange);

            Assert.True(field.IsValid(Condition(FilterFields.TargetMaxRange, "7.5")));
            Assert.False(field.IsValid(Condition(FilterFields.TargetMaxRange, "far")));
        }

        [Fact]
        public void The_at_most_operator_flips_the_comparison()
        {
            FilterQuery atLeast = new();
            atLeast.Groups.Add(Group(new FilterCondition
            {
                Field = FilterFields.Tier, Value = "3", Operator = FilterOperator.AtLeast
            }));

            FilterQuery atMost = new();
            atMost.Groups.Add(Group(new FilterCondition
            {
                Field = FilterFields.Tier, Value = "3", Operator = FilterOperator.AtMost
            }));

            ISpellModel tier5 = Tiered(5);

            Assert.True(Compile(atLeast).Filter(tier5));
            Assert.False(Compile(atMost).Filter(tier5));
        }

        [Theory]
        [InlineData(FilterFields.EffectDelay)]
        [InlineData(FilterFields.EffectTick)]
        [InlineData(FilterFields.EffectDuration)]
        [InlineData(FilterFields.EffectThreat)]
        public void The_effects_pane_offers_its_own_thresholds(string key)
        {
            FilterFieldSchema field = Registry().For(PaneDescriptor.Effects).Field(key);

            Assert.NotNull(field);
            Assert.Equal([FilterOperator.AtLeast, FilterOperator.AtMost], field.AllowedOperators);
        }

        [Fact]
        public void The_effect_types_pane_can_cap_as_well_as_floor_its_spell_count()
        {
            FilterFieldSchema field = Registry().For(PaneDescriptor.EffectTypes).Field(FilterFields.TypeSpells);

            Assert.Contains(FilterOperator.AtMost, field.AllowedOperators);
        }

        private static ISpellModel Tiered(uint tier) => new TestSpell
        {
            Entry          = new Spell4Entry { Id = 1, TierIndex = tier },
            SpellBaseModel = new TestBase { Entry = new Spell4BaseEntry() }
        };

        // ------------------------------------------------------------------ named bits and columns

        [Theory]
        [InlineData(FilterFields.TargetMechanicFlags)]
        [InlineData(FilterFields.EffectTargetFlags)]
        public void A_named_mask_offers_its_bits_rather_than_a_raw_number(string key)
        {
            FilterFieldSchema field = Registry().For(PaneDescriptor.Spell4).Field(key);

            Assert.Equal(FilterControlKind.Flags, field.Control);
            Assert.NotEmpty(field.Bits);

            // The zero member names the absence of every flag, so ticking it would be a no-op.
            Assert.DoesNotContain(field.Bits, b => b.Value == 0);
        }

        [Fact]
        public void A_picked_mask_compiles_exactly_as_a_typed_one_does()
        {
            FilterSchema<ISpellModel> schema = Registry().For<ISpellModel>(PaneDescriptor.Spell4);
            FilterFieldSchema<ISpellModel> field = schema.Field(FilterFields.TargetMechanicFlags);

            Assert.NotNull(field.Factory(Condition(FilterFields.TargetMechanicFlags, "0x1")));
            Assert.NotNull(field.Factory(Condition(FilterFields.TargetMechanicFlags, "1")));
        }

        [Fact]
        public void A_generic_table_offers_one_field_per_column()
        {
            _catalog.With("Spell4Effects", ["Id", "SpellId", "Flags"], []);

            FilterSchema schema = Registry().For(GameTable("Spell4Effects"));

            Assert.NotNull(schema.Field(FilterFields.Column("Id")));
            Assert.NotNull(schema.Field(FilterFields.Column("SpellId")));
            Assert.NotNull(schema.Field(FilterFields.Column("Flags")));
        }

        [Fact]
        public void A_column_field_reads_only_its_own_column()
        {
            _catalog.With("Spell4Effects", ["Id", "SpellId"], []);

            FilterSchema<string[]> schema = Registry().For<string[]>(GameTable("Spell4Effects"));
            IModelFilter<string[]> filter = schema.Field(FilterFields.Column("SpellId"))
                .Factory(Condition(FilterFields.Column("SpellId"), "7157"));

            Assert.True(filter.Filter(["1", "7157"]));
            Assert.False(filter.Filter(["7157", "1"]));
        }

        [Fact]
        public void A_column_field_can_be_asked_for_an_exact_value_or_a_substring()
        {
            _catalog.With("Spell4Effects", ["Id", "SpellId"], []);

            FilterSchema<string[]> schema = Registry().For<string[]>(GameTable("Spell4Effects"));
            FilterFieldSchema<string[]> field = schema.Field(FilterFields.Column("SpellId"));

            Assert.Equal([FilterOperator.Contains, FilterOperator.Equals], field.AllowedOperators);

            var exact = new FilterCondition
            {
                Field = field.Key, Value = "715", Operator = FilterOperator.Equals
            };

            var substring = new FilterCondition
            {
                Field = field.Key, Value = "715", Operator = FilterOperator.Contains
            };

            Assert.False(field.Factory(exact).Filter(["1", "7157"]));
            Assert.True(field.Factory(substring).Filter(["1", "7157"]));
        }

        [Fact]
        public void A_column_field_key_survives_a_reordered_table()
        {
            // Keys are written into Workspace.json, so they name the column rather than its position.
            _catalog.With("T", ["A", "B"], []);
            Assert.NotNull(Registry().For(GameTable("T")).Field(FilterFields.Column("B")));

            _catalog.With("T", ["B", "A"], []);
            Assert.NotNull(Registry().For(GameTable("T")).Field(FilterFields.Column("B")));
        }

        // ------------------------------------------------------------------ value parsing

        [Theory]
        [InlineData("6", true, 6u)]
        [InlineData("0x06", true, 6u)]
        [InlineData("0X0F", true, 15u)]
        [InlineData("0xzz", false, 0u)]
        [InlineData("nope", false, 0u)]
        [InlineData("", false, 0u)]
        [InlineData(null, false, 0u)]
        public void A_mask_reads_decimal_or_hex_and_tolerates_neither(string input, bool parsed, uint expected)
        {
            Assert.Equal(parsed, FilterValue.TryUInt(input, out uint value));
            Assert.Equal(expected, value);
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("  x  ", "x")]
        public void Trimming_a_value_tolerates_a_missing_one(string input, string expected)
        {
            Assert.Equal(expected, FilterValue.Trimmed(input));
        }

        [Fact]
        public void The_Any_sentinel_never_parses_as_an_enum()
        {
            Assert.False(FilterValue.TryEnum(FilterFieldSchema.Any, out DamageType _));
            Assert.True(FilterValue.TryEnum(" Magic ", out DamageType magic));
            Assert.Equal(DamageType.Magic, magic);
        }

        [Fact]
        public void A_column_field_key_is_prefixed_so_it_cannot_collide_with_a_fixed_one()
        {
            // Generic table columns are discovered at runtime, so their keys share one open namespace.
            Assert.Equal("col:DataBits00", FilterFields.Column("DataBits00"));
            Assert.StartsWith(FilterFields.ColumnPrefix, FilterFields.Column("Flags"));
        }

        [Fact]
        public void A_mask_on_a_table_with_no_flags_column_is_no_constraint()
        {
            // The column the user is asking about does not exist here, and emptying the grid instead would
            // read as a broken table.
            _catalog.With("NoFlags", ["Id", "Name"], []);

            FilterSchema<string[]> schema = Registry().For<string[]>(GameTable("NoFlags"));
            IModelFilter<string[]> filter = schema.Field(FilterFields.RowFlags)
                .Factory(new FilterCondition { Field = FilterFields.RowFlags, Value = "0x06" });

            Assert.True(filter.Filter(["1", "anything"]));
        }

        // ------------------------------------------------------------------ helpers

        [Fact]
        public async Task The_schema_cache_survives_being_read_from_several_threads_at_once()
        {
            // The registry is a singleton, and a grid compiles its filter on the thread pool while the
            // component that owns it renders from the same schema. A plain dictionary corrupts itself here,
            // and it surfaces as a NullReferenceException from inside Dictionary rather than anywhere near
            // the cause.
            FilterSchemaRegistry registry = Registry();
            PaneDescriptor[] descriptors = [.. Enum.GetValues<PaneKind>().Select(Descriptor)];

            await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 200; i++)
                    foreach (PaneDescriptor descriptor in descriptors)
                        registry.For(descriptor);
            })));

            // And the cache still answers with one schema per kind afterwards.
            Assert.Same(registry.For(PaneDescriptor.Spell4), registry.For(PaneDescriptor.Spell4));
        }

        // ------------------------------------------------------------------ nothing to compile

        [Fact]
        public void A_query_or_schema_that_is_not_there_compiles_to_a_filter_that_keeps_everything()
        {
            // A pane with no form has no schema, and a grid can ask for its rows before a query exists.
            // Neither is an error: both mean "no constraint".
            FilterSchema<ISpellModel> schema = Registry().For<ISpellModel>(PaneDescriptor.Spell4);

            IModelFilter<ISpellModel> noQuery = FilterQueryCompiler.Compile(null, schema, out _);
            IModelFilter<ISpellModel> noSchema = FilterQueryCompiler.Compile<ISpellModel>(new FilterQuery(), null, out _);

            Assert.True(noQuery.Filter(Spell(7157, "Arcane Missile")));
            Assert.True(noSchema.Filter(Spell(7157, "Arcane Missile")));
        }

        // ------------------------------------------------------------------ the id box on the other panes

        [Fact]
        public void The_effects_id_box_searches_the_effect_type_id()
        {
            FilterSchema<ISpellEffectModel> schema = Registry().For<ISpellEffectModel>(PaneDescriptor.Effects);

            IModelFilter<ISpellEffectModel> filter = FilterQueryCompiler.Compile(
                new FilterQuery { IdSearch = ((uint)SpellEffectType.Damage).ToString() }, schema, out _);

            Assert.True(filter.Filter(new TestEffect { Type = SpellEffectType.Damage }));
            Assert.False(filter.Filter(new TestEffect { Type = SpellEffectType.Heal }));
        }

        [Fact]
        public void The_game_table_id_box_searches_the_first_column()
        {
            // "Row id" means column zero; the other box searches every column, which would also match a
            // value that happens to appear elsewhere in the row.
            FilterSchema<string[]> schema = Registry().For<string[]>(GameTable("Spell4Effects"));

            IModelFilter<string[]> filter = FilterQueryCompiler.Compile(
                new FilterQuery { IdSearch = "7157" }, schema, out _);

            Assert.True(filter.Filter(["7157", "something"]));
            Assert.False(filter.Filter(["1", "7157"]));
        }

        // ------------------------------------------------------------------ mask operators

        [Theory]
        [InlineData(FilterOperator.MaskAll, 0x6u, true)]
        [InlineData(FilterOperator.MaskAll, 0xEu, false)]
        [InlineData(FilterOperator.MaskAny, 0x8u, false)]
        [InlineData(FilterOperator.MaskAny, 0xCu, true)]
        [InlineData(FilterOperator.Equals,  0x6u, false)]
        public void A_data_bits_mask_reads_the_way_its_operator_says(
            FilterOperator op, uint value, bool expected)
        {
            // A data bits column carries whatever its effect type needs, so the field offers an exact value
            // and both mask readings rather than the schema guessing which the column is.
            FilterSchema<ISpellEffectModel> schema = Registry().For<ISpellEffectModel>(PaneDescriptor.Effects);

            var query = new FilterQuery();
            query.Groups.Add(Group(new FilterCondition
            {
                Field = FilterFields.EffectData(0), Value = "0x" + value.ToString("X"), Operator = op
            }));

            IModelFilter<ISpellEffectModel> filter = FilterQueryCompiler.Compile(query, schema, out _);

            Assert.Equal(expected, filter.Filter(new TestEffect
            {
                Entry = new Spell4EffectsEntry { DataBits00 = 0x7 }
            }));
        }

        [Fact]
        public void The_proc_reference_index_is_read_into_the_filter_rather_than_borrowed_live()
        {
            // Handing the predicate the reference index's live KeyCollection would have it read the engine
            // once per row on the thread pool. A reload clearing that dictionary underneath it is a mutated
            // dictionary read mid-enumeration; and even with no other thread in sight, a compiled filter
            // that keeps changing its mind about what it matches is not a compiled filter.
            _models.SpellProcReferences[100] = [1];

            var query = new FilterQuery();
            query.Groups.Add(Group(new FilterCondition
            {
                Field = FilterFields.ProcReferenced, Operator = FilterOperator.IsSet
            }));

            IModelFilter<ISpellProcModel> filter = FilterQueryCompiler.Compile(
                query, Registry().For<ISpellProcModel>(PaneDescriptor.Procs), out _);

            Assert.True(filter.Filter(new TestProc { SpellId = 100 }));
            Assert.False(filter.Filter(new TestProc { SpellId = 200 }));

            // The engine moves on; the filter that was already compiled does not.
            _models.SpellProcReferences.Remove(100);
            _models.SpellProcReferences[200] = [1];

            Assert.True(filter.Filter(new TestProc { SpellId = 100 }));
            Assert.False(filter.Filter(new TestProc { SpellId = 200 }));
        }

        private IModelFilter<ISpellModel> Compile(FilterQuery query) => Compile(query, out _);

        private IModelFilter<ISpellModel> Compile(FilterQuery query, out IReadOnlyList<FilterDiagnostic> diagnostics)
        {
            FilterSchema<ISpellModel> schema = Registry().For<ISpellModel>(PaneDescriptor.Spell4);
            return FilterQueryCompiler.Compile(query, schema, out diagnostics);
        }

        private static FilterCondition Condition(string field, string value = "", bool negate = false) =>
            new() { Field = field, Value = value, Negate = negate };

        private static FilterGroup Group(params FilterCondition[] conditions)
        {
            var group = new FilterGroup();
            group.Conditions.AddRange(conditions);
            return group;
        }

        private static ISpellModel Spell(uint id, string description = "", DamageType school = DamageType.Physical)
        {
            return new TestSpell
            {
                Entry          = new Spell4Entry { Id = id },
                Description    = description,
                SpellBaseModel = new TestBase
                {
                    Entry = new Spell4BaseEntry { School = (uint)school },

                    // The search box reaches the name too, so give each spell its own rather than letting
                    // the fake's default match everything.
                    Name  = description
                }
            };
        }

        private static PaneDescriptor Descriptor(PaneKind kind) => kind switch
        {
            PaneKind.GameTable => GameTable("Spell4Effects"),
            _                  => PaneDescriptor.AllFixed.First(d => d.Kind == kind)
        };

        private static PaneDescriptor GameTable(string name) =>
            new(PaneDescriptor.GameTableId(name), PaneKind.GameTable, name, name, "", "", name);
    }
}
