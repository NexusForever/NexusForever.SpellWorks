using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Models
{
    /// <summary>
    /// The boolean composites every query is folded from. Driven over <c>bool</c> rather than a spell: the
    /// truth table is the whole of their behaviour, and the empty-list cases are the ones worth pinning.
    /// </summary>
    public class CompositeFilterTests
    {
        private static IModelFilter<bool> Yes => new PredicateFilter<bool>(_ => true);
        private static IModelFilter<bool> No  => new PredicateFilter<bool>(_ => false);

        [Theory]
        [InlineData(true,  true,  true)]
        [InlineData(true,  false, false)]
        [InlineData(false, true,  false)]
        [InlineData(false, false, false)]
        public void AllOf_is_conjunction(bool left, bool right, bool expected)
        {
            var filter = new AllOfFilter<bool>(Term(left), Term(right));

            Assert.Equal(expected, filter.Filter(true));
        }

        [Theory]
        [InlineData(true,  true,  true)]
        [InlineData(true,  false, true)]
        [InlineData(false, true,  true)]
        [InlineData(false, false, false)]
        public void AnyOf_is_disjunction(bool left, bool right, bool expected)
        {
            var filter = new AnyOfFilter<bool>(Term(left), Term(right));

            Assert.Equal(expected, filter.Filter(true));
        }

        [Fact]
        public void An_empty_AllOf_matches_everything()
        {
            // The identity for AND, and what an unconstrained form must do.
            Assert.True(new AllOfFilter<bool>().Filter(true));
        }

        [Fact]
        public void An_empty_AnyOf_matches_everything_rather_than_nothing()
        {
            // Deliberately not the identity for OR. An empty one can only come from a construction mistake,
            // and widening the grid is a less alarming failure than blanking it.
            Assert.True(new AnyOfFilter<bool>().Filter(true));
        }

        [Fact]
        public void Not_inverts_its_child()
        {
            Assert.False(new NotFilter<bool>(Yes).Filter(true));
            Assert.True(new NotFilter<bool>(No).Filter(true));
        }

        [Fact]
        public void Not_rejects_a_missing_child()
        {
            Assert.Throws<ArgumentNullException>(() => new NotFilter<bool>(null));
        }

        [Fact]
        public void MatchAll_matches_everything()
        {
            Assert.True(MatchAllFilter<bool>.Instance.Filter(true));
            Assert.True(MatchAllFilter<bool>.Instance.Filter(false));
        }

        [Fact]
        public void Predicate_rejects_a_missing_lambda()
        {
            Assert.Throws<ArgumentNullException>(() => new PredicateFilter<bool>(null));
        }

        [Fact]
        public void Nesting_composes_a_full_DNF_expression()
        {
            // (deprecated AND NOT has-procs) OR id-starts-with-7
            var query = new AnyOfFilter<ISpellModel>(
                new AllOfFilter<ISpellModel>(
                    new SpellModelDeprecatedFilter(),
                    new NotFilter<ISpellModel>(new SpellModelHasProcsFilter())),
                new SpellModelIdFilter { IdPrefix = "7" });

            Assert.True(query.Filter(SpellModelBuilder.A(1, "[DEPRECATED] old").Build()));
            Assert.False(query.Filter(SpellModelBuilder.A(1, "[DEPRECATED] old").Proc(9).Build()));
            Assert.True(query.Filter(SpellModelBuilder.A(7157, "Arcane Missile").Build()));
            Assert.False(query.Filter(SpellModelBuilder.A(8000, "Arcane Missile").Build()));
        }

        [Theory]
        [InlineData(MaskMode.All, 0x06u, 0x06u, true)]
        [InlineData(MaskMode.All, 0x02u, 0x06u, false)]
        [InlineData(MaskMode.Any, 0x02u, 0x06u, true)]
        [InlineData(MaskMode.Any, 0x08u, 0x06u, false)]
        [InlineData(MaskMode.All, 0x00u, 0x00u, true)]
        [InlineData(MaskMode.Any, 0x00u, 0x00u, true)]
        public void Mask_modes_differ_on_partial_overlap(MaskMode mode, uint value, uint mask, bool expected)
        {
            Assert.Equal(expected, mode.Matches(value, mask));
        }

        private static IModelFilter<bool> Term(bool result) => result ? Yes : No;
    }
}
