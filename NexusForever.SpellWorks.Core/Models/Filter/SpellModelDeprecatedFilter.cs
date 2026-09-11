namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// Matches spells the client data marks as deprecated.
    /// </summary>
    /// <remarks>
    /// Phrased positively - it selects deprecated spells rather than hiding them. Polarity is the caller's,
    /// carried by <see cref="NotFilter{T}"/>, so "only deprecated" and "hide deprecated" are the same filter
    /// negated or not. A filter that reads as its own negation cannot be chipped or negated readably.
    ///
    /// <c>Spell4</c> carries no deprecation column - the marker lives in the description, which is how the
    /// client data itself flags them (<c>[DEPRECATED] ...</c>, <c>Deprecate - ...</c>).
    /// </remarks>
    public class SpellModelDeprecatedFilter : ISpellModelFilter
    {
        public static bool IsDeprecated(ISpellModel model)
        {
            string description = model.Description;
            if (string.IsNullOrEmpty(description))
                return false;

            return description.Contains("DEPRECATED", StringComparison.OrdinalIgnoreCase)
                || description.Contains("Deprecate", StringComparison.OrdinalIgnoreCase)
                || description.Contains("[DEP]", StringComparison.OrdinalIgnoreCase);
        }

        public bool Filter(ISpellModel model)
        {
            return IsDeprecated(model);
        }
    }
}
