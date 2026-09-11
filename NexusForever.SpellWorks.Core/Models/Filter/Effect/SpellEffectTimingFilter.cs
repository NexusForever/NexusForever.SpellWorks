namespace NexusForever.SpellWorks.Core.Models.Filter.Effect
{
    /// <summary>
    /// Which of an effect's millisecond timings a threshold applies to.
    /// </summary>
    public enum SpellEffectTiming
    {
        Delay,
        Tick,
        Duration
    }

    /// <summary>
    /// A threshold on one of an effect's timings.
    /// </summary>
    public class SpellEffectTimingFilter : IModelFilter<ISpellEffectModel>
    {
        public SpellEffectTiming Timing { get; set; }

        public uint Value { get; set; }

        /// <summary>Whether <see cref="Value"/> is a floor or a ceiling.</summary>
        public bool AtMost { get; set; }

        public bool Filter(ISpellEffectModel model)
        {
            uint actual = Timing switch
            {
                SpellEffectTiming.Delay    => model.DelayTime,
                SpellEffectTiming.Tick     => model.TickTime,
                SpellEffectTiming.Duration => model.DurationTime,
                _                          => 0
            };

            return AtMost ? actual <= Value : actual >= Value;
        }
    }
}
