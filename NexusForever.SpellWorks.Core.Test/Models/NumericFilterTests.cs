using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter.Effect;
using NexusForever.SpellWorks.Core.Models.Filter.Numeric;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Models
{
    /// <summary>
    /// The numeric thresholds. The comparison lives once in <see cref="SpellModelRangeFilter"/>, so it is
    /// driven hard on one field and each of the rest is checked only for reading the column it names.
    /// </summary>
    public class NumericFilterTests
    {
        [Theory]
        [InlineData(1500u, false, true)]
        [InlineData(2500u, false, false)]
        [InlineData(2500u, true,  true)]
        [InlineData(1500u, true,  false)]
        [InlineData(2000u, false, true)]
        [InlineData(2000u, true,  true)]
        public void A_threshold_is_inclusive_at_either_end(uint value, bool atMost, bool expected)
        {
            var filter = new SpellModelCastTimeFilter { Value = value, AtMost = atMost };
            ISpellModel spell = SpellModelBuilder.A(1).CastTime(2000).Build();

            Assert.Equal(expected, filter.Filter(spell));
        }

        [Fact]
        public void A_spell_with_no_base_row_still_answers_a_threshold_on_its_own_columns()
        {
            // The numeric Spell4 columns are on the spell itself, not the join, so the join going missing
            // does not make them unreadable.
            ISpellModel orphan = SpellModelBuilder.A(1).CastTime(2000).NoBase().Build();

            Assert.True(new SpellModelCastTimeFilter { Value = 1000 }.Filter(orphan));
        }

        [Fact]
        public void Each_named_threshold_reads_the_column_it_names()
        {
            ISpellModel spell = SpellModelBuilder.A(1)
                .Timings(castTime: 100, duration: 200, cooldown: 300, channelMax: 400, channelPulse: 500)
                .Reach(min: 5, max: 30, vertical: 8, missileSpeed: 25)
                .Tier(3)
                .Charges(2)
                .Effect(SpellEffectType.Damage)
                .Proc(9)
                .ReferencedByProc(8)
                .Build();

            Assert.True(new SpellModelCastTimeFilter { Value = 100 }.Filter(spell));
            Assert.True(new SpellModelDurationFilter { Value = 200 }.Filter(spell));
            Assert.True(new SpellModelCooldownFilter { Value = 300 }.Filter(spell));
            Assert.True(new SpellModelChannelTimeFilter { Value = 400 }.Filter(spell));
            Assert.True(new SpellModelChannelPulseFilter { Value = 500 }.Filter(spell));
            Assert.True(new SpellModelTierFilter { Value = 3 }.Filter(spell));
            Assert.True(new SpellModelAbilityChargesFilter { Value = 2 }.Filter(spell));
            Assert.True(new SpellModelTargetMinRangeFilter { Value = 5 }.Filter(spell));
            Assert.True(new SpellModelTargetMaxRangeFilter { Value = 30 }.Filter(spell));
            Assert.True(new SpellModelTargetVerticalRangeFilter { Value = 8 }.Filter(spell));
            Assert.True(new SpellModelMissileSpeedFilter { Value = 25 }.Filter(spell));
            Assert.True(new SpellModelEffectCountFilter { Value = 1 }.Filter(spell));
            Assert.True(new SpellModelProcCountFilter { Value = 1 }.Filter(spell));
            Assert.True(new SpellModelProcReferenceCountFilter { Value = 1 }.Filter(spell));

            // And each says no one step above what the spell actually carries.
            Assert.False(new SpellModelCastTimeFilter { Value = 101 }.Filter(spell));
            Assert.False(new SpellModelTierFilter { Value = 4 }.Filter(spell));
            Assert.False(new SpellModelEffectCountFilter { Value = 2 }.Filter(spell));
            Assert.False(new SpellModelProcReferenceCountFilter { Value = 2 }.Filter(spell));
        }

        [Fact]
        public void A_fractional_range_compares_without_rounding()
        {
            // Several of these columns are floats in the client data, so the comparison is done in double.
            ISpellModel spell = SpellModelBuilder.A(1).Reach(min: 0, max: 7.5f, vertical: 0, missileSpeed: 0).Build();

            Assert.True(new SpellModelTargetMaxRangeFilter { Value = 7.4 }.Filter(spell));
            Assert.False(new SpellModelTargetMaxRangeFilter { Value = 7.6 }.Filter(spell));
        }

        [Theory]
        [InlineData(1.0, false, true)]
        [InlineData(3.0, false, false)]
        [InlineData(3.0, true,  true)]
        public void An_effect_threat_multiplier_compares_as_a_floor_or_a_ceiling(
            double value, bool atMost, bool expected)
        {
            var filter = new SpellEffectThreatFilter { Value = value, AtMost = atMost };
            ISpellEffectModel effect = new FakeSpellEffectModel
            {
                Entry = new Spell4EffectsEntry { ThreatMultiplier = 2.0f }
            };

            Assert.Equal(expected, filter.Filter(effect));
        }

        [Fact]
        public void An_effect_with_no_backing_row_matches_no_threat_threshold()
        {
            Assert.False(new SpellEffectThreatFilter().Filter(new FakeSpellEffectModel()));
        }
    }
}
