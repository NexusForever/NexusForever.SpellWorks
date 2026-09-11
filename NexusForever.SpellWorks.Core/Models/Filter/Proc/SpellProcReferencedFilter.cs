namespace NexusForever.SpellWorks.Core.Models.Filter.Proc
{
    /// <summary>
    /// Matches procs whose cast spell is itself referenced by a proc elsewhere in the table.
    /// </summary>
    /// <remarks>
    /// The reference index lives on the model service, so it is handed in rather than looked up - the filter
    /// stays a pure predicate over the row.
    /// </remarks>
    public class SpellProcReferencedFilter : IModelFilter<ISpellProcModel>
    {
        /// <summary>
        /// The spell ids something procs to. A dictionary's key collection answers <c>Contains</c> in
        /// constant time, so the proc-reference index can be handed in directly.
        /// </summary>
        public ICollection<uint> ReferencedSpellIds { get; set; }

        public bool Filter(ISpellProcModel model)
        {
            return ReferencedSpellIds?.Contains(model.SpellId) ?? false;
        }
    }
}
