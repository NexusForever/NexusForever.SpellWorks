namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// Matches the placeholder spells the client data marks as tests.
    /// </summary>
    /// <remarks>
    /// Phrased positively, as <see cref="SpellModelDeprecatedFilter"/> is: it selects test spells rather
    /// than hiding them, and "hide the test spells" is this filter negated. Polarity belongs to the
    /// caller, carried by <see cref="NotFilter{T}"/>, or the chips row would have to read a filter that
    /// is its own negation.
    ///
    /// The marker is the literal <c>[Test]</c> and nothing more. Deprecation is matched three ways
    /// because the data flags it three ways; "test" is not, and a bare word that loose would catch every
    /// spell whose description merely mentions testing.
    /// </remarks>
    public class SpellModelTestFilter : ISpellModelFilter
    {
        private const string Marker = "[Test]";

        public static bool IsTest(ISpellModel model)
        {
            string description = model.Description;

            return !string.IsNullOrEmpty(description)
                && description.Contains(Marker, StringComparison.OrdinalIgnoreCase);
        }

        public bool Filter(ISpellModel model)
        {
            return IsTest(model);
        }
    }
}
