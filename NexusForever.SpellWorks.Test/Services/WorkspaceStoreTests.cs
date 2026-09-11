using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Messaging;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Services
{
    /// <summary>
    /// Persisting the workspace beside the executable, and reading it back on the next start.
    /// </summary>
    public sealed class WorkspaceStoreTests : IDisposable
    {
        private readonly string _directory;
        private readonly FakeTableCatalog _catalog = new();
        private readonly FakeSpellModelService _models = new();

        public WorkspaceStoreTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "SpellWorks.Store", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
        }

        private WorkspaceState NewState() => new(new WeakReferenceMessenger(), _models, _catalog);

        private FilterSchemaRegistry Schemas => new(_models, _catalog, new Preferences());

        private WorkspaceStore Store(WorkspaceState state) => new(state, Schemas, _directory);

        private string WorkspaceFile => Path.Combine(_directory, "Workspace.json");
        private string ConfigurationFile => Path.Combine(_directory, "Configuration.json");

        [Fact]
        public void Saving_writes_a_workspace_file()
        {
            WorkspaceState state = NewState();

            Store(state).Save();

            Assert.True(File.Exists(WorkspaceFile));
        }

        [Fact]
        public void A_save_leaves_no_half_written_file_behind()
        {
            // The workspace is rewritten on every column resize, pin, tab reorder and preference change,
            // and on exit - which is the one that races the process going away. It is written to a temp
            // file and swapped in, so what is on disk is always one whole workspace or the previous one;
            // this asserts what a test can see of that, which is that the swap completes and clears up
            // after itself.
            WorkspaceState state = NewState();
            state.SetColumnWidth("spells", "Description", 560);

            Store(state).Save();

            Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
            Assert.NotNull(JsonSerializer.Deserialize<JsonNode>(File.ReadAllText(WorkspaceFile)));
        }

        [Fact]
        public void A_save_is_not_lost_to_another_instance_writing_at_the_same_moment()
        {
            // Nothing stops the exe being started twice, and both copies write the same install directory.
            // With one temp name shared between them, each could install the other's workspace - or, as
            // here, find the other's temp file open and lose its own save without a word. The temp file
            // belongs to the process writing it.
            string foreign = WorkspaceFile + ".tmp";

            using (new FileStream(foreign, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                WorkspaceState state = NewState();
                state.SetColumnWidth("spells", "Description", 560);

                Store(state).Save();
            }

            Assert.True(File.Exists(WorkspaceFile));
            Assert.Contains("560", File.ReadAllText(WorkspaceFile));
        }

        [Fact]
        public void A_workspace_that_cannot_be_read_is_kept_rather_than_quietly_replaced()
        {
            // A file that exists and will not parse is a file something went wrong with - a save cut off
            // by a kill, a bad merge, a hand edit. Starting clean is right, but the first save must not
            // then overwrite the only copy of every filter, pin and column width the user had.
            File.WriteAllText(WorkspaceFile, "{ \"Open\": [\"spells\"");

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            string kept = Path.Combine(_directory, "Workspace.corrupt.json");

            Assert.True(File.Exists(kept));
            Assert.Contains("spells", File.ReadAllText(kept));
            Assert.Contains("Workspace", loaded.ConfigurationError);
        }

        [Fact]
        public void The_kept_copy_survives_the_saves_that_follow()
        {
            File.WriteAllText(WorkspaceFile, "not json at all");

            WorkspaceState loaded = NewState();
            WorkspaceStore store = Store(loaded);
            store.Load();
            store.Save();

            Assert.Equal("not json at all", File.ReadAllText(Path.Combine(_directory, "Workspace.corrupt.json")));
        }

        [Fact]
        public void A_workspace_that_is_simply_absent_is_not_an_error()
        {
            // Every first run. Nothing to keep, nothing to report.
            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.Null(loaded.ConfigurationError);
            Assert.False(File.Exists(Path.Combine(_directory, "Workspace.corrupt.json")));
        }

        [Fact]
        public void A_saved_workspace_comes_back_on_the_next_start()
        {
            WorkspaceState saved = NewState();
            saved.SelectView("tables");
            saved.SetLayout(LayoutMode.SplitPanes);
            saved.Pin("tbl:Spell4Effects");
            saved.SetFlexes(["spells", "detail"], [2.5, 0.5]);
            saved.SetColumnWidth("spells", "Description", 560);
            Store(saved).Save();

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.Equal(saved.Open, loaded.Open);
            Assert.Equal("tables", loaded.Active);
            Assert.Equal(LayoutMode.SplitPanes, loaded.Layout);
            Assert.Equal(["tbl:Spell4Effects"], loaded.Pinned);
            Assert.Equal(2.5, loaded.FlexOf("spells"));
            Assert.Equal(560, loaded.ColumnWidth("spells", new GridColumn("Description", "", 300)));
        }

        [Fact]
        public void A_reordered_tab_strip_survives_a_restart()
        {
            WorkspaceState saved = NewState();
            saved.SelectView("tables");
            saved.MoveView("tables", 0);
            Store(saved).Save();

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.Equal(["tables", "spells", "detail"], loaded.Open);
        }

        [Fact]
        public void Preferences_survive_a_restart()
        {
            WorkspaceState saved = NewState();
            saved.Preferences.Locale = "deDE";
            saved.Preferences.LoadOnStart = false;
            saved.Preferences.RestoreWindows = false;
            saved.Preferences.MonospaceIds = false;
            saved.Preferences.RailLabels = false;
            saved.Preferences.FilterEpsilon = 0.25d;
            Store(saved).Save();

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.Equal("deDE", loaded.Preferences.Locale);
            Assert.False(loaded.Preferences.LoadOnStart);
            Assert.False(loaded.Preferences.RestoreWindows);
            Assert.False(loaded.Preferences.MonospaceIds);
            Assert.False(loaded.Preferences.RailLabels);
            Assert.Equal(0.25d, loaded.Preferences.FilterEpsilon);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("\"nonsense\"")]
        public void A_float_tolerance_that_is_not_a_distance_falls_back_to_the_one_in_use(string written)
        {
            // The value is typed into a box and then written to a file anybody can hand-edit; a negative or
            // unreadable one must not quietly turn every float comparison true.
            File.WriteAllText(
                Path.Combine(_directory, "Workspace.json"),
                $"{{ \"Preferences\": {{ \"Locale\": \"enUS\", \"FilterEpsilon\": {written} }} }}");

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.Equal(NumberTolerance.Default, loaded.Preferences.FilterEpsilon);
        }

        [Fact]
        public void Popped_out_windows_are_remembered_for_the_next_start()
        {
            WorkspaceState saved = NewState();
            saved.RegisterPopout("key", "effects");
            Store(saved).Save();

            var store = Store(NewState());
            store.Load();

            Assert.Equal(["effects"], store.RestorablePopouts);
        }

        [Fact]
        public void Loading_without_a_saved_workspace_leaves_the_defaults()
        {
            WorkspaceState state = NewState();

            Store(state).Load();

            Assert.Equal(["spells", "detail"], state.Open);
            Assert.Empty(Store(state).RestorablePopouts);
        }

        [Fact]
        public void A_corrupt_workspace_file_is_ignored_rather_than_failing_the_start()
        {
            File.WriteAllText(WorkspaceFile, "{ this is not json");
            WorkspaceState state = NewState();

            Store(state).Load();

            Assert.Equal(["spells", "detail"], state.Open);
        }

        [Fact]
        public void A_view_whose_column_widths_are_null_does_not_stop_the_rest_loading()
        {
            // Load runs in App.OnStartup, so a hand-edited null here must not keep the app from starting.
            File.WriteAllText(WorkspaceFile,
                "{ \"ColumnWidths\": { \"spells\": null, \"effects\": { \"Id\": 80 } } }");
            WorkspaceState state = NewState();

            Store(state).Load();

            Assert.False(state.ColumnWidths.ContainsKey("spells"));
            Assert.Equal(80, state.ColumnWidths["effects"]["Id"]);
        }

        [Fact]
        public void An_empty_workspace_file_is_ignored()
        {
            File.WriteAllText(WorkspaceFile, "null");
            WorkspaceState state = NewState();

            Store(state).Load();

            Assert.Equal(["spells", "detail"], state.Open);
        }

        [Fact]
        public void A_saved_active_view_that_is_no_longer_open_is_not_restored()
        {
            File.WriteAllText(WorkspaceFile, JsonSerializer.Serialize(new
            {
                Open = new[] { "spells" },
                Active = "tables"
            }));

            WorkspaceState state = NewState();
            Store(state).Load();

            Assert.Equal(["spells"], state.Open);
            Assert.Equal("spells", state.Active);
        }

        [Fact]
        public void An_empty_open_list_leaves_the_defaults_rather_than_an_empty_workspace()
        {
            File.WriteAllText(WorkspaceFile, JsonSerializer.Serialize(new { Open = Array.Empty<string>() }));

            WorkspaceState state = NewState();
            Store(state).Load();

            Assert.Equal(["spells", "detail"], state.Open);
        }

        [Fact]
        public void The_patch_path_is_written_into_the_configuration()
        {
            Store(NewState()).SavePatchPath(@"D:\WildStar\Patch");

            JsonNode root = JsonNode.Parse(File.ReadAllText(ConfigurationFile));

            Assert.Equal(@"D:\WildStar\Patch", root["PatchPath"].GetValue<string>());
        }

        [Fact]
        public void Writing_the_patch_path_leaves_other_configuration_keys_alone()
        {
            File.WriteAllText(ConfigurationFile, """{ "PatchPath": "C:\\Old", "Other": 42 }""");

            Store(NewState()).SavePatchPath(@"D:\New");

            JsonNode root = JsonNode.Parse(File.ReadAllText(ConfigurationFile));
            Assert.Equal(@"D:\New", root["PatchPath"].GetValue<string>());
            Assert.Equal(42, root["Other"].GetValue<int>());
        }

        [Fact]
        public void A_corrupt_configuration_is_replaced_rather_than_failing_the_save()
        {
            File.WriteAllText(ConfigurationFile, "not json at all");

            Store(NewState()).SavePatchPath(@"D:\New");

            JsonNode root = JsonNode.Parse(File.ReadAllText(ConfigurationFile));
            Assert.Equal(@"D:\New", root["PatchPath"].GetValue<string>());
        }

        [Fact]
        public void A_read_only_location_is_tolerated_because_the_workspace_is_a_convenience()
        {
            var store = new WorkspaceStore(NewState(), Schemas, Path.Combine(_directory, "does", "not", "exist"));

            // The directory is missing, so the write fails - and is swallowed rather than taking the app down.
            store.Save();
            store.SavePatchPath("anything");
        }

        [Fact]
        public void A_path_the_process_may_not_write_is_tolerated_the_same_way()
        {
            // A directory standing where the file should be is the reachable form of "this path is not
            // yours to write": Windows refuses it with UnauthorizedAccessException rather than IOException,
            // and an install folder the user cannot write to fails the same way.
            Directory.CreateDirectory(ConfigurationFile);
            Directory.CreateDirectory(WorkspaceFile);

            WorkspaceStore store = Store(NewState());

            store.Save();
            store.SavePatchPath("anything");
        }
    }
}
