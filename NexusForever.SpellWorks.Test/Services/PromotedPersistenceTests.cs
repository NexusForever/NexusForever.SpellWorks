using System.IO;
using CommunityToolkit.Mvvm.Messaging;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Services
{
    /// <summary>
    /// Promoted flex columns surviving a restart, and the two switches that decide whether anything does.
    /// </summary>
    public class PromotedPersistenceTests : IDisposable
    {
        private readonly FakeTableCatalog _catalog = new();
        private readonly FakeSpellModelService _models = new();
        private readonly string _directory;

        public PromotedPersistenceTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "SpellWorks.Promoted", Guid.NewGuid().ToString("n"));
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

        private void Write(string json) => File.WriteAllText(WorkspaceFile, json);

        private static readonly string DataBits00 = FilterFields.Flex(FilterFields.EffectsSource, "DataBits00");
        private static readonly string DataBits01 = FilterFields.Flex(FilterFields.EffectsSource, "DataBits01");

        // ------------------------------------------------------------------ round trip

        [Fact]
        public void A_promotion_survives_a_restart()
        {
            WorkspaceState saved = NewState();
            saved.PaneStateFor("spells").Promoted.Add(DataBits00);
            Store(saved).Save();

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.Equal([DataBits00], loaded.PaneStateFor("spells").Promoted);
        }

        [Fact]
        public void Promotions_come_back_in_the_order_they_were_made()
        {
            // The order is what the promoted card draws, so it is user-visible and has to be kept.
            WorkspaceState saved = NewState();
            saved.PaneStateFor("spells").Promoted.AddRange([DataBits01, DataBits00]);
            Store(saved).Save();

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.Equal([DataBits01, DataBits00], loaded.PaneStateFor("spells").Promoted);
        }

        [Fact]
        public void A_pane_that_promoted_nothing_writes_nothing()
        {
            WorkspaceState saved = NewState();
            saved.PaneStateFor("spells");
            Store(saved).Save();

            // The section, not the word - "RestorePromoted" is a preference and is always written.
            Assert.DoesNotContain("\"Promoted\":", File.ReadAllText(WorkspaceFile));
        }

        [Fact]
        public void A_dead_scope_is_pruned_rather_than_accumulating_forever()
        {
            // A detail pane the user opened once while following a cross-reference should not leave a
            // promotion behind in the file for good - the same pruning the saved filters get.
            WorkspaceState saved = NewState();
            saved.PaneStateFor("detail:7").Promoted.Add(DataBits00);
            Store(saved).Save();

            Assert.DoesNotContain("detail:7", File.ReadAllText(WorkspaceFile));
        }

        [Fact]
        public void A_column_the_archive_no_longer_carries_is_dropped_on_load()
        {
            // Unlike a value that no longer parses there is nothing left to render or repair, so it goes
            // the way an unknown field key does.
            Write($$"""
            {
              "Open": ["spells"],
              "Promoted": { "spells": ["{{DataBits00}}", "fx:effects.Gone", "not-a-flex-key"] }
            }
            """);

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.Equal([DataBits00], loaded.PaneStateFor("spells").Promoted);
        }

        [Fact]
        public void A_column_listed_twice_is_promoted_once()
        {
            Write($$"""
            {
              "Open": ["spells"],
              "Promoted": { "spells": ["{{DataBits00}}", "{{DataBits00}}"] }
            }
            """);

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.Equal([DataBits00], loaded.PaneStateFor("spells").Promoted);
        }

        [Fact]
        public void A_scope_with_no_form_promotes_nothing()
        {
            // Setup has no filter schema, so it has no columns to promote and nothing to validate against.
            Write($$"""
            {
              "Open": ["setup"],
              "Promoted": { "setup": ["{{DataBits00}}"] }
            }
            """);

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.Empty(loaded.PaneStateFor("setup").Promoted);
        }

        [Fact]
        public void A_workspace_with_no_promoted_section_loads_as_before()
        {
            Write("""{ "Open": ["spells"], "Active": "spells" }""");

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.Empty(loaded.PaneStateFor("spells").Promoted);
        }

        // ------------------------------------------------------------------ the two switches

        [Fact]
        public void Both_switches_default_to_restoring()
        {
            Preferences preferences = NewState().Preferences;

            Assert.True(preferences.RestoreFilters);
            Assert.True(preferences.RestorePromoted);
        }

        [Fact]
        public void Both_switches_survive_a_restart()
        {
            WorkspaceState saved = NewState();
            saved.Preferences.RestoreFilters = false;
            saved.Preferences.RestorePromoted = false;
            Store(saved).Save();

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.False(loaded.Preferences.RestoreFilters);
            Assert.False(loaded.Preferences.RestorePromoted);
        }

        [Fact]
        public void Filters_are_not_restored_when_the_switch_is_off()
        {
            // Written into the JSON rather than set on the object: Load overwrites the preferences from
            // the file before it reaches the filter section, so a value set beforehand would be replaced.
            Write($$"""
            {
              "Open": ["spells"],
              "Preferences": { "RestoreFilters": false, "RestorePromoted": true },
              "Filters": { "spells": { "search": "arcane" } },
              "Promoted": { "spells": ["{{DataBits00}}"] }
            }
            """);

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.True(loaded.PaneStateFor("spells").Filters.IsEmpty);

            // The switches are independent: the promotions still came back.
            Assert.Equal([DataBits00], loaded.PaneStateFor("spells").Promoted);
        }

        [Fact]
        public void Promotions_are_not_restored_when_their_switch_is_off()
        {
            Write($$"""
            {
              "Open": ["spells"],
              "Preferences": { "RestoreFilters": true, "RestorePromoted": false },
              "Filters": { "spells": { "search": "arcane" } },
              "Promoted": { "spells": ["{{DataBits00}}"] }
            }
            """);

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.Empty(loaded.PaneStateFor("spells").Promoted);
            Assert.Equal("arcane", loaded.PaneStateFor("spells").Filters.Search);
        }

        [Fact]
        public void Starting_clean_does_not_forget_what_was_saved()
        {
            // The whole point of the switch: it governs the load and never the save, so turning it back
            // on returns the work rather than finding it overwritten with nothing.
            Write($$"""
            {
              "Open": ["spells"],
              "Preferences": { "RestoreFilters": false, "RestorePromoted": false },
              "Filters": { "spells": { "search": "arcane" } },
              "Promoted": { "spells": ["{{DataBits00}}"] }
            }
            """);

            // One store across both calls, as the app has: it is a singleton, loaded on start-up and
            // saved on exit, and the sections it declined to read are stashed on that instance.
            WorkspaceState off = NewState();
            WorkspaceStore store = Store(off);

            store.Load();
            Assert.True(off.PaneStateFor("spells").Filters.IsEmpty);

            // Saving from the clean session must not blank the sections it declined to read.
            store.Save();

            string json = File.ReadAllText(WorkspaceFile);
            Assert.Contains("arcane", json);
            Assert.Contains(DataBits00, json);
        }
    }
}
