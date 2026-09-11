using System.Reflection;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Services
{
    /// <summary>
    /// The browsable catalog built over the loaded game tables.
    /// </summary>
    public class TableCatalogTests
    {
        private static async Task<TableCatalog> Catalog(SyntheticArchive archive)
        {
            var tables = new GameTableService(archive.AsArchiveService());
            await tables.Initialise(new ProgressRecorder());

            var catalog = new TableCatalog(tables);
            catalog.Rebuild();

            return catalog;
        }

        [Fact]
        public async Task Lists_one_descriptor_per_loaded_table()
        {
            TableCatalog catalog = await Catalog(new SyntheticArchive());

            Assert.Equal(SyntheticArchive.Tables.Count, catalog.Tables.Count);
        }

        [Fact]
        public async Task Lists_tables_in_name_order()
        {
            TableCatalog catalog = await Catalog(new SyntheticArchive());

            string[] names = catalog.Tables.Select(t => t.Name).ToArray();

            Assert.Equal(names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase), names);
        }

        [Fact]
        public async Task Reports_the_row_count_of_a_table()
        {
            TableCatalog catalog = await Catalog(new SyntheticArchive()
                .With("Spell4", new Spell4Entry { Id = 1 }, new Spell4Entry { Id = 2 }));

            Assert.Equal(2, catalog.Get("Spell4").RowCount);
        }

        [Fact]
        public async Task Exposes_the_entry_columns()
        {
            TableCatalog catalog = await Catalog(new SyntheticArchive());

            TableDescriptor spell4 = catalog.Get("Spell4");

            Assert.Equal(typeof(Spell4Entry), spell4.EntryType);
            Assert.Contains("Id", spell4.Columns);
            Assert.Contains("Description", spell4.Columns);
        }

        [Fact]
        public async Task Projects_a_row_to_its_column_values_in_column_order()
        {
            TableCatalog catalog = await Catalog(new SyntheticArchive()
                .With("Spell4", new Spell4Entry { Id = 7, Description = "Arcane Missile" }));

            TableDescriptor spell4 = catalog.Get("Spell4");
            object row = spell4.Rows().Single();
            string[] values = spell4.Values(row);

            Assert.Equal(spell4.Columns.Count, values.Length);
            Assert.Equal("7", values[Array.IndexOf(spell4.Columns.ToArray(), "Id")]);
            Assert.Equal("Arcane Missile", values[Array.IndexOf(spell4.Columns.ToArray(), "Description")]);
        }

        [Fact]
        public async Task Looks_a_table_up_case_insensitively()
        {
            TableCatalog catalog = await Catalog(new SyntheticArchive());

            Assert.NotNull(catalog.Get("spell4"));
            Assert.NotNull(catalog.Get("SPELL4"));
        }

        [Fact]
        public async Task Reports_nothing_for_a_table_that_does_not_exist()
        {
            TableCatalog catalog = await Catalog(new SyntheticArchive());

            Assert.Null(catalog.Get("NotATable"));
            Assert.Null(catalog.Get(null));
        }

        [Fact]
        public void Is_empty_until_it_has_been_rebuilt()
        {
            var catalog = new TableCatalog(new GameTableService(new SyntheticArchive().AsArchiveService()));

            Assert.Empty(catalog.Tables);
        }

        [Fact]
        public void Rebuilding_over_unloaded_tables_yields_nothing()
        {
            // Every property is still null before a load, and a null table is skipped rather than throwing.
            var catalog = new TableCatalog(new GameTableService(new SyntheticArchive().AsArchiveService()));

            catalog.Rebuild();

            Assert.Empty(catalog.Tables);
        }

        [Fact]
        public async Task Rebuilding_picks_up_a_reload()
        {
            var tables = new GameTableService(new SyntheticArchive()
                .With("Spell4", new Spell4Entry { Id = 1 })
                .AsArchiveService());

            await tables.Initialise(new ProgressRecorder());

            var catalog = new TableCatalog(tables);
            catalog.Rebuild();
            Assert.Equal(1, catalog.Get("Spell4").RowCount);

            var reloaded = new GameTableService(new SyntheticArchive()
                .With("Spell4", new Spell4Entry { Id = 1 }, new Spell4Entry { Id = 2 })
                .AsArchiveService());

            await reloaded.Initialise(new ProgressRecorder());

            var second = new TableCatalog(reloaded);
            second.Rebuild();

            Assert.Equal(2, second.Get("Spell4").RowCount);
        }

        [Fact]
        public async Task Clearing_drops_every_table()
        {
            // A load that fails must not leave the previous archive's tables on show.
            TableCatalog catalog = await Catalog(new SyntheticArchive()
                .With("Spell4", new Spell4Entry { Id = 1 }));

            Assert.NotEmpty(catalog.Tables);

            catalog.Clear();

            Assert.Empty(catalog.Tables);
            Assert.Null(catalog.Get("Spell4"));
        }

        [Fact]
        public async Task A_second_rebuild_reuses_the_compiled_accessors()
        {
            // The accessor cache is keyed by entry type, so it only ever hits on a rebuild - one pass sees
            // each type once. The projection has to keep working off the cached lambda.
            var tables = new GameTableService(new SyntheticArchive()
                .With("Spell4", new Spell4Entry { Id = 7, Description = "Arcane Missile" })
                .AsArchiveService());

            await tables.Initialise(new ProgressRecorder());

            var catalog = new TableCatalog(tables);
            catalog.Rebuild();
            catalog.Rebuild();

            TableDescriptor spell4 = catalog.Get("Spell4");
            string[] values = spell4.Values(spell4.Rows().Single());

            Assert.Equal(spell4.Columns.Count, values.Length);
            Assert.Equal("7", values[Array.IndexOf(spell4.Columns.ToArray(), "Id")]);
            Assert.Equal("Arcane Missile", values[Array.IndexOf(spell4.Columns.ToArray(), "Description")]);
        }

        [Fact]
        public async Task An_array_column_that_was_never_allocated_reads_as_empty()
        {
            // Entries read out of an archive always have their arrays allocated, but Values takes any
            // instance of the entry type - a freshly constructed one leaves them null.
            TableCatalog catalog = await Catalog(new SyntheticArchive());

            (TableDescriptor descriptor, string column) = catalog.Tables
                .Select(t => (t, t.EntryType
                    .GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(f => f.FieldType.IsArray)?.Name))
                .First(p => p.Name != null);

            string[] values = descriptor.Values(Activator.CreateInstance(descriptor.EntryType));

            Assert.Equal("", values[Array.IndexOf(descriptor.Columns.ToArray(), column)]);
        }

        // ------------------------------------------------------------------ typed columns

        /// <summary>
        /// One field of every shape a game table entry can hold. The real entries are all numbers, strings
        /// and arrays, but the classification has to be decided for the rest rather than fall out of it.
        /// </summary>
        private sealed class ShapesEntry
        {
            public uint Id;
            public int Signed;
            public float Threshold;
            public double Precise;
            public long Big;
            public string Description;
            public uint[] Values;
            public bool Deprecated;
            public DayOfWeek Day;
        }

        private static GameTableColumn Column(string name) =>
            new TableCatalog(null).Columns(typeof(ShapesEntry)).Single(c => c.Name == name);

        [Theory]
        [InlineData("Id")]
        [InlineData("Signed")]
        [InlineData("Threshold")]
        [InlineData("Precise")]
        [InlineData("Big")]
        public void A_number_column_is_offered_as_a_number(string name)
        {
            Assert.True(Column(name).IsNumeric);
            Assert.NotNull(Column(name).Number);
        }

        [Theory]
        [InlineData("Description")]
        [InlineData("Values")]
        [InlineData("Deprecated")]
        [InlineData("Day")]
        public void Everything_that_is_not_a_number_is_offered_as_text(string name)
        {
            // A bool and an enum are not thresholds: comparing them by "at least" would compile and mean
            // nothing, and an enum is worth matching by the name it renders as rather than by an ordinal
            // the user never sees.
            Assert.False(Column(name).IsNumeric);
            Assert.Null(Column(name).Number);
        }

        [Fact]
        public void A_numeric_column_reads_its_value_whatever_the_width_of_the_field()
        {
            var entry = new ShapesEntry { Id = 7, Signed = -3, Threshold = 1.5f, Precise = 2.25, Big = 9 };

            Assert.Equal(7, Column("Id").Number(entry));
            Assert.Equal(-3, Column("Signed").Number(entry));
            Assert.Equal(1.5, Column("Threshold").Number(entry));
            Assert.Equal(2.25, Column("Precise").Number(entry));
            Assert.Equal(9, Column("Big").Number(entry));
        }

        [Fact]
        public void Every_column_also_reads_as_the_text_it_renders_as()
        {
            var entry = new ShapesEntry
            {
                Id          = 7,
                Description = "Arcane Missile",
                Values      = [1, 2, 3],
                Deprecated  = true,
                Day         = DayOfWeek.Friday
            };

            Assert.Equal("7", Column("Id").Text(entry));
            Assert.Equal("Arcane Missile", Column("Description").Text(entry));
            Assert.Equal("1 2 3", Column("Values").Text(entry));
            Assert.Equal("True", Column("Deprecated").Text(entry));
            Assert.Equal("Friday", Column("Day").Text(entry));
        }

        [Fact]
        public void A_string_column_that_was_never_set_reads_as_empty_rather_than_throwing()
        {
            Assert.Equal("", Column("Description").Text(new ShapesEntry()));
            Assert.Equal("", Column("Values").Text(new ShapesEntry()));
        }

        [Fact]
        public void Columns_are_reported_in_declaration_order_and_named_by_their_field()
        {
            Assert.Equal(
                ["Id", "Signed", "Threshold", "Precise", "Big", "Description", "Values", "Deprecated", "Day"],
                new TableCatalog(null).Columns(typeof(ShapesEntry)).Select(c => c.Name));

            Assert.Equal(typeof(uint), Column("Id").Type);
        }

        [Fact]
        public void A_type_asked_for_twice_gives_back_the_columns_it_gave_the_first_time()
        {
            // Compiling an accessor per column is the expensive half; the cache is what makes a flex card
            // per linked row affordable.
            var catalog = new TableCatalog(null);

            Assert.Same(catalog.Columns(typeof(ShapesEntry)), catalog.Columns(typeof(ShapesEntry)));
        }

        [Fact]
        public void Nothing_has_no_columns()
        {
            // A flex source whose linked table is not loaded resolves to no entry type at all, and an empty
            // card is a truthful answer where a throw would take the whole filter form with it.
            Assert.Empty(new TableCatalog(null).Columns(null));
        }

        [Fact]
        public async Task A_real_entry_type_classifies_its_own_columns()
        {
            TableCatalog catalog = await Catalog(new SyntheticArchive());

            IReadOnlyList<GameTableColumn> columns = catalog.Columns(typeof(Spell4Entry));

            Assert.True(columns.Single(c => c.Name == "Id").IsNumeric);
            Assert.False(columns.Single(c => c.Name == "Description").IsNumeric);
            Assert.Equal(catalog.Get("Spell4").Columns, columns.Select(c => c.Name));
        }
    }
}
