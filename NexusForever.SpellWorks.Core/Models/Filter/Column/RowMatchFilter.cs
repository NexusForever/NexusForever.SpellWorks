namespace NexusForever.SpellWorks.Core.Models.Filter.Column
{
    /// <summary>
    /// Lifts constraints on a linked game table row up to the element that owns it: matches when
    /// <em>one</em> of the element's rows satisfies <em>every</em> condition.
    /// </summary>
    /// <remarks>
    /// The correlation is the point. A spell has many effect rows, and "an effect of type 12 whose
    /// DataBits00 is at least 5" has to be answered by one row holding both - asking each condition
    /// independently would return spells with a type 12 effect and, separately, some unrelated effect with
    /// a large DataBits00. The compiler therefore collects every condition on one source within one filter
    /// group into a single instance of this, which is what makes the group's AND mean what it says.
    ///
    /// A single-row source - a spell's <c>Spell4Base</c>, or a base's <c>Spell4HitResults</c> - is the same
    /// shape with a sequence of one, and degenerates correctly: a spell whose link is null yields no rows
    /// and so fails the constraint rather than passing it. A constraint a row cannot answer is not one it
    /// satisfies, which is the stance <see cref="Table.GameTableCellFilter"/> takes on a missing column.
    /// </remarks>
    public sealed class RowMatchFilter<T> : IModelFilter<T>
    {
        public required Func<T, IEnumerable<object>> Rows { get; init; }

        public required IReadOnlyList<IModelFilter<object>> Conditions { get; init; }

        public bool Filter(T model)
        {
            IEnumerable<object> rows = Rows(model);
            if (rows == null)
                return false;

            foreach (object row in rows)
            {
                if (row == null)
                    continue;

                bool all = true;
                foreach (IModelFilter<object> condition in Conditions)
                {
                    if (condition.Filter(row))
                        continue;

                    all = false;
                    break;
                }

                if (all)
                    return true;
            }

            return false;
        }
    }
}
