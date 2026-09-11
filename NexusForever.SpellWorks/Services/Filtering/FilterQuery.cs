using System.Text;

namespace NexusForever.SpellWorks.Services.Filtering
{
    /// <summary>
    /// How a condition compares its field to its value. Which of these a field offers is the schema's call.
    /// </summary>
    public enum FilterOperator
    {
        Equals,
        Contains,
        StartsWith,
        AtLeast,
        AtMost,
        MaskAll,
        MaskAny,

        /// <summary>A bare toggle - the field is either asserted or, negated, denied.</summary>
        IsSet
    }

    /// <summary>
    /// One constraint: a field of the pane's schema, an operator, a raw value, and a polarity.
    /// </summary>
    /// <remarks>
    /// Mutable, and deliberately not a record: the filter form binds straight at <see cref="Value"/> and
    /// edits conditions in place. Record value-equality would also invite using equality for change
    /// detection, which cannot work here - a captured "previous query" shares these very objects and mutates
    /// along with the live one. <see cref="FilterQuery.Signature"/> is the change signal instead.
    ///
    /// <see cref="Value"/> stays a string all the way to the schema factory, which is the only place that
    /// knows how to parse it. That is what keeps "a malformed constraint is ignored, never fatal" true.
    /// </remarks>
    public sealed class FilterCondition
    {
        /// <summary>Schema key - <c>"castMethod"</c>, <c>"effect.type"</c>, <c>"col:DataBits00"</c>.</summary>
        public string Field { get; set; } = "";

        public FilterOperator Operator { get; set; }

        /// <summary>Raw form text. Never parsed here.</summary>
        public string Value { get; set; } = "";

        public bool Negate { get; set; }

        public FilterCondition Clone() => new()
        {
            Field    = Field,
            Operator = Operator,
            Value    = Value,
            Negate   = Negate
        };
    }

    /// <summary>
    /// Conditions AND-ed together. One group renders as one block of the form.
    /// </summary>
    /// <remarks>
    /// Deliberately uncapped. The form creates conditions in several ways, typing into a field among them,
    /// and a cap would have to be enforced by every one of them and by the loader alike. A block the user
    /// could build but not save back is worse than a wide one, and the cost of a wide block is one
    /// predicate per condition. <see cref="FilterQuery.MaxGroups"/> is capped: a block is drawn in full for
    /// every OR block there is, so that one bounds the form as well as the compile.
    /// </remarks>
    public sealed class FilterGroup
    {
        public List<FilterCondition> Conditions { get; } = [];

        public FilterGroup Clone()
        {
            var clone = new FilterGroup();
            foreach (FilterCondition condition in Conditions)
                clone.Conditions.Add(condition.Clone());

            return clone;
        }
    }

    /// <summary>
    /// One pane's whole filter, in disjunctive normal form: conditions AND within a group, groups OR across.
    /// </summary>
    /// <remarks>
    /// Every boolean expression is representable in DNF, and the shape being fixed is what lets the form draw
    /// precedence as layout - stacked blocks with an OR rule between them - instead of parentheses.
    ///
    /// An empty <see cref="Groups"/> matches everything. A group with no valid conditions is <em>dropped</em>
    /// by the compiler rather than treated as true: one unparseable term inside an OR block would otherwise
    /// silently widen the grid to every row, which is the one thing a filter must never do.
    /// </remarks>
    public sealed class FilterQuery
    {
        /// <summary>Cap on OR blocks, so a hand-edited workspace file cannot build a query that takes a second to compile.</summary>
        public const int MaxGroups = 8;

        /// <summary>
        /// The toolbar's text search. AND-ed across the whole disjunction, not into one block.
        /// </summary>
        public string Search { get; set; } = "";

        /// <summary>
        /// The toolbar's id search, kept apart from <see cref="Search"/>.
        /// </summary>
        /// <remarks>
        /// One box doing both would match a typed number against every description as well, so searching
        /// for an id would drag in every row whose text happened to contain those digits. Two boxes,
        /// AND-ed, let each be asked precisely - and let the same query ask for an id <em>and</em> a word.
        /// </remarks>
        public string IdSearch { get; set; } = "";

        /// <summary>
        /// Whether the two search boxes match a whole value rather than a substring.
        /// </summary>
        /// <remarks>
        /// Off by default, so a search matches a substring unless asked otherwise. It governs
        /// both boxes at once: one checkbox that means the same thing wherever it is read.
        /// </remarks>
        public bool ExactSearch { get; set; }

        /// <summary>Conditions AND-ed into every group - the band above the blocks.</summary>
        public FilterGroup Common { get; } = new();

        public List<FilterGroup> Groups { get; } = [];

        /// <summary>Total conditions across the common band and every block. What the tab badge counts.</summary>
        public int ConditionCount => Common.Conditions.Count + Groups.Sum(g => g.Conditions.Count);

        public bool IsEmpty => ConditionCount == 0
            && string.IsNullOrWhiteSpace(Search)
            && string.IsNullOrWhiteSpace(IdSearch);

        /// <summary>
        /// A deep copy.
        /// </summary>
        public FilterQuery Clone()
        {
            var clone = new FilterQuery { Search = Search, IdSearch = IdSearch, ExactSearch = ExactSearch };

            foreach (FilterCondition condition in Common.Conditions)
                clone.Common.Conditions.Add(condition.Clone());

            foreach (FilterGroup group in Groups)
                clone.Groups.Add(group.Clone());

            return clone;
        }

        /// <summary>
        /// Rehydrate in place from <paramref name="other"/>.
        /// </summary>
        public void CopyFrom(FilterQuery other)
        {
            Search      = other?.Search ?? "";
            IdSearch    = other?.IdSearch ?? "";
            ExactSearch = other?.ExactSearch ?? false;

            Common.Conditions.Clear();
            Groups.Clear();

            if (other == null)
                return;

            foreach (FilterCondition condition in other.Common.Conditions)
                Common.Conditions.Add(condition.Clone());

            foreach (FilterGroup group in other.Groups.Take(MaxGroups))
            {
                var copy = new FilterGroup();
                foreach (FilterCondition condition in group.Conditions)
                    copy.Conditions.Add(condition.Clone());

                Groups.Add(copy);
            }
        }

        public void Reset()
        {
            Common.Conditions.Clear();
            Groups.Clear();
        }

        public void ResetAll()
        {
            Reset();

            Search   = "";
            IdSearch = "";
        }

        // Non-printing separators, so no value a user can type could forge a boundary.
        private const char FieldSeparator     = '\u001e';
        private const char ConditionSeparator = '\u001f';
        private const char GroupSeparator     = '\u001d';

        /// <summary>
        /// A cheap identity for "the row set would differ". Compared as a string rather than by structural
        /// equality: conditions are edited in place, so a captured query object mutates along with the live
        /// one and never compares unequal. Group order is user-visible, so reordering does invalidate - a
        /// spurious reload is far cheaper than a missed one.
        /// </summary>
        public string Signature()
        {
            var builder = new StringBuilder();
            builder.Append(Search).Append(FieldSeparator)
                   .Append(IdSearch).Append(FieldSeparator)
                   .Append(ExactSearch);

            builder.Append(GroupSeparator);
            Append(builder, Common);

            foreach (FilterGroup group in Groups)
            {
                builder.Append(GroupSeparator);
                Append(builder, group);
            }

            return builder.ToString();
        }

        private static void Append(StringBuilder builder, FilterGroup group)
        {
            foreach (FilterCondition condition in group.Conditions)
            {
                builder.Append(condition.Field).Append(FieldSeparator)
                       .Append(condition.Operator).Append(FieldSeparator)
                       .Append(condition.Negate).Append(FieldSeparator)
                       .Append(condition.Value).Append(ConditionSeparator);
            }
        }
    }
}
