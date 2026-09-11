namespace NexusForever.SpellWorks.Core.Models.Filter.Effect
{
    /// <summary>Matches an effect row on the effect-group list it belongs to.</summary>
    public class SpellEffectGroupListFilter : IModelFilter<ISpellEffectModel>
    {
        public uint GroupListId { get; set; }

        public bool Filter(ISpellEffectModel model) =>
            model.Entry != null && model.Entry.Spell4EffectGroupListId == GroupListId;
    }
}
