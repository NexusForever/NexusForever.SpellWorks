using NexusForever.SpellWorks.Core.Models;

namespace NexusForever.SpellWorks.Services.Filtering
{
    /// <summary>
    /// Builds the filter for one search term: the text typed, and whether the box is asking for a whole
    /// value rather than a substring.
    /// </summary>
    public delegate IModelFilter<T> SearchTermFactory<T>(string term, bool exact);

    /// <summary>
    /// One linked game table row a pane's elements can be filtered by column on, and how to reach it.
    /// </summary>
    /// <remarks>
    /// <paramref name="Rows"/> returns a sequence because the interesting sources are one-to-many - a spell
    /// has many effect rows - and a one-to-one link is the same shape with one element, or none where the
    /// link is unresolved. That uniformity is what lets one <c>RowMatchFilter</c> serve both.
    /// </remarks>
    public sealed record FilterFlexSource<T>(
        string Key,
        string Title,
        Func<T, IEnumerable<object>> Rows,
        IReadOnlyList<FilterColumnFieldSchema> Columns);

    /// <summary>What the form needs of a flex source: its title and the columns it offers.</summary>
    public sealed record FilterFlexCard(
        string Key,
        string Title,
        IReadOnlyList<FilterColumnFieldSchema> Columns);

    /// <summary>
    /// Every field one pane can be filtered on, in form order.
    /// </summary>
    public abstract class FilterSchema
    {
        public IReadOnlyList<FilterFieldSchema> Fields { get; protected init; } = [];

        /// <summary>Whether this pane has anything numbered to search, and so draws the id box.</summary>
        public abstract bool HasIdSearch { get; }

        /// <summary>What the text box invites the user to type.</summary>
        public string TextPlaceholder { get; protected init; } = "Search…";

        /// <summary>What the id box invites the user to type. Null when the pane has no id box.</summary>
        public string IdPlaceholder { get; protected init; }

        private Dictionary<string, FilterFieldSchema> _byKey;

        /// <summary>The field <paramref name="key"/> names, or null if the schema no longer has one.</summary>
        public FilterFieldSchema Field(string key)
        {
            _byKey ??= Fields.ToDictionary(f => f.Key);

            return key != null && _byKey.TryGetValue(key, out FilterFieldSchema field) ? field : null;
        }

        /// <summary>
        /// The hand-written fields, grouped into the titled cards the form draws, in declaration order.
        /// </summary>
        /// <remarks>
        /// Flex columns are excluded on purpose. They are in <see cref="Fields"/> so that the compiler,
        /// the chips row and the persistence mapper resolve them by key like anything else, but there are
        /// hundreds of them and the form draws every card in every OR block - listing them here would make
        /// the form unusable, which is the very thing the flex card exists to avoid.
        /// </remarks>
        public IEnumerable<IGrouping<string, FilterFieldSchema>> Cards =>
            Fields.Where(f => f is not FilterColumnFieldSchema).GroupBy(f => f.GroupTitle);

        /// <summary>The flex cards the form draws after the hand-written ones, in declaration order.</summary>
        public IReadOnlyList<FilterFlexCard> FlexCards { get; protected init; } = [];
    }

    /// <summary>A schema for a pane listing <typeparamref name="T"/>.</summary>
    public sealed class FilterSchema<T> : FilterSchema
    {
        public FilterSchema(
            IReadOnlyList<FilterFieldSchema<T>> fields,
            SearchTermFactory<T> textTerm,
            string textPlaceholder,
            SearchTermFactory<T> idTerm = null,
            string idPlaceholder = null,
            IReadOnlyList<FilterFlexSource<T>> flex = null)
        {
            Flex = flex ?? [];

            // Flex columns join the field list so that every path that resolves a condition by key - the
            // compiler, the chips row, the persistence mapper - finds them without knowing they exist.
            base.Fields          = [.. fields, .. Flex.SelectMany(source => source.Columns)];
            TextTerm             = textTerm;
            IdTerm               = idTerm;
            base.TextPlaceholder = textPlaceholder;
            base.IdPlaceholder   = idPlaceholder;
            base.FlexCards       = [.. Flex.Select(s => new FilterFlexCard(s.Key, s.Title, s.Columns))];
        }

        /// <summary>The linked rows this pane's elements can be filtered by column on.</summary>
        public IReadOnlyList<FilterFlexSource<T>> Flex { get; }

        private Dictionary<string, FilterFlexSource<T>> _flexByKey;

        /// <summary>The flex source <paramref name="key"/> names, or null.</summary>
        public FilterFlexSource<T> FlexSource(string key)
        {
            _flexByKey ??= Flex.ToDictionary(s => s.Key);

            return key != null && _flexByKey.TryGetValue(key, out FilterFlexSource<T> source) ? source : null;
        }

        /// <summary>
        /// What one word in the text box means here - the description or name on the spell panes, the effect
        /// type's name on the Effects pane, any cell on a generic table.
        /// </summary>
        public SearchTermFactory<T> TextTerm { get; }

        /// <summary>
        /// What one term in the id box means here, or null on a pane with nothing numbered to search - the
        /// table list, whose rows are named rather than numbered, is the only one. The form draws the second
        /// box only where this is set.
        /// </summary>
        public SearchTermFactory<T> IdTerm { get; }

        public override bool HasIdSearch => IdTerm != null;

        /// <summary>
        /// The typed field <paramref name="key"/> names, or null - including for a key the schema has as a
        /// flex column, which is untyped in the element and so is not one of these.
        /// </summary>
        public new FilterFieldSchema<T> Field(string key) => base.Field(key) as FilterFieldSchema<T>;
    }
}
