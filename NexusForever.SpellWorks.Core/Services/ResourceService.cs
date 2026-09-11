namespace NexusForever.SpellWorks.Core.Services
{
    public class ResourceService : IResourceService
    {
        private readonly IArchiveService _archiveService;
        private readonly ITextTableService _textTableService;
        private readonly IGameTableService _gameTableService;
        private readonly ISpellModelService _spellModelService;

        public ResourceService(
            IArchiveService archiveService,
            ITextTableService textTableService,
            IGameTableService gameTableService,
            ISpellModelService spellModelService)
        {
            _archiveService    = archiveService;
            _textTableService  = textTableService;
            _gameTableService  = gameTableService;
            _spellModelService = spellModelService;
        }

        public async Task Initialise(IProgress<EngineProgress> progress)
        {
            await _archiveService.Initialise();
            await _textTableService.Initialise(progress);
            await _gameTableService.Initialise(progress);
            await _spellModelService.Initialise(progress);
        }
    }
}
