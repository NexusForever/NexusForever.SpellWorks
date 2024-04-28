﻿namespace NexusForever.SpellWorks.GameTable.Static
{
    public enum SpellTargetMechanicFlags
    {
        None               = 0x0000,
        IsPlayer           = 0x0001,
        Unknown02          = 0x0002,
        Unknown04          = 0x0004,
        IsFriendly         = 0x0008,
        IsEnemy            = 0x0010,
        AlsoIncludeEnemies = 0x0020, // Presumed that AoeCount is applied to Enemies and Friendlies individually
        Unknown40          = 0x0040,
        Unknown80          = 0x0080,
    }
}
