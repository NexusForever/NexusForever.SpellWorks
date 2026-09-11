using NexusForever.Game.Static.Spell;

namespace NexusForever.SpellWorks.Core.Models.Filter.Effect
{
    /// <summary>Matches one effect row on its <see cref="SpellEffectType"/>.</summary>
    public class SpellEffectTypeFilter : IModelFilter<ISpellEffectModel>
    {
        public SpellEffectType Type { get; set; }

        public bool Filter(ISpellEffectModel model) => model.Type == Type;
    }
}
