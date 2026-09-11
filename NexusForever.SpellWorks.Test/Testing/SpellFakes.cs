using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Static;

namespace NexusForever.SpellWorks.Test.Testing
{
    /// <summary>
    /// Stand-ins for the spell graph the engine builds from the archive. They hold plain settable state, so
    /// a test says "a spell with one Proxy effect" rather than writing a game table to get there.
    /// </summary>
    public sealed class TestSpell : ISpellModel
    {
        public Spell4Entry Entry { get; set; }
        public uint Id => Entry.Id;
        public string Description { get; set; } = "";
        public string ActionBarTooltip => "tooltip";
        public ISpellBaseModel SpellBaseModel { get; set; }
        public List<ISpellEffectModel> Effects { get; } = [];
        public List<ISpellProcModel> Procs { get; } = [];
        public List<uint> ProcReferences { get; } = [];

        public void Initialise(Spell4Entry entry) => Entry = entry;
    }

    public sealed class TestBase : ISpellBaseModel
    {
        public Spell4BaseEntry Entry { get; set; }
        /// <summary>
        /// The localised name. Settable because the search box matches it, so a test that cares about
        /// what matches has to be able to say what this spell is called.
        /// </summary>
        public string Name
        {
            get => LocalisedName?.Invoke() ?? _name;
            set => _name = value;
        }

        private string _name = "Arcane Missile";

        /// <summary>
        /// Read the name through the text service, as <c>SpellBaseModel</c> does, so it follows the locale.
        /// </summary>
        public Func<string> LocalisedName { get; set; }

        public Spell4HitResultsEntry HitResultValue { get; set; } = new();
        public Spell4TargetMechanicsEntry TargetMechanicsValue { get; set; } = new();
        public Spell4BaseEntry PrerequisiteSpellValue { get; set; }

        public Spell4HitResultsEntry HitResult => HitResultValue;
        public Spell4TargetMechanicsEntry TargetMechanics => TargetMechanicsValue;
        public Spell4TargetAngleEntry TargetAngle => null;
        public Spell4PrerequisitesEntry Prerequisites => null;
        public Spell4ValidTargetsEntry ValidTargets => null;
        public TargetGroupEntry CastGroup => null;
        public Creature2Entry PositionalAoe => null;
        public TargetGroupEntry AoeGroup => null;
        public Spell4BaseEntry PrerequisiteSpell => PrerequisiteSpellValue;
        public Spell4SpellTypesEntry SpellType => null;

        public void Initialise(Spell4BaseEntry entry) => Entry = entry;
    }

    public sealed class TestEffect : ISpellEffectModel
    {
        public Spell4EffectsEntry Entry { get; set; }
        public SpellEffectType Type { get; set; }
        public uint TargetFlags => 0;
        public uint DamageType => 0;
        public uint DelayTime => 0;
        public uint TickTime => 0;
        public uint DurationTime => 0;
        public uint Flags => 0;

        public ISpellEffectColumnData ColumnDataValue { get; set; }
        public ISpellEffectColumnData ColumnData => ColumnDataValue;
        public List<ISpellEffectRowData> RowData { get; } = [];

        public void Initialise(Spell4EffectsEntry entry) => Entry = entry;
    }

    public sealed class TestProc : ISpellProcModel
    {
        public Spell4EffectsEntry Entry { get; set; }
        public ProcType ProcType { get; set; }
        public uint SpellId { get; set; }

        public void Initialise(Spell4EffectsEntry entry) => Entry = entry;
    }
}
