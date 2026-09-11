namespace NexusForever.SpellWorks.Core.Models.Filter.Table
{
    /// <summary>
    /// Matches a generic table row by any of its cells.
    /// </summary>
    /// <remarks>
    /// Every cell, the id column included - this is the "somewhere in this row" question. Asking about the
    /// id precisely is <see cref="GameTableColumnFilter"/> against column zero.
    /// </remarks>
    public class GameTableContainsFilter : IModelFilter<string[]>
    {
        public string Query { get; set; }

        public bool Exact { get; set; }

        public bool Filter(string[] cells)
        {
            if (string.IsNullOrWhiteSpace(Query))
                return true;

            return cells.Any(cell => TextMatch.Matches(cell, Query, Exact, StringComparison.OrdinalIgnoreCase));
        }
    }
}
