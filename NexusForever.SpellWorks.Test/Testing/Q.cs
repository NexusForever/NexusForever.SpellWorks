using NexusForever.SpellWorks.Services.Filtering;

namespace NexusForever.SpellWorks.Test.Testing
{
    /// <summary>
    /// Terse construction of a <see cref="FilterQuery"/> for tests.
    /// </summary>
    /// <remarks>
    /// A projection test wants to say "narrowed to Damage effects", not to hand-build a tree three objects
    /// deep. <see cref="And"/> adds to the last block, <see cref="Or"/> opens a new one, so a whole DNF query
    /// reads as one chain in the order it would be drawn on screen.
    /// </remarks>
    internal static class Q
    {
        /// <summary>No constraints: every row.</summary>
        public static FilterQuery All() => new();

        public static FilterQuery Searching(string text) => new() { Search = text };

        /// <summary>The id box, which is a separate question from the text box and AND-ed with it.</summary>
        public static FilterQuery SearchingId(string text) => new() { IdSearch = text };

        /// <summary>Ask both boxes for a whole value rather than a substring.</summary>
        public static FilterQuery Exactly(this FilterQuery query)
        {
            query.ExactSearch = true;
            return query;
        }

        public static FilterQuery SearchingId(this FilterQuery query, string text)
        {
            query.IdSearch = text;
            return query;
        }

        public static FilterQuery With(string field, string value,
            FilterOperator op = FilterOperator.Equals, bool negate = false) =>
            All().And(field, value, op, negate);

        /// <summary>A bare toggle: the field asserted, or denied when <paramref name="negate"/> is set.</summary>
        public static FilterQuery Toggling(string field, bool negate = false) =>
            With(field, "", FilterOperator.IsSet, negate);

        /// <summary>AND another condition into the last block.</summary>
        public static FilterQuery And(this FilterQuery query, string field, string value,
            FilterOperator op = FilterOperator.Equals, bool negate = false)
        {
            query.FirstGroup();
            query.Groups[^1].Conditions.Add(new FilterCondition
            {
                Field = field, Value = value ?? "", Operator = op, Negate = negate
            });

            return query;
        }

        /// <summary>OR a new block on, carrying its first condition.</summary>
        public static FilterQuery Or(this FilterQuery query, string field, string value,
            FilterOperator op = FilterOperator.Equals, bool negate = false)
        {
            query.Groups.Add(new FilterGroup());
            return query.And(field, value, op, negate);
        }

        public static FilterQuery Searching(this FilterQuery query, string text)
        {
            query.Search = text;
            return query;
        }
    }
}
