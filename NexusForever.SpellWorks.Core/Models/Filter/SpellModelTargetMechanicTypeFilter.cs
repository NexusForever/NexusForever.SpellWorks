using NexusForever.SpellWorks.Core.Static;

namespace NexusForever.SpellWorks.Core.Models.Filter
{
    public class SpellModelTargetMechanicTypeFilter : ISpellModelFilter
    {
        public SpellTargetMechanicType TargetMechanicType { get; set; }

        public bool Filter(ISpellModel model)
        {
            // Both hops are optional: the base row may be missing, and a base row may carry no mechanics row.
            return (SpellTargetMechanicType?)model.SpellBaseModel?.TargetMechanics?.TargetType == TargetMechanicType;
        }
    }
}
