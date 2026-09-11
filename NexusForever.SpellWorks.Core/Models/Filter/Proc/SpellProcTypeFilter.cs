namespace NexusForever.SpellWorks.Core.Models.Filter.Proc
{
    /// <summary>
    /// Matches one proc on its numeric proc type.
    /// </summary>
    /// <remarks>
    /// The type is compared as a number, not a name: <c>Core.Static.ProcType</c> has no members, so there is
    /// nothing to offer as a dropdown and nothing to parse a name against.
    /// </remarks>
    public class SpellProcTypeFilter : IModelFilter<ISpellProcModel>
    {
        public uint ProcType { get; set; }

        public bool Filter(ISpellProcModel model) => (uint)model.ProcType == ProcType;
    }
}
