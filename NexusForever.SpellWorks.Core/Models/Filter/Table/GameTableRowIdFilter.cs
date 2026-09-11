namespace NexusForever.SpellWorks.Core.Models.Filter.Table
{
    /// <summary>
    /// Matches a generic table row whose first cell - the id column - starts with <see cref="IdPrefix"/>.
    /// </summary>
    /// <remarks>
    /// Generic-table filters run over the projected cells rather than the entry, because that is the only
    /// shape shared by all tables.
    /// </remarks>
    public class GameTableRowIdFilter : IModelFilter<string[]>
    {
        public string IdPrefix { get; set; }

        public bool Filter(string[] cells)
        {
            if (string.IsNullOrWhiteSpace(IdPrefix))
                return true;

            return cells.Length > 0 && cells[0].StartsWith(IdPrefix.Trim(), StringComparison.Ordinal);
        }
    }
}
