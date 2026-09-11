namespace NexusForever.SpellWorks.Core.Models.Filter.Numeric
{
    /// <summary>A threshold on how many effects the spell carries.</summary>
    public class SpellModelEffectCountFilter : SpellModelRangeFilter
    {
        protected override double Read(ISpellModel model) => model.Effects.Count;
    }
}
