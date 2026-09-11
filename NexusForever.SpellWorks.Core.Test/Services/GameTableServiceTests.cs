using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Services
{
    /// <summary>
    /// Loading the game tables out of a mounted archive.
    /// </summary>
    public class GameTableServiceTests
    {
        [Fact]
        public async Task Loads_every_table_the_interface_exposes()
        {
            var service = new GameTableService(new SyntheticArchive().AsArchiveService());

            await service.Initialise(new ProgressRecorder());

            foreach ((string name, _) in SyntheticArchive.Tables)
            {
                object table = typeof(IGameTableService).GetProperty(name)!.GetValue(service);
                Assert.True(table != null, $"{name} was not loaded");
            }
        }

        [Fact]
        public async Task Reads_the_rows_of_a_table()
        {
            var archive = new SyntheticArchive()
                .With("Spell4",
                    new Spell4Entry { Id = 1, Description = "Arcane Missile", TierIndex = 2 },
                    new Spell4Entry { Id = 2, Description = "Healing Wave", TierIndex = 5 });

            var service = new GameTableService(archive.AsArchiveService());

            await service.Initialise(new ProgressRecorder());

            Assert.Equal(2, service.Spell4.Entries.Length);
            Assert.Equal("Arcane Missile", service.Spell4.Entries[0].Description);
            Assert.Equal(5u, service.Spell4.Entries[1].TierIndex);
        }

        [Fact]
        public async Task Reports_progress_across_the_whole_load()
        {
            var progress = new ProgressRecorder();

            await new GameTableService(new SyntheticArchive().AsArchiveService()).Initialise(progress);

            Assert.Equal("Loading Game Tables...", progress.Reports[0].Message);
            Assert.Equal(SyntheticArchive.Tables.Count, progress.Reports[^1].Value);
        }

        [Fact]
        public async Task A_table_missing_from_the_archive_fails_the_load()
        {
            var service = new GameTableService(new SyntheticArchive().AsArchiveService(omit: "Spell4"));

            await Assert.ThrowsAsync<FileNotFoundException>(() => service.Initialise(new ProgressRecorder()));
        }

        [Fact]
        public async Task Loading_before_an_archive_is_mounted_fails_rather_than_faulting()
        {
            var archive = new FakeArchiveService(null);

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => new GameTableService(archive).Initialise(new ProgressRecorder()));
        }

        [Fact]
        public async Task Reloading_replaces_the_previously_loaded_tables()
        {
            var archive = new SyntheticArchive().With("Spell4", new Spell4Entry { Id = 1 });
            var service = new GameTableService(archive.AsArchiveService());

            await service.Initialise(new ProgressRecorder());
            Assert.Single(service.Spell4.Entries);

            var reloaded = new SyntheticArchive()
                .With("Spell4", new Spell4Entry { Id = 1 }, new Spell4Entry { Id = 2 });

            var second = new GameTableService(reloaded.AsArchiveService());
            await second.Initialise(new ProgressRecorder());

            Assert.Equal(2, second.Spell4.Entries.Length);
        }
    }
}
