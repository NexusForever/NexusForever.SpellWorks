namespace NexusForever.SpellWorks.Core.Models.Filter.Table
{
    /// <summary>
    /// Matches a generic table row carrying data beyond its id.
    /// </summary>
    /// <remarks>
    /// Phrased positively - "non-zero only" is this filter, and its negation is "blank rows only". A row is
    /// blank when every cell after the id reads as an unset number; the client data writes those three ways.
    /// </remarks>
    public class GameTableNonZeroFilter : IModelFilter<string[]>
    {
        public bool Filter(string[] cells)
        {
            return cells.Skip(1).Any(cell => cell is not ("0" or "" or "0.0"));
        }
    }
}
