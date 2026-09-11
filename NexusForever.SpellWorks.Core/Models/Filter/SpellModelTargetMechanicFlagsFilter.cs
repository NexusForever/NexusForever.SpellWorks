namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// Matches spells whose target mechanic flags satisfy <see cref="Flags"/>.
    /// </summary>
    public class SpellModelTargetMechanicFlagsFilter : ISpellModelFilter
    {
        public uint Flags { get; set; }

        /// <summary>Whether every bit must be set, or any one of them. Defaults to a all-bits rule.</summary>
        public MaskMode Mode { get; set; } = MaskMode.All;

        public bool Filter(ISpellModel model)
        {
            if (Flags == 0)
                return true;

            uint flags = model.SpellBaseModel?.TargetMechanics?.Flags ?? 0;
            return Mode.Matches(flags, Flags);
        }
    }
}
