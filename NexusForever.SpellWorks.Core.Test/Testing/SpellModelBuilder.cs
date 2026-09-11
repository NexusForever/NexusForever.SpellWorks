using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Static;

namespace NexusForever.SpellWorks.Core.Test.Testing
{
    /// <summary>
    /// Builds a spell to filter against. The filters read a handful of members off the model graph, so the
    /// builder exposes exactly those and leaves the rest at their defaults.
    /// </summary>
    public sealed class SpellModelBuilder
    {
        private readonly FakeSpellModel _model = new()
        {
            Entry          = new Spell4Entry(),
            SpellBaseModel = new FakeSpellBaseModel { Entry = new Spell4BaseEntry() }
        };

        public static SpellModelBuilder A(uint id = 1, string description = "") =>
            new SpellModelBuilder().Id(id).Description(description);

        public SpellModelBuilder Id(uint id)
        {
            _model.Entry.Id = id;
            return this;
        }

        public SpellModelBuilder Description(string description)
        {
            _model.Description = description;
            return this;
        }

        public SpellModelBuilder CastMethod(CastMethod method)
        {
            Base.Entry.CastMethod = (byte)method;
            return this;
        }

        public SpellModelBuilder Class(uint classId)
        {
            Base.Entry.ClassIdPlayer = (byte)classId;
            return this;
        }

        public SpellModelBuilder School(uint school)
        {
            Base.Entry.School = school;
            return this;
        }

        public SpellModelBuilder TargetMechanic(uint targetType, uint flags)
        {
            Base.TargetMechanics = new Spell4TargetMechanicsEntry { TargetType = targetType, Flags = flags };
            return this;
        }

        public SpellModelBuilder Effect(SpellEffectType type, uint targetFlags = 0)
        {
            _model.Effects.Add(new FakeSpellEffectModel
            {
                Entry       = new Spell4EffectsEntry(),
                Type        = type,
                TargetFlags = targetFlags
            });

            return this;
        }

        public SpellModelBuilder Proc(uint spellId, ProcType type = default)
        {
            _model.Procs.Add(new FakeSpellProcModel { SpellId = spellId, ProcType = type });
            return this;
        }

        public SpellModelBuilder ReferencedByProc(uint spellId)
        {
            _model.ProcReferences.Add(spellId);
            return this;
        }

        public SpellModelBuilder Name(string name)
        {
            Base.Name = name;
            return this;
        }

        public SpellModelBuilder Tooltip(string tooltip)
        {
            _model.ActionBarTooltip = tooltip;
            return this;
        }

        public SpellModelBuilder CastTime(uint castTime)
        {
            _model.Entry.CastTime = castTime;
            return this;
        }

        public SpellModelBuilder Timings(uint castTime = 0, uint duration = 0, uint cooldown = 0,
            uint channelMax = 0, uint channelPulse = 0)
        {
            _model.Entry.CastTime         = castTime;
            _model.Entry.SpellDuration    = duration;
            _model.Entry.SpellCoolDown    = cooldown;
            _model.Entry.ChannelMaxTime   = channelMax;
            _model.Entry.ChannelPulseTime = channelPulse;
            return this;
        }

        public SpellModelBuilder Reach(float min = 0, float max = 0, float vertical = 0, uint missileSpeed = 0)
        {
            _model.Entry.TargetMinRange      = min;
            _model.Entry.TargetMaxRange      = max;
            _model.Entry.TargetVerticalRange = vertical;
            _model.Entry.MissileSpeed        = missileSpeed;
            return this;
        }

        public SpellModelBuilder Tier(uint tier)
        {
            _model.Entry.TierIndex = tier;
            return this;
        }

        public SpellModelBuilder Charges(uint charges)
        {
            _model.Entry.AbilityChargeCount = charges;
            return this;
        }

        /// <summary>Drop the Spell4Base join, as malformed client data does.</summary>
        public SpellModelBuilder NoBase()
        {
            _model.SpellBaseModel = null;
            return this;
        }

        /// <summary>Keep the base row but drop its target-mechanics row.</summary>
        public SpellModelBuilder NoMechanics()
        {
            Base.TargetMechanics = null;
            return this;
        }

        public ISpellModel Build() => _model;

        public static implicit operator Spell4Entry(SpellModelBuilder builder) => builder._model.Entry;

        private FakeSpellBaseModel Base => (FakeSpellBaseModel)_model.SpellBaseModel;
    }

    public sealed class FakeSpellModel : ISpellModel
    {
        public Spell4Entry Entry { get; set; }
        public uint Id => Entry.Id;
        public string Description { get; set; } = "";
        public string ActionBarTooltip { get; set; } = "";

        public ISpellBaseModel SpellBaseModel { get; set; }
        public List<ISpellEffectModel> Effects { get; } = [];
        public List<ISpellProcModel> Procs { get; } = [];
        public List<uint> ProcReferences { get; } = [];

        public void Initialise(Spell4Entry entry) => Entry = entry;
    }

    public sealed class FakeSpellBaseModel : ISpellBaseModel
    {
        public Spell4BaseEntry Entry { get; set; }
        public string Name { get; set; } = "";
        public Spell4HitResultsEntry HitResult { get; set; }
        public Spell4TargetMechanicsEntry TargetMechanics { get; set; } = new();
        public Spell4TargetAngleEntry TargetAngle { get; set; }
        public Spell4PrerequisitesEntry Prerequisites { get; set; }
        public Spell4ValidTargetsEntry ValidTargets { get; set; }
        public TargetGroupEntry CastGroup { get; set; }
        public Creature2Entry PositionalAoe { get; set; }
        public TargetGroupEntry AoeGroup { get; set; }
        public Spell4BaseEntry PrerequisiteSpell { get; set; }
        public Spell4SpellTypesEntry SpellType { get; set; }

        public void Initialise(Spell4BaseEntry entry) => Entry = entry;
    }

    public sealed class FakeSpellEffectModel : ISpellEffectModel
    {
        public Spell4EffectsEntry Entry { get; set; }
        public SpellEffectType Type { get; set; }
        public uint TargetFlags { get; set; }
        public uint DamageType { get; set; }
        public uint DelayTime { get; set; }
        public uint TickTime { get; set; }
        public uint DurationTime { get; set; }
        public uint Flags { get; set; }
        public ISpellEffectColumnData ColumnData { get; set; }
        public List<ISpellEffectRowData> RowData { get; } = [];

        public void Initialise(Spell4EffectsEntry entry) => Entry = entry;
    }

    public sealed class FakeSpellProcModel : ISpellProcModel
    {
        public Spell4EffectsEntry Entry { get; set; }
        public ProcType ProcType { get; set; }
        public uint SpellId { get; set; }

        public void Initialise(Spell4EffectsEntry entry) => Entry = entry;
    }
}
