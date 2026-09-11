using NexusForever.SpellWorks.Core.Services;

namespace NexusForever.SpellWorks.Core.Test.Testing
{
    /// <summary>An archive held in memory: a name-to-bytes map behind the real reader contract.</summary>
    public sealed class FakeArchiveReader : IArchiveReader
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Whether the archive has been released, as a reload releases the one it replaces.</summary>
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;

        public FakeArchiveReader With(string path, byte[] content)
        {
            _files[path] = content;
            return this;
        }

        public FakeArchiveReader With(string path, MemoryStream content) => With(path, content.ToArray());

        public IArchiveFile Find(string path) =>
            _files.TryGetValue(path, out byte[] content) ? new FakeArchiveFile(path, content) : null;

        public IReadOnlyList<IArchiveFile> Search(string pattern)
        {
            // Only the "*.ext" form is used by the loaders.
            string extension = pattern.TrimStart('*');

            return _files
                .Where(f => f.Key.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                .Select(IArchiveFile (f) => new FakeArchiveFile(f.Key, f.Value))
                .ToList();
        }

        private sealed record FakeArchiveFile(string Name, byte[] Content) : IArchiveFile
        {
            public Stream Open() => new MemoryStream(Content, writable: false);
        }
    }

    /// <summary>A synthetic disk: which archive paths exist, and what mounting one yields.</summary>
    public sealed class FakeArchiveMounter : IArchiveMounter
    {
        private readonly Dictionary<string, IArchiveReader> _mountable = new(StringComparer.OrdinalIgnoreCase);

        public List<(string IndexPath, string CoreDataPath)> Mounts { get; } = [];

        public FakeArchiveMounter With(string path, IArchiveReader reader = null)
        {
            _mountable[path] = reader ?? new FakeArchiveReader();
            return this;
        }

        public bool Exists(string path) => _mountable.ContainsKey(path);

        public IArchiveReader Mount(string indexPath, string coreDataPath)
        {
            Mounts.Add((indexPath, coreDataPath));

            return _mountable.TryGetValue(indexPath, out IArchiveReader reader)
                ? reader
                : throw new FileNotFoundException(indexPath);
        }
    }

    /// <summary>A synthetic set of drives and files for the installation search.</summary>
    public sealed class FakeDriveProbe : IDriveProbe
    {
        private readonly List<ProbedDrive> _drives = [];
        private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Set to have <see cref="FileExists"/> throw, covering the guarded path.</summary>
        public bool Throws { get; set; }

        public FakeDriveProbe WithDrive(string root, string kind = "fixed", bool network = false)
        {
            _drives.Add(new ProbedDrive(root, kind, network));
            return this;
        }

        /// <summary>Make <paramref name="folder"/> look like a patch folder.</summary>
        public FakeDriveProbe WithPatchFolder(string folder)
        {
            _files.Add(Path.Combine(folder, "ClientData.archive"));
            _files.Add(Path.Combine(folder, "ClientData.index"));
            return this;
        }

        public IReadOnlyList<ProbedDrive> Drives() => _drives;

        public bool FileExists(string path)
        {
            if (Throws)
                throw new IOException("drive unavailable");

            return _files.Contains(path);
        }
    }

    /// <summary>Records the progress an initialise reports.</summary>
    public sealed class ProgressRecorder : IProgress<EngineProgress>
    {
        public List<EngineProgress> Reports { get; } = [];

        public void Report(EngineProgress value) => Reports.Add(value);
    }
}
