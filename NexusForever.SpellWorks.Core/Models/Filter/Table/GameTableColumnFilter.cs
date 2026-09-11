namespace NexusForever.SpellWorks.Core.Models.Filter.Table
{
    /// <summary>
    /// Matches one column of a generic table row against a value.
    /// </summary>
    /// <remarks>
    /// The column is an index resolved against the table's descriptor when the schema is built, not a name
    /// matched per row - the same hoisting <see cref="GameTableCellFilter"/> does for masks. Comparison is on
    /// the projected cell text, which is the only shape all tables share; a numeric column still compares
    /// sensibly by prefix and by equality, which is what these fields are for.
    /// </remarks>
    public class GameTableColumnFilter : IModelFilter<string[]>
    {
        public int ColumnIndex { get; set; } = -1;

        public string Query { get; set; }

        /// <summary>Whether the query is a whole value rather than a substring.</summary>
        public bool Exact { get; set; }

        public bool Filter(string[] cells)
        {
            if (string.IsNullOrWhiteSpace(Query))
                return true;

            if (ColumnIndex < 0 || ColumnIndex >= cells.Length)
                return false;

            string cell = cells[ColumnIndex];
            string query = Query.Trim();

            return Exact
                ? cell.Equals(query, StringComparison.OrdinalIgnoreCase)
                : cell.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }
}
