namespace NexusForever.SpellWorks.Core.Models.Filter.Numeric
{
    /// <summary>A threshold on how long the spell lasts, in milliseconds.</summary>
    public class SpellModelDurationFilter : SpellModelRangeFilter
    {
        protected override double Read(ISpellModel model) => model.Entry.SpellDuration;
    }
}
