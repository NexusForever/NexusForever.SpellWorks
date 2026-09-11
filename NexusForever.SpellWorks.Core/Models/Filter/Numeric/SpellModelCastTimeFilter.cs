namespace NexusForever.SpellWorks.Core.Models.Filter.Numeric
{
    /// <summary>A threshold on the spells cast time, in milliseconds.</summary>
    public class SpellModelCastTimeFilter : SpellModelRangeFilter
    {
        protected override double Read(ISpellModel model) => model.Entry.CastTime;
    }
}
