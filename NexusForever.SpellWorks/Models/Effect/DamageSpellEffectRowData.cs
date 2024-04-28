﻿using NexusForever.SpellWorks.GameTable.Static;

namespace NexusForever.SpellWorks.Models.Effect
{
    [SpellEffect(SpellEffectType.Damage)]
    public class DamageSpellEffectRowData : DefaultSpellEffectRowData
    {
        public override string Data00 => BitConverter.UInt32BitsToSingle(Entry.DataBits00).ToString();
    }
}
