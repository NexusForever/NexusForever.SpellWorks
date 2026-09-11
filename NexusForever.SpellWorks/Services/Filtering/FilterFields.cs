namespace NexusForever.SpellWorks.Services.Filtering
{
    /// <summary>
    /// Every schema field key, in one place.
    /// </summary>
    /// <remarks>
    /// These strings are written into <c>Workspace.json</c>, so they are the one part of the filter model
    /// that must never change: renaming a key silently drops the saved conditions that used it. They live in
    /// their own class so the registry can name enum types like <c>Class</c> and <c>CastMethod</c> without a
    /// constant of the same name shadowing them.
    /// </remarks>
    public static class FilterFields
    {
        // Spell4 and Effect Type spells
        public const string Id                  = "id";
        public const string Name                = "name";
        public const string Tooltip             = "tooltip";
        public const string CastMethod          = "castMethod";
        public const string School              = "school";
        public const string Class               = "class";
        public const string TargetMechanic      = "targetMechanic";
        public const string TargetMechanicFlags = "targetMechanicFlags";
        public const string EffectType          = "effectType";
        public const string EffectTargetFlags   = "effectTargetFlags";
        public const string Deprecated          = "deprecated";
        public const string HasProcs            = "hasProcs";
        public const string TestSpell           = "testSpell";

        // Spell4 numeric thresholds
        public const string CastTime            = "castTime";
        public const string Duration            = "duration";
        public const string Cooldown            = "cooldown";
        public const string Tier                = "tier";
        public const string TargetMinRange      = "targetMinRange";
        public const string TargetMaxRange      = "targetMaxRange";
        public const string TargetVerticalRange = "targetVerticalRange";
        public const string MissileSpeed        = "missileSpeed";
        public const string ChannelTime         = "channelTime";
        public const string ChannelPulse        = "channelPulse";
        public const string AbilityCharges      = "abilityCharges";
        public const string EffectCount         = "effectCount";
        public const string ProcCount           = "procCount";
        public const string ProcReferenceCount  = "procReferenceCount";

        // Effects
        public const string EffectFlags    = "effect.flags";
        public const string EffectDelay    = "effect.delay";
        public const string EffectTick     = "effect.tick";
        public const string EffectDuration = "effect.duration";
        public const string EffectThreat   = "effect.threat";
        public const string EffectDamage   = "effect.damageType";
        public const string EffectPhase    = "effect.phaseFlags";
        public const string EffectOrder    = "effect.orderIndex";
        public const string EffectGroup    = "effect.groupList";
        public const string EffectParamType  = "effect.parameterType";
        public const string EffectParamValue = "effect.parameterValue";
        public const string EffectEmmComparison = "effect.emmComparison";
        public const string EffectEmmValue      = "effect.emmValue";
        public const string EffectPrerequisite  = "effect.prerequisite";

        /// <summary>One key per <c>DataBits</c> column, numbered as the column is.</summary>
        public static string EffectData(int index) => $"effect.data{index:00}";

        // Procs
        public const string ProcType       = "proc.type";
        public const string ProcSpellId    = "proc.spellId";
        public const string ProcReferenced = "proc.referenced";

        // Effect types
        public const string TypeId     = "type.id";
        public const string TypeSpells = "type.spells";

        // Table list
        public const string TableName   = "table.name";
        public const string TableLoaded = "table.loaded";

        // Generic game table
        public const string RowId       = "row.id";
        public const string RowContains = "row.contains";
        public const string RowFlags    = "row.flags";
        public const string RowNonZero  = "row.nonZero";

        /// <summary>Prefix for a per-column condition on a generic game table.</summary>
        public const string ColumnPrefix = "col:";

        public static string Column(string columnName) => ColumnPrefix + columnName;

        // Flex columns
        //
        // One key per (linked row, column) pair, so a flex condition is an ordinary condition and needs
        // nothing of its own from the query model, the persisted file or the compiler's diagnostics.

        /// <summary>Prefix for a condition on one column of one linked game table row.</summary>
        public const string FlexPrefix = "fx:";

        /// <summary>
        /// The key for <paramref name="column"/> of the linked row <paramref name="source"/> names -
        /// <c>fx:effects.DataBits00</c>.
        /// </summary>
        public static string Flex(string source, string column) => $"{FlexPrefix}{source}.{column}";

        // Flex source keys. Persisted inside every flex condition's key, so as fixed as the keys are.
        public const string SpellSource            = "spell4";
        public const string BaseSource             = "base";
        public const string EffectsSource          = "effects";
        public const string HitResultSource        = "base.hitResult";
        public const string TargetMechanicsSource  = "base.targetMechanics";
        public const string TargetAngleSource      = "base.targetAngle";
        public const string PrerequisitesSource    = "base.prerequisites";
        public const string ValidTargetsSource     = "base.validTargets";
        public const string PrerequisiteSpellSource = "base.prerequisiteSpell";
        public const string SpellTypeSource        = "base.spellType";

        /// <summary>The effect row a single effect is, on the pane that lists one spell's effects.</summary>
        public const string EffectRowSource = "effect";

        /// <summary>The effect row a proc is read from, on the procs pane.</summary>
        public const string ProcRowSource = "proc";
    }
}
