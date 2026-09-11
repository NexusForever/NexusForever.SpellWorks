using NexusForever.GameTable.Model;
using NexusForever.Game.Static.Spell;

namespace NexusForever.SpellWorks.Core.Models
{
    public interface ISpellEffectModel
    {
        /// <summary>
        /// Backing game table row.
        /// </summary>
        Spell4EffectsEntry Entry { get; }

        SpellEffectType Type { get; }
        uint TargetFlags { get; }
        uint DamageType { get; }
        uint DelayTime { get; }
        uint TickTime { get; }
        uint DurationTime { get; }
        uint Flags { get; }
        ISpellEffectColumnData ColumnData { get; }
        List<ISpellEffectRowData> RowData { get; }

        void Initialise(Spell4EffectsEntry entry);
    }
}
