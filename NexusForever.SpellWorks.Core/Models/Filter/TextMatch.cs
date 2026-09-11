namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// How every search constraint compares one value to what was typed.
    /// </summary>
    /// <remarks>
    /// Written once because the choice between "contains" and "is exactly" is the search box's, not each
    /// filter's - a filter is told which to do and must not decide for itself, or the checkbox would mean
    /// something different on every pane.
    /// </remarks>
    public static class TextMatch
    {
        /// <summary>
        /// Whether <paramref name="value"/> satisfies <paramref name="query"/>. An empty value never matches
        /// and an empty query is the caller's to skip - a filter that is asked nothing should not be built.
        /// </summary>
        public static bool Matches(string value, string query, bool exact,
            StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(query))
                return false;

            query = query.Trim();

            return exact ? value.Equals(query, comparison) : value.Contains(query, comparison);
        }

        /// <summary>
        /// As <see cref="Matches"/>, for an id. Compared ordinally: an id is digits, and culture rules have
        /// nothing to say about them.
        /// </summary>
        public static bool MatchesId(uint value, string query, bool exact) =>
            Matches(value.ToString(), query, exact, StringComparison.Ordinal);
    }
}
