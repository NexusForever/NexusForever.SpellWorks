using NexusForever.Game.Static.Spell;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Services;

namespace NexusForever.SpellWorks.Test.Testing
{
    /// <summary>
    /// Hand-written stand-ins for the engine's services. They hold plain in-memory state and record the
    /// calls a test needs to assert on, so a test reads as "given this workspace" rather than as a script
    /// of expectations.
    /// </summary>
    public sealed class FakeTableCatalog : ITableCatalog
    {
        private readonly List<TableDescriptor> _tables = [];

        public int RebuildCount { get; private set; }

        public IReadOnlyList<TableDescriptor> Tables => _tables;

        public TableDescriptor Get(string name) =>
            _tables.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

        // A type's columns are a fact of the type, not of which tables this fake was given, so there is
        // nothing here to fake - the real catalog answers it by reflection over the entry type alone.
        private readonly TableCatalog _columns = new(null);

        public IReadOnlyList<GameTableColumn> Columns(Type entryType) => _columns.Columns(entryType);

        public int ClearCount { get; private set; }

        public void Rebuild() => RebuildCount++;

        public void Clear()
        {
            ClearCount++;
            _tables.Clear();
        }

        public FakeTableCatalog With(string name, params string[] columns) => With(name, columns, []);

        public FakeTableCatalog With(string name, IReadOnlyList<string> columns, IReadOnlyList<string[]> rows)
        {
            _tables.Add(new TableDescriptor(
                name,
                typeof(object),
                rows.Count,
                columns,
                () => rows.Cast<object>().ToList(),
                entry => (string[])entry));

            return this;
        }
    }

    public sealed class FakeEngineHost : IEngineHost
    {
        public EngineState State { get; set; } = EngineState.Ready;
        public string Error { get; set; }
        public ArchiveInfo Info { get; set; }
        public string PatchPath { get; set; } = @"C:\WildStar\Patch";

        /// <summary>Set to make the next load fail the way a missing archive does.</summary>
        public string FailWith { get; set; }

        public List<string> Reloads { get; } = [];

        /// <summary>Progress the load reports before it finishes, the way the real engine does per table.</summary>
        public List<EngineProgress> Reports { get; } = [];

        /// <summary>
        /// Set to hold the load open. The boot overlay is only on screen while the load is in flight, so a
        /// test that wants to look at it has to be able to stop the load from finishing first.
        /// </summary>
        public TaskCompletionSource Gate { get; set; }

        public Task LoadAsync(IProgress<EngineProgress> progress, CancellationToken cancellationToken = default)
            => ReloadAsync(PatchPath, progress, cancellationToken);

        public async Task ReloadAsync(string patchPath, IProgress<EngineProgress> progress, CancellationToken cancellationToken = default)
        {
            Reloads.Add(patchPath);
            PatchPath = patchPath;

            // Loading for the duration, as the real host is. Setup's Apply button reads exactly this to
            // decide whether a reload is already under way, so a fake that is never busy is a fake that
            // cannot answer the question the guard asks.
            State = EngineState.Loading;

            foreach (EngineProgress report in Reports)
                progress?.Report(report);

            if (Gate != null)
                await Gate.Task;

            if (FailWith != null)
            {
                State = EngineState.Failed;
                Error = FailWith;
            }
            else
            {
                State = EngineState.Ready;
                Error = null;
            }
        }
    }

    public sealed class FakeSpellModelService : ISpellModelService
    {
        public Dictionary<uint, ISpellBaseModel> SpellBaseModels { get; } = [];
        public Dictionary<uint, ISpellModel> SpellModels { get; } = [];
        public Dictionary<uint, List<ISpellEffectModel>> SpellEffectModels { get; } = [];
        public Dictionary<uint, List<ISpellProcModel>> SpellProcModels { get; } = [];
        public Dictionary<uint, List<uint>> SpellProcReferences { get; } = [];
        public Dictionary<SpellEffectType, EffectTypeUsage> EffectTypeUsages { get; } = [];

        public bool WasReset { get; private set; }

        public void Reset()
        {
            WasReset = true;
            SpellBaseModels.Clear();
            SpellModels.Clear();
            SpellEffectModels.Clear();
            SpellProcModels.Clear();
            SpellProcReferences.Clear();
            EffectTypeUsages.Clear();
        }

        public Task Initialise(IProgress<EngineProgress> progress) => Task.CompletedTask;
    }

    public sealed class FakeTextTableService : ITextTableService
    {
        private readonly Dictionary<uint, string> _text = [];

        public string TableName { get; set; } = "en-US.bin";
        public string Locale { get; set; } = "enUS";
        public IReadOnlyList<string> AvailableLocales { get; set; } = ["enUS", "deDE"];
        public int EntryCount => _text.Count;

        /// <summary>Strings that exist only in one locale, keyed by locale tag and then by id.</summary>
        private readonly Dictionary<string, Dictionary<uint, string>> _localised = [];

        public FakeTextTableService With(uint id, string value)
        {
            _text[id] = value;
            return this;
        }

        /// <summary>
        /// A string that reads differently in <paramref name="locale"/>, for a test that needs what is on
        /// screen to depend on the locale the way it does against a real archive.
        /// </summary>
        public FakeTextTableService With(string locale, uint id, string value)
        {
            if (!_localised.TryGetValue(locale, out Dictionary<uint, string> table))
                _localised[locale] = table = [];

            table[id] = value;
            return this;
        }

        public Task Initialise(IProgress<EngineProgress> progress) => Task.CompletedTask;

        public string GetText(uint id)
        {
            if (Locale != null && _localised.TryGetValue(Locale, out Dictionary<uint, string> table)
                && table.TryGetValue(id, out string localised))
                return localised;

            return _text.TryGetValue(id, out string value) ? value : "UNKNOWN LOCALISED TEXT ID";
        }
    }

    /// <summary>
    /// Stands in for the windowing host. It registers with the workspace exactly as the real one does -
    /// that is what takes a popped-out view off the tab strip - but opens no window.
    /// </summary>
    /// <summary>
    /// Windows for the real <see cref="PopoutHost"/> to place and close.
    /// </summary>
    /// <remarks>
    /// Faking the factory rather than the host is what puts the host's own rules - the cap, the keys, and
    /// what a closing window does to the workspace - under test.
    /// </remarks>
    public sealed class FakePopoutWindowFactory : IPopoutWindowFactory
    {
        /// <summary>View ids a window was opened for, in order.</summary>
        public List<string> Popped { get; } = [];

        /// <summary>Every window handed out, by the pane key it carries.</summary>
        public Dictionary<string, FakePopoutWindow> Windows { get; } = [];

        public (double Left, double Top) Anchor { get; set; } = (100, 100);

        public IPopoutWindow Create(string paneKey, string viewId)
        {
            Popped.Add(viewId);

            var window = new FakePopoutWindow(paneKey, viewId);
            Windows[paneKey] = window;

            return window;
        }

        /// <summary>The window carrying <paramref name="viewId"/>, for a test that never saw its key.</summary>
        public FakePopoutWindow For(string viewId) =>
            Windows.Values.First(w => w.ViewId == viewId);
    }

    public sealed class FakePopoutWindow : IPopoutWindow
    {
        public FakePopoutWindow(string paneKey, string viewId)
        {
            PaneKey = paneKey;
            ViewId  = viewId;
        }

        public string PaneKey { get; }
        public string ViewId { get; }

        public double Left { get; set; }
        public double Top { get; set; }

        public bool IsShown { get; private set; }
        public bool IsClosed { get; private set; }

        public event EventHandler Closed;

        public void Show() => IsShown = true;

        /// <summary>
        /// Closing raises <see cref="Closed"/> the way a real window does, which is how the host learns a
        /// window went away without being asked - the user's own close button takes the same path.
        /// </summary>
        public void Close()
        {
            if (IsClosed)
                return;

            IsClosed = true;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Records the window gestures a chrome binding is supposed to forward.</summary>
    public sealed class FakeWindowBridge : IWindowBridge
    {
        public int DragCount { get; private set; }
        public int MaximizeCount { get; private set; }
        public int MinimizeCount { get; private set; }
        public int CloseCount { get; private set; }

        public void BeginDrag() => DragCount++;
        public void ToggleMaximize() => MaximizeCount++;
        public void Minimize() => MinimizeCount++;
        public void Close() => CloseCount++;
    }

    public sealed class FakeInstallationProbe : IInstallationProbe
    {
        public List<InstallationCandidate> Candidates { get; } = [];
        public HashSet<string> PatchFolders { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int DetectCount { get; private set; }

        public IReadOnlyList<InstallationCandidate> Detect()
        {
            DetectCount++;
            return Candidates;
        }

        public bool IsPatchFolder(string path) => path != null && PatchFolders.Contains(path);
    }

    public sealed class FakeFolderPicker : IFolderPicker
    {
        /// <summary>What the dialog returns; <c>null</c> stands for the user cancelling.</summary>
        public string Choice { get; set; }

        public List<string> Prompts { get; } = [];

        public string Pick(string title, string initialDirectory)
        {
            Prompts.Add(title);
            return Choice;
        }
    }

    /// <summary>
    /// Passes every model through, so grid tests see the rows they supplied - and can hold one projection
    /// open while another overtakes it.
    /// </summary>
    /// <remarks>
    /// Filtering eagerly rather than returning a lazy query, so <see cref="Completed"/> means the projection
    /// this call was part of is done rather than merely started. A grid reads its rows on a background
    /// thread, so two of them can be in flight at once: <see cref="Hold"/> is how a test decides which of
    /// them lands last.
    /// </remarks>
    public sealed class PassThroughFilterService : ISpellModelFilterService
    {
        /// <summary>Filter calls started, and the subset that has returned.</summary>
        public int Calls { get; private set; }

        public int Completed { get; private set; }

        private readonly Dictionary<int, TaskCompletionSource> _gates = [];
        private readonly object _lock = new();

        /// <summary>Set to make the next filter call throw, the way a projection over half-built state can.</summary>
        public bool FailNext { get; set; }

        /// <summary>Calls that have returned or thrown - every call that is no longer running.</summary>
        public int Finished { get; private set; }

        private readonly HashSet<int> _failing = [];

        /// <summary>Make the <paramref name="call"/>th filter call throw once it is released.</summary>
        public void Fail(int call)
        {
            lock (_lock)
                _failing.Add(call);
        }

        /// <summary>
        /// Block the <paramref name="call"/>th filter call until the returned source is completed.
        /// </summary>
        public TaskCompletionSource Hold(int call)
        {
            var gate = new TaskCompletionSource();

            lock (_lock)
                _gates[call] = gate;

            return gate;
        }

        public IEnumerable<ISpellModel> Filter(IEnumerable<IModelFilter<ISpellModel>> filters, IEnumerable<ISpellModel> models)
            => Await(models.Where(m => filters.All(f => f.Filter(m))));

        public IEnumerable<T> Filter<T>(IModelFilter<T> filter, IEnumerable<T> models)
            => Await(models.Where(filter.Filter));

        private List<T> Await<T>(IEnumerable<T> query)
        {
            TaskCompletionSource gate;
            bool fail;

            lock (_lock)
            {
                Calls++;
                _gates.Remove(Calls, out gate);
                fail = _failing.Remove(Calls);
            }

            try
            {
                // Deliberately blocking: this stands in for the work a real projection does, which is what a
                // newer one has to be able to overtake.
                gate?.Task.GetAwaiter().GetResult();

                if (FailNext || fail)
                {
                    FailNext = false;
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                List<T> rows = query.ToList();

                lock (_lock)
                    Completed++;

                return rows;
            }
            finally
            {
                lock (_lock)
                    Finished++;
            }
        }
    }
}
