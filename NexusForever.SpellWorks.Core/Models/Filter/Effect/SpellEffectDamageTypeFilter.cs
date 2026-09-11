using NexusForever.Game.Static.Spell;

namespace NexusForever.SpellWorks.Core.Models.Filter.Effect
{
    /// <summary>Matches an effect row on the damage school it deals in.</summary>
    public class SpellEffectDamageTypeFilter : IModelFilter<ISpellEffectModel>
    {
        public DamageType DamageType { get; set; }

        public bool Filter(ISpellEffectModel model) => (DamageType)model.DamageType == DamageType;
    }
}
