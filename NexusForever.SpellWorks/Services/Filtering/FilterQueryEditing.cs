namespace NexusForever.SpellWorks.Services.Filtering
{
    /// <summary>
    /// Editing a query as a whole rather than a condition at a time.
    /// </summary>
    /// <remarks>
    /// The "filter this pane to what I just clicked" affordances in the context menus want to say
    /// <c>Set(class, Esper)</c> without caring how many blocks the pane happens to have open, and the form
    /// wants the same helpers for its own rows. Both land here rather than in Razor.
    /// </remarks>
    public static class FilterQueryEditing
    {
        /// <summary>
        /// Constrain <paramref name="field"/> to <paramref name="value"/>, replacing anything already on that
        /// field anywhere in the query.
        /// </summary>
        /// <remarks>
        /// Replacing rather than adding is what a cross-reference affordance means: "show me the Esper ones"
        /// twice running should leave one class constraint, not two that between them match nothing.
        /// </remarks>
        public static FilterCondition Set(this FilterQuery query, string field, string value,
            FilterOperator op = FilterOperator.Equals, bool negate = false)
        {
            query.Remove(field);

            FilterGroup group = query.FirstGroup();
            var condition = new FilterCondition { Field = field, Value = value ?? "", Operator = op, Negate = negate };
            group.Conditions.Add(condition);

            return condition;
        }

        /// <summary>Drop every condition on <paramref name="field"/>, in the common band and every block.</summary>
        public static void Remove(this FilterQuery query, string field)
        {
            query.Common.Conditions.RemoveAll(c => c.Field == field);

            foreach (FilterGroup group in query.Groups)
                group.Conditions.RemoveAll(c => c.Field == field);

            query.Groups.RemoveAll(g => g.Conditions.Count == 0);
        }

        /// <summary>Every condition on <paramref name="field"/>, common band first, then blocks in order.</summary>
        public static IEnumerable<FilterCondition> On(this FilterQuery query, string field)
        {
            return query.AllGroups().SelectMany(g => g.Conditions).Where(c => c.Field == field);
        }

        /// <summary>The value of the first condition on <paramref name="field"/>, or an empty string.</summary>
        public static string ValueOf(this FilterQuery query, string field) =>
            query.On(field).FirstOrDefault()?.Value ?? "";

        public static bool Has(this FilterQuery query, string field) => query.On(field).Any();

        /// <summary>The common band, then every block. The order the chips row reads in.</summary>
        public static IEnumerable<FilterGroup> AllGroups(this FilterQuery query)
        {
            yield return query.Common;

            foreach (FilterGroup group in query.Groups)
                yield return group;
        }

        /// <summary>The first block, adding one if the query has none yet.</summary>
        public static FilterGroup FirstGroup(this FilterQuery query)
        {
            if (query.Groups.Count == 0)
                query.Groups.Add(new FilterGroup());

            return query.Groups[0];
        }

        /// <summary>Append a block, up to the guard rail.</summary>
        public static FilterGroup AddGroup(this FilterQuery query)
        {
            if (query.Groups.Count >= FilterQuery.MaxGroups)
                return query.Groups[^1];

            var group = new FilterGroup();
            query.Groups.Add(group);
            return group;
        }

        /// <summary>
        /// Drop the condition wherever it sits, and the block with it if that leaves the block empty.
        /// </summary>
        public static void RemoveCondition(this FilterQuery query, FilterCondition condition)
        {
            query.Common.Conditions.Remove(condition);

            foreach (FilterGroup group in query.Groups)
                group.Conditions.Remove(condition);

            query.Groups.RemoveAll(g => g.Conditions.Count == 0);
        }

        /// <summary>Move a condition into the common band, so it narrows every block.</summary>
        public static void MakeCommon(this FilterQuery query, FilterCondition condition)
        {
            foreach (FilterGroup group in query.Groups)
                group.Conditions.Remove(condition);

            query.Groups.RemoveAll(g => g.Conditions.Count == 0);

            if (!query.Common.Conditions.Contains(condition))
                query.Common.Conditions.Add(condition);
        }

        /// <summary>Move a condition out of the common band and back into the first block.</summary>
        public static void MakeLocal(this FilterQuery query, FilterCondition condition)
        {
            if (!query.Common.Conditions.Remove(condition))
                return;

            query.FirstGroup().Conditions.Add(condition);
        }

        /// <summary>
        /// Drop conditions that constrain nothing - an empty box, or a choice still on <c>Any</c>. Run before
        /// saving, so a workspace file records what the user meant rather than every control they touched.
        /// </summary>
        public static void Prune(this FilterQuery query, FilterSchema schema)
        {
            if (schema == null)
                return;

            foreach (FilterGroup group in query.AllGroups().ToList())
                group.Conditions.RemoveAll(c => schema.Field(c.Field) is not { } field || field.IsBlank(c));

            query.Groups.RemoveAll(g => g.Conditions.Count == 0);
        }
    }
}
