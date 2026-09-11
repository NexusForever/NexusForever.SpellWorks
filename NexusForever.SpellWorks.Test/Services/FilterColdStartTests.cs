using CommunityToolkit.Mvvm.Messaging;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Services
{
    /// <summary>
    /// Filters that name something only the archive can describe, read back before the archive has been
    /// read.
    /// </summary>
    /// <remarks>
    /// <c>App.OnStartup</c> loads the workspace before the shell exists, and the shell is what reads the
    /// archive - so the only fields a generic table offers at that moment are the four written out by hand.
    /// Its per-column fields are built from <see cref="FakeTableCatalog"/>'s descriptors, which arrive with
    /// the load, so a condition on one of them has to be kept until they do rather than dropped as an
    /// unknown key and written out of the file on exit.
    /// </remarks>
    public class FilterColdStartTests : IDisposable
    {
        private const string Scope = "tbl:Spell4";

        private readonly FakeSpellModelService _models = new();
        private readonly string _directory;

        public FilterColdStartTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "SpellWorks.ColdStart", Guid.NewGuid().ToString("n"));
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

        /// <summary>A catalog with the table the filter names, as it stands once the archive has been read.</summary>
        private static FakeTableCatalog Warm() => new FakeTableCatalog().With("Spell4", ["Id", "TierIndex"], []);

        private (WorkspaceState State, WorkspaceStore Store) Session(FakeTableCatalog catalog)
        {
            var state = new WorkspaceState(new WeakReferenceMessenger(), _models, catalog);
            return (state, new WorkspaceStore(state, new FilterSchemaRegistry(_models, catalog, state.Preferences), _directory));
        }

        private string Column => FilterFields.Column("TierIndex");

        /// <summary>A workspace holding one column filter on the generic Spell4 table.</summary>
        private void GivenASavedColumnFilter()
        {
            (WorkspaceState state, WorkspaceStore store) = Session(Warm());
            state.SelectView(Scope);
            state.PaneStateFor(Scope).Filters.And(Column, "3", FilterOperator.Contains);

            store.Save();

            Assert.Contains(Column, File.ReadAllText(Path.Combine(_directory, "Workspace.json")));
        }

        [Fact]
        public void A_column_filter_comes_back_once_the_archive_has_been_read()
        {
            GivenASavedColumnFilter();

            // The restart: the workspace is read while the catalog is still empty …
            var catalog = new FakeTableCatalog();
            (WorkspaceState state, WorkspaceStore store) = Session(catalog);
            store.Load();

            // … and the archive lands afterwards, which is what the shell then reports.
            catalog.With("Spell4", ["Id", "TierIndex"], []);
            store.ReapplyFilters();

            Assert.Equal("3", state.PaneStateFor(Scope).Filters.ValueOf(Column));
        }

        [Fact]
        public void A_column_filter_is_not_erased_by_a_save_from_a_session_that_never_read_the_archive()
        {
            // Load on start switched off, or an archive that failed to read: the per-column fields never
            // appear, and the save on exit must not write the file back without them.
            GivenASavedColumnFilter();

            (_, WorkspaceStore store) = Session(new FakeTableCatalog());
            store.Load();
            store.Save();

            Assert.Contains(Column, File.ReadAllText(Path.Combine(_directory, "Workspace.json")));
        }

        [Fact]
        public void A_filter_the_user_has_since_changed_is_not_overwritten_by_the_saved_one()
        {
            // Reapplying is only ever a repair of what the load could not resolve. A pane the user has
            // edited since is theirs, and the file's version is stale.
            GivenASavedColumnFilter();

            var catalog = new FakeTableCatalog();
            (WorkspaceState state, WorkspaceStore store) = Session(catalog);
            store.Load();

            state.PaneStateFor(Scope).Filters.Search = "typed by hand";

            catalog.With("Spell4", ["Id", "TierIndex"], []);
            store.ReapplyFilters();

            Assert.Equal("typed by hand", state.PaneStateFor(Scope).Filters.Search);
        }
    }
}
