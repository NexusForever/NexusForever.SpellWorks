using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Services
{
    /// <summary>
    /// The progress a text table load reports.
    /// </summary>
    /// <remarks>
    /// A localisation archive can hold more than one <c>.bin</c> locale, so the number of archives is not
    /// the number of steps. Whatever the layout, <see cref="TextTableService.Initialise"/> must never report
    /// a value past the maximum it announced, or a progress bar reads as more than complete.
    /// </remarks>
    public class TextTableServiceProgressTests
    {
        [Fact]
        public async Task Never_reports_past_the_maximum_it_announced()
        {
            // One archive, two locales inside it - the shape a repacked client can have.
            var archive = new FakeArchiveReader()
                .With("en-US.bin", TextTableWriter.Stream(0, (1, "Arcane Missile")))
                .With("de-DE.bin", TextTableWriter.Stream(0, (1, "Arkanes Geschoss")));

            var progress = new ProgressRecorder();
            var service = new TextTableService(new FakeArchiveService(new FakeArchiveReader(), archive));

            await service.Initialise(progress);

            Assert.Equal(2, service.AvailableLocales.Count);
            Assert.All(progress.Reports, report => Assert.True(report.Value <= report.Maximum,
                $"reported {report.Value} of {report.Maximum}"));
        }
    }
}
