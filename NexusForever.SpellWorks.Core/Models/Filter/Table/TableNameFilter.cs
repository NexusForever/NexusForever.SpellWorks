using NexusForever.SpellWorks.Core.Services;

namespace NexusForever.SpellWorks.Core.Models.Filter.Table
{
    /// <summary>Matches game tables whose name starts with <see cref="Prefix"/>, case-insensitively.</summary>
    public class TableNameFilter : IModelFilter<TableDescriptor>
    {
        public string Prefix { get; set; }

        public bool Filter(TableDescriptor model)
        {
            if (string.IsNullOrWhiteSpace(Prefix))
                return true;

            return model.Name.StartsWith(Prefix.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
