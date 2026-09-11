using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.SpellWorks.Services.Filtering;

namespace NexusForever.SpellWorks.Services
{
    /// <summary>
    /// Persists the workspace (open views, layout, pinned tables, preferences) beside
    /// <c>Configuration.json</c>, and writes the patch path back into it.
    /// </summary>
    public sealed class WorkspaceStore
    {
        private sealed class Snapshot
        {
            public List<string> Open { get; set; }
            public string Active { get; set; }
            public string Layout { get; set; }
            public Dictionary<string, double> Flexes { get; set; }
            public List<string> Pinned { get; set; }
            public Preferences Preferences { get; set; }
            public List<string> Popouts { get; set; }
            public Dictionary<string, Dictionary<string, int>> ColumnWidths { get; set; }

            /// <summary>Per-pane filters, keyed by pane scope - the same key <c>PaneStateFor</c> uses.</summary>
            /// <remarks>Omitted entirely when nothing is filtered, so an unfiltered workspace reads as one.</remarks>
            [System.Text.Json.Serialization.JsonIgnore(
                Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public Dictionary<string, FilterQueryDto> Filters { get; set; }

            /// <summary>Per-pane promoted flex columns, keyed by pane scope as the filters are.</summary>
            /// <remarks>
            /// Its own section rather than a member of the filter DTO, because the two are governed by
            /// their own preferences: a user can keep their promoted fields while starting clean.
            /// </remarks>
            [System.Text.Json.Serialization.JsonIgnore(
                Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public Dictionary<string, List<string>> Promoted { get; set; }
        }

        private static readonly JsonSerializerOptions options = new() { WriteIndented = true };

        /// <summary>
        /// The sections the last <see cref="Load"/> declined to read, because the preference governing
        /// them was off.
        /// </summary>
        /// <remarks>
        /// Kept so that saving from a session which started clean preserves them instead of writing the
        /// file back with nothing in their place. The switch governs what is <em>applied</em>, never what
        /// is kept - a setting that quietly deleted a user's saved work the first time the app wrote its
        /// workspace would be the one setting nobody could risk trying.
        ///
        /// They are pruned against the live scopes on the way out exactly as the live state is, so a
        /// session that started clean writes the same file a session that loaded would have.
        /// </remarks>
        private Dictionary<string, FilterQueryDto> _unreadFilters;
        private Dictionary<string, List<string>> _unreadPromoted;

        /// <summary>
        /// The filters section exactly as the last <see cref="Load"/> read it, and the signature each scope's
        /// query carried once that load had applied it.
        /// </summary>
        /// <remarks>
        /// Both exist because a filter can name something only the archive can describe - a generic table's
        /// per-column fields - and the workspace is read before the archive is: <c>App.OnStartup</c> loads it,
        /// and the shell is what mounts the client. A condition on one of those columns resolved against a
        /// schema that has no columns yet, so without these it would be dropped as an unknown key and then
        /// written back out of the file on exit, and a column filter would not survive a single restart.
        ///
        /// The signature is what makes that repairable without ever overwriting the user: a scope whose query
        /// still matches what the load left behind has not been touched since, so the file's version of it is
        /// still authoritative - both for <see cref="ReapplyFilters"/> and for what <see cref="SaveFilters"/>
        /// writes back. A scope the user has edited is theirs, and the file's version of it is stale.
        /// </remarks>
        private Dictionary<string, FilterQueryDto> _loadedFilters;
        private readonly Dictionary<string, string> _appliedSignatures = [];

        private readonly string _workspacePath;
        private readonly string _configurationPath;

        #region Dependency Injection

        private readonly WorkspaceState _state;
        private readonly FilterSchemaRegistry _schemas;

        public WorkspaceStore(
            WorkspaceState state,
            FilterSchemaRegistry schemas)
            : this(state, schemas, AppContext.BaseDirectory)
        {
        }

        /// <summary>
        /// Persist into <paramref name="directory"/> rather than beside the executable. Tests point this at
        /// a scratch folder so a save never writes into the install.
        /// </summary>
        public WorkspaceStore(
            WorkspaceState state,
            FilterSchemaRegistry schemas,
            string directory)
        {
            _state             = state;
            _schemas           = schemas;
            _workspacePath     = Path.Combine(directory, "Workspace.json");
            _configurationPath = Path.Combine(directory, "Configuration.json");
        }

        #endregion

        /// <summary>
        /// View ids that were popped out when the workspace was last saved. Honoured only when
        /// <see cref="Preferences.RestoreWindows"/> is set.
        /// </summary>
        public List<string> RestorablePopouts { get; private set; } = [];

        public void Load()
        {
            if (!File.Exists(_workspacePath))
                return;

            Snapshot snapshot;
            try
            {
                snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(_workspacePath));
            }
            catch (Exception)
            {
                // A corrupt workspace file is not worth failing startup over - fall back to the defaults.
                // But it is worth keeping: the file that would not parse holds the only copy of every
                // filter, pin and column width the user had, and the first save of this session is about
                // to write defaults over it.
                Keep();
                return;
            }

            if (snapshot == null)
                return;

            if (snapshot.Open is { Count: > 0 })
            {
                _state.Open.Clear();
                _state.Open.AddRange(snapshot.Open);
            }

            if (snapshot.Active != null && _state.Open.Contains(snapshot.Active))
                _state.ActivateOnly(snapshot.Active);

            if (Enum.TryParse(snapshot.Layout, out LayoutMode layout))
                _state.SetLayout(layout);

            if (snapshot.Flexes != null)
                foreach ((string id, double flex) in snapshot.Flexes)
                    _state.Flexes[id] = flex;

            if (snapshot.Pinned != null)
            {
                _state.Pinned.Clear();
                _state.Pinned.AddRange(snapshot.Pinned);
            }

            if (snapshot.Preferences != null)
            {
                _state.Preferences.Locale          = snapshot.Preferences.Locale;
                _state.Preferences.LoadOnStart     = snapshot.Preferences.LoadOnStart;
                _state.Preferences.RestoreWindows  = snapshot.Preferences.RestoreWindows;
                _state.Preferences.MonospaceIds    = snapshot.Preferences.MonospaceIds;
                _state.Preferences.RailLabels      = snapshot.Preferences.RailLabels;
                _state.Preferences.RestoreFilters  = snapshot.Preferences.RestoreFilters;
                _state.Preferences.RestorePromoted = snapshot.Preferences.RestorePromoted;

                // A tolerance that is not a distance reads as the default rather than as itself: it is typed
                // into a box and then written to a file anybody can hand-edit, and a negative one would make
                // every float comparison answer everything.
                _state.Preferences.FilterEpsilon = Distance(snapshot.Preferences.FilterEpsilon);
            }

            if (snapshot.ColumnWidths != null)
                // A null entry is skipped rather than copied: this runs in App.OnStartup, outside the parse's
                // try, so it would keep the app from starting at all.
                foreach ((string viewId, Dictionary<string, int> widths) in snapshot.ColumnWidths)
                    if (widths != null)
                        _state.ColumnWidths[viewId] = new Dictionary<string, int>(widths);

            // Kept whatever happens next, so a condition the load cannot resolve yet is neither lost on the
            // way out nor beyond repair once the archive has been read.
            _loadedFilters = snapshot.Filters;

            // Read after the preferences block above, so the switch that governs these is the one the file
            // carries rather than the default it was constructed with. Both gate the load and not the
            // save: the sections stay in the file, ready for the switch to be turned back on.
            if (_state.Preferences.RestoreFilters)
                LoadFilters(snapshot.Filters);
            else
                _unreadFilters = snapshot.Filters;

            if (_state.Preferences.RestorePromoted)
                LoadPromoted(snapshot.Promoted);
            else
                _unreadPromoted = snapshot.Promoted;

            RestorablePopouts = snapshot.Popouts ?? [];
        }

        public void Save()
        {
            var snapshot = new Snapshot
            {
                Open        = [.. _state.Open],
                Active      = _state.Active,
                Layout      = _state.Layout.ToString(),
                Flexes      = new Dictionary<string, double>(_state.Flexes),
                Pinned      = [.. _state.Pinned],
                Preferences = _state.Preferences,
                Popouts     = _state.Popouts.Select(p => p.ViewId).ToList(),

                ColumnWidths = _state.ColumnWidths.ToDictionary(
                    e => e.Key,
                    e => new Dictionary<string, int>(e.Value)),

                Filters = SaveFilters(),
                Promoted = SavePromoted()
            };

            TryWrite(_workspacePath, JsonSerializer.Serialize(snapshot, options));
        }

        /// <summary>
        /// The filters worth keeping: those of panes that are open, pinned or popped out, and not empty.
        /// </summary>
        /// <remarks>
        /// Spawned scopes - <c>detail:2</c>, <c>effecttype:3</c> - are created liberally as the user follows
        /// cross-references, so persisting every one of them would grow the file forever with entries no pane
        /// will ever read back.
        /// </remarks>
        private Dictionary<string, FilterQueryDto> SaveFilters()
        {
            HashSet<string> live = LiveScopes();

            Dictionary<string, FilterQueryDto> filters = [];

            foreach ((string scope, PaneState pane) in _state.PaneStates)
            {
                if (!live.Contains(scope))
                    continue;

                // A pane still as the load left it is written as it was read, not as it was resolved. The
                // two differ only where the load could not resolve a condition - a column of a table the
                // archive had not been read for yet - and writing the resolved version back would delete it
                // from the file for good.
                if (Untouched(scope, pane) && _loadedFilters.TryGetValue(scope, out FilterQueryDto read))
                    filters[scope] = read;
                else if (FilterQueryDtoMapper.ToDto(pane.Filters) is { } dto)
                    filters[scope] = dto;
            }

            // A pane this session never filtered keeps whatever the file already held for it. The live
            // one wins where there is both: the user filtering a pane is them saying what it should be.
            foreach ((string scope, FilterQueryDto dto) in _unreadFilters ?? [])
                if (live.Contains(scope) && !filters.ContainsKey(scope))
                    filters[scope] = dto;

            return filters.Count > 0 ? filters : null;
        }

        /// <summary>
        /// Whether <paramref name="pane"/>'s filter is still exactly what the last <see cref="Load"/> left
        /// there, and so whether the file's own version of it is still the better one.
        /// </summary>
        private bool Untouched(string scope, PaneState pane) =>
            _loadedFilters != null
            && _appliedSignatures.TryGetValue(scope, out string applied)
            && pane.Filters.Signature() == applied;

        /// <summary>The scopes a pane is still reachable through, and so still worth persisting.</summary>
        private HashSet<string> LiveScopes() =>
        [
            .. _state.Open,
            .. _state.Pinned,
            .. _state.Popouts.Select(p => p.ViewId)
        ];

        /// <summary>
        /// The promoted flex columns of every pane still worth remembering.
        /// </summary>
        /// <remarks>
        /// Pruned to live scopes exactly as <see cref="SaveFilters"/> is, and for the same reason: a
        /// <c>detail:7</c> the user opened once should not leave a promotion behind forever.
        /// </remarks>
        private Dictionary<string, List<string>> SavePromoted()
        {
            HashSet<string> live = LiveScopes();

            Dictionary<string, List<string>> promoted = [];

            foreach ((string scope, PaneState pane) in _state.PaneStates)
            {
                if (!live.Contains(scope) || pane.Promoted.Count == 0)
                    continue;

                promoted[scope] = [.. pane.Promoted];
            }

            foreach ((string scope, List<string> keys) in _unreadPromoted ?? [])
                if (live.Contains(scope) && !promoted.ContainsKey(scope))
                    promoted[scope] = keys;

            return promoted.Count > 0 ? promoted : null;
        }

        private void LoadPromoted(Dictionary<string, List<string>> promoted)
        {
            if (promoted == null || _schemas == null)
                return;

            foreach ((string scope, List<string> keys) in promoted)
            {
                // Per scope, as the filters are: one unreadable entry costs its own pane and no more.
                try
                {
                    FilterSchema schema = _schemas.For(_state.Describe(scope));
                    if (schema == null)
                        continue;

                    PaneState pane = _state.PaneStateFor(scope);
                    pane.Promoted.Clear();

                    // A column the archive no longer carries is dropped rather than kept: unlike a value
                    // that no longer parses there is nothing left to render or repair, which is the same
                    // stance FilterQueryDtoMapper takes on an unknown field key.
                    foreach (string key in (keys ?? []).Distinct())
                        if (schema.Field(key) is FilterColumnFieldSchema)
                            pane.Promoted.Add(key);
                }
                catch (Exception)
                {
                }
            }
        }

        private void LoadFilters(Dictionary<string, FilterQueryDto> filters)
        {
            if (filters == null || _schemas == null)
                return;

            foreach ((string scope, FilterQueryDto dto) in filters)
            {
                // Per scope, so one unreadable entry costs its own pane's filter and nothing else - a
                // corrupt condition must never take the layout down with it.
                try
                {
                    FilterSchema schema = _schemas.For(_state.Describe(scope));
                    FilterQuery query = _state.PaneStateFor(scope).Filters;

                    FilterQueryDtoMapper.Load(query, dto, schema);

                    // What this pane looks like having been loaded and nothing else, so a later read can
                    // tell "still as the file left it" from "the user has since said otherwise".
                    _appliedSignatures[scope] = query.Signature();
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// Apply the saved filters again, now that the archive has been read.
        /// </summary>
        /// <remarks>
        /// Called by the shell once a load finishes, which is the first moment a generic table's per-column
        /// fields exist at all - see <see cref="_loadedFilters"/> for why they do not at start-up. Only panes
        /// the user has not touched since the load are rewritten: reapplying is a repair of what the load
        /// could not resolve, never a second opinion on what the user has done since.
        /// </remarks>
        public void ReapplyFilters()
        {
            if (_loadedFilters == null || !_state.Preferences.RestoreFilters)
                return;

            foreach ((string scope, FilterQueryDto dto) in _loadedFilters)
            {
                if (!_appliedSignatures.TryGetValue(scope, out string applied))
                    continue;

                if (_state.PaneStateFor(scope).Filters.Signature() != applied)
                    continue;

                try
                {
                    FilterSchema schema = _schemas?.For(_state.Describe(scope));
                    FilterQuery query = _state.PaneStateFor(scope).Filters;

                    FilterQueryDtoMapper.Load(query, dto, schema);
                    _appliedSignatures[scope] = query.Signature();
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// A tolerance as a distance: anything that is not one reads as the default.
        /// </summary>
        private static double Distance(double epsilon) =>
            !double.IsFinite(epsilon) || epsilon < 0 ? NumberTolerance.Default : epsilon;

        /// <summary>
        /// Rewrite <c>PatchPath</c> in <c>Configuration.json</c>, leaving any other keys untouched.
        /// </summary>
        public void SavePatchPath(string patchPath)
        {
            JsonObject root = null;

            if (File.Exists(_configurationPath))
            {
                try
                {
                    root = JsonNode.Parse(File.ReadAllText(_configurationPath)) as JsonObject;
                }
                catch (Exception)
                {
                    root = null;
                }
            }

            root ??= [];
            root["PatchPath"] = patchPath;

            TryWrite(_configurationPath, root.ToJsonString(options));
        }

        /// <summary>
        /// Set aside a workspace file that could not be read, and say so where the user will see it.
        /// </summary>
        /// <remarks>
        /// Setup already prints <see cref="WorkspaceState.ConfigurationError"/>, so it is the one place a
        /// start-up problem is reported. An error already there - <c>Configuration.json</c> failing, which
        /// is the more urgent of the two, because nothing loads without it - is left alone.
        /// </remarks>
        private void Keep()
        {
            string kept = Path.Combine(Path.GetDirectoryName(_workspacePath) ?? "", "Workspace.corrupt.json");

            try
            {
                File.Copy(_workspacePath, kept, overwrite: true);
            }
            catch (Exception)
            {
                // Nothing more can be done for it; the message below is still worth showing.
            }

            _state.ConfigurationError ??=
                $"Workspace.json could not be read and was not applied. It has been kept as {kept}.";
        }

        /// <summary>
        /// Write <paramref name="content"/> so that the file on disk is never a half-written one.
        /// </summary>
        /// <remarks>
        /// <see cref="File.WriteAllText(string, string)"/> truncates and then writes, so anything that
        /// stops the process in between - and the save on exit races the process going away by
        /// construction - leaves a torn file, and a file that will not parse means starting from defaults.
        /// A temp file swapped in cannot be observed half-written at all.
        /// </remarks>
        private static void TryWrite(string path, string content)
        {
            // Named for this process. Nothing stops the app being started twice, and both copies write the
            // same directory: with one shared temp name, each could install the other's content, or find
            // the other's temp file open and lose its own save without a word.
            string temporary = $"{path}.{Environment.ProcessId}.tmp";

            try
            {
                File.WriteAllText(temporary, content);

                if (File.Exists(path))
                    File.Replace(temporary, path, destinationBackupFileName: null);
                else
                    File.Move(temporary, path);
            }
            catch (IOException)
            {
                // Read-only install directory; the workspace is a convenience, not a requirement.
                Discard(temporary);
            }
            catch (UnauthorizedAccessException)
            {
                Discard(temporary);
            }
        }

        private static void Discard(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
            }
        }
    }
}
