namespace NexusForever.SpellWorks.Core.Models.Filter.Numeric
{
    /// <summary>A threshold on the nearest range the spell can be cast at.</summary>
    public class SpellModelTargetMinRangeFilter : SpellModelRangeFilter
    {
        protected override double Read(ISpellModel model) => model.Entry.TargetMinRange;
    }
}
