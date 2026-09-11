using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Options;
using NexusForever.SpellWorks.Core.Configuration;
using NexusForever.SpellWorks.Core.Messages;
using NexusForever.Game.Static.Spell;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Services
{
    /// <summary>
    /// The engine load: what it mounts, what it reports, and how it behaves when a load fails.
    /// </summary>
    public class EngineHostTests
    {
        private const string Patch = @"C:\WildStar\Patch";

        private readonly FakeArchiveService _archive = new(new FakeArchiveReader());
        private readonly RecordingResourceService _resources = new();
        private readonly FakeTextTable _text = new();
        private readonly RecordingSpellModelService _models = new();
        private readonly RecordingCatalog _catalog = new();
        private readonly IMessenger _messenger = new WeakReferenceMessenger();
        private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));

        private EngineHost Host(string configuredPath = Patch) => new(
            _resources, _archive, _text, _models, _catalog, _messenger,
            Options.Create(new SpelllWorksConfiguration { PatchPath = configuredPath }), _clock);

        [Fact]
        public void Starts_idle()
        {
            Assert.Equal(EngineState.Idle, Host().State);
        }

        [Fact]
        public async Task A_successful_load_ends_ready()
        {
            EngineHost host = Host();

            await host.ReloadAsync(Patch, new ProgressRecorder());

            Assert.Equal(EngineState.Ready, host.State);
            Assert.Null(host.Error);
        }

        [Fact]
        public async Task Mounts_the_requested_patch_path()
        {
            EngineHost host = Host();

            await host.ReloadAsync(@"D:\Elsewhere", new ProgressRecorder());

            Assert.Equal(@"D:\Elsewhere", _archive.PatchPath);
            Assert.Equal(@"D:\Elsewhere", host.PatchPath);
        }

        [Fact]
        public async Task Loading_uses_the_configured_path_when_none_is_mounted()
        {
            _archive.PatchPath = null;
            EngineHost host = Host(@"E:\Configured");

            await host.LoadAsync(new ProgressRecorder());

            Assert.Equal(@"E:\Configured", _archive.PatchPath);
        }

        [Fact]
        public async Task Clears_the_previous_models_before_reloading()
        {
            await Host().ReloadAsync(Patch, new ProgressRecorder());

            Assert.Equal(1, _models.ResetCount);
        }

        [Fact]
        public async Task Rebuilds_the_table_catalog_after_the_resources_load()
        {
            await Host().ReloadAsync(Patch, new ProgressRecorder());

            Assert.Equal(1, _catalog.RebuildCount);
            Assert.True(_resources.Initialised);
        }

        [Fact]
        public async Task Describes_the_archive_it_mounted()
        {
            _catalog.Add("Spell4");
            _catalog.Add("Spell4Base");
            _text.TableNameValue = "en-US.bin";
            _text.EntryCountValue = 4321;

            EngineHost host = Host();

            await host.ReloadAsync(Patch, new ProgressRecorder());

            ArchiveInfo info = host.Info;
            Assert.Equal(Patch, info.PatchPath);
            Assert.Equal("ClientData.archive", info.ArchiveName);
            Assert.Equal(2, info.TableCount);
            Assert.Equal("en-US.bin", info.TextTableName);
            Assert.Equal(4321, info.TextEntryCount);
            Assert.Equal(_clock.GetLocalNow(), info.LastRead);
        }

        [Fact]
        public async Task Announces_that_the_resources_loaded()
        {
            var announced = 0;
            _messenger.Register<SpellResourcesLoaded>(this, (_, _) => announced++);

            await Host().ReloadAsync(Patch, new ProgressRecorder());

            Assert.Equal(1, announced);
        }

        [Fact]
        public async Task A_failed_load_is_reported_rather_than_thrown()
        {
            _resources.Failure = new InvalidDataException("archive is corrupt");
            EngineHost host = Host();

            await host.ReloadAsync(Patch, new ProgressRecorder());

            Assert.Equal(EngineState.Failed, host.State);
            Assert.Equal("archive is corrupt", host.Error);
            Assert.Null(host.Info);
        }

        [Fact]
        public async Task A_failed_load_takes_the_previous_archives_tables_off_show()
        {
            // Otherwise the status bar and the Tables view keep reporting the old row counts as though the
            // client were still mounted, while Setup says nothing is loaded.
            EngineHost host = Host();
            await host.ReloadAsync(Patch, new ProgressRecorder());
            _catalog.Add("Spell4");
            _catalog.Add("Spell4Base");

            _resources.Failure = new InvalidDataException("archive is corrupt");
            await host.ReloadAsync(@"D:Nowhere", new ProgressRecorder());

            Assert.Empty(_catalog.Tables);
            Assert.Equal(1, _catalog.ClearCount);
        }

        [Fact]
        public async Task A_load_that_works_leaves_the_catalog_alone()
        {
            EngineHost host = Host();

            await host.ReloadAsync(Patch, new ProgressRecorder());

            Assert.Equal(0, _catalog.ClearCount);
            Assert.Equal(1, _catalog.RebuildCount);
        }

        [Fact]
        public async Task A_failed_load_leaves_no_half_built_models_behind()
        {
            _resources.Failure = new InvalidDataException("archive is corrupt");

            await Host().ReloadAsync(Patch, new ProgressRecorder());

            // Once before the load and once while unwinding it.
            Assert.Equal(2, _models.ResetCount);
        }

        [Fact]
        public async Task A_failed_load_still_announces_so_the_ui_stops_waiting()
        {
            _resources.Failure = new InvalidDataException("boom");
            var announced = 0;
            _messenger.Register<SpellResourcesLoaded>(this, (_, _) => announced++);

            await Host().ReloadAsync(Patch, new ProgressRecorder());

            Assert.Equal(1, announced);
        }

        [Fact]
        public async Task A_later_load_clears_the_earlier_failure()
        {
            EngineHost host = Host();
            _resources.Failure = new InvalidDataException("boom");
            await host.ReloadAsync(Patch, new ProgressRecorder());

            _resources.Failure = null;
            await host.ReloadAsync(Patch, new ProgressRecorder());

            Assert.Equal(EngineState.Ready, host.State);
            Assert.Null(host.Error);
            Assert.NotNull(host.Info);
        }

        [Fact]
        public async Task Passes_progress_through_to_the_resource_load()
        {
            var progress = new ProgressRecorder();
            _resources.Reports = [new EngineProgress("Loading Game Tables...", 3, 0, 32)];

            await Host().ReloadAsync(Patch, progress);

            Assert.Contains(progress.Reports, r => r.Message == "Loading Game Tables...");
        }

        [Fact]
        public async Task A_token_cancelled_before_the_load_starts_escapes_rather_than_being_reported()
        {
            // The gate is awaited outside the try, so a token that is already cancelled throws out of
            // ReloadAsync, while a failure inside the load is caught and surfaced through State/Error.
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            EngineHost host = Host();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => host.ReloadAsync(Patch, new ProgressRecorder(), cancellation.Token));

            Assert.Equal(EngineState.Idle, host.State);
        }

        [Fact]
        public async Task A_second_load_waits_for_the_first_to_finish()
        {
            // One gate guards the engine, so two reloads cannot interleave and leave a half-built graph.
            EngineHost host = Host();

            await Task.WhenAll(
                host.ReloadAsync(Patch, new ProgressRecorder()),
                host.ReloadAsync(Patch, new ProgressRecorder()));

            Assert.Equal(EngineState.Ready, host.State);
            Assert.Equal(2, _catalog.RebuildCount);
        }

        // ------------------------------------------------------------------ doubles

        private sealed class RecordingResourceService : IResourceService
        {
            public bool Initialised { get; private set; }
            public Exception Failure { get; set; }
            public List<EngineProgress> Reports { get; set; } = [];

            public Task Initialise(IProgress<EngineProgress> progress)
            {
                foreach (EngineProgress report in Reports)
                    progress.Report(report);

                if (Failure != null)
                    throw Failure;

                Initialised = true;
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingSpellModelService : ISpellModelService
        {
            public int ResetCount { get; private set; }

            public Dictionary<uint, ISpellBaseModel> SpellBaseModels { get; } = [];
            public Dictionary<uint, ISpellModel> SpellModels { get; } = [];
            public Dictionary<uint, List<ISpellEffectModel>> SpellEffectModels { get; } = [];
            public Dictionary<uint, List<ISpellProcModel>> SpellProcModels { get; } = [];
            public Dictionary<uint, List<uint>> SpellProcReferences { get; } = [];
            public Dictionary<SpellEffectType, EffectTypeUsage> EffectTypeUsages { get; } = [];

            public void Reset() => ResetCount++;

            public Task Initialise(IProgress<EngineProgress> progress) => Task.CompletedTask;
        }

        private sealed class RecordingCatalog : ITableCatalog
        {
            private readonly List<TableDescriptor> _tables = [];

            public int RebuildCount { get; private set; }
            public IReadOnlyList<TableDescriptor> Tables => _tables;

            public void Add(string name) =>
                _tables.Add(new TableDescriptor(name, typeof(object), 0, [], () => [], _ => []));

            public TableDescriptor Get(string name) => _tables.FirstOrDefault(t => t.Name == name);

            // Nothing here reaches for a column; this catalog exists to count Rebuild and Clear.
            public IReadOnlyList<GameTableColumn> Columns(Type entryType) => [];

            public int ClearCount { get; private set; }

            public void Rebuild() => RebuildCount++;

            public void Clear()
            {
                ClearCount++;
                _tables.Clear();
            }
        }

        private sealed class FakeTextTable : ITextTableService
        {
            public string TableNameValue { get; set; } = "en-US.bin";
            public int EntryCountValue { get; set; }

            public string TableName => TableNameValue;
            public string Locale { get; set; } = "enUS";
            public IReadOnlyList<string> AvailableLocales => ["enUS"];
            public int EntryCount => EntryCountValue;

            public Task Initialise(IProgress<EngineProgress> progress) => Task.CompletedTask;

            public string GetText(uint id) => "";
        }

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }
    }
}
