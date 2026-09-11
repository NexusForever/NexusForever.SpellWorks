using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Core.Static;

namespace NexusForever.SpellWorks.Core.Test.Testing
{
    /// <summary>
    /// Builds one effect row to filter against, in the same fluent style as <see cref="SpellModelBuilder"/>.
    /// </summary>
    public sealed class SpellEffectModelBuilder
    {
        private readonly FakeSpellEffectModel _model = new() { Entry = new Spell4EffectsEntry() };

        public static SpellEffectModelBuilder A(SpellEffectType type = default) =>
            new SpellEffectModelBuilder().Type(type);

        public SpellEffectModelBuilder Type(SpellEffectType type)
        {
            _model.Type = type;
            return this;
        }

        public SpellEffectModelBuilder Flags(uint flags)
        {
            _model.Flags = flags;
            return this;
        }

        public SpellEffectModelBuilder TargetFlags(uint flags)
        {
            _model.TargetFlags = flags;
            return this;
        }

        public SpellEffectModelBuilder Timing(uint delay = 0, uint tick = 0, uint duration = 0)
        {
            _model.DelayTime    = delay;
            _model.TickTime     = tick;
            _model.DurationTime = duration;
            return this;
        }

        public ISpellEffectModel Build() => _model;
    }

    /// <summary>Builds one proc row to filter against.</summary>
    public sealed class SpellProcModelBuilder
    {
        private readonly FakeSpellProcModel _model = new();

        public static SpellProcModelBuilder A(uint spellId = 1, uint procType = 0) =>
            new SpellProcModelBuilder().SpellId(spellId).ProcType(procType);

        public SpellProcModelBuilder SpellId(uint spellId)
        {
            _model.SpellId = spellId;
            return this;
        }

        public SpellProcModelBuilder ProcType(uint procType)
        {
            _model.ProcType = (ProcType)procType;
            return this;
        }

        public ISpellProcModel Build() => _model;
    }

    /// <summary>
    /// Builds a table descriptor to filter against. Only the name and row count are read by the filters, so
    /// the projection members are stubbed rather than made real.
    /// </summary>
    public static class TableDescriptorBuilder
    {
        public static TableDescriptor A(string name, int rowCount = 0, params string[] columns) =>
            new(name, typeof(object), rowCount, columns.Length > 0 ? columns : ["Id"], () => [], _ => []);
    }
}
