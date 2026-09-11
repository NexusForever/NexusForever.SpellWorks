namespace NexusForever.SpellWorks.Services
{
    public enum PaneKind
    {
        Spell4,
        Detail,
        Effects,
        Procs,
        EffectTypes,
        EffectTypeSpells,
        Tables,
        GameTable,
        Setup
    }

    /// <summary>
    /// One unit of layout. The same descriptor renders identically in a tab, a split panel or a pop-out window.
    /// </summary>
    /// <remarks>
    /// <see cref="Id"/> is the stable key used for tabs, per-pane state,
    /// pinning and layout persistence - <c>"spells"</c>, <c>"detail"</c> … or <c>"tbl:Spell4Effects"</c>.
    /// </remarks>
    public sealed record PaneDescriptor(
        string Id,
        PaneKind Kind,
        string Title,
        string Label,
        string Icon,
        string Meta,
        string TableName = null)
    {
        public const string GameTablePrefix = "tbl:";

        /// <summary>
        /// Ids of the extra Spell detail panes, opened when the primary one is locked. <c>"detail"</c> stays
        /// the primary; the rest are <c>"detail:2"</c>, <c>"detail:3"</c>, and so on.
        /// </summary>
        public const string DetailPrefix = "detail:";

        /// <summary>
        /// Ids of the extra Effect Type spell panes, spawned the same way the detail ones are: <c>"effecttype"</c>
        /// stays the primary, the rest are <c>"effecttype:2"</c>, <c>"effecttype:3"</c>, and so on.
        /// </summary>
        public const string EffectTypePrefix = "effecttype:";

        public bool CanPin => Kind == PaneKind.GameTable;

        public bool IsTableKind => Kind is PaneKind.Spell4 or PaneKind.Effects or PaneKind.Procs
            or PaneKind.EffectTypes or PaneKind.EffectTypeSpells or PaneKind.Tables or PaneKind.GameTable;

        public static string GameTableId(string tableName) => GameTablePrefix + tableName;

        public static string DetailId(int index) => index <= 1 ? "detail" : DetailPrefix + index;

        public static bool IsDetailId(string id) =>
            id == "detail" || (id != null && id.StartsWith(DetailPrefix, StringComparison.Ordinal));

        public static string EffectTypeSpellsId(int index) => index <= 1 ? "effecttype" : EffectTypePrefix + index;

        public static bool IsEffectTypeSpellsId(string id) =>
            id == "effecttype" || (id != null && id.StartsWith(EffectTypePrefix, StringComparison.Ordinal));

        public static readonly PaneDescriptor Spell4 =
            new("spells", PaneKind.Spell4, "Spell4 browser", "Spell4", "ph ph-list-magnifying-glass", "Spell4.tbl");

        public static readonly PaneDescriptor Detail =
            new("detail", PaneKind.Detail, "Spell detail", "Detail", "ph ph-lightning", "Spell4 + Spell4Base");

        public static readonly PaneDescriptor Effects =
            new("effects", PaneKind.Effects, "Spell4Effects", "Effects", "ph ph-flow-arrow", "Spell4Effects.tbl");

        public static readonly PaneDescriptor Procs =
            new("procs", PaneKind.Procs, "Proc references", "Procs", "ph ph-repeat", "Spell4Effects · proxy");

        public static readonly PaneDescriptor EffectTypes =
            new("effecttypes", PaneKind.EffectTypes, "Effect Types", "Types", "ph ph-tree-structure", "Spell4Effects · by type");

        public static readonly PaneDescriptor EffectTypeSpells =
            new("effecttype", PaneKind.EffectTypeSpells, "Effect Type spells", "Type spells", "ph ph-crosshair", "Spell4 · by effect type");

        public static readonly PaneDescriptor Tables =
            new("tables", PaneKind.Tables, "Game tables", "Tables", "ph ph-table", "archive index");

        public static readonly PaneDescriptor Setup =
            new("setup", PaneKind.Setup, "Setup", "Setup", "ph ph-gear-six", "Configuration.json");

        /// <summary>
        /// The rail views, in rail order. Setup is pinned to the bottom separately.
        /// </summary>
        public static readonly IReadOnlyList<PaneDescriptor> RailViews =
            [Spell4, Detail, Effects, Procs, EffectTypes, EffectTypeSpells, Tables];

        public static readonly IReadOnlyList<PaneDescriptor> AllFixed =
            [Spell4, Detail, Effects, Procs, EffectTypes, EffectTypeSpells, Tables, Setup];
    }
}
