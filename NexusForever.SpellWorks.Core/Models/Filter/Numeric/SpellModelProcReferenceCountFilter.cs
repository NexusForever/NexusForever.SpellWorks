namespace NexusForever.SpellWorks.Core.Models.Filter.Numeric
{
    /// <summary>A threshold on how many procs elsewhere cast this spell.</summary>
    public class SpellModelProcReferenceCountFilter : SpellModelRangeFilter
    {
        protected override double Read(ISpellModel model) => model.ProcReferences.Count;
    }
}
