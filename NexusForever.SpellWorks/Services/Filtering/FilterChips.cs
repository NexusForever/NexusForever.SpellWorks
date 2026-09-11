namespace NexusForever.SpellWorks.Services.Filtering
{
    /// <summary>
    /// One entry of the chips row: a readable constraint and the action that clears it, or the <c>OR</c>
    /// rule that separates two blocks.
    /// </summary>
    public sealed record FilterChip(string Label, Action Clear, bool IsSeparator = false);

    /// <summary>
    /// Renders a query back as chips, in disjunctive normal form order.
    /// </summary>
    /// <remarks>
    /// This is the only place the whole expression is stated in one line, which is what makes an OR query
    /// readable at a glance - the tab badge counts conditions and cannot say how they compose.
    /// </remarks>
    public static class FilterChips
    {
        public static List<FilterChip> For(FilterQuery query, FilterSchema schema)
        {
            List<FilterChip> chips = [];
            if (query == null || schema == null)
                return chips;

            foreach (FilterCondition condition in query.Common.Conditions)
                Add(chips, query, schema, condition, common: true);

            foreach (FilterGroup group in query.Groups)
            {
                int before = chips.Count;

                foreach (FilterCondition condition in group.Conditions)
                    Add(chips, query, schema, condition, common: false);

                // Only rule off between blocks that actually contributed something to read.
                if (chips.Count > before && group != query.Groups[^1] && chips.Count > 0)
                    chips.Add(new FilterChip("OR", null, IsSeparator: true));
            }

            // A trailing rule means the last block was all blanks; it separates nothing.
            if (chips.Count > 0 && chips[^1].IsSeparator)
                chips.RemoveAt(chips.Count - 1);

            return chips;
        }

        /// <summary>How many conditions actually constrain anything. What the tab badge shows.</summary>
        public static int ActiveCount(FilterQuery query, FilterSchema schema)
        {
            if (query == null || schema == null)
                return 0;

            return query.AllGroups()
                .SelectMany(g => g.Conditions)
                .Count(c => schema.Field(c.Field) is { } field && !field.IsBlank(c));
        }

        private static void Add(
            List<FilterChip> chips, FilterQuery query, FilterSchema schema, FilterCondition condition, bool common)
        {
            FilterFieldSchema field = schema.Field(condition.Field);
            if (field == null || field.IsBlank(condition))
                return;

            string label = Label(field, condition);
            if (common)
                label = "all · " + label;

            chips.Add(new FilterChip(label, () => query.RemoveCondition(condition)));
        }

        private static string Label(FilterFieldSchema field, FilterCondition condition)
        {
            // a flex column's own label is just a column name, and "databits00 ≥ 5"
            // does not say which row it was asked of. The chips row is where the whole query is meant to
            // be readable in one line, so it reads the label that stands on its own.
            string name = field.QualifiedLabel.ToLowerInvariant();
            string not = condition.Negate ? "not " : "";

            if (field.Control == FilterControlKind.Toggle)
                return not + name;

            return $"{not}{name} {Operator(condition.Operator)} {condition.Value.Trim()}";
        }

        private static string Operator(FilterOperator op) => op switch
        {
            FilterOperator.Contains   => "has",
            FilterOperator.StartsWith => "=",
            FilterOperator.AtLeast    => "≥",
            FilterOperator.AtMost     => "≤",
            FilterOperator.MaskAll    => "all of",
            FilterOperator.MaskAny    => "any of",
            _                         => "="
        };
    }
}
