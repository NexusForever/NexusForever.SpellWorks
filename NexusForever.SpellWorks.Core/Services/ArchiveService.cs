using Microsoft.Extensions.Options;
using NexusForever.SpellWorks.Core.Configuration;

namespace NexusForever.SpellWorks.Core.Services
{
    public class ArchiveService : IArchiveService
    {
        private static readonly string[] localisationIndexes =
        [
            "ClientDataEN.index",
            "ClientDataFR.index",
            "ClientDataDE.index"
        ];

        /// <summary>
        /// Main client ClientData archive.
        /// </summary>
        public IArchiveReader MainArchive { get; private set; }

        /// <summary>
        /// Collection of client localisation archives.
        /// </summary>
        public IReadOnlyList<IArchiveReader> LocalisationArchives => localisationArchives;

        /// <summary>
        /// Folder the archives are read from.
        /// </summary>
        public string PatchPath { get; set; }

        /// <summary>
        /// Name of the mounted archive, or <c>null</c> when nothing has been read yet.
        /// </summary>
        public string ArchiveName { get; private set; }

        private readonly List<IArchiveReader> localisationArchives = [];

        #region Dependency Injection

        private readonly IArchiveMounter _mounter;

        public ArchiveService(
            IOptions<SpelllWorksConfiguration> options,
            IArchiveMounter mounter)
        {
            _mounter  = mounter;
            PatchPath = options.Value.PatchPath;
        }

        #endregion

        /// <summary>
        /// Mount the archives in <see cref="PatchPath"/>, releasing whatever the previous load mounted.
        /// </summary>
        /// <remarks>
        /// A mount maps the archive file and holds it open. Left to the finaliser, a replaced mount would keep
        /// its file handle and mapping for an unknown while, and an install the user has just pointed away
        /// from could not be moved or deleted. So it is released here, the moment it is replaced. That is
        /// safe because nothing reads a mount after its load: every game table and text table is copied
        /// into memory before the load that read it returns, and this runs inside the engine's load gate.
        /// </remarks>
        public Task Initialise()
        {
            MainArchive?.Dispose();
            foreach (IArchiveReader archive in localisationArchives)
                archive?.Dispose();

            localisationArchives.Clear();
            ArchiveName = null;
            MainArchive = null;

            // CoreData archive only applicable to Steam client.
            string coreData = Path.Combine(PatchPath ?? "", "CoreData.archive");
            if (!_mounter.Exists(coreData))
                coreData = null;

            MainArchive = _mounter.Mount(Path.Combine(PatchPath ?? "", "ClientData.index"), coreData);
            ArchiveName = "ClientData.archive";

            foreach (string index in localisationIndexes)
            {
                string path = Path.Combine(PatchPath ?? "", index);
                if (!_mounter.Exists(path))
                    continue;

                localisationArchives.Add(_mounter.Mount(path, coreData));
            }

            return Task.CompletedTask;
        }
    }
}
