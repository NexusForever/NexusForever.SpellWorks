namespace NexusForever.SpellWorks.Core.Models.Filter.Table
{
    /// <summary>
    /// A bitmask constraint on one column of a generic table.
    /// </summary>
    /// <remarks>
    /// The column is an index resolved against the table's descriptor at construction, not a name matched
    /// per row, so the lookup happens once rather than inside the loop. A negative index
    /// means the table has no such column, and the constraint then matches nothing rather than everything:
    /// a mask the table cannot answer is not a mask it satisfies.
    /// </remarks>
    public class GameTableCellFilter : IModelFilter<string[]>
    {
        public int ColumnIndex { get; set; } = -1;

        public uint Mask { get; set; }

        public MaskMode Mode { get; set; } = MaskMode.All;

        public bool Filter(string[] cells)
        {
            if (Mask == 0)
                return true;

            if (ColumnIndex < 0 || ColumnIndex >= cells.Length)
                return false;

            return uint.TryParse(cells[ColumnIndex], out uint value) && Mode.Matches(value, Mask);
        }
    }
}
