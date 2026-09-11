using NexusForever.GameTable.Model;

namespace NexusForever.SpellWorks.Core.Models
{
    public interface ISpellBaseModel
    {
        Spell4BaseEntry Entry { get; }
        string Name { get; }
        Spell4HitResultsEntry HitResult { get; }
        Spell4TargetMechanicsEntry TargetMechanics { get; }
        Spell4TargetAngleEntry TargetAngle { get; }
        Spell4PrerequisitesEntry Prerequisites { get; }
        Spell4ValidTargetsEntry ValidTargets { get; }
        TargetGroupEntry CastGroup { get; }
        Creature2Entry PositionalAoe { get; }
        TargetGroupEntry AoeGroup { get; }
        Spell4BaseEntry PrerequisiteSpell { get; }
        Spell4SpellTypesEntry SpellType { get; }

        void Initialise(Spell4BaseEntry entry);
    }
}