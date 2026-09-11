namespace NexusForever.SpellWorks.Core.Services
{
    /// <summary>
    /// Describes one game table exposed by <see cref="IGameTableService"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Values"/> projects an entry to its column values in <see cref="Columns"/> order using a
    /// compiled accessor - never reflection per cell.
    /// </remarks>
    public sealed record TableDescriptor(
        string Name,
        Type EntryType,
        int RowCount,
        IReadOnlyList<string> Columns,
        Func<IReadOnlyList<object>> Rows,
        Func<object, string[]> Values);

    /// <summary>
    /// One column of one game table entry type, with compiled accessors for both readings of it.
    /// </summary>
    /// <remarks>
    /// <see cref="TableDescriptor.Values"/> answers "render this row" and is deliberately all-strings.
    /// This answers the other question - "compare this one column" - which needs to know whether the
    /// column is a number, because a threshold on a numeric column and a substring on a text one are not
    /// the same operation and must not be offered interchangeably.
    ///
    /// <see cref="Number"/> is null for a text column; <see cref="Text"/> is set for every column, so a
    /// number can still be matched as the text it renders as.
    /// </remarks>
    public sealed record GameTableColumn(
        string Name,
        Type Type,
        bool IsNumeric,
        Func<object, double> Number,
        Func<object, string> Text);

    public interface ITableCatalog
    {
        IReadOnlyList<TableDescriptor> Tables { get; }

        TableDescriptor Get(string name);

        /// <summary>
        /// The columns of <paramref name="entryType"/>, in declaration order, with compiled accessors.
        /// </summary>
        /// <remarks>
        /// Keyed by entry type rather than by table name because the callers that want it - the flex filter
        /// schemas - reach a row through the spell graph rather than through a table, and never have a
        /// table name to hand. Cached per type, so asking twice costs one dictionary lookup.
        /// </remarks>
        IReadOnlyList<GameTableColumn> Columns(Type entryType);

        /// <summary>
        /// Rebuild the catalog against the currently loaded game tables.
        /// </summary>
        void Rebuild();

        /// <summary>
        /// Drop every table. A failed load must not leave the previous archive's tables on show, which
        /// reads as if the client were still mounted.
        /// </summary>
        void Clear();
    }
}
