namespace NexusForever.SpellWorks.Core.Static
{
    /// <summary>
    /// The <c>Flags</c> bitmask on a <c>Spell4TargetMechanics</c> row.
    /// </summary>
    [Flags]
    public enum SpellTargetMechanicFlags
    {
        None               = 0x0000,
        IsPlayer           = 0x0001,
        Unknown02          = 0x0002,
        Unknown04          = 0x0004,
        IsFriendly         = 0x0008,
        IsEnemy            = 0x0010,
        AlsoIncludeEnemies = 0x0020,
        Unknown40          = 0x0040,
        Unknown80          = 0x0080,
    }
}