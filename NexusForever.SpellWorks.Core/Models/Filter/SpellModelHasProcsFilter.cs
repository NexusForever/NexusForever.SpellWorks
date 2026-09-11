namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// Keeps only spells that cast at least one proc.
    /// </summary>
    public class SpellModelHasProcsFilter : ISpellModelFilter
    {
        public bool Filter(ISpellModel model)
        {
            return model.Procs.Count > 0;
        }
    }
}
