using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;

namespace NexusForever.SpellWorks.Core.Models.Effect
{
    [SpellEffect(SpellEffectType.VitalModifier)]
    public class VitalModifierSpellEffectRowData : DefaultSpellEffectRowData
    {
        public override string Data00 => $"{Entry.DataBits00} - {Enum.GetName(typeof(Vital), Entry.DataBits00)}";
    }
}
