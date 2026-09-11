namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// Matches spells whose id starts with <see cref="IdPrefix"/>.
    /// </summary>
    public class SpellModelIdFilter : ISpellModelFilter
    {
        public string IdPrefix { get; set; }

        public bool Filter(ISpellModel model)
        {
            if (string.IsNullOrWhiteSpace(IdPrefix))
                return true;

            return model.Id.ToString().StartsWith(IdPrefix, StringComparison.Ordinal);
        }
    }
}
