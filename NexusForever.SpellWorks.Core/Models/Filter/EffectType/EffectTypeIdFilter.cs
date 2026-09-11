namespace NexusForever.SpellWorks.Core.Models.Filter.EffectType
{
    /// <summary>Matches effect-type usages whose numeric type id starts with <see cref="IdPrefix"/>.</summary>
    public class EffectTypeIdFilter : IModelFilter<EffectTypeUsage>
    {
        public string IdPrefix { get; set; }

        public bool Filter(EffectTypeUsage model)
        {
            if (string.IsNullOrWhiteSpace(IdPrefix))
                return true;

            return ((uint)model.Type).ToString().StartsWith(IdPrefix.Trim(), StringComparison.Ordinal);
        }
    }
}
