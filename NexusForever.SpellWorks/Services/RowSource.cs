using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.SpellWorks.Core.Models.Filter.Effect;
using NexusForever.SpellWorks.Core.Models.Filter.EffectType;
using NexusForever.SpellWorks.Core.Models.Filter.Proc;
using NexusForever.SpellWorks.Core.Models.Filter.Table;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Core.Static;

namespace NexusForever.SpellWorks.Services
{
    /// <summary>
    /// Projects the engine's object graphs to flat <see cref="GridRow"/> arrays, once per table load or
    /// filter apply. Everything here is meant to run on a background thread.
    /// </summary>
    public sealed class RowSource
    {
        private static readonly GridColumn[] spell4Columns =
        [
            new("Id",          "id",  84),
            new("Description", "",    560),
            new("Tier",        "num", 64),
            new("Class",       "dim", 116),
            new("School",      "dim", 104),
            new("Cast",        "dim", 140),
            new("Fx",          "num", 56),
            new("Procs",       "num", 68)
        ];

        private static readonly GridColumn[] effectColumns =
        [
            new("#",           "num", 48),
            new("Type Id",     "num", 72),
            new("Effect Type", "",    190),
            new("Damage",      "num", 96),
            new("Delay",       "num", 72),
            new("Tick",        "num", 72),
            new("Duration",    "num", 80),
            new("Flags",       "num", 72),
            new("Data00",      "num", 104),
            new("Data01",      "num", 104),
            new("Data02",      "num", 104)
        ];

        private static readonly GridColumn[] procColumns =
        [
            new("Proc Type", "num", 96),
            new("Spell Id",  "id",  96),
            new("Reference", "dim", 420)
        ];

        private static readonly GridColumn[] effectTypeColumns =
        [
            new("Type Id",     "num", 84),
            new("Effect Type", "",    220),
            new("Spells",      "num", 96),
            new("Effect rows", "num", 110)
        ];

        private static readonly GridColumn[] effectTypeSpellColumns =
        [
            new("Id",          "id",  84),
            new("Description", "",    560),
            new("Tier",        "num", 64),
            new("Class",       "dim", 116),
            new("School",      "dim", 104),
            new("Uses",        "num", 72)
        ];

        private static readonly GridColumn[] tableColumns =
        [
            new("Table",   "",    260),
            new("Rows",    "num", 110),
            new("Columns", "num", 96),
            new("State",   "dim", 96)
        ];

        #region Dependency Injection

        private readonly ISpellModelService _spellModelService;
        private readonly ISpellModelFilterService _filterService;
        private readonly ITableCatalog _tableCatalog;
        private readonly FilterSchemaRegistry _schemas;

        public RowSource(
            ISpellModelService spellModelService,
            ISpellModelFilterService filterService,
            ITableCatalog tableCatalog,
            FilterSchemaRegistry schemas)
        {
            _spellModelService = spellModelService;
            _filterService     = filterService;
            _tableCatalog      = tableCatalog;
            _schemas           = schemas;
        }

        #endregion

        /// <summary>
        /// Project one pane's rows, narrowed by <paramref name="query"/>.
        /// </summary>
        /// <remarks>
        /// Every builder computes its <c>Total</c> before filtering, so "Total stays unfiltered" holds by
        /// construction rather than by remembering to.
        /// </remarks>
        public GridData Build(PaneDescriptor descriptor, FilterQuery query, ISpellModel spell,
            SpellEffectType? effectType = null)
        {
            return descriptor.Kind switch
            {
                PaneKind.Spell4           => BuildSpell4(Compile<ISpellModel>(descriptor, query)),
                PaneKind.Effects          => BuildEffects(spell, Compile<ISpellEffectModel>(descriptor, query)),
                PaneKind.Procs            => BuildProcs(spell, Compile<ISpellProcModel>(descriptor, query)),
                PaneKind.EffectTypes      => BuildEffectTypes(Compile<EffectTypeUsage>(descriptor, query)),
                PaneKind.EffectTypeSpells => BuildEffectTypeSpells(effectType, Compile<ISpellModel>(descriptor, query)),
                PaneKind.Tables           => BuildTables(Compile<TableDescriptor>(descriptor, query)),
                PaneKind.GameTable        => BuildGameTable(descriptor, Compile<string[]>(descriptor, query)),
                _                         => GridData.Empty
            };
        }

        private IModelFilter<T> Compile<T>(PaneDescriptor descriptor, FilterQuery query)
        {
            FilterSchema<T> schema = _schemas.For<T>(descriptor);

            return schema == null
                ? MatchAllFilter<T>.Instance
                : FilterQueryCompiler.Compile(query, schema);
        }

        // ------------------------------------------------------------------ Spell4

        private GridData BuildSpell4(IModelFilter<ISpellModel> filter)
        {
            IEnumerable<ISpellModel> models =
                _filterService.Filter(filter, _spellModelService.SpellModels.Values);

            GridRow[] rows = models.Select(ToSpell4Row).ToArray();
            return new GridData(spell4Columns, rows, _spellModelService.SpellModels.Count);
        }

        private static GridRow ToSpell4Row(ISpellModel model)
        {
            ISpellBaseModel spellBase = model.SpellBaseModel;

            return new GridRow(model.Id,
            [
                model.Id.ToString(),
                model.Description ?? "",
                model.Entry.TierIndex.ToString(),
                EnumText.Name<Class>(spellBase?.Entry.ClassIdPlayer),
                EnumText.Name<DamageType>(spellBase?.Entry.School),
                EnumText.Name<CastMethod>(spellBase?.Entry.CastMethod),
                model.Effects.Count.ToString(),
                model.Procs.Count.ToString()
            ], model);
        }

        // ------------------------------------------------------------------ effects

        /// <summary>
        /// The Effects view lists the effects of the pane's current spell - not all 100k rows of <c>Spell4Effects</c>.
        /// </summary>
        private GridData BuildEffects(ISpellModel spell, IModelFilter<ISpellEffectModel> filter)
        {
            if (spell == null)
                return new GridData(effectColumns, [], 0);

            int total = spell.Effects.Count;

            // Numbered before the filter runs, not after: the "#" cell and the row key are the effect's
            // position in this spell's own row list - what the context menu prints as "idx" and what the
            // Detail cards number themselves by. Counting the surviving sequence instead renumbers it from
            // zero, so a narrowed grid names a row the spell does not have there.
            IEnumerable<(ISpellEffectModel Effect, int Index)> effects = spell.Effects
                .Select((effect, index) => (Effect: effect, Index: index))
                .Where(entry => filter.Filter(entry.Effect));

            GridRow[] rows = effects.Select(entry =>
            {
                (ISpellEffectModel effect, int index) = entry;
                ISpellEffectRowData data = effect.RowData.FirstOrDefault();

                return new GridRow((uint)index,
                [
                    index.ToString(),
                    ((uint)effect.Type).ToString(),
                    effect.Type.ToString(),
                    effect.DamageType.ToString(),
                    effect.DelayTime.ToString(),
                    effect.TickTime.ToString(),
                    effect.DurationTime.ToString(),
                    effect.Flags.ToString(),
                    data?.Data00 ?? effect.Entry.DataBits00.ToString(),
                    data?.Data01 ?? effect.Entry.DataBits01.ToString(),
                    data?.Data02 ?? effect.Entry.DataBits02.ToString()
                ], effect);
            }).ToArray();

            return new GridData(effectColumns, rows, total);
        }

        // ------------------------------------------------------------------ procs

        private GridData BuildProcs(ISpellModel spell, IModelFilter<ISpellProcModel> filter)
        {
            if (spell == null)
                return new GridData(procColumns, [], 0);

            int total = spell.Procs.Count;

            IEnumerable<ISpellProcModel> procs = _filterService.Filter(filter, spell.Procs);

            GridRow[] rows = procs.Select(proc => new GridRow(proc.SpellId,
            [
                ((uint)proc.ProcType).ToString(),
                proc.SpellId.ToString(),
                Description(proc.SpellId) ?? "unknown spell"
            ], proc)).ToArray();

            return new GridData(procColumns, rows, total);
        }

        private string Description(uint spellId)
        {
            return _spellModelService.SpellModels.TryGetValue(spellId, out ISpellModel model) ? model.Description : null;
        }

        // ------------------------------------------------------------------ effect types

        /// <summary>
        /// Every effect type the client actually uses, with how many spells reach for it. The reverse of the
        /// Effects view: that one asks what a spell does, this one asks who does a given thing.
        /// </summary>
        private GridData BuildEffectTypes(IModelFilter<EffectTypeUsage> filter)
        {
            int total = _spellModelService.EffectTypeUsages.Count;

            IEnumerable<EffectTypeUsage> usages =
                _filterService.Filter(filter, _spellModelService.EffectTypeUsages.Values);

            GridRow[] rows = usages
                .OrderBy(u => (uint)u.Type)
                .Select(usage => new GridRow((uint)usage.Type,
                [
                    ((uint)usage.Type).ToString(),
                    usage.Type.ToString(),
                    usage.SpellIds.Count.ToString("n0"),
                    usage.EffectRowCount.ToString("n0")
                ], usage))
                .ToArray();

            return new GridData(effectTypeColumns, rows, total);
        }

        /// <summary>
        /// The spells behind one effect type. Every Spell4 filter applies here as it does in the Spell4 browser,
        /// so the reverse lookup can be narrowed the same way the forward one is.
        /// </summary>
        private GridData BuildEffectTypeSpells(SpellEffectType? effectType, IModelFilter<ISpellModel> filter)
        {
            if (effectType is not { } type || !_spellModelService.EffectTypeUsages.TryGetValue(type, out EffectTypeUsage usage))
                return new GridData(effectTypeSpellColumns, [], 0);

            List<ISpellModel> models = [];
            foreach (uint spellId in usage.SpellIds)
                if (_spellModelService.SpellModels.TryGetValue(spellId, out ISpellModel model))
                    models.Add(model);

            int total = models.Count;

            IEnumerable<ISpellModel> filtered = _filterService.Filter(filter, models);

            GridRow[] rows = filtered.Select(model => ToEffectTypeSpellRow(model, type)).ToArray();
            return new GridData(effectTypeSpellColumns, rows, total);
        }

        private static GridRow ToEffectTypeSpellRow(ISpellModel model, SpellEffectType type)
        {
            ISpellBaseModel spellBase = model.SpellBaseModel;

            return new GridRow(model.Id,
            [
                model.Id.ToString(),
                model.Description ?? "",
                model.Entry.TierIndex.ToString(),
                EnumText.Name<Class>(spellBase?.Entry.ClassIdPlayer),
                EnumText.Name<DamageType>(spellBase?.Entry.School),
                model.Effects.Count(e => e.Type == type).ToString()
            ], model);
        }

        // ------------------------------------------------------------------ table list

        private GridData BuildTables(IModelFilter<TableDescriptor> filter)
        {
            int total = _tableCatalog.Tables.Count;

            IEnumerable<TableDescriptor> tables = _filterService.Filter(filter, _tableCatalog.Tables);

            GridRow[] rows = tables.Select((table, index) => new GridRow((uint)index,
            [
                table.Name,
                table.RowCount.ToString("n0"),
                table.Columns.Count.ToString(),
                table.RowCount > 0 ? "loaded" : "empty"
            ], table)).ToArray();

            return new GridData(tableColumns, rows, total);
        }

        // ------------------------------------------------------------------ generic table

        private GridData BuildGameTable(PaneDescriptor descriptor, IModelFilter<string[]> filter)
        {
            TableDescriptor table = _tableCatalog.Get(descriptor.TableName);
            if (table == null)
                return GridData.Empty;

            GridColumn[] columns = table.Columns
                .Select((column, index) => new GridColumn(column, index == 0 ? "id" : "num", index == 0 ? 96 : 128))
                .ToArray();

            List<GridRow> rows = [];
            IReadOnlyList<object> entries = table.Rows();

            for (int i = 0; i < entries.Count; i++)
            {
                string[] cells = table.Values(entries[i]);

                if (!filter.Filter(cells))
                    continue;

                rows.Add(new GridRow(uint.TryParse(cells[0], out uint id) ? id : (uint)i, cells, entries[i]));
            }

            return new GridData(columns, rows.ToArray(), entries.Count);
        }
    }
}
