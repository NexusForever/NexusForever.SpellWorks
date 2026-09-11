using NexusForever.Game.Static.Spell;

namespace NexusForever.SpellWorks.Core.Models.Effect
{
    [SpellEffect(SpellEffectType.Proxy)]
    public class ProxySpellEffectRowData : DefaultSpellEffectRowData
    {
        /// <summary>
        /// A proxy casts the spells named in its first three Data columns.
        /// </summary>
        public override IReadOnlyList<int> HyperlinkedDataIndices => [0, 1, 2];
    }
}
