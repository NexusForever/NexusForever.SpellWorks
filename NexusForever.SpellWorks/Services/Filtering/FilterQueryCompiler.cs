using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.SpellWorks.Core.Models.Filter.Column;
using NexusForever.SpellWorks.Core.Services;

namespace NexusForever.SpellWorks.Services.Filtering
{
    /// <summary>Why a condition did not make it into the compiled filter.</summary>
    public enum FilterDiagnosticKind
    {
        /// <summary>The field key is not in this pane's schema - structurally meaningless, so dropped.</summary>
        UnknownField,

        /// <summary>The value does not parse. Dropped from the filter, but kept and marked in the form.</summary>
        UnparseableValue,

        /// <summary>A whole OR block compiled to nothing and was dropped rather than made true.</summary>
        EmptyGroup
    }

    public sealed record FilterDiagnostic(FilterDiagnosticKind Kind, string Field);

    /// <summary>
    /// Folds a <see cref="FilterQuery"/> into one Core filter:
    /// <c>AllOf(search, common, AnyOf(groups))</c>.
    /// </summary>
    public static class FilterQueryCompiler
    {
        public static IModelFilter<T> Compile<T>(FilterQuery query, FilterSchema<T> schema) =>
            Compile(query, schema, out _);

        public static IModelFilter<T> Compile<T>(
            FilterQuery query, FilterSchema<T> schema, out IReadOnlyList<FilterDiagnostic> diagnostics)
        {
            List<FilterDiagnostic> found = [];
            diagnostics = found;

            if (query == null || schema == null)
                return MatchAllFilter<T>.Instance;

            List<IModelFilter<T>> conjunction = [];

            // The two boxes are separate questions AND-ed together, so "7157" in the id box and "damage" in
            // the text box asks for both rather than for rows matching either.
            Add(conjunction, query.Search, schema.TextTerm, query.ExactSearch);
            Add(conjunction, query.IdSearch, schema.IdTerm, query.ExactSearch);

            // The common band is AND-ed once around the whole disjunction rather than copied into each block,
            // which is both cheaper and what "applies to all blocks" says on screen.
            conjunction.AddRange(CompileGroup(query.Common, schema, found));

            List<IModelFilter<T>> alternatives = [];
            foreach (FilterGroup group in query.Groups)
            {
                List<IModelFilter<T>> conditions = CompileGroup(group, schema, found);

                // A block that compiled to nothing is dropped, never treated as true: one unparseable term
                // inside an OR would otherwise silently widen the grid to every row.
                if (conditions.Count > 0)
                    alternatives.Add(new AllOfFilter<T>(conditions));
                else if (group.Conditions.Count > 0)
                    found.Add(new FilterDiagnostic(FilterDiagnosticKind.EmptyGroup, null));
            }

            if (alternatives.Count > 0)
                conjunction.Add(new AnyOfFilter<T>(alternatives));

            return conjunction.Count > 0 ? new AllOfFilter<T>(conjunction) : MatchAllFilter<T>.Instance;
        }

        private static void Add<T>(
            List<IModelFilter<T>> conjunction, string typed, SearchTermFactory<T> factory, bool exact)
        {
            if (factory == null || string.IsNullOrWhiteSpace(typed))
                return;

            IModelFilter<T> search = SearchExpression.Parse(typed).Compile(term => factory(term, exact));

            if (search is not MatchAllFilter<T>)
                conjunction.Add(search);
        }

        /// <summary>
        /// One group's conditions, as filters over the element.
        /// </summary>
        /// <remarks>
        /// Flex conditions do not compile one apiece. Every condition on one linked row within this group is
        /// collected and answered by a single <see cref="RowMatchFilter{T}"/>, so that a block asking for an
        /// effect of type 12 with a large <c>DataBits00</c> asks both of the <em>same</em> effect row rather
        /// than of any two. The correlation is scoped to the group because that is where the AND lives: the
        /// common band is compiled by its own call and each OR block by its own, so the disjunctive normal
        /// form the rest of the query model is built on is unaffected.
        /// </remarks>
        private static List<IModelFilter<T>> CompileGroup<T>(
            FilterGroup group, FilterSchema<T> schema, List<FilterDiagnostic> diagnostics)
        {
            List<IModelFilter<T>> compiled = [];

            // Insertion-ordered, so a query compiles to the same shape twice running - which is what makes
            // a compiled filter reproducible and its tests worth writing.
            List<(FilterFlexSource<T> Source, List<IModelFilter<object>> Conditions)> rowGroups = [];

            foreach (FilterCondition condition in group.Conditions)
            {
                // Through the base lookup: the typed one narrows to FilterFieldSchema<T>, and a flex column
                // is deliberately not one of those.
                FilterFieldSchema untyped = ((FilterSchema)schema).Field(condition.Field);
                if (untyped == null)
                {
                    diagnostics.Add(new FilterDiagnostic(FilterDiagnosticKind.UnknownField, condition.Field));
                    continue;
                }

                // An untouched control asks for nothing. Not an error, and not a reason to drop the block.
                if (untyped.IsBlank(condition))
                    continue;

                if (untyped is FilterColumnFieldSchema column)
                {
                    // A flex column reaches the field list only by being one of the schema's own sources'
                    // columns - that is how the schema assembles it - so this lookup cannot miss.
                    AddRowCondition(rowGroups, schema.FlexSource(column.Source), column, condition, diagnostics);
                    continue;
                }

                IModelFilter<T> filter = ((FilterFieldSchema<T>)untyped).Factory(condition);
                if (filter == null)
                {
                    diagnostics.Add(new FilterDiagnostic(FilterDiagnosticKind.UnparseableValue, condition.Field));
                    continue;
                }

                compiled.Add(condition.Negate ? new NotFilter<T>(filter) : filter);
            }

            foreach ((FilterFlexSource<T> source, List<IModelFilter<object>> conditions) in rowGroups)
                compiled.Add(new RowMatchFilter<T> { Rows = source.Rows, Conditions = conditions });

            return compiled;
        }

        /// <summary>
        /// Bucket one flex condition under its source, so the whole group's constraints on that row are
        /// answered together.
        /// </summary>
        /// <remarks>
        /// <see cref="FilterCondition.Negate"/> is applied to the row constraint rather than to the match:
        /// "an effect whose type is not 12" is a row that exists and differs, not the absence of a type 12
        /// effect. Negating the match instead would make the <c>!</c> button mean something different on a
        /// flex row than on every other row of the form.
        /// </remarks>
        private static void AddRowCondition<T>(
            List<(FilterFlexSource<T> Source, List<IModelFilter<object>> Conditions)> rowGroups,
            FilterFlexSource<T> source,
            FilterColumnFieldSchema column,
            FilterCondition condition,
            List<FilterDiagnostic> diagnostics)
        {
            IModelFilter<object> filter = column.RowFactory(condition);
            if (filter == null)
            {
                diagnostics.Add(new FilterDiagnostic(FilterDiagnosticKind.UnparseableValue, condition.Field));
                return;
            }

            if (condition.Negate)
                filter = new NotFilter<object>(filter);

            int index = rowGroups.FindIndex(g => g.Source.Key == source.Key);
            if (index < 0)
            {
                rowGroups.Add((source, [filter]));
                return;
            }

            rowGroups[index].Conditions.Add(filter);
        }
    }
}
