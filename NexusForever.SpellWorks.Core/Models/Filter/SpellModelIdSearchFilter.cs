namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// The spell search box's id half: matches the spell id.
    /// </summary>
    /// <remarks>
    /// Its own box rather than folded into the text search, because the two answer different questions and
    /// mixing them served neither: exact is what an id search usually wants, and substring is what a text
    /// search usually wants.
    /// </remarks>
    public class SpellModelIdSearchFilter : ISpellModelFilter
    {
        public string Query { get; set; }

        public bool Exact { get; set; }

        public bool Filter(ISpellModel model)
        {
            if (string.IsNullOrWhiteSpace(Query))
                return true;

            return TextMatch.MatchesId(model.Id, Query, Exact);
        }
    }
}
