using NexusForever.SpellWorks.Core.Static;

namespace NexusForever.SpellWorks.Core.Models.Filter
{
    public class SpellModelCastMethodFilter : ISpellModelFilter
    {
        public CastMethod CastMethod { get; set; }

        public bool Filter(ISpellModel model)
        {
            // Spell4Base is a join, and the join misses on malformed data - a spell with no base row simply
            // has no cast method, so it matches nothing rather than throwing.
            if (model.SpellBaseModel == null)
                return false;

            return (CastMethod)model.SpellBaseModel.Entry.CastMethod == CastMethod;
        }
    }
}
