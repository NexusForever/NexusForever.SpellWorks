namespace NexusForever.SpellWorks.Core.Models.Filter.Effect
{
    /// <summary>Matches an effect row on its phase flags bitmask.</summary>
    public class SpellEffectPhaseFlagsFilter : IModelFilter<ISpellEffectModel>
    {
        public uint Flags { get; set; }

        public MaskMode Mode { get; set; } = MaskMode.All;

        public bool Filter(ISpellEffectModel model)
        {
            if (Flags == 0)
                return true;

            return model.Entry != null && Mode.Matches(model.Entry.PhaseFlags, Flags);
        }
    }
}
