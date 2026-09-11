namespace NexusForever.SpellWorks.Core.Models.Filter.Numeric
{
    /// <summary>A threshold on how often a channelled spell pulses, in milliseconds.</summary>
    public class SpellModelChannelPulseFilter : SpellModelRangeFilter
    {
        protected override double Read(ISpellModel model) => model.Entry.ChannelPulseTime;
    }
}
