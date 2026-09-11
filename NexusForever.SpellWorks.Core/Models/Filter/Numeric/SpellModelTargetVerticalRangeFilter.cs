namespace NexusForever.SpellWorks.Core.Models.Filter.Numeric
{
    /// <summary>A threshold on how far above or below the caster the spell reaches.</summary>
    public class SpellModelTargetVerticalRangeFilter : SpellModelRangeFilter
    {
        protected override double Read(ISpellModel model) => model.Entry.TargetVerticalRange;
    }
}
