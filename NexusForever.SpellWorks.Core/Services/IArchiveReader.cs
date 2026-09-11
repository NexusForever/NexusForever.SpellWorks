namespace NexusForever.SpellWorks.Core.Services
{
    /// <summary>
    /// One file inside a mounted archive.
    /// </summary>
    public interface IArchiveFile
    {
        /// <summary>Name of the file as the archive index records it.</summary>
        string Name { get; }

        /// <summary>Open the file's contents. The caller owns the stream.</summary>
        Stream Open();
    }

    /// <summary>
    /// A mounted archive, reduced to the two things the loaders actually do with one: find a file by path,
    /// and list the files matching a pattern.
    /// </summary>
    /// <remarks>
    /// This exists so the table loaders can be exercised without a client installation. The concrete
    /// <c>Nexus.Archive.Archive</c> is sealed off behind here rather than being handed out, because it can
    /// only ever be produced by reading real files off disk.
    ///
    /// Disposable because a mount maps the archive file and holds it open. Whoever mounts one releases it
    /// when it is replaced - see <c>ArchiveService.Initialise</c>.
    /// </remarks>
    public interface IArchiveReader : IDisposable
    {
        /// <summary>The file at <paramref name="path"/>, or <c>null</c> when the archive has no such entry.</summary>
        IArchiveFile Find(string path);

        /// <summary>Every file matching a glob such as <c>*.bin</c>.</summary>
        IReadOnlyList<IArchiveFile> Search(string pattern);
    }

    /// <summary>
    /// Opens archives off disk. The one place that knows archives are files.
    /// </summary>
    public interface IArchiveMounter
    {
        bool Exists(string path);

        /// <summary>
        /// Mount the archive described by <paramref name="indexPath"/>. <paramref name="coreDataPath"/> is
        /// the Steam client's shared data archive, or <c>null</c> when there is not one.
        /// </summary>
        IArchiveReader Mount(string indexPath, string coreDataPath);
    }
}
