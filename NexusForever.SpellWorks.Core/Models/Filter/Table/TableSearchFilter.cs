using NexusForever.SpellWorks.Core.Services;

namespace NexusForever.SpellWorks.Core.Models.Filter.Table
{
    /// <summary>
    /// The Tables search box: the table's name.
    /// </summary>
    /// <remarks>
    /// The only grid with no id to search - a table is named, not numbered - so this pane offers the one box.
    /// </remarks>
    public class TableSearchFilter : IModelFilter<TableDescriptor>
    {
        public string Query { get; set; }

        public bool Exact { get; set; }

        public bool Filter(TableDescriptor model)
        {
            if (string.IsNullOrWhiteSpace(Query))
                return true;

            return TextMatch.Matches(model.Name, Query, Exact);
        }
    }
}
