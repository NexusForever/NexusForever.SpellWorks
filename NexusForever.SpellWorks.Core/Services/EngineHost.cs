using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Options;
using NexusForever.SpellWorks.Core.Configuration;
using NexusForever.SpellWorks.Core.Messages;

namespace NexusForever.SpellWorks.Core.Services
{
    /// <summary>
    /// Owns the engine load. A reload rewrites the patch path, clears the spell model dictionaries and
    /// re-runs <see cref="IResourceService.Initialise"/>, then announces <see cref="SpellResourcesLoaded"/>.
    /// </summary>
    public class EngineHost : IEngineHost
    {
        public EngineState State { get; private set; } = EngineState.Idle;
        public string Error { get; private set; }
        public ArchiveInfo Info { get; private set; }

        public string PatchPath => _archiveService.PatchPath;

        private readonly SemaphoreSlim _gate = new(1, 1);

        #region Dependency Injection

        private readonly IResourceService _resourceService;
        private readonly IArchiveService _archiveService;
        private readonly ITextTableService _textTableService;
        private readonly ISpellModelService _spellModelService;
        private readonly ITableCatalog _tableCatalog;
        private readonly IMessenger _messenger;
        private readonly SpelllWorksConfiguration _options;
        private readonly TimeProvider _timeProvider;

        public EngineHost(
            IResourceService resourceService,
            IArchiveService archiveService,
            ITextTableService textTableService,
            ISpellModelService spellModelService,
            ITableCatalog tableCatalog,
            IMessenger messenger,
            IOptions<SpelllWorksConfiguration> options,
            TimeProvider timeProvider)
        {
            _resourceService   = resourceService;
            _archiveService    = archiveService;
            _textTableService  = textTableService;
            _spellModelService = spellModelService;
            _tableCatalog      = tableCatalog;
            _messenger         = messenger;
            _options           = options.Value;
            _timeProvider      = timeProvider;
        }

        #endregion

        public Task LoadAsync(IProgress<EngineProgress> progress, CancellationToken cancellationToken = default)
        {
            return ReloadAsync(_archiveService.PatchPath ?? _options.PatchPath, progress, cancellationToken);
        }

        public async Task ReloadAsync(string patchPath, IProgress<EngineProgress> progress, CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);

            try
            {
                State = EngineState.Loading;
                Error = null;

                _archiveService.PatchPath = patchPath;
                _spellModelService.Reset();
                
                await Task.Run(() => _resourceService.Initialise(progress), cancellationToken);

                _tableCatalog.Rebuild();

                Info = new ArchiveInfo(
                    patchPath,
                    _archiveService.ArchiveName,
                    _tableCatalog.Tables.Count,
                    _textTableService.TableName,
                    _textTableService.EntryCount,
                    _timeProvider.GetLocalNow());

                State = EngineState.Ready;
            }
            catch (Exception exception)
            {
                State = EngineState.Failed;
                Error = exception.Message;
                Info  = null;

                // Nothing was read, so nothing may be left on show. Without this the status bar and the
                // Tables view keep reporting the previous archive's row counts as though it were mounted.
                _spellModelService.Reset();
                _tableCatalog.Clear();
            }
            finally
            {
                _gate.Release();
                _messenger.Send(new SpellResourcesLoaded());
            }
        }
    }
}
