using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter;

namespace NexusForever.SpellWorks.Core.Services
{
    /// <summary>One term of a search expression: the text to match, and whether it is negated.</summary>
    public sealed record SearchTerm(string Text, bool Negate);

    /// <summary>
    /// The grid search box, parsed. Supports <c>||</c>, <c>&amp;&amp;</c> and a leading <c>!</c> on a term.
    /// </summary>
    /// <remarks>
    /// Precedence <c>!</c> &gt; <c>&amp;&amp;</c> &gt; <c>||</c> falls straight out of "split on <c>||</c>,
    /// then split each part on <c>&amp;&amp;</c>", so the parse is already disjunctive normal form - the same
    /// shape the filter form's OR blocks have, and it folds through the same composites.
    ///
    /// Whitespace is <em>not</em> an implicit AND: <c>fire bolt</c> matches that literal phrase. A single
    /// <c>&amp;</c> or <c>|</c> is likewise ordinary text, since game data is
    /// full of both. Anything that parses to nothing - <c>a &amp;&amp;</c>, <c>|| ||</c>, a lone <c>!</c> -
    /// has its empty terms dropped rather than raising, so incomplete typing degrades to the literal search
    /// it was a moment ago instead of erroring mid-keystroke.
    /// </remarks>
    public sealed class SearchExpression
    {
        /// <summary>Terms AND-ed within a group, groups OR-ed across. Empty means no constraint.</summary>
        public IReadOnlyList<IReadOnlyList<SearchTerm>> Groups { get; }

        public bool IsEmpty => Groups.Count == 0;

        private SearchExpression(IReadOnlyList<IReadOnlyList<SearchTerm>> groups)
        {
            Groups = groups;
        }

        public static readonly SearchExpression Empty = new([]);

        public static SearchExpression Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Empty;

            List<IReadOnlyList<SearchTerm>> groups = [];

            foreach (string alternative in SplitOn(input, '|'))
            {
                List<SearchTerm> terms = [];

                foreach (string part in SplitOn(alternative, '&'))
                {
                    string text = part.Trim();

                    bool negate = text.StartsWith('!');
                    if (negate)
                        text = text[1..].Trim();

                    // A term with no text left constrains nothing - drop it rather than matching everything.
                    if (text.Length > 0)
                        terms.Add(new SearchTerm(text, negate));
                }

                if (terms.Count > 0)
                    groups.Add(terms);
            }

            return groups.Count > 0 ? new SearchExpression(groups) : Empty;
        }

        /// <summary>
        /// Fold into one filter, with <paramref name="termFactory"/> supplying each pane's own idea of what a
        /// bare search term means - description-or-id for spells, the type name for effects, and so on.
        /// </summary>
        public IModelFilter<T> Compile<T>(Func<string, IModelFilter<T>> termFactory)
        {
            if (IsEmpty)
                return MatchAllFilter<T>.Instance;

            List<IModelFilter<T>> alternatives = [];

            foreach (IReadOnlyList<SearchTerm> group in Groups)
            {
                List<IModelFilter<T>> terms = [];

                foreach (SearchTerm term in group)
                {
                    IModelFilter<T> filter = termFactory(term.Text);
                    if (filter == null)
                        continue;

                    terms.Add(term.Negate ? new NotFilter<T>(filter) : filter);
                }

                if (terms.Count > 0)
                    alternatives.Add(new AllOfFilter<T>(terms));
            }

            return alternatives.Count > 0 ? new AnyOfFilter<T>(alternatives) : MatchAllFilter<T>.Instance;
        }

        /// <summary>
        /// Split on a doubled operator character, leaving a single one as ordinary text - game data is full
        /// of lone ampersands and pipes, and splitting on those would break ordinary searches.
        /// </summary>
        private static List<string> SplitOn(string input, char op)
        {
            List<string> parts = [];
            int start = 0;

            for (int i = 0; i + 1 < input.Length; i++)
            {
                if (input[i] != op || input[i + 1] != op)
                    continue;

                parts.Add(input[start..i]);
                i++;
                start = i + 1;
            }

            parts.Add(input[start..]);
            return parts;
        }
    }
}
