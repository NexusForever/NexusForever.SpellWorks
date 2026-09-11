using NexusForever.Game.Static.Spell;

namespace NexusForever.SpellWorks.Core.Models
{
    /// <summary>
    /// How one <see cref="SpellEffectType"/> is used across the whole of <c>Spell4Effects</c>: which spells
    /// carry an effect of that type, and how many rows they carry between them.
    /// </summary>
    /// <remarks>
    /// The reverse of the forward walk everything else does. A <c>Spell4Effects</c> row belongs to exactly one
    /// spell through its <c>SpellId</c>, so the row id answers nothing; the effect <em>type</em> is the key
    /// that spans spells. A spell with three Damage effects counts once in <see cref="SpellIds"/> and three
    /// times in <see cref="EffectRowCount"/> - the two columns say different things and the browser shows both.
    /// </remarks>
    public sealed class EffectTypeUsage
    {
        public SpellEffectType Type { get; init; }

        /// <summary>The spells carrying at least one effect of this type. Distinct, ascending.</summary>
        public IReadOnlyList<uint> SpellIds { get; init; } = [];

        /// <summary><c>Spell4Effects</c> rows of this type, which is at least <c>SpellIds.Count</c>.</summary>
        public int EffectRowCount { get; init; }
    }
}
