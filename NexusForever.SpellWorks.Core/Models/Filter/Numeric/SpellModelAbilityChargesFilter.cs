namespace NexusForever.SpellWorks.Core.Models.Filter.Numeric
{
    /// <summary>A threshold on how many charges of the ability are held at once.</summary>
    public class SpellModelAbilityChargesFilter : SpellModelRangeFilter
    {
        protected override double Read(ISpellModel model) => model.Entry.AbilityChargeCount;
    }
}
