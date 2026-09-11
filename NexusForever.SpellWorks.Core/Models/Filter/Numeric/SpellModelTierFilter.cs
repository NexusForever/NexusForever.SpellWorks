namespace NexusForever.SpellWorks.Core.Models.Filter.Numeric
{
    /// <summary>A threshold on the spell's tier index.</summary>
    public class SpellModelTierFilter : SpellModelRangeFilter
    {
        protected override double Read(ISpellModel model) => model.Entry.TierIndex;
    }
}
