using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Services
{
    /// <summary>
    /// Which locale answers when no locale has been chosen, or when the chosen one is not present.
    /// </summary>
    /// <remarks>
    /// <see cref="TextTableService"/> loads every localisation archive in parallel, so the tables finish in
    /// whatever order their parses complete. The fallback follows the order
    /// <see cref="IArchiveService.LocalisationArchives"/> hands them over in instead, which is English-first
    /// by construction (<c>ArchiveService.localisationIndexes</c>), so it is English whatever the timings.
    /// </remarks>
    public class TextTableServiceFallbackTests
    {
        [Fact]
        public async Task Falls_back_to_the_first_archive_rather_than_the_first_to_finish_parsing()
        {
            // The archives are handed over English-first, as ArchiveService produces them, but English is
            // the slower of the two to read - a bigger table, a colder disk, a busier thread pool.
            var service = new TextTableService(new FakeArchiveService(
                new FakeArchiveReader(),
                SlowLocale("en-US.bin", TimeSpan.FromMilliseconds(250), (1, "Arcane Missile")),
                SlowLocale("de-DE.bin", TimeSpan.Zero, (1, "Arkanes Geschoss"))));

            await service.Initialise(new ProgressRecorder());

            // No locale was ever selected, so this is what the engine reports and what every spell name
            // resolves through.
            Assert.Equal("enUS.bin", service.TableName);
            Assert.Equal("Arcane Missile", service.GetText(1));
        }

        [Fact]
        public async Task An_absent_locale_falls_back_to_the_first_archive_too()
        {
            var service = new TextTableService(new FakeArchiveService(
                new FakeArchiveReader(),
                SlowLocale("en-US.bin", TimeSpan.FromMilliseconds(250), (1, "Arcane Missile")),
                SlowLocale("de-DE.bin", TimeSpan.Zero, (1, "Arkanes Geschoss"))));

            await service.Initialise(new ProgressRecorder());

            service.Locale = "frFR";

            Assert.Equal("enUS.bin", service.TableName);
            Assert.Equal("Arcane Missile", service.GetText(1));
        }

        [Fact]
        public async Task Lists_the_available_locales_in_the_order_the_archives_were_mounted()
        {
            var service = new TextTableService(new FakeArchiveService(
                new FakeArchiveReader(),
                SlowLocale("en-US.bin", TimeSpan.FromMilliseconds(250), (1, "x")),
                SlowLocale("de-DE.bin", TimeSpan.Zero, (1, "y"))));

            await service.Initialise(new ProgressRecorder());

            Assert.Equal(["enUS", "deDE"], service.AvailableLocales);
        }

        private static IArchiveReader SlowLocale(string fileName, TimeSpan delay,
            params (uint Id, string Text)[] entries) =>
            new DelayedArchiveReader(fileName, TextTableWriter.Stream(0, entries).ToArray(), delay);

        /// <summary>
        /// One locale whose file takes <paramref name="delay"/> to open, so the parallel load finishes in a
        /// known order instead of whichever order the thread pool happens to produce.
        /// </summary>
        private sealed class DelayedArchiveReader(string name, byte[] content, TimeSpan delay) : IArchiveReader
        {
            public IArchiveFile Find(string path) =>
                string.Equals(path, name, StringComparison.OrdinalIgnoreCase) ? File() : null;

            public IReadOnlyList<IArchiveFile> Search(string pattern) =>
                name.EndsWith(pattern.TrimStart('*'), StringComparison.OrdinalIgnoreCase) ? [File()] : [];

            private IArchiveFile File() => new DelayedArchiveFile(name, content, delay);

            public void Dispose()
            {
            }

            private sealed record DelayedArchiveFile(string Name, byte[] Content, TimeSpan Delay) : IArchiveFile
            {
                public Stream Open()
                {
                    if (Delay > TimeSpan.Zero)
                        Thread.Sleep(Delay);

                    return new MemoryStream(Content, writable: false);
                }
            }
        }
    }
}
