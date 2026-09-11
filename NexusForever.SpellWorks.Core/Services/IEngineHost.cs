namespace NexusForever.SpellWorks.Core.Services
{
    /// <summary>
    /// Owns the lifetime of the engine load, including reloading against a different patch path at runtime.
    /// </summary>
    public interface IEngineHost
    {
        EngineState State { get; }

        /// <summary>
        /// Last failure message when <see cref="State"/> is <see cref="EngineState.Failed"/>.
        /// </summary>
        string Error { get; }

        ArchiveInfo Info { get; }

        /// <summary>
        /// Patch path the engine is mounted against, or will be on the next load.
        /// </summary>
        string PatchPath { get; }

        Task LoadAsync(IProgress<EngineProgress> progress, CancellationToken cancellationToken = default);

        Task ReloadAsync(string patchPath, IProgress<EngineProgress> progress, CancellationToken cancellationToken = default);
    }
}
