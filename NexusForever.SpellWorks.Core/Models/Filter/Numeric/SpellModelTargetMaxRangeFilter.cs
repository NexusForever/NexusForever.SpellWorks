namespace NexusForever.SpellWorks.Core.Models.Filter.Numeric
{
    /// <summary>A threshold on the furthest range the spell can be cast at.</summary>
    public class SpellModelTargetMaxRangeFilter : SpellModelRangeFilter
    {
        protected override double Read(ISpellModel model) => model.Entry.TargetMaxRange;
    }
}
