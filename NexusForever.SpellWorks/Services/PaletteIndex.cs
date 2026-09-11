using NexusForever.Game.Static.Spell;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Services;

namespace NexusForever.SpellWorks.Services
{
    /// <summary>
    /// One command-palette hit. <see cref="Haystack"/> is projected once at index time, never per keystroke.
    /// </summary>
    public sealed record PaletteEntry(string Kind, string Icon, string Label, string Sub, string Haystack, string ViewId, uint SpellId, SpellEffectType? EffectType = null);

    /// <summary>
    /// The flat search index behind ⌘K: every fixed view, every game table, every effect type, and every
    /// spell by id and description.
    /// Rebuilt once on <c>SpellResourcesLoaded</c>.
    /// </summary>
    public sealed class PaletteIndex
    {
        public const int MaxResults = 40;

        private PaletteEntry[] _entries = [];

        #region Dependency Injection

        private readonly ISpellModelService _spellModelService;
        private readonly ITableCatalog _tableCatalog;

        public PaletteIndex(
            ISpellModelService spellModelService,
            ITableCatalog tableCatalog)
        {
            _spellModelService = spellModelService;
            _tableCatalog      = tableCatalog;
        }

        #endregion

        public void Rebuild()
        {
            List<PaletteEntry> entries = [];

            foreach (PaneDescriptor view in PaneDescriptor.AllFixed)
                entries.Add(new PaletteEntry("view", view.Icon, view.Title, view.Meta,
                    (view.Title + " " + view.Label).ToLowerInvariant(), view.Id, 0));

            foreach (TableDescriptor table in _tableCatalog.Tables)
                entries.Add(new PaletteEntry("table", "ph ph-table", table.Name + ".tbl",
                    $"{table.RowCount:n0} rows · {table.Columns.Count} columns",
                    table.Name.ToLowerInvariant(), PaneDescriptor.GameTableId(table.Name), 0));

            foreach (EffectTypeUsage usage in _spellModelService.EffectTypeUsages.Values.OrderBy(u => (uint)u.Type))
                entries.Add(new PaletteEntry("effecttype", "ph ph-crosshair", usage.Type.ToString(),
                    $"{usage.SpellIds.Count:n0} spells · {usage.EffectRowCount:n0} effect rows",
                    (usage.Type + " " + (uint)usage.Type).ToLowerInvariant(),
                    PaneDescriptor.EffectTypeSpells.Id, 0, usage.Type));

            foreach (ISpellModel spell in _spellModelService.SpellModels.Values)
                entries.Add(new PaletteEntry("spell", "ph ph-lightning", spell.Id.ToString(),
                    $"{spell.Description} · Tier {spell.Entry.TierIndex}",
                    (spell.Id + " " + spell.Description).ToLowerInvariant(), PaneDescriptor.Detail.Id, spell.Id));

            _entries = entries.ToArray();
        }

        public List<PaletteEntry> Search(string query)
        {
            PaletteEntry[] entries = _entries;

            if (string.IsNullOrWhiteSpace(query))
                return entries.Take(MaxResults).ToList();

            query = query.Trim().ToLowerInvariant();

            List<PaletteEntry> results = new(MaxResults);
            foreach (PaletteEntry entry in entries)
            {
                if (!entry.Haystack.Contains(query, StringComparison.Ordinal))
                    continue;

                results.Add(entry);
                if (results.Count == MaxResults)
                    break;
            }

            return results;
        }
    }
}
