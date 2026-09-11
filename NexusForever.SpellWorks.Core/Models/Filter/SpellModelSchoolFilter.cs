using NexusForever.Game.Static.Spell;

namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// Matches on <c>Spell4BaseEntry.School</c>.
    /// </summary>
    public class SpellModelSchoolFilter : ISpellModelFilter
    {
        public DamageType School { get; set; }

        public bool Filter(ISpellModel model)
        {
            if (model.SpellBaseModel == null)
                return false;

            return (DamageType)model.SpellBaseModel.Entry.School == School;
        }
    }
}
