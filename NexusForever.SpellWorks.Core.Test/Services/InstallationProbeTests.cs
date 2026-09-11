using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Services
{
    /// <summary>
    /// Searching a machine for a patch folder, driven over a synthetic disk.
    /// </summary>
    public class InstallationProbeTests
    {
        [Fact]
        public void Finds_a_patch_folder_under_a_known_relative_root()
        {
            var drives = new FakeDriveProbe()
                .WithDrive(@"C:\")
                .WithPatchFolder(@"C:\WildStar\Patch");

            IReadOnlyList<InstallationCandidate> found = new InstallationProbe(drives).Detect();

            InstallationCandidate candidate = Assert.Single(found);
            Assert.Equal(@"C:\WildStar\Patch", candidate.Path);
        }

        [Fact]
        public void Searches_every_drive()
        {
            var drives = new FakeDriveProbe()
                .WithDrive(@"C:\")
                .WithDrive(@"D:\")
                .WithPatchFolder(@"D:\Games\WildStar\Patch");

            IReadOnlyList<InstallationCandidate> found = new InstallationProbe(drives).Detect();

            Assert.Equal(@"D:\Games\WildStar\Patch", Assert.Single(found).Path);
        }

        [Fact]
        public void Finds_a_steam_installation()
        {
            var drives = new FakeDriveProbe()
                .WithDrive(@"C:\")
                .WithPatchFolder(@"C:\Program Files (x86)\Steam\steamapps\common\WildStar\Patch");

            Assert.Single(new InstallationProbe(drives).Detect());
        }

        [Fact]
        public void Reports_nothing_when_no_drive_holds_an_installation()
        {
            var drives = new FakeDriveProbe().WithDrive(@"C:\");

            Assert.Empty(new InstallationProbe(drives).Detect());
        }

        [Fact]
        public void Reports_nothing_when_there_are_no_drives_to_search()
        {
            Assert.Empty(new InstallationProbe(new FakeDriveProbe()).Detect());
        }

        [Fact]
        public void Notes_a_network_share_as_slow_and_gives_it_its_own_icon()
        {
            var drives = new FakeDriveProbe()
                .WithDrive(@"Z:\", "network", network: true)
                .WithPatchFolder(@"Z:\WildStar\Patch");

            InstallationCandidate candidate = Assert.Single(new InstallationProbe(drives).Detect());

            Assert.Equal("network share · slow", candidate.Note);
            Assert.Equal("ph ph-network", candidate.Icon);
        }

        [Fact]
        public void Notes_a_local_drive_by_its_kind()
        {
            var drives = new FakeDriveProbe()
                .WithDrive(@"C:\", "fixed")
                .WithPatchFolder(@"C:\WildStar\Patch");

            InstallationCandidate candidate = Assert.Single(new InstallationProbe(drives).Detect());

            Assert.Equal("fixed drive", candidate.Note);
            Assert.Equal("ph ph-hard-drives", candidate.Icon);
        }

        [Fact]
        public void A_folder_needs_both_client_data_files_to_count()
        {
            var drives = new FakeDriveProbe().WithDrive(@"C:\");
            var probe = new InstallationProbe(drives);

            Assert.False(probe.IsPatchFolder(@"C:\WildStar\Patch"));

            drives.WithPatchFolder(@"C:\WildStar\Patch");
            Assert.True(probe.IsPatchFolder(@"C:\WildStar\Patch"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void An_empty_path_is_never_a_patch_folder(string path)
        {
            Assert.False(new InstallationProbe(new FakeDriveProbe()).IsPatchFolder(path));
        }

        [Fact]
        public void An_unreadable_drive_is_treated_as_not_a_patch_folder()
        {
            var drives = new FakeDriveProbe { Throws = true };

            Assert.False(new InstallationProbe(drives).IsPatchFolder(@"C:\WildStar\Patch"));
        }
    }
}
