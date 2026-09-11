using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.SpellWorks.Core.Models.Filter.Column;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Models
{
    /// <summary>
    /// Filtering a linked game table row by one of its columns: the two readings of a column, and the
    /// correlation that makes several constraints on one row mean one row.
    /// </summary>
    public class ColumnFilterTests
    {
        /// <summary>A stand-in row. The real ones are game table entries, which are fields on a class.</summary>
        private sealed class Row
        {
            public double Number { get; init; }
            public string Text { get; init; } = "";
        }

        private static ColumnNumberFilter Number(double value, NumberMatch match = NumberMatch.Equals) =>
            new() { Read = row => ((Row)row).Number, Value = value, Match = match };

        private static ColumnTextFilter Text(string query, bool exact = false) =>
            new() { Read = row => ((Row)row).Text, Query = query, Exact = exact };

        [Theory]
        [InlineData(NumberMatch.Equals,  5, true)]
        [InlineData(NumberMatch.Equals,  4, false)]
        [InlineData(NumberMatch.AtLeast, 5, true)]
        [InlineData(NumberMatch.AtLeast, 6, false)]
        [InlineData(NumberMatch.AtMost,  5, true)]
        [InlineData(NumberMatch.AtMost,  4, false)]
        public void A_numeric_column_compares_by_the_operator_it_was_given(
            NumberMatch match, double value, bool expected)
        {
            Assert.Equal(expected, Number(value, match).Filter(new Row { Number = 5 }));
        }

        [Theory]
        [InlineData(NumberMatch.MaskAll, 0x06, true)]
        [InlineData(NumberMatch.MaskAll, 0x08, false)]
        [InlineData(NumberMatch.MaskAny, 0x0C, true)]
        [InlineData(NumberMatch.MaskAny, 0x08, false)]
        public void A_whole_number_column_can_also_be_read_as_a_mask(
            NumberMatch match, double value, bool expected)
        {
            // 0x07: an all-bits 0x06 hits, an all-bits 0x08 misses, and 0x0C has one bit in common.
            Assert.Equal(expected, Number(value, match).Filter(new Row { Number = 0x07 }));
        }

        [Theory]
        [InlineData(-1d, 1d)]
        [InlineData(1d, -1d)]
        [InlineData(4294967296d, 1d)]
        [InlineData(1d, 4294967296d)]
        public void A_value_with_no_bits_to_test_satisfies_no_mask(double actual, double mask)
        {
            // A signed or oversized column is not a bitfield, and reading one as if it were would answer a
            // question the column cannot be asked. Failing is the same stance a missing column takes.
            Assert.False(Number(mask, NumberMatch.MaskAll).Filter(new Row { Number = actual }));
            Assert.False(Number(mask, NumberMatch.MaskAny).Filter(new Row { Number = actual }));
        }

        [Theory]
        [InlineData("arcane", false, true)]
        [InlineData("can", false, true)]
        [InlineData("can", true, false)]
        [InlineData("Arcane", true, true)]
        [InlineData("bolt", false, false)]
        public void A_text_column_matches_by_substring_or_whole_value(string query, bool exact, bool expected)
        {
            Assert.Equal(expected, Text(query, exact).Filter(new Row { Text = "arcane" }));
        }

        [Fact]
        public void A_column_constraint_is_asked_of_a_row_and_so_never_of_nothing()
        {
            Assert.False(Number(5).Filter(null));
            Assert.False(Text("arcane").Filter(null));
        }

        // ------------------------------------------------------------------ correlation

        private static RowMatchFilter<Row[]> Match(params IModelFilter<object>[] conditions) =>
            new() { Rows = rows => rows, Conditions = conditions };

        [Fact]
        public void A_match_needs_one_row_that_satisfies_every_condition()
        {
            RowMatchFilter<Row[]> filter = Match(Number(5), Text("arcane"));

            Assert.True(filter.Filter([new Row { Number = 5, Text = "arcane" }]));
            Assert.False(filter.Filter([new Row { Number = 5, Text = "bolt" }]));
        }

        [Fact]
        public void Two_rows_that_each_satisfy_half_of_a_group_satisfy_none_of_it()
        {
            // This is the whole point. Asking the conditions independently would call this a match, and a
            // block asking for "an effect of type 12 with a large DataBits00" would return spells whose
            // type 12 effect is not the one with the large value.
            Assert.False(Match(Number(5), Text("arcane")).Filter(
            [
                new Row { Number = 5, Text = "bolt" },
                new Row { Number = 9, Text = "arcane" }
            ]));
        }

        [Fact]
        public void One_row_out_of_many_is_enough()
        {
            Assert.True(Match(Number(5), Text("arcane")).Filter(
            [
                new Row { Number = 1, Text = "bolt" },
                new Row { Number = 5, Text = "arcane" },
                new Row { Number = 9, Text = "shock" }
            ]));
        }

        [Fact]
        public void An_element_with_no_rows_fails_the_constraint_rather_than_passing_it()
        {
            // An unresolved link - a spell whose base has no hit results row - cannot answer a question
            // about that row, and a question it cannot answer is not one it satisfies.
            Assert.False(Match(Number(5)).Filter([]));
            Assert.False(Match(Number(5)).Filter(null));
        }

        [Fact]
        public void A_null_row_among_real_ones_is_skipped_rather_than_matched()
        {
            Assert.True(Match(Number(5)).Filter([null, new Row { Number = 5 }]));
            Assert.False(Match(Number(5)).Filter([null]));
        }
    }
}
