namespace NexusForever.SpellWorks.Core.Models.Filter.EffectType
{
    /// <summary>A threshold on how many spells use an effect type.</summary>
    public class EffectTypeSpellCountFilter : IModelFilter<EffectTypeUsage>
    {
        public uint Value { get; set; }

        /// <summary>Whether <see cref="Value"/> is a floor or a ceiling.</summary>
        public bool AtMost { get; set; }

        public bool Filter(EffectTypeUsage model)
        {
            return AtMost ? model.SpellIds.Count <= Value : model.SpellIds.Count >= Value;
        }
    }
}
