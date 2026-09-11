using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Services;

namespace NexusForever.SpellWorks.Core.Models
{
    public class SpellBaseModel : ISpellBaseModel
    {
        public Spell4BaseEntry Entry { get; private set; }
        public string Name => _textTableService.GetText(Entry.LocalizedTextIdName);

        public Spell4HitResultsEntry HitResult { get; private set; }
        public Spell4TargetMechanicsEntry TargetMechanics { get; private set; }
        public Spell4TargetAngleEntry TargetAngle { get; private set; }
        public Spell4PrerequisitesEntry Prerequisites { get; private set; }
        public Spell4ValidTargetsEntry ValidTargets { get; private set; }
        public TargetGroupEntry CastGroup { get; private set; }
        public Creature2Entry PositionalAoe { get; private set; }
        public TargetGroupEntry AoeGroup { get; private set; }
        public Spell4BaseEntry PrerequisiteSpell { get; private set; }
        public Spell4SpellTypesEntry SpellType { get; private set; }

        #region Dependency Injection

        private readonly IGameTableService _gameTableService;
        private readonly ITextTableService _textTableService;

        public SpellBaseModel(
            IGameTableService gameTableManager,
            ITextTableService textTableService)
        {
            _gameTableService = gameTableManager;
            _textTableService = textTableService;
        }

        #endregion

        public void Initialise(Spell4BaseEntry entry)
        {
            Entry             = entry;
            HitResult         = _gameTableService.Spell4HitResults.GetEntry(Entry.Spell4HitResultId);
            TargetMechanics   = _gameTableService.Spell4TargetMechanics.GetEntry(Entry.Spell4TargetMechanicId);
            TargetAngle       = _gameTableService.Spell4TargetAngle.GetEntry(Entry.Spell4TargetAngleId);
            Prerequisites     = _gameTableService.Spell4Prerequisites.GetEntry(Entry.Spell4PrerequisiteId);
            ValidTargets      = _gameTableService.Spell4ValidTargets.GetEntry(Entry.Spell4ValidTargetId);
            //CastGroup         = _gameTableService.TargetGroup.GetEntry(Entry.TargetGroupIdCastGroup);
            //PositionalAoe     = _gameTableService.Creature2.GetEntry(Entry.Creature2IdPositionalAoe);
            //AoeGroup          = _gameTableService.TargetGroup.GetEntry(Entry.TargetGroupIdAoeGroup);
            PrerequisiteSpell = _gameTableService.Spell4Base.GetEntry(Entry.Spell4BaseIdPrerequisiteSpell);
            SpellType         = _gameTableService.Spell4SpellTypes.GetEntry(Entry.Spell4SpellTypesIdSpellType);
        }
    }
}
