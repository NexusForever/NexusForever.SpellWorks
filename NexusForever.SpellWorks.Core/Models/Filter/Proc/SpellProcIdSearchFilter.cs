namespace NexusForever.SpellWorks.Core.Models.Filter.Proc
{
    /// <summary>The Procs search box's id half: the id of the spell the proc casts.</summary>
    public class SpellProcIdSearchFilter : IModelFilter<ISpellProcModel>
    {
        public string Query { get; set; }

        public bool Exact { get; set; }

        public bool Filter(ISpellProcModel model)
        {
            if (string.IsNullOrWhiteSpace(Query))
                return true;

            return TextMatch.MatchesId(model.SpellId, Query, Exact);
        }
    }
}
