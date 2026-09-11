namespace NexusForever.SpellWorks.Core.Services
{
    /// <summary>
    /// Read-only description of the archive the engine is currently mounted against.
    /// </summary>
    public record ArchiveInfo(
        string PatchPath,
        string ArchiveName,
        int TableCount,
        string TextTableName,
        int TextEntryCount,
        DateTimeOffset LastRead);
}
