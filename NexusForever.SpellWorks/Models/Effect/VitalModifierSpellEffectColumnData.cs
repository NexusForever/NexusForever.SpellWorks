﻿using NexusForever.SpellWorks.GameTable.Static;

namespace NexusForever.SpellWorks.Models.Effect
{
    [SpellEffect(SpellEffectType.VitalModifier)]
    public class VitalModifierSpellEffectColumnData : DefaultSpellEffectColumnData
    {
        public override string Data00ColumnName => "Vital";
    }
}
