using System.Diagnostics.CodeAnalysis;
using Nexus.Archive;

namespace NexusForever.SpellWorks.Core.Services
{
    /// <summary>
    /// The real archive implementation, wrapping <c>Nexus.Archive</c>.
    /// </summary>
    /// <remarks>
    /// Excluded from coverage: every member is a one-line forward to a sealed third-party type whose only
    /// entry points read files from disk, so there is nothing here a test could drive without a client
    /// installation. The behaviour that decides <em>which</em> archives to mount lives in
    /// <see cref="ArchiveService"/>, which is covered.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public sealed class NexusArchiveMounter : IArchiveMounter
    {
        public bool Exists(string path) => File.Exists(path);

        public IArchiveReader Mount(string indexPath, string coreDataPath)
        {
            ArchiveFile coreData = coreDataPath != null && File.Exists(coreDataPath)
                ? ArchiveFileBase.FromFile(coreDataPath) as ArchiveFile
                : null;

            return new NexusArchiveReader(Archive.FromFile(indexPath, coreData));
        }

        private sealed class NexusArchiveReader(Archive archive) : IArchiveReader
        {
            public IArchiveFile Find(string path)
            {
                return archive.IndexFile.FindEntry(path) is IArchiveFileEntry entry
                    ? new NexusArchiveFile(archive, entry)
                    : null;
            }

            public IReadOnlyList<IArchiveFile> Search(string pattern)
            {
                return archive.IndexFile.GetFiles(pattern)
                    .Select(IArchiveFile (entry) => new NexusArchiveFile(archive, entry))
                    .ToList();
            }

            public void Dispose() => archive.Dispose();
        }

        private sealed class NexusArchiveFile(Archive archive, IArchiveFileEntry entry) : IArchiveFile
        {
            public string Name => entry.FileName;

            public Stream Open() => archive.OpenFileStream(entry);
        }
    }
}
