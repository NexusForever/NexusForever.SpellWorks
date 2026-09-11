using NexusForever.SpellWorks.Core.Services;

namespace NexusForever.SpellWorks.Core.Models.Filter.Table
{
    /// <summary>
    /// Matches game tables that actually carry rows.
    /// </summary>
    /// <remarks>
    /// Phrased positively, as every toggle filter is - "loaded only" is this filter, "empty only" is it
    /// negated. See <see cref="SpellModelDeprecatedFilter"/> for why polarity belongs to the caller.
    /// </remarks>
    public class TableLoadedFilter : IModelFilter<TableDescriptor>
    {
        public bool Filter(TableDescriptor model) => model.RowCount > 0;
    }
}
