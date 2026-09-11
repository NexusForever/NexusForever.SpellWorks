namespace NexusForever.SpellWorks.Core.Models.Filter.Effect
{
    /// <summary>
    /// The Effects search box's id half: the numeric effect type id, which is the grid's "Type Id" column.
    /// </summary>
    public class SpellEffectIdSearchFilter : IModelFilter<ISpellEffectModel>
    {
        public string Query { get; set; }

        public bool Exact { get; set; }

        public bool Filter(ISpellEffectModel model)
        {
            if (string.IsNullOrWhiteSpace(Query))
                return true;

            return TextMatch.MatchesId((uint)model.Type, Query, Exact);
        }
    }
}
