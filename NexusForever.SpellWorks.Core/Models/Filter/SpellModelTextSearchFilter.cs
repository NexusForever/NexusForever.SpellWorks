namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// The spell search box: matches the description or the spell's localised name.
    /// </summary>
    /// <remarks>
    /// Text only. The id is <see cref="SpellModelIdSearchFilter"/>, deliberately a separate box: one box
    /// doing both would match a typed number against every description as well, so searching an id would
    /// drag in every spell whose text happened to contain those digits.
    ///
    /// The name is here because it is what a player actually sees. The unresolved-text sentinel is excluded,
    /// so a missing localisation never becomes a match.
    /// </remarks>
    public class SpellModelTextSearchFilter : ISpellModelFilter
    {
        public string Query { get; set; }

        public bool Exact { get; set; }

        public bool Filter(ISpellModel model)
        {
            if (string.IsNullOrWhiteSpace(Query))
                return true;

            return TextMatch.Matches(model.Description, Query, Exact) || Named(model);
        }

        private bool Named(ISpellModel model)
        {
            string name = model.SpellBaseModel?.Name;

            return name != SpellModelTextFilter.Unknown && TextMatch.Matches(name, Query, Exact);
        }
    }
}
