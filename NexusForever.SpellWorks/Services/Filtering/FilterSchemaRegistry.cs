using System.Collections.Concurrent;
using NexusForever.Game.Static.Entity;
using NexusForever.GameTable.Model;
using NexusForever.Game.Static.Spell;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.SpellWorks.Core.Models.Filter.Column;
using NexusForever.SpellWorks.Core.Models.Filter.Effect;
using NexusForever.SpellWorks.Core.Models.Filter.EffectType;
using NexusForever.SpellWorks.Core.Models.Filter.Numeric;
using NexusForever.SpellWorks.Core.Models.Filter.Proc;
using NexusForever.SpellWorks.Core.Models.Filter.Table;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Core.Static;

namespace NexusForever.SpellWorks.Services.Filtering
{
    /// <summary>
    /// The schema for each pane kind: what it can be filtered on, and how each constraint becomes a filter.
    /// </summary>
    /// <remarks>
    /// Resolved from a <see cref="PaneDescriptor"/> rather than a bare <see cref="PaneKind"/> because a
    /// descriptor carries its table name, which is what a generic table's fields are built from. The fixed
    /// kinds are cached; a game table's schema is rebuilt per descriptor, since its columns are a runtime fact
    /// and the column a mask reads has to be resolved to an index before the row loop starts.
    ///
    /// The cache is concurrent because the registry is a singleton reached from two threads at once: a grid
    /// compiles its filter on the thread pool while the component that owns it renders its placeholder from
    /// the same schema. A plain dictionary corrupts itself when those land together.
    /// </remarks>
    public sealed class FilterSchemaRegistry
    {
        private readonly ConcurrentDictionary<PaneKind, FilterSchema> _cache = new();

        #region Dependency Injection

        private readonly ISpellModelService _spellModelService;
        private readonly ITableCatalog _tableCatalog;
        private readonly Preferences _preferences;

        public FilterSchemaRegistry(
            ISpellModelService spellModelService,
            ITableCatalog tableCatalog,
            Preferences preferences)
        {
            _spellModelService = spellModelService;
            _tableCatalog      = tableCatalog;
            _preferences       = preferences;
        }

        #endregion

        public FilterSchema For(PaneDescriptor descriptor)
        {
            if (descriptor == null)
                return null;

            if (descriptor.Kind == PaneKind.GameTable)
                return BuildGameTable(descriptor);

            if (_cache.TryGetValue(descriptor.Kind, out FilterSchema cached))
                return cached;

            FilterSchema schema = descriptor.Kind switch
            {
                // The Effect Type spells pane lists Spell4 rows, so it carries the Spell4 schema verbatim -
                // narrowing a reverse lookup is the same job as narrowing the forward one.
                PaneKind.Spell4           => BuildSpell(),
                PaneKind.EffectTypeSpells => BuildSpell(),
                PaneKind.Effects          => BuildEffects(),
                PaneKind.Procs            => BuildProcs(),
                PaneKind.EffectTypes      => BuildEffectTypes(),
                PaneKind.Tables           => BuildTables(),
                _                         => null
            };

            if (schema != null)
                _cache[descriptor.Kind] = schema;

            return schema;
        }

        public FilterSchema<T> For<T>(PaneDescriptor descriptor) => For(descriptor) as FilterSchema<T>;

        /// <summary>Drop the cached schemas, so a reload's new tables are picked up.</summary>
        public void Invalidate() => _cache.Clear();

        /// <summary>
        /// The float tolerance every numeric constraint is built with.
        /// </summary>
        /// <remarks>
        /// Read at the moment a condition is compiled rather than when the schema is built, so changing the
        /// setting takes effect on the next apply instead of needing the schemas thrown away - a cached
        /// schema holds these factories for the life of the process.
        /// </remarks>
        private double Epsilon => _preferences?.FilterEpsilon ?? NumberTolerance.Default;

        // ------------------------------------------------------------------ spells

        private FilterSchema<ISpellModel> BuildSpell()
        {
            const string general    = "Base · General";
            const string mechanic   = "Target Mechanic";
            const string effects    = "Effects";
            const string housekeeping = "Housekeeping";
            const string timing       = "Timing";
            const string reach        = "Reach";
            const string counts       = "Counts";

            return new FilterSchema<ISpellModel>(
            [
                Text<ISpellModel>(FilterFields.Id, "Id", general, "7157", FilterOperator.StartsWith,
                    c => new SpellModelIdFilter { IdPrefix = FilterValue.Trimmed(c.Value) }),

                // The localised name and the tooltip are dictionary lookups, so scanning them is cheap
                Text<ISpellModel>(FilterFields.Name, "Name", general, "Arcane Missile", FilterOperator.Contains,
                    c => new SpellModelTextFilter { Text = SpellText.Name, Query = FilterValue.Trimmed(c.Value) }),

                Text<ISpellModel>(FilterFields.Tooltip, "Tooltip", general, "damage over time",
                    FilterOperator.Contains,
                    c => new SpellModelTextFilter { Text = SpellText.Tooltip, Query = FilterValue.Trimmed(c.Value) }),

                Choice<ISpellModel, CastMethod>(FilterFields.CastMethod, "Cast Method", general,
                    v => new SpellModelCastMethodFilter { CastMethod = v }),

                Choice<ISpellModel, DamageType>(FilterFields.School, "School", general,
                    v => new SpellModelSchoolFilter { School = v }),

                Choice<ISpellModel, Class>(FilterFields.Class, "Class", general,
                    v => new SpellModelClassFilter { Class = v }),

                Choice<ISpellModel, SpellTargetMechanicType>(FilterFields.TargetMechanic, "Type", mechanic,
                    v => new SpellModelTargetMechanicTypeFilter { TargetMechanicType = v }),

                Flags<ISpellModel, SpellTargetMechanicFlags>(FilterFields.TargetMechanicFlags, "Flags", mechanic,
                    (v, mode) => new SpellModelTargetMechanicFlagsFilter { Flags = v, Mode = mode }),

                Choice<ISpellModel, SpellEffectType>(FilterFields.EffectType, "Effect Type", effects,
                    v => new SpellModelEffectTypeFilter { SpellEffectType = v }),

                Flags<ISpellModel, SpellEffectTargetFlags>(FilterFields.EffectTargetFlags, "Target Flags", effects,
                    (v, mode) => new SpellModelEffectTargetFlagsFilter { Flags = v, Mode = mode }),

                // Both phrased positively; the form seeds them negated, so switching one on means "hide
                // deprecated" or "hide the placeholder spells".
                Toggle<ISpellModel>(FilterFields.Deprecated, "Deprecated", housekeeping,
                    _ => new SpellModelDeprecatedFilter(), seedNegated: true),

                Toggle<ISpellModel>(FilterFields.TestSpell, "Test", housekeeping,
                    _ => new SpellModelTestFilter(), seedNegated: true),

                Toggle<ISpellModel>(FilterFields.HasProcs, "Has procs", housekeeping,
                    _ => new SpellModelHasProcsFilter()),
                
                Range<ISpellModel, SpellModelCastTimeFilter>(FilterFields.CastTime, "Cast time", timing, "ms"),
                Range<ISpellModel, SpellModelDurationFilter>(FilterFields.Duration, "Duration", timing, "ms"),
                Range<ISpellModel, SpellModelCooldownFilter>(FilterFields.Cooldown, "Cooldown", timing, "ms"),
                Range<ISpellModel, SpellModelChannelTimeFilter>(FilterFields.ChannelTime, "Channel", timing, "ms"),
                Range<ISpellModel, SpellModelChannelPulseFilter>(FilterFields.ChannelPulse, "Channel pulse", timing, "ms"),

                Range<ISpellModel, SpellModelTierFilter>(FilterFields.Tier, "Tier", general, "1"),
                Range<ISpellModel, SpellModelAbilityChargesFilter>(FilterFields.AbilityCharges, "Charges", general, "1"),

                Range<ISpellModel, SpellModelTargetMinRangeFilter>(FilterFields.TargetMinRange, "Min range", reach, "0"),
                Range<ISpellModel, SpellModelTargetMaxRangeFilter>(FilterFields.TargetMaxRange, "Max range", reach, "30"),
                Range<ISpellModel, SpellModelTargetVerticalRangeFilter>(FilterFields.TargetVerticalRange, "Vertical", reach, "5"),
                Range<ISpellModel, SpellModelMissileSpeedFilter>(FilterFields.MissileSpeed, "Missile speed", reach, "20"),

                Range<ISpellModel, SpellModelEffectCountFilter>(FilterFields.EffectCount, "Effects", counts, "1"),
                Range<ISpellModel, SpellModelProcCountFilter>(FilterFields.ProcCount, "Procs", counts, "1"),
                Range<ISpellModel, SpellModelProcReferenceCountFilter>(FilterFields.ProcReferenceCount, "Referenced by", counts, "1")
            ],
            (term, exact) => new SpellModelTextSearchFilter { Query = term, Exact = exact },
            "Search description or name…",
            (term, exact) => new SpellModelIdSearchFilter { Query = term, Exact = exact },
            "Spell id…",
            SpellFlexSources());
        }

        /// <summary>
        /// The linked rows a spell can be filtered by column on: its own <c>Spell4</c> row, its
        /// <c>Spell4Base</c>, its effect rows, and the rows the base itself points at.
        /// </summary>
        /// <remarks>
        /// This is what the hand-written fields above cannot scale to. Those tables carry a hundred columns
        /// each and only a few dozen are worth a named field, so the rest arrive here - one card per row,
        /// one row per constraint, the column picked rather than declared.
        ///
        /// <c>CastGroup</c>, <c>PositionalAoe</c> and <c>AoeGroup</c> are absent because
        /// <c>SpellBaseModel.Initialise</c> leaves them unresolved - their tables are not loaded, so a card
        /// for them would offer columns that no spell could ever answer.
        /// </remarks>
        private IReadOnlyList<FilterFlexSource<ISpellModel>> SpellFlexSources()
        {
            return
            [
                Flex<ISpellModel, Spell4Entry>(FilterFields.SpellSource, "Spell4",
                    m => [m.Entry]),

                Flex<ISpellModel, Spell4BaseEntry>(FilterFields.BaseSource, "Base",
                    m => [m.SpellBaseModel?.Entry]),

                Flex<ISpellModel, Spell4EffectsEntry>(FilterFields.EffectsSource, "Effects",
                    m => m.Effects.Select(effect => (object)effect.Entry)),

                Flex<ISpellModel, Spell4HitResultsEntry>(FilterFields.HitResultSource,
                    "Hit results", m => [m.SpellBaseModel?.HitResult]),

                Flex<ISpellModel, Spell4TargetMechanicsEntry>(FilterFields.TargetMechanicsSource,
                    "Target mechanics", m => [m.SpellBaseModel?.TargetMechanics]),

                Flex<ISpellModel, Spell4TargetAngleEntry>(FilterFields.TargetAngleSource,
                    "Target angle", m => [m.SpellBaseModel?.TargetAngle]),

                Flex<ISpellModel, Spell4PrerequisitesEntry>(FilterFields.PrerequisitesSource,
                    "Prerequisites", m => [m.SpellBaseModel?.Prerequisites]),

                Flex<ISpellModel, Spell4ValidTargetsEntry>(FilterFields.ValidTargetsSource,
                    "Valid targets", m => [m.SpellBaseModel?.ValidTargets]),

                Flex<ISpellModel, Spell4BaseEntry>(FilterFields.PrerequisiteSpellSource,
                    "Prerequisite spell", m => [m.SpellBaseModel?.PrerequisiteSpell]),

                Flex<ISpellModel, Spell4SpellTypesEntry>(FilterFields.SpellTypeSource,
                    "Spell type", m => [m.SpellBaseModel?.SpellType])
            ];
        }

        // ------------------------------------------------------------------ effects

        private FilterSchema<ISpellEffectModel> BuildEffects()
        {
            const string effect     = "Effect";
            const string timing     = "Timing";
            const string internals  = "Row internals";
            const string parameters = "Parameters";

            return new FilterSchema<ISpellEffectModel>(
            [
                Choice<ISpellEffectModel, SpellEffectType>(FilterFields.EffectType, "Type", effect,
                    v => new SpellEffectTypeFilter { Type = v }),

                Mask<ISpellEffectModel>(FilterFields.EffectFlags, "Flags", effect,
                    (v, mode) => new SpellEffectFlagsFilter { Flags = v, Mode = mode }),

                Number<ISpellEffectModel>(FilterFields.EffectTick, "Tick", timing, "ms",
                    (v, atMost) => new SpellEffectTimingFilter
                    {
                        Timing = SpellEffectTiming.Tick, Value = v, AtMost = atMost
                    }),

                Number<ISpellEffectModel>(FilterFields.EffectDuration, "Duration", timing, "ms",
                    (v, atMost) => new SpellEffectTimingFilter
                    {
                        Timing = SpellEffectTiming.Duration, Value = v, AtMost = atMost
                    }),

                Number<ISpellEffectModel>(FilterFields.EffectDelay, "Delay", timing, "ms",
                    (v, atMost) => new SpellEffectTimingFilter
                    {
                        Timing = SpellEffectTiming.Delay, Value = v, AtMost = atMost
                    }),

                Threshold<ISpellEffectModel>(FilterFields.EffectThreat, "Threat", effect, "1.0",
                    (v, atMost) => new SpellEffectThreatFilter { Value = v, AtMost = atMost, Epsilon = Epsilon }),

                Choice<ISpellEffectModel, DamageType>(FilterFields.EffectDamage, "Damage", effect,
                    v => new SpellEffectDamageTypeFilter { DamageType = v }),

                Mask<ISpellEffectModel>(FilterFields.EffectPhase, "Phase flags", internals,
                    (v, mode) => new SpellEffectPhaseFlagsFilter { Flags = v, Mode = mode }),

                Number<ISpellEffectModel>(FilterFields.EffectOrder, "Order index", internals, "0",
                    (v, atMost) => new SpellEffectOrderIndexFilter { Value = v, AtMost = atMost }),

                Text<ISpellEffectModel>(FilterFields.EffectGroup, "Group list", internals, "0",
                    FilterOperator.Equals,
                    c => FilterValue.TryUInt(c.Value, out uint v)
                        ? new SpellEffectGroupListFilter { GroupListId = v }
                        : null),

                Choice<ISpellEffectModel, SpellEffectParameterType>(FilterFields.EffectParamType,
                    "Parameter", parameters,
                    v => new SpellEffectParameterFilter { ParameterType = v, Epsilon = Epsilon }),

                Text<ISpellEffectModel>(FilterFields.EffectEmmComparison, "EMM comparison", parameters, "0",
                    FilterOperator.Equals,
                    c => FilterValue.TryUInt(c.Value, out uint v)
                        ? new SpellEffectEmmFilter { Part = EmmPart.Comparison, Value = v }
                        : null),

                Text<ISpellEffectModel>(FilterFields.EffectEmmValue, "EMM value", parameters, "0",
                    FilterOperator.Equals,
                    c => FilterValue.TryUInt(c.Value, out uint v)
                        ? new SpellEffectEmmFilter { Part = EmmPart.Value, Value = v }
                        : null),

                Text<ISpellEffectModel>(FilterFields.EffectPrerequisite, "Prerequisite", parameters, "0",
                    FilterOperator.Equals,
                    c => FilterValue.TryUInt(c.Value, out uint v)
                        ? new SpellEffectPrerequisiteFilter
                        {
                            Slot = EffectPrerequisite.CasterApply, PrerequisiteId = v
                        }
                        : null),

                .. DataBitFields()
            ],
            (term, exact) => new SpellEffectNameSearchFilter { Query = term, Exact = exact },
            "Search effect type…",
            (term, exact) => new SpellEffectIdSearchFilter { Query = term, Exact = exact },
            "Type id…",
            [
                Flex<ISpellEffectModel, Spell4EffectsEntry>(FilterFields.EffectRowSource,
                    "Effect row", m => [m.Entry])
            ]);
        }

        /// <summary>
        /// One field per <c>DataBits</c> column. They carry whatever the effect type needs - a spell id, a
        /// percentage, a packed bitfield - so each offers an exact value and both mask readings rather than
        /// the schema guessing which the column is.
        /// </summary>
        private static IEnumerable<FilterFieldSchema<ISpellEffectModel>> DataBitFields()
        {
            const string data = "Data bits";

            for (int i = 0; i < SpellEffectDataBitsFilter.Columns; i++)
            {
                int index = i;

                yield return new FilterFieldSchema<ISpellEffectModel>
                {
                    Key              = FilterFields.EffectData(index),
                    Label            = $"Data{index:00}",
                    GroupTitle       = data,
                    Control          = FilterControlKind.Mask,
                    Placeholder      = "value or 0x mask",
                    AllowedOperators = [FilterOperator.Equals, FilterOperator.MaskAll, FilterOperator.MaskAny],
                    Factory          = c => FilterValue.TryUInt(c.Value, out uint value)
                        ? new SpellEffectDataBitsFilter { Index = index, Value = value, Match = Match(c.Operator) }
                        : null
                };
            }
        }

        private static DataBitsMatch Match(FilterOperator op) => op switch
        {
            FilterOperator.MaskAll => DataBitsMatch.MaskAll,
            FilterOperator.MaskAny => DataBitsMatch.MaskAny,
            _                      => DataBitsMatch.Equals
        };

        // ------------------------------------------------------------------ procs

        private FilterSchema<ISpellProcModel> BuildProcs()
        {
            const string proc = "Proc";

            return new FilterSchema<ISpellProcModel>(
            [
                // Numeric, not a dropdown: Core.Static.ProcType is an empty enum, so there is nothing to
                // offer as options and nothing to parse a name against.
                Text<ISpellProcModel>(FilterFields.ProcType, "Proc Type", proc, "16", FilterOperator.Equals,
                    c => FilterValue.TryUInt(c.Value, out uint v) ? new SpellProcTypeFilter { ProcType = v } : null),

                Text<ISpellProcModel>(FilterFields.ProcSpellId, "Spell Id", proc, "7161", FilterOperator.StartsWith,
                    c => new SpellProcSpellIdFilter { IdPrefix = FilterValue.Trimmed(c.Value) }),

                // Read into the filter rather than borrowed live. Every other factory here parses the
                // condition eagerly and keeps the result. Handing the predicate the engine's own key
                // collection instead would have it walked once per row on the thread pool - so a reload
                // clearing that dictionary would be a mutated dictionary read mid-lookup, and a filter
                // compiled before the reload would quietly answer a different question after it.
                Toggle<ISpellProcModel>(FilterFields.ProcReferenced, "Referenced", proc,
                    _ => new SpellProcReferencedFilter
                    {
                        ReferencedSpellIds = new HashSet<uint>(
                            _spellModelService?.SpellProcReferences.Keys ?? Enumerable.Empty<uint>())
                    })
            ],
            (term, exact) => new SpellProcTextSearchFilter
            {
                Query = term, Exact = exact, Description = DescriptionOf
            },
            "Search description…",
            (term, exact) => new SpellProcIdSearchFilter { Query = term, Exact = exact },
            "Spell id…",
            [
                // A proc is a Spell4Effects row read a different way, so the card offers that row's columns.
                Flex<ISpellProcModel, Spell4EffectsEntry>(FilterFields.ProcRowSource,
                    "Effect row", m => [m.Entry])
            ]);
        }

        private string DescriptionOf(uint spellId)
        {
            return _spellModelService != null
                && _spellModelService.SpellModels.TryGetValue(spellId, out ISpellModel model)
                    ? model.Description
                    : null;
        }

        // ------------------------------------------------------------------ effect types

        private static FilterSchema<EffectTypeUsage> BuildEffectTypes()
        {
            const string type = "Effect type";

            return new FilterSchema<EffectTypeUsage>(
            [
                Text<EffectTypeUsage>(FilterFields.TypeId, "Type Id starts", type, "17", FilterOperator.StartsWith,
                    c => new EffectTypeIdFilter { IdPrefix = FilterValue.Trimmed(c.Value) }),

                Number<EffectTypeUsage>(FilterFields.TypeSpells, "Spells", type, "10",
                    (v, atMost) => new EffectTypeSpellCountFilter { Value = v, AtMost = atMost })
            ],
            (term, exact) => new EffectTypeNameSearchFilter { Query = term, Exact = exact },
            "Search effect type…",
            (term, exact) => new EffectTypeIdSearchFilter { Query = term, Exact = exact },
            "Type id…");
        }

        // ------------------------------------------------------------------ table list

        private static FilterSchema<TableDescriptor> BuildTables()
        {
            const string archive = "Archive";

            return new FilterSchema<TableDescriptor>(
            [
                Text<TableDescriptor>(FilterFields.TableName, "Name starts", archive, "Spell4",
                    FilterOperator.StartsWith,
                    c => new TableNameFilter { Prefix = FilterValue.Trimmed(c.Value) }),

                Toggle<TableDescriptor>(FilterFields.TableLoaded, "Loaded", archive, _ => new TableLoadedFilter())
            ],
            // The only grid with no id box: a table is named, not numbered.
            (term, exact) => new TableSearchFilter { Query = term, Exact = exact },
            "Search table name…");
        }

        // ------------------------------------------------------------------ generic game table

        private FilterSchema<string[]> BuildGameTable(PaneDescriptor descriptor)
        {
            string row = $"{descriptor.TableName} · row";
            const string columns = "Columns";

            TableDescriptor table = _tableCatalog?.Get(descriptor.TableName);
            int flagsColumn = ColumnIndex(table, "Flags");

            return new FilterSchema<string[]>(
            [
                Text<string[]>(FilterFields.RowId, "Id starts", row, "1", FilterOperator.StartsWith,
                    c => new GameTableRowIdFilter { IdPrefix = FilterValue.Trimmed(c.Value) }),

                Text<string[]>(FilterFields.RowContains, "Contains", row, "any column value",
                    FilterOperator.Contains,
                    c => new GameTableContainsFilter { Query = FilterValue.Trimmed(c.Value) }),

                Mask<string[]>(FilterFields.RowFlags, "Flags", columns,
                    (v, mode) => flagsColumn < 0
                        // This table has no Flags column, so a mask on it is not a constraint the table can
                        // answer - silently emptying the grid instead would read as a broken table.
                        ? MatchAllFilter<string[]>.Instance
                        : new GameTableCellFilter { ColumnIndex = flagsColumn, Mask = v, Mode = mode }),

                Toggle<string[]>(FilterFields.RowNonZero, "Non-zero", columns, _ => new GameTableNonZeroFilter()),

                .. ColumnFields(table)
            ],
            (term, exact) => new GameTableContainsFilter { Query = term, Exact = exact },
            "Search any column…",
            (term, exact) => new GameTableColumnFilter { ColumnIndex = 0, Query = term, Exact = exact },
            "Row id…");
        }

        /// <summary>
        /// One field per column of the table, so any of the tables becomes queryable by column rather than only
        /// by "some cell somewhere contains this".
        /// </summary>
        /// <remarks>
        /// The field list comes free from the descriptor and the values from its compiled accessor, so this
        /// costs nothing per table beyond resolving each column to its index once.
        /// </remarks>
        private static IEnumerable<FilterFieldSchema<string[]>> ColumnFields(TableDescriptor table)
        {
            if (table == null)
                yield break;

            const string card = "Columns · by name";

            for (int i = 0; i < table.Columns.Count; i++)
            {
                int index = i;
                string column = table.Columns[index];

                yield return new FilterFieldSchema<string[]>
                {
                    Key              = FilterFields.Column(column),
                    Label            = column,
                    GroupTitle       = card,
                    Control          = FilterControlKind.Text,
                    Placeholder      = "value",
                    AllowedOperators = [FilterOperator.Contains, FilterOperator.Equals],
                    Factory          = c => new GameTableColumnFilter
                    {
                        ColumnIndex = index,
                        Query       = FilterValue.Trimmed(c.Value),
                        Exact       = c.Operator == FilterOperator.Equals
                    }
                };
            }
        }

        private static int ColumnIndex(TableDescriptor table, string columnName)
        {
            if (table == null)
                return -1;

            for (int i = 0; i < table.Columns.Count; i++)
                if (table.Columns[i].Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return i;

            return -1;
        }

        // ------------------------------------------------------------------ flex sources

        /// <summary>
        /// A card offering every column of <typeparamref name="TEntry"/>, reached from the element by
        /// <paramref name="rows"/>.
        /// </summary>
        /// <remarks>
        /// The columns come from <see cref="ITableCatalog.Columns"/>, which compiles one accessor per column
        /// per entry type - so a card costs one reflection pass over a type the catalog has usually walked
        /// already, and evaluating a condition costs a delegate call.
        ///
        /// Which operators a column offers is decided here, from the column's type: a number is asked for a threshold,
        /// anything else for a substring. A column with no accessible columns at all - a type the catalog cannot
        /// walk - yields an empty card rather than a broken one.
        /// </remarks>
        private FilterFlexSource<T> Flex<T, TEntry>(
            string key, string name, Func<T, IEnumerable<object>> rows)
        {
            IReadOnlyList<GameTableColumn> columns = _tableCatalog?.Columns(typeof(TEntry)) ?? [];

            // The card is titled from the row's name rather than the other way round, so a column can
            // qualify its own label with that name instead of parsing it back out of a card heading.
            string title = $"{name} · any column";

            return new FilterFlexSource<T>(key, title, rows,
                [.. columns.Select(column => Column(key, name, title, column))]);
        }

        private FilterColumnFieldSchema Column(
            string source, string name, string title, GameTableColumn column)
        {
            return new FilterColumnFieldSchema
            {
                Key              = FilterFields.Flex(source, column.Name),
                Label            = column.Name,
                GroupTitle       = title,
                Source           = source,
                SourceName       = name,
                Control          = FilterControlKind.Text,
                Placeholder      = column.IsNumeric ? "value" : "text",
                AllowedOperators = Operators(column),
                RowFactory       = column.IsNumeric ? Number(column) : Text(column)
            };
        }

        /// <summary>
        /// What a column can be asked. A number gets the comparisons; everything else - a string, an array,
        /// a bool, an enum - gets the two text readings, since its rendered form is all it shares.
        /// </summary>
        /// <remarks>
        /// The two mask readings ride along on whole-number columns because these tables pack bitfields into
        /// ordinary integer columns as readily as they hold counts - <c>Flags</c> and <c>DataBits00</c> being
        /// the obvious cases - and the curated <c>SpellEffectDataBitsFilter</c> already offers exactly this
        /// pair. A float column gets no mask: there are no bits there to mean anything.
        /// </remarks>
        private static IReadOnlyList<FilterOperator> Operators(GameTableColumn column)
        {
            if (!column.IsNumeric)
                return [FilterOperator.Contains, FilterOperator.Equals];

            return Integral(column.Type)
                ? [FilterOperator.Equals, FilterOperator.AtLeast, FilterOperator.AtMost,
                   FilterOperator.MaskAll, FilterOperator.MaskAny]
                : [FilterOperator.Equals, FilterOperator.AtLeast, FilterOperator.AtMost];
        }

        private static bool Integral(Type type) => Type.GetTypeCode(type)
            is TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16
            or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64;

        private Func<FilterCondition, IModelFilter<object>> Number(GameTableColumn column)
        {
            return c => TryValue(c.Value, out double value)
                ? new ColumnNumberFilter
                {
                    Read    = column.Number,
                    Value   = value,
                    Match   = Number(c.Operator),
                    Epsilon = Epsilon
                }
                : null;
        }

        /// <summary>
        /// Read what was typed into a numeric column's box.
        /// </summary>
        /// <remarks>
        /// A whole number first, which is also what accepts <c>0x</c> hex - these columns pack bitfields as
        /// readily as they hold counts, and the mask operators are unusable without it. Anything else falls
        /// through to the ordinary parse, which is what a threshold on a float column needs. Trying both
        /// rather than branching on the operator means a hex value keeps its meaning when the row is
        /// switched from ALL to =, instead of silently becoming unparseable.
        /// </remarks>
        private static bool TryValue(string text, out double value)
        {
            if (FilterValue.TryUInt(text, out uint whole))
            {
                value = whole;
                return true;
            }

            return FilterValue.TryNumber(text, out value);
        }

        private static NumberMatch Number(FilterOperator op) => op switch
        {
            FilterOperator.AtLeast => NumberMatch.AtLeast,
            FilterOperator.AtMost  => NumberMatch.AtMost,
            FilterOperator.MaskAll => NumberMatch.MaskAll,
            FilterOperator.MaskAny => NumberMatch.MaskAny,
            _                      => NumberMatch.Equals
        };

        private static Func<FilterCondition, IModelFilter<object>> Text(GameTableColumn column)
        {
            return c => new ColumnTextFilter
            {
                Read  = column.Text,
                Query = FilterValue.Trimmed(c.Value),
                Exact = c.Operator == FilterOperator.Equals
            };
        }

        // ------------------------------------------------------------------ field constructors

        private static FilterFieldSchema<T> Text<T>(
            string key, string label, string card, string placeholder, FilterOperator op,
            Func<FilterCondition, IModelFilter<T>> factory)
        {
            return new FilterFieldSchema<T>
            {
                Key              = key,
                Label            = label,
                GroupTitle       = card,
                Control          = FilterControlKind.Text,
                Placeholder      = placeholder,
                AllowedOperators = [op],
                Factory          = factory
            };
        }

        private static FilterFieldSchema<T> Choice<T, TEnum>(
            string key, string label, string card, Func<TEnum, IModelFilter<T>> factory)
            where TEnum : struct, Enum
        {
            return new FilterFieldSchema<T>
            {
                Key              = key,
                Label            = label,
                GroupTitle       = card,
                Control          = FilterControlKind.Choice,
                // Option lists come from the engine's enums, never from a hard-coded array.
                Options          = [FilterFieldSchema.Any, .. Enum.GetNames<TEnum>()],
                AllowedOperators = [FilterOperator.Equals],
                Factory          = c => FilterValue.TryEnum(c.Value, out TEnum value) ? factory(value) : null
            };
        }

        /// <summary>
        /// A bitmask whose bits have names, so the form can offer a picker. The stored value is still the
        /// assembled number, so a picked mask and a typed one persist and compile identically.
        /// </summary>
        private static FilterFieldSchema<T> Flags<T, TEnum>(
            string key, string label, string card, Func<uint, MaskMode, IModelFilter<T>> factory)
            where TEnum : struct, Enum
        {
            return new FilterFieldSchema<T>
            {
                Key              = key,
                Label            = label,
                GroupTitle       = card,
                Control          = FilterControlKind.Flags,
                Placeholder      = "bitmask",
                Bits             = EnumText.Bits<TEnum>(),
                AllowedOperators = [FilterOperator.MaskAll, FilterOperator.MaskAny],
                Factory          = c => FilterValue.TryUInt(c.Value, out uint value)
                    ? factory(value, FilterValue.MaskMode(c))
                    : null
            };
        }

        private static FilterFieldSchema<T> Mask<T>(
            string key, string label, string card, Func<uint, MaskMode, IModelFilter<T>> factory)
        {
            return new FilterFieldSchema<T>
            {
                Key              = key,
                Label            = label,
                GroupTitle       = card,
                Control          = FilterControlKind.Mask,
                Placeholder      = "bitmask",
                AllowedOperators = [FilterOperator.MaskAll, FilterOperator.MaskAny],
                Factory          = c => FilterValue.TryUInt(c.Value, out uint value)
                    ? factory(value, FilterValue.MaskMode(c))
                    : null
            };
        }

        private static FilterFieldSchema<T> Number<T>(
            string key, string label, string card, string placeholder,
            Func<uint, bool, IModelFilter<T>> factory)
        {
            return new FilterFieldSchema<T>
            {
                Key              = key,
                Label            = label,
                GroupTitle       = card,
                Control          = FilterControlKind.Text,
                Placeholder      = placeholder,
                AllowedOperators = [FilterOperator.AtLeast, FilterOperator.AtMost],
                Factory          = c => FilterValue.TryUInt(c.Value, out uint value)
                    ? factory(value, c.Operator == FilterOperator.AtMost)
                    : null
            };
        }

        /// <summary>
        /// A threshold on a decimal-capable value. Distinct from <see cref="Number{T}"/>, which is for the
        /// whole-number columns and keeps their <c>uint</c> parse.
        /// </summary>
        private FilterFieldSchema<T> Threshold<T>(
            string key, string label, string card, string placeholder,
            Func<double, bool, IModelFilter<T>> factory)
        {
            return new FilterFieldSchema<T>
            {
                Key              = key,
                Label            = label,
                GroupTitle       = card,
                Control          = FilterControlKind.Text,
                Placeholder      = placeholder,
                AllowedOperators = [FilterOperator.AtLeast, FilterOperator.AtMost],
                Factory          = c => FilterValue.TryNumber(c.Value, out double value)
                    ? factory(value, c.Operator == FilterOperator.AtMost)
                    : null
            };
        }

        /// <summary>
        /// A threshold backed by one of the named <see cref="SpellModelRangeFilter"/> subclasses - the whole
        /// of a numeric field's wiring is its type, its key and its label.
        /// </summary>
        private FilterFieldSchema<T> Range<T, TFilter>(
            string key, string label, string card, string placeholder)
            where TFilter : SpellModelRangeFilter, IModelFilter<T>, new()
        {
            return Threshold<T>(key, label, card, placeholder,
                (v, atMost) => new TFilter { Value = v, AtMost = atMost, Epsilon = Epsilon });
        }

        /// <summary>
        /// A bare on/off constraint. <paramref name="seedNegated"/> marks the ones whose useful reading is
        /// the negative one, so the form starts them denied rather than asserted.
        /// </summary>
        private static FilterFieldSchema<T> Toggle<T>(
            string key, string label, string card, Func<FilterCondition, IModelFilter<T>> factory,
            bool seedNegated = false)
        {
            return new FilterFieldSchema<T>
            {
                Key              = key,
                Label            = label,
                GroupTitle       = card,
                Control          = FilterControlKind.Toggle,
                AllowedOperators = [FilterOperator.IsSet],
                SeedNegated      = seedNegated,
                Factory          = factory
            };
        }
    }
}
