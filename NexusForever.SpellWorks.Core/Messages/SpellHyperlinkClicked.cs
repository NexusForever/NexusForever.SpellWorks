using NexusForever.SpellWorks.Core.Models;

namespace NexusForever.SpellWorks.Core.Messages
{
    public class SpellHyperlinkClicked
    {
        public ISpellModel Spell { get; set; }
    }
}
