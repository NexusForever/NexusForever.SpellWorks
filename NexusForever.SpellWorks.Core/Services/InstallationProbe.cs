using System.Diagnostics.CodeAnalysis;

namespace NexusForever.SpellWorks.Core.Services
{
    /// <summary>
    /// A patch folder found on this machine. <see cref="Note"/> is derived from the drive
    /// </summary>
    public record InstallationCandidate(string Path, string Note, string Icon);

    /// <summary>A drive worth searching, as reported by <see cref="IDriveProbe"/>.</summary>
    public record ProbedDrive(string Root, string Kind, bool IsNetwork);

    /// <summary>
    /// The machine's drives and files. Exists so the search can be driven over a synthetic disk.
    /// </summary>
    public interface IDriveProbe
    {
        IReadOnlyList<ProbedDrive> Drives();

        bool FileExists(string path);
    }

    public interface IInstallationProbe
    {
        /// <summary>Every patch folder found on the searchable drives.</summary>
        IReadOnlyList<InstallationCandidate> Detect();

        /// <summary>Whether <paramref name="path"/> holds the two files a patch folder must have.</summary>
        bool IsPatchFolder(string path);
    }

    public class InstallationProbe : IInstallationProbe
    {
        private static readonly string[] relativeRoots =
        [
            @"WildStar\Patch",
            @"WildStar\WildStar\Patch",
            @"Games\WildStar\Patch",
            @"Program Files (x86)\WildStar\Patch",
            @"Program Files\WildStar\Patch",
            @"Program Files (x86)\Steam\steamapps\common\WildStar\Patch",
            @"SteamLibrary\steamapps\common\WildStar\Patch",
            @"NCSOFT\WildStar\Patch"
        ];

        #region Dependency Injection

        private readonly IDriveProbe _drives;

        public InstallationProbe(
            IDriveProbe drives)
        {
            _drives = drives;
        }

        #endregion

        public IReadOnlyList<InstallationCandidate> Detect()
        {
            List<InstallationCandidate> candidates = [];

            foreach (ProbedDrive drive in _drives.Drives())
            {
                foreach (string relative in relativeRoots)
                {
                    string path = Path.Combine(drive.Root, relative);
                    if (!IsPatchFolder(path))
                        continue;

                    candidates.Add(new InstallationCandidate(
                        path,
                        drive.IsNetwork ? "network share · slow" : drive.Kind + " drive",
                        drive.IsNetwork ? "ph ph-network" : "ph ph-hard-drives"));
                }
            }

            return candidates;
        }

        public bool IsPatchFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                return _drives.FileExists(Path.Combine(path, "ClientData.archive"))
                    && _drives.FileExists(Path.Combine(path, "ClientData.index"));
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The real drives on this machine.
    /// </summary>
    /// <remarks>
    /// Excluded from coverage: it reports whatever hardware the host happens to have, so no assertion about
    /// its output could hold on another machine. The search logic that consumes it lives in
    /// <see cref="InstallationProbe"/>, which is covered.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public sealed class DriveProbe : IDriveProbe
    {
        public bool FileExists(string path) => File.Exists(path);

        public IReadOnlyList<ProbedDrive> Drives()
        {
            DriveInfo[] drives;
            try
            {
                drives = DriveInfo.GetDrives();
            }
            catch (Exception)
            {
                return [];
            }

            List<ProbedDrive> probed = [];

            foreach (DriveInfo drive in drives)
            {
                try
                {
                    if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Network or DriveType.Removable))
                        continue;

                    probed.Add(new ProbedDrive(
                        drive.RootDirectory.FullName,
                        drive.DriveType.ToString().ToLowerInvariant(),
                        drive.DriveType == DriveType.Network));
                }
                catch (Exception)
                {
                }
            }

            return probed;
        }
    }
}
