using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Services
{
    /// <summary>
    /// Loading the localisation tables out of the mounted archives, and reading strings back out of the
    /// one the chosen locale points at.
    /// </summary>
    public class TextTableServiceTests
    {
        private static IArchiveReader Locale(string fileName, params (uint Id, string Text)[] entries) =>
            new FakeArchiveReader().With(fileName, TextTableWriter.Stream(0, entries));

        private static async Task<TextTableService> Service(params IArchiveReader[] locales)
        {
            var service = new TextTableService(new FakeArchiveService(new FakeArchiveReader(), locales));
            await service.Initialise(new ProgressRecorder());

            return service;
        }

        [Fact]
        public async Task Loads_a_locale_out_of_the_archive()
        {
            TextTableService service = await Service(Locale("en-US.bin", (1, "Arcane Missile")));

            Assert.Equal("Arcane Missile", service.GetText(1));
            Assert.Equal(1, service.EntryCount);
        }

        [Fact]
        public async Task Names_the_table_it_is_reading_from()
        {
            TextTableService service = await Service(Locale("en-US.bin", (1, "x")));

            Assert.Equal("enUS.bin", service.TableName);
        }

        [Fact]
        public async Task Derives_the_locale_tag_from_the_file_name()
        {
            // en-US.bin becomes enUS, matching the tags the setup view offers.
            TextTableService service = await Service(
                Locale("en-US.bin", (1, "x")),
                Locale("de-DE.bin", (1, "y")));

            Assert.Equal(["enUS", "deDE"], service.AvailableLocales);
        }

        [Fact]
        public async Task Reads_from_the_selected_locale()
        {
            TextTableService service = await Service(
                Locale("en-US.bin", (1, "Arcane Missile")),
                Locale("de-DE.bin", (1, "Arkanes Geschoss")));

            service.Locale = "deDE";

            Assert.Equal("Arkanes Geschoss", service.GetText(1));
            Assert.Equal("deDE.bin", service.TableName);
        }

        [Fact]
        public async Task Reports_back_the_locale_it_was_given()
        {
            // The setter resolves against what is loaded; the getter still reports what was asked for, so
            // the settings control shows the user's choice rather than the table that answered it.
            TextTableService service = await Service(Locale("en-US.bin", (1, "Arcane Missile")));

            service.Locale = "frFR";

            Assert.Equal("frFR", service.Locale);
        }

        [Fact]
        public async Task Falls_back_to_a_loaded_locale_when_the_chosen_one_is_absent()
        {
            TextTableService service = await Service(Locale("en-US.bin", (1, "Arcane Missile")));

            service.Locale = "frFR";

            Assert.Equal("Arcane Missile", service.GetText(1));
            Assert.Equal("enUS.bin", service.TableName);
        }

        [Fact]
        public async Task An_id_that_is_not_in_the_table_says_so_rather_than_returning_nothing()
        {
            TextTableService service = await Service(Locale("en-US.bin", (1, "Arcane Missile")));

            Assert.Equal("UNKNOWN LOCALISED TEXT ID", service.GetText(999));
        }

        [Fact]
        public async Task With_no_locale_archives_there_is_nothing_to_read()
        {
            TextTableService service = await Service();

            Assert.Null(service.TableName);
            Assert.Equal(0, service.EntryCount);
            Assert.Empty(service.AvailableLocales);
            Assert.Equal("UNKNOWN LOCALISED TEXT ID", service.GetText(1));
        }

        [Fact]
        public async Task Reports_progress_across_the_locales()
        {
            var progress = new ProgressRecorder();
            var service = new TextTableService(new FakeArchiveService(
                new FakeArchiveReader(),
                Locale("en-US.bin", (1, "x")),
                Locale("de-DE.bin", (1, "y"))));

            await service.Initialise(progress);

            Assert.Equal("Loading Text Tables...", progress.Reports[0].Message);
            Assert.Equal(2, progress.Reports[0].Maximum);
            Assert.Contains(progress.Reports, r => r.Value == 2);
        }

        [Fact]
        public async Task Reloading_replaces_the_previously_loaded_locales()
        {
            var service = new TextTableService(new FakeArchiveService(
                new FakeArchiveReader(),
                Locale("en-US.bin", (1, "first"))));

            await service.Initialise(new ProgressRecorder());
            await service.Initialise(new ProgressRecorder());

            Assert.Single(service.AvailableLocales);
            Assert.Equal("first", service.GetText(1));
        }

        [Fact]
        public async Task Reads_every_string_in_a_locale()
        {
            TextTableService service = await Service(Locale("en-US.bin",
                (1, "Arcane Missile"),
                (2, "Healing Wave"),
                (5, "Ünïcödé")));

            Assert.Equal(3, service.EntryCount);
            Assert.Equal("Healing Wave", service.GetText(2));
            Assert.Equal("Ünïcödé", service.GetText(5));
        }
    }
}
