namespace NexusForever.SpellWorks.Core.Models.Filter.EffectType
{
    /// <summary>
    /// The Effect Types search box's id half: the numeric effect type id.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="EffectTypeIdFilter"/>, which is the form's prefix field. This one is the
    /// search box, and so offers the whole-value match the box's checkbox asks for.
    /// </remarks>
    public class EffectTypeIdSearchFilter : IModelFilter<EffectTypeUsage>
    {
        public string Query { get; set; }

        public bool Exact { get; set; }

        public bool Filter(EffectTypeUsage model)
        {
            if (string.IsNullOrWhiteSpace(Query))
                return true;

            return TextMatch.MatchesId((uint)model.Type, Query, Exact);
        }
    }
}
