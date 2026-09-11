using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.SpellWorks.Core.Models.Filter.Column;
using NexusForever.SpellWorks.Core.Models.Filter.Effect;
using NexusForever.SpellWorks.Core.Models.Filter.Numeric;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Models
{
    /// <summary>
    /// Comparing what a filter was typed with against a column holding a <c>float</c>.
    /// </summary>
    /// <remarks>
    /// The client stores these columns as single-precision and every comparison is made in <c>double</c>, so
    /// the number the row carries is almost never the number the user typed: <c>0.1f</c> widens to
    /// 0.100000001490116…, and an exact <c>==</c> against 0.1 can only ever be false. A tolerance is what
    /// makes "= 0.1" answerable at all, and it is settable because how close is close enough is a property of
    /// the data being read rather than of the comparison.
    /// </remarks>
    public class FloatToleranceTests
    {
        /// <summary>A stand-in row carrying its column as the archive does - a single-precision float.</summary>
        private sealed class Row
        {
            public float Value { get; init; }
        }

        private static ColumnNumberFilter Column(double value, NumberMatch match = NumberMatch.Equals,
            double epsilon = NumberTolerance.Default)
        {
            return new ColumnNumberFilter
            {
                Read    = row => ((Row)row).Value,
                Value   = value,
                Match   = match,
                Epsilon = epsilon
            };
        }

        [Fact]
        public void A_float_column_equals_the_decimal_the_user_typed()
        {
            Assert.True(Column(0.1).Filter(new Row { Value = 0.1f }));
        }

        [Fact]
        public void A_float_column_equals_a_large_decimal_the_user_typed()
        {
            // The slack scales with magnitude: a float near 123456.78 is out by ~0.008, which no absolute
            // epsilon small enough to be honest about 0.1 could ever cover.
            Assert.True(Column(123456.78).Filter(new Row { Value = 123456.78f }));
        }

        [Fact]
        public void A_value_that_is_genuinely_different_still_does_not_match()
        {
            Assert.False(Column(0.1).Filter(new Row { Value = 0.2f }));
            Assert.False(Column(123456.78).Filter(new Row { Value = 123466.78f }));
        }

        [Theory]
        [InlineData(NumberMatch.AtLeast)]
        [InlineData(NumberMatch.AtMost)]
        public void A_threshold_is_inclusive_at_a_bound_the_float_cannot_hold_exactly(NumberMatch match)
        {
            // Both readings are inclusive, so a row sitting on the bound satisfies either - and "on the
            // bound" is the one thing a float is not.
            Assert.True(Column(0.1, match).Filter(new Row { Value = 0.1f }));
        }

        [Fact]
        public void The_tolerance_is_the_callers_to_set()
        {
            // What exposing it buys: a demand for the bit pattern itself is one setting away, and so is a
            // looser reading for data that is only nominally precise.
            Assert.False(Column(0.1, epsilon: 0d).Filter(new Row { Value = 0.1f }));
            Assert.True(Column(0.2, epsilon: 0.5d).Filter(new Row { Value = 0.1f }));
        }

        [Fact]
        public void A_spell_threshold_on_a_float_column_is_inclusive_at_its_bound()
        {
            // The ranges are float columns, so a bare >= in double would miss a row sitting on the bound.
            ISpellModel spell = SpellModelBuilder.A(1).Reach(max: 0.1f).Build();

            Assert.True(new SpellModelTargetMaxRangeFilter { Value = 0.1 }.Filter(spell));
            Assert.True(new SpellModelTargetMaxRangeFilter { Value = 0.1, AtMost = true }.Filter(spell));
            Assert.False(new SpellModelTargetMaxRangeFilter { Value = 0.2 }.Filter(spell));
        }

        [Fact]
        public void An_effect_threat_threshold_is_inclusive_at_its_bound()
        {
            ISpellEffectModel effect = new FakeSpellEffectModel
            {
                Entry = new Spell4EffectsEntry { ThreatMultiplier = 0.1f }
            };

            Assert.True(new SpellEffectThreatFilter { Value = 0.1 }.Filter(effect));
            Assert.True(new SpellEffectThreatFilter { Value = 0.1, AtMost = true }.Filter(effect));
            Assert.False(new SpellEffectThreatFilter { Value = 0.2 }.Filter(effect));
        }

        [Fact]
        public void A_parameter_threshold_on_a_float_slot_is_inclusive_at_its_bound()
        {
            ISpellEffectModel effect = new FakeSpellEffectModel
            {
                Entry = new Spell4EffectsEntry
                {
                    ParameterType  = [SpellEffectParameterType.Finesse],
                    ParameterValue = [0.1f]
                }
            };

            Assert.True(new SpellEffectParameterFilter
            {
                ParameterType = SpellEffectParameterType.Finesse, Value = 0.1
            }.Filter(effect));

            Assert.True(new SpellEffectParameterFilter
            {
                ParameterType = SpellEffectParameterType.Finesse, Value = 0.1, AtMost = true
            }.Filter(effect));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(-1d)]
        public void A_tolerance_that_is_not_a_distance_reads_as_none(double epsilon)
        {
            // The setting is typed into a box, so it has to survive whatever comes out of one without
            // quietly turning every comparison true.
            Assert.False(Column(0.1, epsilon: epsilon).Filter(new Row { Value = 0.2f }));
            Assert.True(Column(0.5, epsilon: epsilon).Filter(new Row { Value = 0.5f }));
        }
    }
}
