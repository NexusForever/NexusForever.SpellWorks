using System.IO;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;

namespace NexusForever.SpellWorks.Core.Services
{
    public class GameTableService : IGameTableService
    {
        public GameTable<Spell4Entry> Spell4 { get; private set; }
        public GameTable<Spell4AoeTargetConstraintsEntry> Spell4AoeTargetConstraints { get; private set; }
        public GameTable<Spell4BaseEntry> Spell4Base { get; private set; }
        public GameTable<Spell4CCConditionsEntry> Spell4CCConditions { get; private set; }
        public GameTable<Spell4CastResultEntry> Spell4CastResult { get; private set; }
        public GameTable<Spell4ClientMissileEntry> Spell4ClientMissile { get; private set; }
        public GameTable<Spell4ConditionsEntry> Spell4Conditions { get; private set; }
        public GameTable<Spell4EffectGroupListEntry> Spell4EffectGroupList { get; private set; }
        public GameTable<Spell4EffectModificationEntry> Spell4EffectModification { get; private set; }
        public GameTable<Spell4EffectsEntry> Spell4Effects { get; private set; }
        public GameTable<Spell4GroupListEntry> Spell4GroupList { get; private set; }
        public GameTable<Spell4HitResultsEntry> Spell4HitResults { get; private set; }
        public GameTable<Spell4ModificationEntry> Spell4Modification { get; private set; }
        public GameTable<Spell4PrerequisitesEntry> Spell4Prerequisites { get; private set; }
        public GameTable<Spell4ReagentEntry> Spell4Reagent { get; private set; }
        public GameTable<Spell4RunnerEntry> Spell4Runner { get; private set; }
        public GameTable<Spell4ServiceTokenCostEntry> Spell4ServiceTokenCost { get; private set; }
        public GameTable<Spell4SpellTypesEntry> Spell4SpellTypes { get; private set; }
        public GameTable<Spell4StackGroupEntry> Spell4StackGroup { get; private set; }
        public GameTable<Spell4TagEntry> Spell4Tag { get; private set; }
        public GameTable<Spell4TargetAngleEntry> Spell4TargetAngle { get; private set; }
        public GameTable<Spell4TargetMechanicsEntry> Spell4TargetMechanics { get; private set; }
        public GameTable<Spell4TelegraphEntry> Spell4Telegraph { get; private set; }
        public GameTable<Spell4ThresholdsEntry> Spell4Thresholds { get; private set; }
        public GameTable<Spell4TierRequirementsEntry> Spell4TierRequirements { get; private set; }
        public GameTable<Spell4ValidTargetsEntry> Spell4ValidTargets { get; private set; }
        public GameTable<Spell4VisualEntry> Spell4Visual { get; private set; }
        public GameTable<Spell4VisualGroupEntry> Spell4VisualGroup { get; private set; }
        public GameTable<SpellCoolDownEntry> SpellCoolDown { get; private set; }
        public GameTable<SpellEffectTypeEntry> SpellEffectType { get; private set; }
        public GameTable<SpellLevelEntry> SpellLevel { get; private set; }
        public GameTable<SpellPhaseEntry> SpellPhase { get; private set; }


        #region Dependency Injection

        private readonly IArchiveService _archiveService;

        public GameTableService(
            IArchiveService archiveService)
        {
            _archiveService = archiveService;
        }

        #endregion

        private int count;

        public async Task Initialise(IProgress<EngineProgress> progress)
        {
            count = 0;
            progress.Report(new EngineProgress("Loading Game Tables...", 0, 0, 32));

            Spell4 = await LoadGameTable<Spell4Entry>(progress, "Spell4.tbl");
            Spell4AoeTargetConstraints = await LoadGameTable<Spell4AoeTargetConstraintsEntry>(progress, "Spell4AoeTargetConstraints.tbl");
            Spell4Base = await LoadGameTable<Spell4BaseEntry>(progress, "Spell4Base.tbl");
            Spell4CastResult = await LoadGameTable<Spell4CastResultEntry>(progress, "Spell4CastResult.tbl");
            Spell4CCConditions = await LoadGameTable<Spell4CCConditionsEntry>(progress, "Spell4CCConditions.tbl");
            Spell4ClientMissile = await LoadGameTable<Spell4ClientMissileEntry>(progress, "Spell4ClientMissile.tbl");
            Spell4Conditions = await LoadGameTable<Spell4ConditionsEntry>(progress, "Spell4Conditions.tbl");
            Spell4EffectGroupList     = await LoadGameTable<Spell4EffectGroupListEntry>(progress, "Spell4EffectGroupList.tbl");
            Spell4EffectModification  = await LoadGameTable<Spell4EffectModificationEntry>(progress, "Spell4EffectModification.tbl");
            Spell4Effects             = await LoadGameTable<Spell4EffectsEntry>(progress, "Spell4Effects.tbl");
            Spell4GroupList           = await LoadGameTable<Spell4GroupListEntry>(progress, "Spell4GroupList.tbl");
            Spell4HitResults          = await LoadGameTable<Spell4HitResultsEntry>(progress, "Spell4HitResults.tbl");
            Spell4Modification        = await LoadGameTable<Spell4ModificationEntry>(progress, "Spell4Modification.tbl");
            Spell4Prerequisites       = await LoadGameTable<Spell4PrerequisitesEntry>(progress, "Spell4Prerequisites.tbl");
            Spell4Reagent             = await LoadGameTable<Spell4ReagentEntry>(progress, "Spell4Reagent.tbl");
            Spell4Runner              = await LoadGameTable<Spell4RunnerEntry>(progress, "Spell4Runner.tbl");
            Spell4ServiceTokenCost    = await LoadGameTable<Spell4ServiceTokenCostEntry>(progress, "Spell4ServiceTokenCost.tbl");
            Spell4SpellTypes          = await LoadGameTable<Spell4SpellTypesEntry>(progress, "Spell4SpellTypes.tbl");
            Spell4StackGroup          = await LoadGameTable<Spell4StackGroupEntry>(progress, "Spell4StackGroup.tbl");
            Spell4Tag                 = await LoadGameTable<Spell4TagEntry>(progress, "Spell4Tag.tbl");
            Spell4TargetAngle         = await LoadGameTable<Spell4TargetAngleEntry>(progress, "Spell4TargetAngle.tbl");
            Spell4TargetMechanics     = await LoadGameTable<Spell4TargetMechanicsEntry>(progress, "Spell4TargetMechanics.tbl");
            Spell4Telegraph           = await LoadGameTable<Spell4TelegraphEntry>(progress, "Spell4Telegraph.tbl");
            Spell4Thresholds          = await LoadGameTable<Spell4ThresholdsEntry>(progress, "Spell4Thresholds.tbl");
            Spell4TierRequirements    = await LoadGameTable<Spell4TierRequirementsEntry>(progress, "Spell4TierRequirements.tbl");
            Spell4ValidTargets        = await LoadGameTable<Spell4ValidTargetsEntry>(progress, "Spell4ValidTargets.tbl");
            Spell4Visual              = await LoadGameTable<Spell4VisualEntry>(progress, "Spell4Visual.tbl");
            Spell4VisualGroup         = await LoadGameTable<Spell4VisualGroupEntry>(progress, "Spell4VisualGroup.tbl");
            SpellCoolDown             = await LoadGameTable<SpellCoolDownEntry>(progress, "SpellCoolDown.tbl");
            SpellEffectType           = await LoadGameTable<SpellEffectTypeEntry>(progress, "SpellEffectType.tbl");
            SpellLevel                = await LoadGameTable<SpellLevelEntry>(progress, "SpellLevel.tbl");
            SpellPhase                = await LoadGameTable<SpellPhaseEntry>(progress, "SpellPhase.tbl");

        }

        private Task<GameTable<T>> LoadGameTable<T>(IProgress<EngineProgress> progress, string name) where T : class, new()
        {
            return Task.Run(() =>
            {
                string filePath = Path.Combine("DB", name);
                if (_archiveService.MainArchive?.Find(filePath) is not IArchiveFile file)
                    throw new FileNotFoundException($"{name} is not present in the archive.", filePath);

                using Stream archiveStream = file.Open();
                using var memoryStream = new MemoryStream();
                archiveStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                var gameTable = new GameTable<T>(memoryStream);

                // The value the increment returns, as TextTableService reports it. These loads are awaited
                // one at a time, but reporting the returned value stays correct if they ever run in parallel.
                progress.Report(new EngineProgress(Value: Interlocked.Increment(ref count), Maximum: 32));

                return gameTable;
            });
        }
    }
}
