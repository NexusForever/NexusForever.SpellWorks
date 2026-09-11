namespace NexusForever.SpellWorks.Core.Models.Filter.EffectType
{
    /// <summary>The Effect Types search box: the effect type's name.</summary>
    public class EffectTypeNameSearchFilter : IModelFilter<EffectTypeUsage>
    {
        public string Query { get; set; }

        public bool Exact { get; set; }

        public bool Filter(EffectTypeUsage model)
        {
            if (string.IsNullOrWhiteSpace(Query))
                return true;

            return TextMatch.Matches(model.Type.ToString(), Query, Exact);
        }
    }
}
