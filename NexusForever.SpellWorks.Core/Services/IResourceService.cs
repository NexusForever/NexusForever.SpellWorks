namespace NexusForever.SpellWorks.Core.Services
{
    public interface IResourceService
    {
        Task Initialise(IProgress<EngineProgress> progress);
    }
}