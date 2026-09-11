using CommunityToolkit.Mvvm.Messaging;
using NexusForever.Game.Static.Spell;
using NexusForever.SpellWorks.Core.Messages;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.SpellWorks.Core.Services;

namespace NexusForever.SpellWorks.Services
{
    public enum LayoutMode
    {
        Tabbed,
        SplitPanes
    }

    public sealed class Preferences
    {
        public string Locale { get; set; } = "enUS";
        public bool LoadOnStart { get; set; } = true;
        public bool RestoreWindows { get; set; } = true;

        /// <summary>
        /// Whether a saved filter comes back on the next start.
        /// </summary>
        /// <remarks>
        /// Gates the load, never the save: switched off, the app starts clean but <c>Workspace.json</c>
        /// keeps every query, and switching it back on returns them. A preference that quietly deleted
        /// work would be the one setting nobody could risk trying.
        /// </remarks>
        public bool RestoreFilters { get; set; } = true;

        /// <summary>As <see cref="RestoreFilters"/>, for the promoted flex columns.</summary>
        public bool RestorePromoted { get; set; } = true;
        public bool MonospaceIds { get; set; } = true;
        public bool RailLabels { get; set; } = true;

        /// <summary>
        /// How close a float or double column has to be to a typed value to count as equal to it.
        /// </summary>
        /// <remarks>
        /// Relative rather than absolute - see <see cref="NumberTolerance"/>, which is where every numeric
        /// filter reads it through. Exposed in Setup because the right answer depends on the column being
        /// asked: the default absorbs the single-to-double conversion and nothing more, while data that is
        /// only nominally precise wants a looser one and a demand for the bit pattern itself wants zero.
        /// </remarks>
        public double FilterEpsilon { get; set; } = NumberTolerance.Default;
    }

    public sealed record PopoutEntry(string Key, string ViewId);

    /// <summary>
    /// Everything two windows must agree on. Registered as a singleton - a BlazorWebView's scope is
    /// per-window, so scoped state would silently give each pop-out its own copy.
    /// </summary>
    public sealed class WorkspaceState
    {
        /// <summary>
        /// Raised on any mutation. Handlers run inline on whoever mutated the state, so every subscriber
        /// marshals through <c>InvokeAsync</c> before touching its render tree.
        /// </summary>
        /// <remarks>
        /// That caller is the one UI thread in every current path: a WPF <c>BlazorWebView</c>
        /// renders on the WPF dispatcher, and every pop-out window is built on it too, so the windows
        /// share a thread rather than having one each. The marshalling is kept all the same - it is what
        /// makes the rule hold for a handler reached from somewhere else, which is exactly what the engine
        /// load is: its continuation only lands back here because it was started from the dispatcher.
        /// </remarks>
        public event Action Changed;

        public List<string> Open { get; } = ["spells", "detail"];
        public string Active { get; private set; } = "spells";

        public LayoutMode Layout { get; private set; } = LayoutMode.Tabbed;
        public Dictionary<string, double> Flexes { get; } = [];

        public List<string> Pinned { get; } = [];

        /// <summary>
        /// User-resized grid columns, keyed by view id and then by column name. Only overridden columns
        /// appear; everything else falls back to the width <see cref="RowSource"/> projected.
        /// </summary>
        public Dictionary<string, Dictionary<string, int>> ColumnWidths { get; } = [];

        public uint SelectedSpellId { get; private set; }
        public List<uint> History { get; } = [];

        /// <summary>
        /// The effect type the Effect Type spell panes follow, the analogue of <see cref="SelectedSpellId"/>.
        /// Null until a row in the Effect Types browser is picked, which is what the panes' empty state says.
        /// </summary>
        public SpellEffectType? SelectedEffectType { get; private set; }

        public Preferences Preferences { get; } = new();

        public List<PopoutEntry> Popouts { get; } = [];

        /// <summary>Draft patch path in the Setup view, shared across windows.</summary>
        public string PathDraft { get; set; }

        /// <summary>
        /// Set when <c>Configuration.json</c> could not be read at start-up and defaults were used instead.
        /// Surfaced in Setup, because that is where the path it holds gets corrected.
        /// </summary>
        public string ConfigurationError { get; set; }

        public bool IsBrowsing { get; set; }

        private readonly Dictionary<string, PaneState> _paneStates = [];
        private readonly Dictionary<string, PaneDescriptor> _gameTablePanes = [];

        #region Dependency Injection

        private readonly IMessenger _messenger;
        private readonly ISpellModelService _spellModelService;
        private readonly ITableCatalog _tableCatalog;

        public WorkspaceState(
            IMessenger messenger,
            ISpellModelService spellModelService,
            ITableCatalog tableCatalog)
        {
            _messenger         = messenger;
            _spellModelService = spellModelService;
            _tableCatalog      = tableCatalog;
        }

        #endregion

        public void Notify() => Changed?.Invoke();

        // ------------------------------------------------------------------ descriptors

        /// <summary>
        /// Resolve a pane id to its descriptor, building one on demand for <c>tbl:</c> ids.
        /// </summary>
        public PaneDescriptor Describe(string id)
        {
            if (id == null)
                return null;

            PaneDescriptor known = PaneDescriptor.AllFixed.FirstOrDefault(v => v.Id == id);
            if (known != null)
                return known;

            if (id.StartsWith(PaneDescriptor.EffectTypePrefix, StringComparison.Ordinal))
            {
                string suffix = id[PaneDescriptor.EffectTypePrefix.Length..];
                return PaneDescriptor.EffectTypeSpells with
                {
                    Id    = id,
                    Title = $"Effect Type spells {suffix}",
                    Label = $"Type spells {suffix}"
                };
            }

            if (id.StartsWith(PaneDescriptor.DetailPrefix, StringComparison.Ordinal))
            {
                string suffix = id[PaneDescriptor.DetailPrefix.Length..];
                return PaneDescriptor.Detail with
                {
                    Id    = id,
                    Title = $"Spell detail {suffix}",
                    Label = $"Detail {suffix}"
                };
            }

            if (!id.StartsWith(PaneDescriptor.GameTablePrefix, StringComparison.Ordinal))
                return PaneDescriptor.Spell4;

            if (_gameTablePanes.TryGetValue(id, out PaneDescriptor cached))
                return cached;

            string name = id[PaneDescriptor.GameTablePrefix.Length..];
            TableDescriptor table = _tableCatalog.Get(name);

            string meta = table != null
                ? $"{table.RowCount:n0} rows · {table.Columns.Count} columns"
                : "generic table";

            var descriptor = new PaneDescriptor(id, PaneKind.GameTable, name + ".tbl", name, "ph ph-table", meta, name);
            _gameTablePanes[id] = descriptor;
            return descriptor;
        }

        /// <summary>Drop cached game-table descriptors so their row counts pick up a reload.</summary>
        public void InvalidateDescriptors() => _gameTablePanes.Clear();

        /// <summary>
        /// Every pane state that has been asked for. The save path reads this to persist filters, and prunes
        /// the scopes that are no longer reachable.
        /// </summary>
        public IReadOnlyDictionary<string, PaneState> PaneStates => _paneStates;

        public PaneState PaneStateFor(string scope)
        {
            if (!_paneStates.TryGetValue(scope, out PaneState state))
                _paneStates[scope] = state = new PaneState();

            return state;
        }

        // ------------------------------------------------------------------ navigation

        public void SelectView(string id)
        {
            if (!Open.Contains(id))
                Open.Add(id);

            Active = id;
            Notify();
        }

        public void ActivateOnly(string id)
        {
            Active = id;
            Notify();
        }

        public void CloseView(string id)
        {
            Open.Remove(id);
            Retire(id);

            if (Active == id)
                Active = Open.FirstOrDefault();

            Notify();
        }

        /// <summary>
        /// Drop what a closed pane must not keep. A lock is the important one: it is unreachable once the
        /// pane carrying it is gone, and <see cref="FreeDetailScope"/> would keep routing spells around it
        /// forever. A spawned <c>detail:N</c> or <c>effecttype:N</c> hands its id back to the pool, so a pane
        /// that reuses that id is a different pane and starts clean; the fixed views keep their filters, since
        /// reopening one is reopening the same view.
        /// </summary>
        private void Retire(string scope)
        {
            if (!_paneStates.TryGetValue(scope, out PaneState state))
                return;

            bool spawned = (PaneDescriptor.IsDetailId(scope) && scope != PaneDescriptor.Detail.Id)
                || (PaneDescriptor.IsEffectTypeSpellsId(scope) && scope != PaneDescriptor.EffectTypeSpells.Id);

            if (spawned)
            {
                _paneStates.Remove(scope);
            }
            else
            {
                state.LockedSpellId    = null;
                state.LockedEffectType = null;
            }
        }

        /// <summary>
        /// Move an open view to <paramref name="toIndex"/>, expressed as an insertion slot in the strip as it
        /// looks before the move - so a drop marker sitting between tabs 2 and 3 is index 3, whichever side
        /// the dragged tab came from.
        /// </summary>
        public void MoveView(string id, int toIndex)
        {
            int from = Open.IndexOf(id);
            if (from < 0)
                return;

            // Removing the tab first shifts every slot after it down by one.
            if (toIndex > from)
                toIndex--;

            toIndex = Math.Clamp(toIndex, 0, Open.Count - 1);
            if (toIndex == from)
                return;

            Open.RemoveAt(from);
            Open.Insert(toIndex, id);

            Notify();
        }

        public void SetLayout(LayoutMode layout)
        {
            Layout = layout;
            Notify();
        }

        public void SetFlexes(IReadOnlyList<string> ids, IReadOnlyList<double> flexes)
        {
            for (int i = 0; i < ids.Count && i < flexes.Count; i++)
                Flexes[ids[i]] = flexes[i];

            Notify();
        }

        public double FlexOf(string id) => Flexes.TryGetValue(id, out double flex) ? flex : 1d;

        // ------------------------------------------------------------------ grid columns

        public const int MinColumnWidth = 48;
        public const int MaxColumnWidth = 900;

        /// <summary>The width <paramref name="column"/> should render at in <paramref name="viewId"/>.</summary>
        public int ColumnWidth(string viewId, GridColumn column)
        {
            return ColumnWidths.TryGetValue(viewId, out Dictionary<string, int> widths)
                && widths.TryGetValue(column.Name, out int width)
                    ? width
                    : column.Width;
        }

        public void SetColumnWidth(string viewId, string column, int width)
        {
            if (!ColumnWidths.TryGetValue(viewId, out Dictionary<string, int> widths))
                ColumnWidths[viewId] = widths = [];

            widths[column] = Math.Clamp(width, MinColumnWidth, MaxColumnWidth);
            Notify();
        }

        /// <summary>Drop one column's override so it returns to its projected width.</summary>
        public void ResetColumnWidth(string viewId, string column)
        {
            if (!ColumnWidths.TryGetValue(viewId, out Dictionary<string, int> widths) || !widths.Remove(column))
                return;

            if (widths.Count == 0)
                ColumnWidths.Remove(viewId);

            Notify();
        }

        public void Pin(string id)
        {
            if (!Pinned.Contains(id))
                Pinned.Add(id);

            Notify();
        }

        public void Unpin(string id)
        {
            Pinned.Remove(id);
            Notify();
        }

        // ------------------------------------------------------------------ selection

        public ISpellModel Spell(uint id)
        {
            return _spellModelService.SpellModels.TryGetValue(id, out ISpellModel model) ? model : null;
        }

        /// <summary>The spell a pane is showing: its lock if it has one, otherwise the shared selection.</summary>
        public uint SelectedIn(string scope)
        {
            return PaneStateFor(scope).LockedSpellId ?? SelectedSpellId;
        }

        public void Select(uint id)
        {
            if (SelectedSpellId == id)
                return;

            if (SelectedSpellId != 0)
            {
                History.Add(SelectedSpellId);
                if (History.Count > 25)
                    History.RemoveAt(0);
            }

            SelectedSpellId = id;
            Notify();

            _messenger.Send(new SpellSelectedMessage { Spell = Spell(id) });
        }

        public void Back()
        {
            if (History.Count == 0)
                return;

            uint previous = History[^1];
            History.RemoveAt(History.Count - 1);
            SelectedSpellId = previous;

            Notify();
            _messenger.Send(new SpellSelectedMessage { Spell = Spell(previous) });
        }

        public void OpenDetail(uint id, DetailSubTab? subTab = null)
        {
            Select(id);
            ShowIn(FreeDetailScope(id), subTab);
        }

        /// <summary>
        /// The detail pane a spell should be shown in. A locked pane keeps the spell it was locked to - that
        /// is what the lock is for - so a spell that cannot go there takes the next free pane, and a brand
        /// new one when every open pane is spoken for. Without this the navigation is simply swallowed: the
        /// user is switched to a locked pane that still shows the spell from before.
        /// </summary>
        public string FreeDetailScope(uint spellId)
        {
            List<string> candidates = [.. Open.Where(PaneDescriptor.IsDetailId)];

            if (!candidates.Contains(PaneDescriptor.Detail.Id))
                candidates.Add(PaneDescriptor.Detail.Id);

            foreach (string scope in candidates)
                if (PaneStateFor(scope).LockedSpellId is not { } locked || locked == spellId)
                    return scope;

            for (int index = 2; ; index++)
            {
                string scope = PaneDescriptor.DetailId(index);
                if (!Open.Contains(scope))
                    return scope;
            }
        }

        private void ShowIn(string scope, DetailSubTab? subTab)
        {
            PaneState state = PaneStateFor(scope);
            if (subTab.HasValue)
                state.SubTab = subTab.Value;

            state.Mode = PaneMode.Rows;
            SelectView(scope);
        }

        public void ToggleLock(string scope)
        {
            PaneState state = PaneStateFor(scope);
            state.LockedSpellId = state.LockedSpellId.HasValue ? null : SelectedSpellId;
            Notify();
        }

        /// <summary>
        /// Follow a cross-reference out of the pane at <paramref name="scope"/>. Normally the pane simply
        /// moves to the new spell; when it is locked it cannot, so the spell is given a pane that can show
        /// it - on the same sub-tab, since that is where the link was.
        /// </summary>
        public void FollowHyperlink(ISpellModel spell, string scope)
        {
            if (spell == null)
                return;

            Select(spell.Id);
            _messenger.Send(new SpellHyperlinkClicked { Spell = spell });

            PaneState from = PaneStateFor(scope);
            if (from.LockedSpellId is { } locked && locked != spell.Id)
                ShowIn(FreeDetailScope(spell.Id), from.SubTab);
        }

        // ------------------------------------------------------------------ effect types

        /// <summary>The effect type a pane is showing: its lock if it has one, otherwise the shared selection.</summary>
        public SpellEffectType? EffectTypeIn(string scope)
        {
            return PaneStateFor(scope).LockedEffectType ?? SelectedEffectType;
        }

        public void SelectEffectType(SpellEffectType type)
        {
            if (SelectedEffectType == type)
                return;

            SelectedEffectType = type;
            Notify();
        }

        public void OpenEffectTypeSpells(SpellEffectType type)
        {
            SelectEffectType(type);

            string scope = FreeEffectTypeScope(type);
            PaneStateFor(scope).Mode = PaneMode.Rows;

            SelectView(scope);
        }

        /// <summary>
        /// The Effect Type spells pane a type should be shown in - the same reasoning as
        /// <see cref="FreeDetailScope"/>: a locked pane keeps the type it was locked to, so another type takes
        /// the next free pane and a brand new one when every open pane is spoken for. Without this the
        /// navigation is simply swallowed.
        /// </summary>
        public string FreeEffectTypeScope(SpellEffectType type)
        {
            List<string> candidates = [.. Open.Where(PaneDescriptor.IsEffectTypeSpellsId)];

            if (!candidates.Contains(PaneDescriptor.EffectTypeSpells.Id))
                candidates.Add(PaneDescriptor.EffectTypeSpells.Id);

            foreach (string scope in candidates)
                if (PaneStateFor(scope).LockedEffectType is not { } locked || locked == type)
                    return scope;

            for (int index = 2; ; index++)
            {
                string scope = PaneDescriptor.EffectTypeSpellsId(index);
                if (!Open.Contains(scope))
                    return scope;
            }
        }

        public void ToggleEffectTypeLock(string scope)
        {
            PaneState state = PaneStateFor(scope);
            state.LockedEffectType = state.LockedEffectType.HasValue ? null : SelectedEffectType;
            Notify();
        }

        // ------------------------------------------------------------------ pop-outs

        public void RegisterPopout(string key, string viewId)
        {
            Popouts.Add(new PopoutEntry(key, viewId));

            Open.Remove(viewId);
            if (Active == viewId)
                Active = Open.FirstOrDefault();

            Notify();
        }

        public void UnregisterPopout(string key, bool dockBack)
        {
            PopoutEntry entry = Popouts.FirstOrDefault(p => p.Key == key);
            if (entry == null)
                return;

            Popouts.Remove(entry);

            // The key is never reused, so its pane state is dead either way - docking back returns the view
            // to its own scope, not to this one.
            _paneStates.Remove(key);

            if (dockBack)
            {
                if (!Open.Contains(entry.ViewId))
                    Open.Add(entry.ViewId);

                Active = entry.ViewId;
            }

            Notify();
        }
    }
}
