namespace NexusForever.SpellWorks.Core.Models.Filter.Effect
{
    /// <summary>Matches one effect row on its flags bitmask.</summary>
    public class SpellEffectFlagsFilter : IModelFilter<ISpellEffectModel>
    {
        public uint Flags { get; set; }

        public MaskMode Mode { get; set; } = MaskMode.All;

        public bool Filter(ISpellEffectModel model) => Mode.Matches(model.Flags, Flags);
    }
}
