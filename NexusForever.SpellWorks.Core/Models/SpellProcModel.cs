using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Static;

namespace NexusForever.SpellWorks.Core.Models
{
    public class SpellProcModel : ISpellProcModel
    {
        public Spell4EffectsEntry Entry { get; private set; }

        public ProcType ProcType => (ProcType)Entry.DataBits00;
        public uint SpellId => Entry.DataBits01;

        public void Initialise(Spell4EffectsEntry entry)
        {
            Entry = entry;
        }
    }
}
