using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Static;

namespace NexusForever.SpellWorks.Core.Models
{
    public interface ISpellProcModel
    {
        /// <summary>
        /// Backing game table row. A proc is a <c>Spell4Effects</c> row read a different way, so the row
        /// itself is what a column filter over the procs grid has to reach.
        /// </summary>
        Spell4EffectsEntry Entry { get; }

        ProcType ProcType { get; }
        uint SpellId { get; }

        void Initialise(Spell4EffectsEntry entry);
    }
}