namespace NexusForever.SpellWorks.Core.Models.Filter
{
    public class SpellModelDescriptionFilter : ISpellModelFilter
    {
        public string Description { get; set; }

        public bool Filter(ISpellModel model)
        {
            if (string.IsNullOrEmpty(Description))
                return true;

            // A spell with no description cannot contain anything - guarded here as the search filter does.
            return model.Description?.Contains(Description, StringComparison.InvariantCultureIgnoreCase) ?? false;
        }
    }
}
