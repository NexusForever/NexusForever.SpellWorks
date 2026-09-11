using NexusForever.Game.Static.Entity;

namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// Matches on <c>Spell4BaseEntry.ClassIdPlayer</c>.
    /// </summary>
    public class SpellModelClassFilter : ISpellModelFilter
    {
        public Class Class { get; set; }

        public bool Filter(ISpellModel model)
        {
            if (model.SpellBaseModel == null)
                return false;

            return (Class)model.SpellBaseModel.Entry.ClassIdPlayer == Class;
        }
    }
}
