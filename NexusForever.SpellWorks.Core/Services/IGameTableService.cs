using NexusForever.GameTable;
using NexusForever.GameTable.Model;

namespace NexusForever.SpellWorks.Core.Services
{
    public interface IGameTableService
    {
        GameTable<Spell4Entry> Spell4 { get; }
        GameTable<Spell4AoeTargetConstraintsEntry> Spell4AoeTargetConstraints { get; }
        GameTable<Spell4BaseEntry> Spell4Base { get; }
        GameTable<Spell4CCConditionsEntry> Spell4CCConditions { get; }
        GameTable<Spell4CastResultEntry> Spell4CastResult { get; }
        GameTable<Spell4ClientMissileEntry> Spell4ClientMissile { get; }
        GameTable<Spell4ConditionsEntry> Spell4Conditions { get; }
        GameTable<Spell4EffectGroupListEntry> Spell4EffectGroupList { get; }
        GameTable<Spell4EffectModificationEntry> Spell4EffectModification { get; }
        GameTable<Spell4EffectsEntry> Spell4Effects { get; }
        GameTable<Spell4GroupListEntry> Spell4GroupList { get; }
        GameTable<Spell4HitResultsEntry> Spell4HitResults { get; }
        GameTable<Spell4ModificationEntry> Spell4Modification { get; }
        GameTable<Spell4PrerequisitesEntry> Spell4Prerequisites { get; }
        GameTable<Spell4ReagentEntry> Spell4Reagent { get; }
        GameTable<Spell4RunnerEntry> Spell4Runner { get; }
        GameTable<Spell4ServiceTokenCostEntry> Spell4ServiceTokenCost { get; }
        GameTable<Spell4SpellTypesEntry> Spell4SpellTypes { get; }
        GameTable<Spell4StackGroupEntry> Spell4StackGroup { get; }
        GameTable<Spell4TagEntry> Spell4Tag { get; }
        GameTable<Spell4TargetAngleEntry> Spell4TargetAngle { get; }
        GameTable<Spell4TargetMechanicsEntry> Spell4TargetMechanics { get; }
        GameTable<Spell4TelegraphEntry> Spell4Telegraph { get; }
        GameTable<Spell4ThresholdsEntry> Spell4Thresholds { get; }
        GameTable<Spell4TierRequirementsEntry> Spell4TierRequirements { get; }
        GameTable<Spell4ValidTargetsEntry> Spell4ValidTargets { get; }
        GameTable<Spell4VisualEntry> Spell4Visual { get; }
        GameTable<Spell4VisualGroupEntry> Spell4VisualGroup { get; }
        GameTable<SpellCoolDownEntry> SpellCoolDown { get; }
        GameTable<SpellEffectTypeEntry> SpellEffectType { get; }
        GameTable<SpellLevelEntry> SpellLevel { get; }
        GameTable<SpellPhaseEntry> SpellPhase { get; }

        Task Initialise(IProgress<EngineProgress> progress);
    }
}