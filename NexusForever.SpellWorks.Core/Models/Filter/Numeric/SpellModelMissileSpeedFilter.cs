namespace NexusForever.SpellWorks.Core.Models.Filter.Numeric
{
    /// <summary>A threshold on the speed of the spell's missile, if it has one.</summary>
    public class SpellModelMissileSpeedFilter : SpellModelRangeFilter
    {
        protected override double Read(ISpellModel model) => model.Entry.MissileSpeed;
    }
}
