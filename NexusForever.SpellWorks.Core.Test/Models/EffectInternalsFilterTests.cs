using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.SpellWorks.Core.Models.Filter.Effect;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Models
{
    /// <summary>
    /// The effect row's internals: its data bits, parameters, phase, order, group and prerequisites. These
    /// are what make the Effects pane investigable rather than four fields deep.
    /// </summary>
    public class EffectInternalsFilterTests
    {
        [Theory]
        [InlineData(0, 100u)]
        [InlineData(1, 101u)]
        [InlineData(2, 102u)]
        [InlineData(3, 103u)]
        [InlineData(4, 104u)]
        [InlineData(5, 105u)]
        [InlineData(6, 106u)]
        [InlineData(7, 107u)]
        [InlineData(8, 108u)]
        [InlineData(9, 109u)]
        public void Each_data_bits_column_is_addressed_by_its_own_index(int index, uint expected)
        {
            ISpellEffectModel effect = WithData();

            Assert.True(new SpellEffectDataBitsFilter { Index = index, Value = expected }.Filter(effect));
            Assert.False(new SpellEffectDataBitsFilter { Index = index, Value = expected + 1 }.Filter(effect));
        }

        [Theory]
        [InlineData(DataBitsMatch.Equals,  0x06u, false)]
        [InlineData(DataBitsMatch.MaskAll, 0x06u, true)]
        [InlineData(DataBitsMatch.MaskAll, 0x08u, false)]
        [InlineData(DataBitsMatch.MaskAny, 0x08u, false)]
        [InlineData(DataBitsMatch.MaskAny, 0x04u, true)]
        public void A_data_bits_column_can_be_read_as_a_value_or_as_a_mask(
            DataBitsMatch match, uint value, bool expected)
        {
            // 0x07 - so an exact 0x06 misses, an all-bits 0x06 hits, and an any-bits 0x08 misses.
            ISpellEffectModel effect = Effect(e => e.DataBits00 = 0x07);

            Assert.Equal(expected,
                new SpellEffectDataBitsFilter { Index = 0, Value = value, Match = match }.Filter(effect));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(10)]
        public void A_data_bits_index_outside_the_ten_columns_matches_nothing(int index)
        {
            Assert.False(new SpellEffectDataBitsFilter { Index = index }.Filter(WithData()));
        }

        [Fact]
        public void The_damage_type_matches_the_school_the_effect_deals_in()
        {
            var filter = new SpellEffectDamageTypeFilter { DamageType = DamageType.Magic };

            Assert.True(filter.Filter(new FakeSpellEffectModel { DamageType = (uint)DamageType.Magic }));
            Assert.False(filter.Filter(new FakeSpellEffectModel { DamageType = (uint)DamageType.Physical }));
        }

        [Theory]
        [InlineData(0x06u, MaskMode.All, true)]
        [InlineData(0x08u, MaskMode.All, false)]
        [InlineData(0x0Cu, MaskMode.Any, true)]
        public void Phase_flags_honour_the_mask_mode(uint flags, MaskMode mode, bool expected)
        {
            ISpellEffectModel effect = Effect(e => e.PhaseFlags = 0x07);

            Assert.Equal(expected,
                new SpellEffectPhaseFlagsFilter { Flags = flags, Mode = mode }.Filter(effect));
        }

        [Fact]
        public void An_empty_phase_mask_keeps_everything()
        {
            Assert.True(new SpellEffectPhaseFlagsFilter().Filter(new FakeSpellEffectModel()));
        }

        [Theory]
        [InlineData(2u, false, true)]
        [InlineData(4u, false, false)]
        [InlineData(4u, true,  true)]
        public void The_order_index_compares_as_a_floor_or_a_ceiling(uint value, bool atMost, bool expected)
        {
            ISpellEffectModel effect = Effect(e => e.OrderIndex = 3);

            Assert.Equal(expected,
                new SpellEffectOrderIndexFilter { Value = value, AtMost = atMost }.Filter(effect));
        }

        [Fact]
        public void The_group_list_matches_exactly()
        {
            ISpellEffectModel effect = Effect(e => e.Spell4EffectGroupListId = 42);

            Assert.True(new SpellEffectGroupListFilter { GroupListId = 42 }.Filter(effect));
            Assert.False(new SpellEffectGroupListFilter { GroupListId = 43 }.Filter(effect));
        }

        [Theory]
        [InlineData(EmmPart.Comparison, 2u, true)]
        [InlineData(EmmPart.Comparison, 9u, false)]
        [InlineData(EmmPart.Value,      7u, true)]
        [InlineData(EmmPart.Value,      2u, false)]
        public void Either_half_of_the_emm_condition_can_be_asked_for(EmmPart part, uint value, bool expected)
        {
            ISpellEffectModel effect = Effect(e =>
            {
                e.EmmComparison = 2;
                e.EmmValue      = 7;
            });

            Assert.Equal(expected, new SpellEffectEmmFilter { Part = part, Value = value }.Filter(effect));
        }

        [Theory]
        [InlineData(EffectPrerequisite.CasterApply,       11u, true)]
        [InlineData(EffectPrerequisite.TargetApply,       22u, true)]
        [InlineData(EffectPrerequisite.CasterPersistence, 33u, true)]
        [InlineData(EffectPrerequisite.TargetPersistence, 44u, true)]
        [InlineData(EffectPrerequisite.TargetSuspend,     55u, true)]
        [InlineData(EffectPrerequisite.CasterApply,       22u, false)]
        public void Each_prerequisite_slot_is_read_separately(
            EffectPrerequisite slot, uint id, bool expected)
        {
            ISpellEffectModel effect = Effect(e =>
            {
                e.PrerequisiteIdCasterApply       = 11;
                e.PrerequisiteIdTargetApply       = 22;
                e.PrerequisiteIdCasterPersistence = 33;
                e.PrerequisiteIdTargetPersistence = 44;
                e.PrerequisiteIdTargetSuspend     = 55;
            });

            Assert.Equal(expected,
                new SpellEffectPrerequisiteFilter { Slot = slot, PrerequisiteId = id }.Filter(effect));
        }

        [Fact]
        public void Asking_for_prerequisite_zero_finds_the_unconditional_effects()
        {
            // A zero slot means "no prerequisite", so it is a real question rather than an empty constraint.
            Assert.True(new SpellEffectPrerequisiteFilter().Filter(Effect(_ => { })));
        }

        [Fact]
        public void A_parameter_constraint_on_the_type_alone_asks_whether_the_effect_has_one()
        {
            ISpellEffectModel effect = WithParameters();

            Assert.True(new SpellEffectParameterFilter
            {
                ParameterType = SpellEffectParameterType.Finesse
            }.Filter(effect));
        }

        [Fact]
        public void A_parameter_threshold_reads_only_the_slot_of_that_type()
        {
            // The value in another slot means something else entirely, so it must not answer this question.
            ISpellEffectModel effect = WithParameters();

            Assert.True(new SpellEffectParameterFilter
            {
                ParameterType = SpellEffectParameterType.Finesse, Value = 5
            }.Filter(effect));

            Assert.False(new SpellEffectParameterFilter
            {
                ParameterType = SpellEffectParameterType.Finesse, Value = 50
            }.Filter(effect));

            Assert.True(new SpellEffectParameterFilter
            {
                ParameterType = SpellEffectParameterType.Finesse, Value = 50, AtMost = true
            }.Filter(effect));
        }

        [Fact]
        public void A_parameter_slot_with_no_value_behind_it_is_skipped()
        {
            // The two arrays are parallel but nothing guarantees they are the same length; a type with no
            // value alongside it has nothing to compare, so it must be passed over rather than read.
            ISpellEffectModel effect = Effect(e =>
            {
                e.ParameterType  = [SpellEffectParameterType.Brutality, SpellEffectParameterType.Finesse];
                e.ParameterValue = [99f];
            });

            Assert.False(new SpellEffectParameterFilter
            {
                ParameterType = SpellEffectParameterType.Finesse, Value = 5
            }.Filter(effect));

            // The type alone still matches - that question never reaches the values.
            Assert.True(new SpellEffectParameterFilter
            {
                ParameterType = SpellEffectParameterType.Finesse
            }.Filter(effect));
        }

        [Fact]
        public void An_effect_with_no_parameters_matches_no_parameter_constraint()
        {
            Assert.False(new SpellEffectParameterFilter().Filter(new FakeSpellEffectModel()));
            Assert.False(new SpellEffectParameterFilter().Filter(Effect(_ => { })));
        }

        [Fact]
        public void Every_internals_filter_tolerates_an_effect_with_no_backing_row()
        {
            var bare = new FakeSpellEffectModel();

            Assert.False(new SpellEffectDataBitsFilter().Filter(bare));
            Assert.False(new SpellEffectOrderIndexFilter().Filter(bare));
            Assert.False(new SpellEffectGroupListFilter().Filter(bare));
            Assert.False(new SpellEffectEmmFilter().Filter(bare));
            Assert.False(new SpellEffectPrerequisiteFilter().Filter(bare));
            Assert.False(new SpellEffectPhaseFlagsFilter { Flags = 1 }.Filter(bare));
        }

        private static ISpellEffectModel Effect(Action<Spell4EffectsEntry> configure)
        {
            var entry = new Spell4EffectsEntry();
            configure(entry);

            return new FakeSpellEffectModel { Entry = entry };
        }

        private static ISpellEffectModel WithData() => Effect(e =>
        {
            e.DataBits00 = 100;
            e.DataBits01 = 101;
            e.DataBits02 = 102;
            e.DataBits03 = 103;
            e.DataBits04 = 104;
            e.DataBits05 = 105;
            e.DataBits06 = 106;
            e.DataBits07 = 107;
            e.DataBits08 = 108;
            e.DataBits09 = 109;
        });

        private static ISpellEffectModel WithParameters() => Effect(e =>
        {
            e.ParameterType  = [SpellEffectParameterType.Brutality, SpellEffectParameterType.Finesse];
            e.ParameterValue = [99f, 10f];
        });
    }
}
