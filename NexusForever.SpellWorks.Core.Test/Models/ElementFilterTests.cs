using NexusForever.Game.Static.Spell;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.SpellWorks.Core.Models.Filter.Effect;
using NexusForever.SpellWorks.Core.Models.Filter.EffectType;
using NexusForever.SpellWorks.Core.Models.Filter.Proc;
using NexusForever.SpellWorks.Core.Models.Filter.Table;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Models
{
    /// <summary>
    /// The named filters behind the five element panes. Same shape as <see cref="FilterTests"/> - one
    /// matching case, one not, plus whatever edge each carves out.
    /// </summary>
    public class ElementFilterTests
    {
        // ------------------------------------------------------------------ effects

        [Fact]
        public void Effect_type_matches_exactly()
        {
            var filter = new SpellEffectTypeFilter { Type = SpellEffectType.Damage };

            Assert.True(filter.Filter(SpellEffectModelBuilder.A(SpellEffectType.Damage).Build()));
            Assert.False(filter.Filter(SpellEffectModelBuilder.A(SpellEffectType.Heal).Build()));
        }

        [Theory]
        [InlineData(MaskMode.All, 0x07u, true)]
        [InlineData(MaskMode.All, 0x02u, false)]
        [InlineData(MaskMode.Any, 0x02u, true)]
        [InlineData(MaskMode.Any, 0x08u, false)]
        public void Effect_flags_honour_the_mask_mode(MaskMode mode, uint flags, bool expected)
        {
            var filter = new SpellEffectFlagsFilter { Flags = 0x06, Mode = mode };

            Assert.Equal(expected, filter.Filter(SpellEffectModelBuilder.A().Flags(flags).Build()));
        }

        [Fact]
        public void An_empty_effect_mask_keeps_everything()
        {
            Assert.True(new SpellEffectFlagsFilter().Filter(SpellEffectModelBuilder.A().Build()));
            Assert.True(new SpellEffectTargetFlagsFilter().Filter(SpellEffectModelBuilder.A().Build()));
        }

        [Theory]
        [InlineData(SpellEffectTiming.Delay,    100u, false, true)]
        [InlineData(SpellEffectTiming.Tick,     300u, false, false)]
        [InlineData(SpellEffectTiming.Duration, 900u, true,  true)]
        [InlineData(SpellEffectTiming.Duration, 100u, true,  false)]
        public void Effect_timings_compare_as_a_floor_or_a_ceiling(
            SpellEffectTiming timing, uint value, bool atMost, bool expected)
        {
            var filter = new SpellEffectTimingFilter { Timing = timing, Value = value, AtMost = atMost };
            ISpellEffectModel effect = SpellEffectModelBuilder.A().Timing(delay: 100, tick: 200, duration: 800).Build();

            Assert.Equal(expected, filter.Filter(effect));
        }

        [Fact]
        public void A_timing_the_filter_does_not_know_compares_against_nothing()
        {
            // The switch has to have a default arm; a value outside the enum falls into it and reads as 0,
            // so a floor of 1 rejects and a ceiling of 0 accepts rather than the filter throwing.
            var filter = new SpellEffectTimingFilter { Timing = (SpellEffectTiming)99, Value = 1 };
            ISpellEffectModel effect = SpellEffectModelBuilder.A().Timing(delay: 100, tick: 200, duration: 800).Build();

            Assert.False(filter.Filter(effect));

            filter.AtMost = true;
            filter.Value  = 0;

            Assert.True(filter.Filter(effect));
        }

        [Fact]
        public void The_effects_search_matches_the_type_name()
        {
            var filter = new SpellEffectNameSearchFilter { Query = "dam" };

            Assert.True(filter.Filter(SpellEffectModelBuilder.A(SpellEffectType.Damage).Build()));
            Assert.False(filter.Filter(SpellEffectModelBuilder.A(SpellEffectType.Heal).Build()));
            Assert.True(new SpellEffectNameSearchFilter { Query = "  " }
                .Filter(SpellEffectModelBuilder.A(SpellEffectType.Heal).Build()));
        }

        [Fact]
        public void The_effects_id_search_matches_the_numeric_type_id()
        {
            string id = ((uint)SpellEffectType.Damage).ToString();

            Assert.True(new SpellEffectIdSearchFilter { Query = id, Exact = true }
                .Filter(SpellEffectModelBuilder.A(SpellEffectType.Damage).Build()));
            Assert.False(new SpellEffectIdSearchFilter { Query = id, Exact = true }
                .Filter(SpellEffectModelBuilder.A(SpellEffectType.Heal).Build()));

            Assert.True(new SpellEffectIdSearchFilter()
                .Filter(SpellEffectModelBuilder.A(SpellEffectType.Heal).Build()));
        }

        // ------------------------------------------------------------------ procs

        [Fact]
        public void Proc_type_matches_the_number()
        {
            var filter = new SpellProcTypeFilter { ProcType = 16 };

            Assert.True(filter.Filter(SpellProcModelBuilder.A(procType: 16).Build()));
            Assert.False(filter.Filter(SpellProcModelBuilder.A(procType: 4).Build()));
        }

        [Fact]
        public void Proc_spell_id_matches_on_a_prefix_not_a_substring()
        {
            var filter = new SpellProcSpellIdFilter { IdPrefix = "716" };

            Assert.True(filter.Filter(SpellProcModelBuilder.A(7161).Build()));
            Assert.False(filter.Filter(SpellProcModelBuilder.A(17161).Build()));
            Assert.True(new SpellProcSpellIdFilter().Filter(SpellProcModelBuilder.A(1).Build()));
        }

        [Fact]
        public void Proc_referenced_reads_the_index_it_is_handed()
        {
            var filter = new SpellProcReferencedFilter { ReferencedSpellIds = new HashSet<uint> { 7161 } };

            Assert.True(filter.Filter(SpellProcModelBuilder.A(7161).Build()));
            Assert.False(filter.Filter(SpellProcModelBuilder.A(7162).Build()));

            // No index at all is not "everything is referenced".
            Assert.False(new SpellProcReferencedFilter().Filter(SpellProcModelBuilder.A(7161).Build()));
        }

        [Fact]
        public void The_procs_text_search_matches_the_looked_up_description()
        {
            var filter = new SpellProcTextSearchFilter
            {
                Query       = "arcane",
                Description = id => id == 7161 ? "Arcane Missile" : null
            };

            Assert.True(filter.Filter(SpellProcModelBuilder.A(7161).Build()));
            Assert.False(filter.Filter(SpellProcModelBuilder.A(7162).Build()));

            // With no lookup supplied there is no text to match, and the id is the other box's question.
            Assert.False(new SpellProcTextSearchFilter { Query = "716" }
                .Filter(SpellProcModelBuilder.A(7161).Build()));
        }

        [Fact]
        public void The_procs_id_search_matches_the_cast_spell_id()
        {
            Assert.True(new SpellProcIdSearchFilter { Query = "716" }
                .Filter(SpellProcModelBuilder.A(7161).Build()));
            Assert.False(new SpellProcIdSearchFilter { Query = "716", Exact = true }
                .Filter(SpellProcModelBuilder.A(7161).Build()));
            Assert.True(new SpellProcIdSearchFilter { Query = "7161", Exact = true }
                .Filter(SpellProcModelBuilder.A(7161).Build()));
        }

        // ------------------------------------------------------------------ effect types

        [Fact]
        public void Effect_type_id_matches_on_a_prefix()
        {
            var filter = new EffectTypeIdFilter { IdPrefix = ((uint)SpellEffectType.Damage).ToString() };

            Assert.True(filter.Filter(Usage(SpellEffectType.Damage)));
            Assert.True(new EffectTypeIdFilter().Filter(Usage(SpellEffectType.Heal)));
        }

        [Theory]
        [InlineData(2u, false, true)]
        [InlineData(4u, false, false)]
        [InlineData(4u, true,  true)]
        [InlineData(1u, true,  false)]
        public void Effect_type_spell_counts_compare_as_a_floor_or_a_ceiling(uint value, bool atMost, bool expected)
        {
            var filter = new EffectTypeSpellCountFilter { Value = value, AtMost = atMost };

            Assert.Equal(expected, filter.Filter(Usage(SpellEffectType.Damage, 1, 2, 3)));
        }

        [Fact]
        public void The_effect_types_name_search_matches_the_name_only()
        {
            Assert.True(new EffectTypeNameSearchFilter { Query = "dam" }.Filter(Usage(SpellEffectType.Damage)));
            Assert.False(new EffectTypeNameSearchFilter { Query = "zzz" }.Filter(Usage(SpellEffectType.Damage)));
        }

        [Fact]
        public void The_effect_types_id_search_matches_the_numeric_id_only()
        {
            string id = ((uint)SpellEffectType.Damage).ToString();

            Assert.True(new EffectTypeIdSearchFilter { Query = id }.Filter(Usage(SpellEffectType.Damage)));
            Assert.False(new EffectTypeIdSearchFilter { Query = "dam" }.Filter(Usage(SpellEffectType.Damage)));
        }

        // ------------------------------------------------------------------ tables

        [Fact]
        public void Table_name_matches_a_prefix_case_insensitively()
        {
            var filter = new TableNameFilter { Prefix = "spell4" };

            Assert.True(filter.Filter(TableDescriptorBuilder.A("Spell4Effects")));
            Assert.False(filter.Filter(TableDescriptorBuilder.A("CreatureType")));
        }

        [Fact]
        public void Table_loaded_is_phrased_positively()
        {
            var filter = new TableLoadedFilter();

            Assert.True(filter.Filter(TableDescriptorBuilder.A("Spell4", 12)));
            Assert.False(filter.Filter(TableDescriptorBuilder.A("Spell4")));
        }

        [Fact]
        public void The_tables_search_matches_a_substring()
        {
            Assert.True(new TableSearchFilter { Query = "Effect" }.Filter(TableDescriptorBuilder.A("Spell4Effects")));
            Assert.True(new TableSearchFilter().Filter(TableDescriptorBuilder.A("Spell4Effects")));
        }

        [Fact]
        public void An_exact_tables_search_matches_the_whole_name()
        {
            Assert.False(new TableSearchFilter { Query = "Effect", Exact = true }
                .Filter(TableDescriptorBuilder.A("Spell4Effects")));
            Assert.True(new TableSearchFilter { Query = "spell4effects", Exact = true }
                .Filter(TableDescriptorBuilder.A("Spell4Effects")));
        }

        // ------------------------------------------------------------------ generic table cells

        [Fact]
        public void Row_id_matches_the_first_cell_as_a_prefix()
        {
            var filter = new GameTableRowIdFilter { IdPrefix = "71" };

            Assert.True(filter.Filter(["7157", "x"]));
            Assert.False(filter.Filter(["8157", "x"]));
            Assert.False(filter.Filter([]));
        }

        [Fact]
        public void Contains_matches_any_cell()
        {
            var filter = new GameTableContainsFilter { Query = "arcane" };

            Assert.True(filter.Filter(["1", "Arcane Missile"]));
            Assert.False(filter.Filter(["1", "Healing Wave"]));
        }

        [Fact]
        public void An_exact_contains_matches_a_whole_cell()
        {
            var filter = new GameTableContainsFilter { Query = "Arcane Missile", Exact = true };

            Assert.True(filter.Filter(["1", "arcane missile"]));
            Assert.False(filter.Filter(["1", "Arcane Missile II"]));
        }

        [Theory]
        [InlineData(0,  "6",            MaskMode.All, true)]
        [InlineData(0,  "2",            MaskMode.All, false)]
        [InlineData(0,  "2",            MaskMode.Any, true)]
        [InlineData(0,  "not a number", MaskMode.All, false)]
        [InlineData(9,  "6",            MaskMode.All, false)]
        [InlineData(-1, "6",            MaskMode.All, false)]
        public void A_cell_mask_reads_one_resolved_column(int column, string cell, MaskMode mode, bool expected)
        {
            var filter = new GameTableCellFilter { ColumnIndex = column, Mask = 0x06, Mode = mode };

            Assert.Equal(expected, filter.Filter([cell]));
        }

        [Fact]
        public void An_empty_cell_mask_keeps_everything_even_without_a_column()
        {
            Assert.True(new GameTableCellFilter().Filter(["anything"]));
        }

        [Theory]
        [InlineData(new[] { "1", "0", "" },    false)]
        [InlineData(new[] { "1", "0", "0.0" }, false)]
        [InlineData(new[] { "1", "0", "3" },   true)]
        [InlineData(new[] { "1" },             false)]
        public void Non_zero_ignores_the_id_column(string[] cells, bool expected)
        {
            Assert.Equal(expected, new GameTableNonZeroFilter().Filter(cells));
        }

        [Theory]
        [InlineData(0, "7157", false, true)]
        [InlineData(0, "715",  false, true)]
        [InlineData(0, "715",  true,  false)]
        [InlineData(1, "7157", false, false)]
        [InlineData(9, "7157", false, false)]
        [InlineData(-1, "7157", false, false)]
        public void A_column_constraint_reads_one_resolved_column(
            int column, string query, bool exact, bool expected)
        {
            var filter = new GameTableColumnFilter { ColumnIndex = column, Query = query, Exact = exact };

            Assert.Equal(expected, filter.Filter(["7157", "1"]));
        }

        [Fact]
        public void An_empty_column_constraint_keeps_everything_even_without_a_column()
        {
            Assert.True(new GameTableColumnFilter().Filter(["7157"]));
        }

        // ------------------------------------------------------------------ empty constraints

        [Fact]
        public void An_empty_constraint_keeps_everything_whatever_it_reads()
        {
            // Every filter is skipped rather than applied when its own value says nothing, so a half-filled
            // form narrows by what was filled in and no more.
            Assert.True(new EffectTypeNameSearchFilter().Filter(Usage(SpellEffectType.Damage)));
            Assert.True(new EffectTypeIdSearchFilter().Filter(Usage(SpellEffectType.Damage)));
            Assert.True(new SpellProcTextSearchFilter().Filter(SpellProcModelBuilder.A(1).Build()));
            Assert.True(new SpellProcIdSearchFilter().Filter(SpellProcModelBuilder.A(1).Build()));
            Assert.True(new GameTableRowIdFilter().Filter(["1"]));
            Assert.True(new GameTableContainsFilter().Filter(["1"]));
            Assert.True(new TableNameFilter().Filter(TableDescriptorBuilder.A("Spell4")));
        }

        private static EffectTypeUsage Usage(SpellEffectType type, params uint[] spellIds) =>
            new() { Type = type, SpellIds = spellIds, EffectRowCount = spellIds.Length };
    }
}
