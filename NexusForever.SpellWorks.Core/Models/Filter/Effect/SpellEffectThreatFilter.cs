namespace NexusForever.SpellWorks.Core.Models.Filter.Effect
{
    /// <summary>A threshold on how much threat an effect generates, relative to its damage or healing.</summary>
    public class SpellEffectThreatFilter : IModelFilter<ISpellEffectModel>
    {
        public double Value { get; set; }

        /// <summary>Whether <see cref="Value"/> is a ceiling rather than a floor.</summary>
        public bool AtMost { get; set; }

        /// <summary>
        /// How far past the bound still counts as on it. The multiplier is a float column, so the bound the
        /// user typed is one no row holds exactly - see <see cref="NumberTolerance"/>.
        /// </summary>
        public double Epsilon { get; set; } = NumberTolerance.Default;

        public bool Filter(ISpellEffectModel model)
        {
            if (model.Entry == null)
                return false;

            double actual = model.Entry.ThreatMultiplier;

            return AtMost
                ? NumberTolerance.AtMost(actual, Value, Epsilon)
                : NumberTolerance.AtLeast(actual, Value, Epsilon);
        }
    }
}
