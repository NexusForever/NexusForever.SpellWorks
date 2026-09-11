namespace NexusForever.SpellWorks.Core.Models.Filter.Numeric
{
    /// <summary>A threshold on the spells own cooldown, in milliseconds.</summary>
    public class SpellModelCooldownFilter : SpellModelRangeFilter
    {
        protected override double Read(ISpellModel model) => model.Entry.SpellCoolDown;
    }
}
