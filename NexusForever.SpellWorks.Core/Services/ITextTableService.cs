namespace NexusForever.SpellWorks.Core.Services
{
    public interface ITextTableService
    {
        /// <summary>
        /// File name of the localisation table currently in use, or <c>null</c> when none is loaded.
        /// </summary>
        string TableName { get; }

        /// <summary>
        /// Locale tag (<c>enUS</c>, <c>deDE</c>, ...) selecting which loaded text table <see cref="GetText"/>
        /// reads from. Every localisation archive present is loaded once; switching is free.
        /// </summary>
        string Locale { get; set; }

        /// <summary>
        /// Locale tags actually available in the mounted archives.
        /// </summary>
        IReadOnlyList<string> AvailableLocales { get; }

        /// <summary>
        /// Number of localised strings in the current table.
        /// </summary>
        int EntryCount { get; }

        Task Initialise(IProgress<EngineProgress> progress);

        string GetText(uint id);
    }
}
