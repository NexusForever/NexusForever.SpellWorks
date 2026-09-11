namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// Keeps only spells that are the target of at least one proc elsewhere in the table.
    /// </summary>
    public class SpellModelProcReferencedFilter : ISpellModelFilter
    {
        public bool Filter(ISpellModel model)
        {
            return model.ProcReferences.Count > 0;
        }
    }
}
