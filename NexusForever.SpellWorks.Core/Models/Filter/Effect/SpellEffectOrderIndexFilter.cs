namespace NexusForever.SpellWorks.Core.Models.Filter.Effect
{
    /// <summary>
    /// Matches an effect row on its order index - the slot it occupies in the spells effect list.
    /// </summary>
    public class SpellEffectOrderIndexFilter : IModelFilter<ISpellEffectModel>
    {
        public uint Value { get; set; }

        /// <summary>Whether <see cref="Value"/> is a ceiling rather than a floor.</summary>
        public bool AtMost { get; set; }

        public bool Filter(ISpellEffectModel model)
        {
            if (model.Entry == null)
                return false;

            uint actual = model.Entry.OrderIndex;
            return AtMost ? actual <= Value : actual >= Value;
        }
    }
}
