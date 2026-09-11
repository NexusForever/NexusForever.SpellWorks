using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.SpellWorks.Core.Services;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Services
{
    /// <summary>
    /// The search box grammar. The parse is invisible to the user, so the cases that matter most are the ones
    /// where it must <em>not</em> do anything clever with what they typed.
    /// </summary>
    public class SearchExpressionTests
    {
        [Fact]
        public void A_bare_term_is_one_group_of_one()
        {
            SearchExpression expression = SearchExpression.Parse("missile");

            SearchTerm term = Assert.Single(Assert.Single(expression.Groups));
            Assert.Equal("missile", term.Text);
            Assert.False(term.Negate);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Nothing_typed_is_no_constraint(string input)
        {
            Assert.True(SearchExpression.Parse(input).IsEmpty);
        }

        [Fact]
        public void Whitespace_is_not_an_implicit_and()
        {
            // "fire bolt" matches that literal phrase. Splitting on whitespace would change the meaning of
            // ordinary searches.
            SearchTerm term = Assert.Single(Assert.Single(SearchExpression.Parse("fire bolt").Groups));

            Assert.Equal("fire bolt", term.Text);
        }

        [Fact]
        public void Or_splits_into_groups()
        {
            SearchExpression expression = SearchExpression.Parse("7157 || 7158");

            Assert.Equal(2, expression.Groups.Count);
            Assert.Equal("7157", expression.Groups[0][0].Text);
            Assert.Equal("7158", expression.Groups[1][0].Text);
        }

        [Fact]
        public void And_splits_within_a_group()
        {
            SearchExpression expression = SearchExpression.Parse("arcane && missile");

            IReadOnlyList<SearchTerm> group = Assert.Single(expression.Groups);
            Assert.Equal(["arcane", "missile"], group.Select(t => t.Text));
        }

        [Fact]
        public void And_binds_tighter_than_or()
        {
            // a && b || c parses as (a AND b) OR c - the same precedence the form's blocks encode.
            SearchExpression expression = SearchExpression.Parse("a && b || c");

            Assert.Equal(2, expression.Groups.Count);
            Assert.Equal(["a", "b"], expression.Groups[0].Select(t => t.Text));
            Assert.Equal(["c"], expression.Groups[1].Select(t => t.Text));
        }

        [Fact]
        public void A_leading_bang_negates_one_term()
        {
            SearchExpression expression = SearchExpression.Parse("arcane && !deprecated");

            Assert.False(expression.Groups[0][0].Negate);
            Assert.True(expression.Groups[0][1].Negate);
            Assert.Equal("deprecated", expression.Groups[0][1].Text);
        }

        [Fact]
        public void A_bang_inside_a_word_is_ordinary_text()
        {
            Assert.Equal("wow!", Assert.Single(Assert.Single(SearchExpression.Parse("wow!").Groups)).Text);
        }

        [Theory]
        [InlineData("a & b")]
        [InlineData("a | b")]
        public void A_single_operator_character_is_literal_text(string input)
        {
            // Game data is full of lone ampersands and pipes; splitting on those would break real searches.
            Assert.Equal(input, Assert.Single(Assert.Single(SearchExpression.Parse(input).Groups)).Text);
        }

        [Theory]
        [InlineData("a &&", "a")]
        [InlineData("&& a", "a")]
        [InlineData("a ||", "a")]
        public void Incomplete_input_drops_the_empty_term_rather_than_erroring(string input, string expected)
        {
            Assert.Equal(expected, Assert.Single(Assert.Single(SearchExpression.Parse(input).Groups)).Text);
        }

        [Theory]
        [InlineData("|| ||")]
        [InlineData("&&")]
        [InlineData("!")]
        public void Input_that_is_all_operators_constrains_nothing(string input)
        {
            Assert.True(SearchExpression.Parse(input).IsEmpty);
        }

        [Fact]
        public void Compiling_folds_the_parse_into_the_same_composites_the_form_uses()
        {
            IModelFilter<string> filter = SearchExpression
                .Parse("a && !b || c")
                .Compile<string>(term => new PredicateFilter<string>(s => s.Contains(term)));

            Assert.True(filter.Filter("a"));     // a AND NOT b
            Assert.False(filter.Filter("ab"));   // b excluded
            Assert.True(filter.Filter("c"));     // second alternative
            Assert.True(filter.Filter("abc"));   // still matches through c
            Assert.False(filter.Filter("b"));
        }

        [Fact]
        public void An_empty_expression_compiles_to_match_all()
        {
            IModelFilter<string> filter = SearchExpression.Empty.Compile<string>(_ => null);

            Assert.IsType<MatchAllFilter<string>>(filter);
            Assert.True(filter.Filter("anything"));
        }

        [Fact]
        public void A_factory_that_declines_a_term_leaves_the_rest_standing()
        {
            IModelFilter<string> filter = SearchExpression
                .Parse("a && b")
                .Compile<string>(term => term == "a" ? new PredicateFilter<string>(s => s.Contains("a")) : null);

            Assert.True(filter.Filter("a"));
            Assert.False(filter.Filter("b"));
        }
    }
}
