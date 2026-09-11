namespace NexusForever.SpellWorks.Core.Models.Filter.Numeric
{
    /// <summary>A threshold on how many procs the spell casts.</summary>
    public class SpellModelProcCountFilter : SpellModelRangeFilter
    {
        protected override double Read(ISpellModel model) => model.Procs.Count;
    }
}
