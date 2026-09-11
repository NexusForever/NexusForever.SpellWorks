namespace NexusForever.SpellWorks.Core.Models.Filter.Numeric
{
    /// <summary>A threshold on the longest the spell can be channelled, in milliseconds.</summary>
    public class SpellModelChannelTimeFilter : SpellModelRangeFilter
    {
        protected override double Read(ISpellModel model) => model.Entry.ChannelMaxTime;
    }
}
