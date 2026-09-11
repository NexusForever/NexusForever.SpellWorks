namespace NexusForever.SpellWorks.Core.Services
{
    public record EngineProgress(string Message = null, double? Value = null, double Minimum = 0, double Maximum = 100);
}
