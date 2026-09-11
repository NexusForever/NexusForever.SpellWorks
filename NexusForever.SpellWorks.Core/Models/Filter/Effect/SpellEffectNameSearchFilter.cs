namespace NexusForever.SpellWorks.Core.Models.Filter.Effect
{
    /// <summary>The Efcfcts search box: the effect type's name.</summary>
    public class SpellEffectNameSearchFilter : IModelFilter<ISpellEffectModel>
    {
        public string Query { get; set; }

        public bool Exact { get; set; }

        public bool Filter(ISpellEffectModel model)
        {
            if (string.IsNullOrWhiteSpace(Query))
                return true;

            return TextMatch.Matches(model.Type.ToString(), Query, Exact);
        }
    }
}
