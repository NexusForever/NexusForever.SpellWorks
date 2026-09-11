namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>Which piece of a spell's localised text a constraint reads.</summary>
    public enum SpellText
    {
        /// <summary>The readable name off <c>Spell4Base</c>.</summary>
        Name,

        /// <summary>The action-bar tooltip.</summary>
        Tooltip
    }

    /// <summary>
    /// Matches a substring of a spell's localised name or tooltip.
    /// </summary>
    /// <remarks>
    /// The lookup returns a literal <see cref="Unknown"/> sentinel for a text id it has no entry for, so that
    /// string is treated as no text at all - otherwise searching for "unknown" would match every spell with
    /// a missing localisation, which is the opposite of a search.
    /// </remarks>
    public class SpellModelTextFilter : ISpellModelFilter
    {
        /// <summary>What <c>ITextTableService.GetText</c> returns for an id it cannot resolve.</summary>
        public const string Unknown = "UNKNOWN LOCALISED TEXT ID";

        public SpellText Text { get; set; }

        public string Query { get; set; }

        public bool Filter(ISpellModel model)
        {
            if (string.IsNullOrWhiteSpace(Query))
                return true;

            string text = Text == SpellText.Name ? model.SpellBaseModel?.Name : model.ActionBarTooltip;

            if (string.IsNullOrEmpty(text) || text == Unknown)
                return false;

            return text.Contains(Query.Trim(), StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
