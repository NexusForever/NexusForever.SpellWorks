namespace NexusForever.SpellWorks.Core.Models.Filter.Effect
{
    /// <summary>Which of an effect row's five prerequisite slots a constraint reads.</summary>
    public enum EffectPrerequisite
    {
        CasterApply,
        TargetApply,
        CasterPersistence,
        TargetPersistence,
        TargetSuspend
    }

    /// <summary>
    /// Matches an effect row on one of its prerequisite ids.
    /// </summary>
    /// <remarks>
    /// A zero in any of these slots means "no prerequisite", so asking for zero is a real question - it finds
    /// the unconditional effects - and is not treated as an empty constraint.
    /// </remarks>
    public class SpellEffectPrerequisiteFilter : IModelFilter<ISpellEffectModel>
    {
        public EffectPrerequisite Slot { get; set; }

        public uint PrerequisiteId { get; set; }

        public bool Filter(ISpellEffectModel model)
        {
            if (model.Entry == null)
                return false;

            uint actual = Slot switch
            {
                EffectPrerequisite.CasterApply       => model.Entry.PrerequisiteIdCasterApply,
                EffectPrerequisite.TargetApply       => model.Entry.PrerequisiteIdTargetApply,
                EffectPrerequisite.CasterPersistence => model.Entry.PrerequisiteIdCasterPersistence,
                EffectPrerequisite.TargetPersistence => model.Entry.PrerequisiteIdTargetPersistence,
                _                                    => model.Entry.PrerequisiteIdTargetSuspend
            };

            return actual == PrerequisiteId;
        }
    }
}
