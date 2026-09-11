namespace NexusForever.SpellWorks.Core.Models.Filter.Effect
{
    /// <summary>Which half of an effect row's EMM condition a constraint reads.</summary>
    public enum EmmPart
    {
        Comparison,
        Value
    }

    /// <summary>
    /// Matches an effect row on its EMM condition - the comparison it makes, or the value it makes it against.
    /// </summary>
    public class SpellEffectEmmFilter : IModelFilter<ISpellEffectModel>
    {
        public EmmPart Part { get; set; }

        public uint Value { get; set; }

        public bool Filter(ISpellEffectModel model)
        {
            if (model.Entry == null)
                return false;

            return (Part == EmmPart.Comparison ? model.Entry.EmmComparison : model.Entry.EmmValue) == Value;
        }
    }
}
