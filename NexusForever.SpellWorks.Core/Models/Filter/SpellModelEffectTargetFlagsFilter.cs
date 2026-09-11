namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// Matches spells with at least one effect whose target flags satisfy <see cref="Flags"/>.
    /// </summary>
    public class SpellModelEffectTargetFlagsFilter : ISpellModelFilter
    {
        public uint Flags { get; set; }

        /// <summary>Whether every bit must be set, or any one of them. Defaults to a all-bits rule.</summary>
        public MaskMode Mode { get; set; } = MaskMode.All;

        public bool Filter(ISpellModel model)
        {
            if (Flags == 0)
                return true;

            return model.Effects.Any(effect => Mode.Matches(effect.TargetFlags, Flags));
        }
    }
}
