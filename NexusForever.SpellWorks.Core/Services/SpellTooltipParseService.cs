using NexusForever.SpellWorks.Core.Models;

namespace NexusForever.SpellWorks.Core.Services
{
    public class SpellTooltipParseService : ISpellTooltipParseService
    {
        private readonly ITextTableService _textTableService;

        public SpellTooltipParseService(
            ITextTableService textTableService)
        {
            _textTableService = textTableService;
        }

        public string Parse(ISpellModel spell)
        {
            // TODO: parse
            return _textTableService.GetText(spell.Entry.LocalizedTextIdActionBarTooltip);
        }
    }
}
