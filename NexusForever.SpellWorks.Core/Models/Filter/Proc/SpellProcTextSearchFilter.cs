namespace NexusForever.SpellWorks.Core.Models.Filter.Proc
{
    /// <summary>
    /// The Procs search box: the description of the spell the proc casts.
    /// </summary>
    /// <remarks>
    /// The description belongs to another spell entirely, so it is resolved through the caller's lookup
    /// rather than off the proc row, which carries only an id. The id itself is
    /// <see cref="SpellProcIdSearchFilter"/>.
    /// </remarks>
    public class SpellProcTextSearchFilter : IModelFilter<ISpellProcModel>
    {
        public string Query { get; set; }

        public bool Exact { get; set; }

        public Func<uint, string> Description { get; set; }

        public bool Filter(ISpellProcModel model)
        {
            if (string.IsNullOrWhiteSpace(Query))
                return true;

            return TextMatch.Matches(Description?.Invoke(model.SpellId), Query, Exact);
        }
    }
}
