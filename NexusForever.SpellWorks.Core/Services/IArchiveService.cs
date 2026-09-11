namespace NexusForever.SpellWorks.Core.Services
{
    public interface IArchiveService
    {
        /// <summary>
        /// Main client ClientData archive, or <c>null</c> before <see cref="Initialise"/> succeeds.
        /// </summary>
        IArchiveReader MainArchive { get; }

        /// <summary>
        /// Client localisation archives, one per locale present in the patch folder.
        /// </summary>
        IReadOnlyList<IArchiveReader> LocalisationArchives { get; }

        /// <summary>
        /// Folder the archives are read from. Defaults to <c>SpelllWorksConfiguration.PatchPath</c>
        /// and may be reassigned before a reload.
        /// </summary>
        string PatchPath { get; set; }

        /// <summary>
        /// Name of the mounted archive, or <c>null</c> when nothing has been read yet.
        /// </summary>
        string ArchiveName { get; }

        Task Initialise();
    }
}
