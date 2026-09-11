namespace NexusForever.SpellWorks.Core.Models.Filter.Column
{
    /// <summary>
    /// One constraint on one text-valued column of one game table row.
    /// </summary>
    /// <remarks>
    /// "Text" here is every column that is not a number: a string, an array (rendered as its values joined
    /// by a space), a bool, an enum. They share one comparison because the only thing they share is their
    /// rendered form, which is exactly what the user typed against.
    /// </remarks>
    public sealed class ColumnTextFilter : IModelFilter<object>
    {
        public required Func<object, string> Read { get; init; }

        public string Query { get; init; }

        /// <summary>Whether the query is a whole value rather than a substring.</summary>
        public bool Exact { get; init; }

        public bool Filter(object row) => row != null && TextMatch.Matches(Read(row), Query, Exact);
    }
}
