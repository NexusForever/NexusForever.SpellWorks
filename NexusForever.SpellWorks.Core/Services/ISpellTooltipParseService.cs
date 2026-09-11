using NexusForever.SpellWorks.Core.Models;

namespace NexusForever.SpellWorks.Core.Services
{
    public interface ISpellTooltipParseService
    {
        string Parse(ISpellModel spell);
    }
}