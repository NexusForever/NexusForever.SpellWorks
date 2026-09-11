using Microsoft.Extensions.Options;
using NexusForever.SpellWorks.Core.Configuration;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Services
{
    /// <summary>
    /// Which archives get mounted, in which order, from a given patch folder.
    /// </summary>
    public class ArchiveServiceTests
    {
        private const string Patch = @"C:\WildStar\Patch";

        private static ArchiveService Service(FakeArchiveMounter mounter, string patchPath = Patch) =>
            new(Options.Create(new SpelllWorksConfiguration { PatchPath = patchPath }), mounter);

        [Fact]
        public async Task Mounts_the_main_archive_from_the_patch_path()
        {
            var mounter = new FakeArchiveMounter().With(Path.Combine(Patch, "ClientData.index"));
            ArchiveService service = Service(mounter);

            await service.Initialise();

            Assert.NotNull(service.MainArchive);
            Assert.Equal("ClientData.archive", service.ArchiveName);
            Assert.Equal(Path.Combine(Patch, "ClientData.index"), mounter.Mounts[0].IndexPath);
        }

        [Fact]
        public async Task A_reload_releases_the_archives_it_replaces()
        {
            // Mounting an archive maps the file and holds it open, so a reload has to release the mount it
            // replaces rather than leave it to the finaliser. Releasing it is safe: every game table and
            // text table is copied into memory before the load returns.
            string main = Path.Combine(Patch, "ClientData.index");
            string english = Path.Combine(Patch, "ClientDataEN.index");

            var first = new FakeArchiveReader();
            var firstEnglish = new FakeArchiveReader();
            var mounter = new FakeArchiveMounter().With(main, first).With(english, firstEnglish);

            ArchiveService service = Service(mounter);
            await service.Initialise();

            var second = new FakeArchiveReader();
            var secondEnglish = new FakeArchiveReader();
            mounter.With(main, second).With(english, secondEnglish);

            await service.Initialise();

            Assert.True(first.Disposed);
            Assert.True(firstEnglish.Disposed);

            // And not the archives now mounted, which the rest of the load is about to read.
            Assert.False(second.Disposed);
            Assert.False(secondEnglish.Disposed);
        }

        [Fact]
        public async Task Takes_the_patch_path_from_configuration()
        {
            const string other = @"D:\Games\WildStar\Patch";
            var mounter = new FakeArchiveMounter().With(Path.Combine(other, "ClientData.index"));

            ArchiveService service = Service(mounter, other);

            Assert.Equal(other, service.PatchPath);
            await service.Initialise();
            Assert.Equal(Path.Combine(other, "ClientData.index"), mounter.Mounts[0].IndexPath);
        }

        [Fact]
        public async Task Mounts_only_the_localisation_archives_that_are_present()
        {
            var mounter = new FakeArchiveMounter()
                .With(Path.Combine(Patch, "ClientData.index"))
                .With(Path.Combine(Patch, "ClientDataEN.index"))
                .With(Path.Combine(Patch, "ClientDataDE.index"));

            ArchiveService service = Service(mounter);

            await service.Initialise();

            // FR is absent, so only two localisation archives mount.
            Assert.Equal(2, service.LocalisationArchives.Count);
            Assert.DoesNotContain(mounter.Mounts, m => m.IndexPath.Contains("FR"));
        }

        [Fact]
        public async Task Passes_the_core_data_archive_when_the_steam_client_has_one()
        {
            var mounter = new FakeArchiveMounter()
                .With(Path.Combine(Patch, "ClientData.index"))
                .With(Path.Combine(Patch, "CoreData.archive"))
                .With(Path.Combine(Patch, "ClientDataEN.index"));

            await Service(mounter).Initialise();

            Assert.All(mounter.Mounts, m => Assert.Equal(Path.Combine(Patch, "CoreData.archive"), m.CoreDataPath));
        }

        [Fact]
        public async Task Passes_no_core_data_archive_when_there_is_not_one()
        {
            var mounter = new FakeArchiveMounter().With(Path.Combine(Patch, "ClientData.index"));

            await Service(mounter).Initialise();

            Assert.Null(mounter.Mounts[0].CoreDataPath);
        }

        [Fact]
        public async Task Re_initialising_replaces_the_previous_mounts_rather_than_adding_to_them()
        {
            var mounter = new FakeArchiveMounter()
                .With(Path.Combine(Patch, "ClientData.index"))
                .With(Path.Combine(Patch, "ClientDataEN.index"));

            ArchiveService service = Service(mounter);

            await service.Initialise();
            await service.Initialise();

            Assert.Single(service.LocalisationArchives);
        }

        [Fact]
        public async Task A_reassigned_patch_path_is_used_on_the_next_load()
        {
            const string moved = @"E:\WildStar\Patch";
            var mounter = new FakeArchiveMounter()
                .With(Path.Combine(Patch, "ClientData.index"))
                .With(Path.Combine(moved, "ClientData.index"));

            ArchiveService service = Service(mounter);
            await service.Initialise();

            service.PatchPath = moved;
            await service.Initialise();

            Assert.Equal(Path.Combine(moved, "ClientData.index"), mounter.Mounts[^1].IndexPath);
        }

        [Fact]
        public async Task A_patch_folder_with_no_main_archive_fails_the_load()
        {
            ArchiveService service = Service(new FakeArchiveMounter());

            await Assert.ThrowsAsync<FileNotFoundException>(service.Initialise);
            Assert.Null(service.MainArchive);
        }
    }
}
