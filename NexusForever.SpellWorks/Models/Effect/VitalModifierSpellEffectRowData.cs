﻿using NexusForever.SpellWorks.GameTable.Static;

namespace NexusForever.SpellWorks.Models.Effect
{
    [SpellEffect(SpellEffectType.VitalModifier)]
    public class VitalModifierSpellEffectRowData : DefaultSpellEffectRowData
    {
        public override string Data00 => $"{Entry.DataBits00} - {Enum.GetName(typeof(Vital), Entry.DataBits00)}";
    }
}
