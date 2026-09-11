using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Services;

namespace NexusForever.SpellWorks.Core.Models
{
    public class SpellModel : ISpellModel
    {
        public Spell4Entry Entry { get; private set; }
        public uint Id => Entry.Id;
        public string Description => Entry.Description;
        public string ActionBarTooltip => _spellTooltipParseService.Parse(this);

        public ISpellBaseModel SpellBaseModel { get; private set; }
        public List<ISpellEffectModel> Effects { get; } = [];
        public List<ISpellProcModel> Procs { get; } = [];
        public List<uint> ProcReferences { get; } = [];

        #region Dependency Injection

        private readonly ISpellModelService _spellModelService;
        private readonly ISpellTooltipParseService _spellTooltipParseService;

        public SpellModel(
            ISpellModelService spellModelService,
            ISpellTooltipParseService spellTooltipParseService)
        {
            _spellModelService = spellModelService;
            _spellTooltipParseService = spellTooltipParseService;
        }

        #endregion

        public void Initialise(Spell4Entry entry)
        {
            Entry          = entry;
            SpellBaseModel = _spellModelService.SpellBaseModels[entry.Spell4BaseIdBaseSpell];

            if (_spellModelService.SpellEffectModels.TryGetValue(entry.Id, out List<ISpellEffectModel> effects))
                foreach (ISpellEffectModel effectModel in effects)
                    Effects.Add(effectModel);

            if (_spellModelService.SpellProcModels.TryGetValue(entry.Id, out List<ISpellProcModel> procs))
                foreach (ISpellProcModel procModel in procs)
                    Procs.Add(procModel);

            if (_spellModelService.SpellProcReferences.TryGetValue(entry.Id, out List<uint> references))
                ProcReferences.AddRange(references);
        }
    }
}
