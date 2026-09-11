namespace NexusForever.SpellWorks.Services
{
    /// <summary>
    /// A grid column. <see cref="Width"/> is an explicit pixel count - the table is
    /// <c>table-layout: fixed</c>, because <c>auto</c> measures every row.
    /// </summary>
    public sealed record GridColumn(string Name, string CellClass, int Width);

    /// <summary>
    /// One already-flattened row. Cells are pre-formatted strings, so the row template never touches the
    /// model graph, reflection or a lazily-parsed member while scrolling.
    /// </summary>
    public sealed record GridRow(uint Key, string[] Cells, object Source);

    public sealed record GridData(IReadOnlyList<GridColumn> Columns, GridRow[] Rows, int Total)
    {
        public static readonly GridData Empty = new([], [], 0);

        /// <summary>
        /// Sum of the column widths, rendered as the table's own width.
        /// </summary>
        /// <remarks>
        /// a table whose specified width is <c>auto</c> falls back to the automatic
        /// layout algorithm no matter what <c>table-layout</c> says, and that algorithm re-measures cell
        /// content - so columns would resize as virtualised rows scrolled in and out.
        /// </remarks>
        public int TotalWidth { get; } = Columns.Sum(c => c.Width);
    }
}
