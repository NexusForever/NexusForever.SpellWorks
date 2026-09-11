using Microsoft.Extensions.DependencyInjection;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Services
{
    /// <summary>
    /// Reading the engine's state while a reload replaces it.
    /// </summary>
    /// <remarks>
    /// The app has one UI thread. A grid projects its rows on the thread pool, the command palette searches
    /// its index there, and the engine reads the archive there, so any reader of these services can be
    /// running while a reload is under way.
    ///
    /// A <see cref="Dictionary{TKey, TValue}"/> mutated while another thread walks it is undefined: it can
    /// throw <see cref="InvalidOperationException"/>, return a torn read or spin inside a resizing bucket
    /// chain, which shows up as a hung window with nothing in the log.
    ///
    /// These tests pin the contract <c>PaletteIndex</c> also keeps: <em>publish by swapping the reference,
    /// read by snapshotting it</em>. A reader then sees the state as it was or as it is after the reload,
    /// never one being rebuilt underneath it.
    ///
    /// Each test loops because the window for a clash is small; a handful of iterations is enough to hit it.
    /// </remarks>
    public class ReloadUnderReadTests
    {
        /// <summary>Reloads to run. Long enough for the two sides to overlap, short enough to stay a unit test.</summary>
        private const int Reloads = 40;

        // ------------------------------------------------------------------ spell models

        private static SyntheticArchive Spells(int count)
        {
            var bases   = new object[count];
            var spells  = new object[count];
            var effects = new object[count];

            for (uint i = 0; i < count; i++)
            {
                bases[i]   = new Spell4BaseEntry { Id = i + 1 };
                spells[i]  = new Spell4Entry { Id = i + 1, Spell4BaseIdBaseSpell = i + 1 };
                effects[i] = new Spell4EffectsEntry { Id = i + 1, SpellId = i + 1, EffectType = SpellEffectType.Damage };
            }

            return new SyntheticArchive()
                .With("Spell4Base", bases)
                .With("Spell4", spells)
                .With("Spell4Effects", effects);
        }

        private static async Task<SpellModelService> Models(SyntheticArchive archive)
        {
            var tables = new GameTableService(archive.AsArchiveService());
            await tables.Initialise(new ProgressRecorder());

            ServiceProvider provider = new ServiceCollection()
                .AddSingleton<IGameTableService>(tables)
                .AddSingleton<ITextTableService>(new StubText())
                .AddSingleton<ISpellTooltipParseService, SpellTooltipParseService>()
                .AddTransient<ISpellModel, SpellModel>()
                .AddTransient<ISpellBaseModel, SpellBaseModel>()
                .AddTransient<ISpellEffectModel, SpellEffectModel>()
                .AddTransient<ISpellProcModel, SpellProcModel>()
                .AddSpellEffectData()
                .AddSingleton<ISpellModelService, SpellModelService>()
                .BuildServiceProvider();

            var service = (SpellModelService)provider.GetRequiredService<ISpellModelService>();
            await service.Initialise(new ProgressRecorder());

            return service;
        }

        [Fact]
        public async Task A_projection_reading_the_spell_models_survives_a_reload()
        {
            // The grid path: RowSource enumerates SpellModels.Values on the pool while Setup reloads.
            SpellModelService service = await Models(Spells(400));

            await Race(
                () =>
                {
                    foreach (ISpellModel model in service.SpellModels.Values)
                        _ = model.Id;

                    foreach (EffectTypeUsage usage in service.EffectTypeUsages.Values)
                        _ = usage.SpellIds.Count;
                },
                () => service.Initialise(new ProgressRecorder()).GetAwaiter().GetResult());
        }

        [Fact]
        public async Task A_reader_holding_the_spell_models_keeps_the_world_it_started_with()
        {
            // The other half of the contract: a reset is a new, empty dictionary rather than the old one
            // emptied, so work already under way finishes against what it was handed.
            SpellModelService service = await Models(Spells(8));

            Dictionary<uint, ISpellModel> held = service.SpellModels;
            Dictionary<SpellEffectType, EffectTypeUsage> heldUsages = service.EffectTypeUsages;

            service.Reset();

            Assert.Equal(8, held.Count);
            Assert.NotEmpty(heldUsages);

            Assert.Empty(service.SpellModels);
            Assert.Empty(service.SpellBaseModels);
            Assert.Empty(service.SpellEffectModels);
            Assert.Empty(service.SpellProcModels);
            Assert.Empty(service.SpellProcReferences);
            Assert.Empty(service.EffectTypeUsages);
        }

        // ------------------------------------------------------------------ table catalog

        [Fact]
        public async Task A_grid_resolving_its_table_survives_a_rebuild()
        {
            // RowSource.BuildGameTable and FilterSchemaRegistry.BuildGameTable both call Get on the pool;
            // Rebuild runs on the UI thread as the reload finishes.
            var tables = new GameTableService(Spells(50).AsArchiveService());
            await tables.Initialise(new ProgressRecorder());

            var catalog = new TableCatalog(tables);
            catalog.Rebuild();

            await Race(
                () =>
                {
                    // Never null: the table exists before and after the rebuild, so a null here means the
                    // rebuild is visible to readers. That would render a just-reloaded table as an empty
                    // grid and drop a saved column filter, since a schema built from a null descriptor has
                    // no per-column fields for its conditions to resolve against.
                    TableDescriptor descriptor = catalog.Get("Spell4");

                    Assert.NotNull(descriptor);
                    Assert.Equal("Spell4", descriptor.Name);
                    Assert.NotEmpty(catalog.Tables);
                },
                catalog.Rebuild);
        }

        // ------------------------------------------------------------------ text tables

        [Fact]
        public async Task Reading_a_localised_string_survives_a_reload_of_the_text_tables()
        {
            // Setup renders AvailableLocales and every spell name goes through GetText - both while the
            // shell re-renders on the load's own progress reports, 80 ms apart.
            var service = new TextTableService(new FakeArchiveService(
                new FakeArchiveReader(),
                [
                    new FakeArchiveReader().With("en-US.bin", TextTableWriter.Stream(0, [(1u, "Arcane Missile")])),
                    new FakeArchiveReader().With("de-DE.bin", TextTableWriter.Stream(0, [(1u, "Arkanes Geschoss")]))
                ]));

            await service.Initialise(new ProgressRecorder());

            await Race(
                () =>
                {
                    _ = service.AvailableLocales.Count;
                    _ = service.GetText(1);
                    _ = service.TableName;
                },
                () => service.Initialise(new ProgressRecorder()).GetAwaiter().GetResult());
        }

        // ------------------------------------------------------------------ the race harness

        /// <summary>
        /// Run <paramref name="read"/> against <see cref="Reloads"/> rounds of <paramref name="reload"/>,
        /// and fail with whichever threw first.
        /// </summary>
        /// <remarks>
        /// The reader runs until the reloads are done rather than for a count of its own. A read is far
        /// cheaper than a reload, so a fixed count could finish before the first reload began and the two
        /// would never overlap. Reading for as long as the reloads take is also what a grid does.
        /// </remarks>
        private static async Task Race(Action read, Action reload)
        {
            using var start = new Barrier(2);
            int finished = 0;
            int reads = 0;

            Task reader = Task.Run(() =>
            {
                start.SignalAndWait();

                while (Volatile.Read(ref finished) == 0)
                {
                    read();
                    reads++;
                }
            });

            Task writer = Task.Run(() =>
            {
                start.SignalAndWait();

                for (int i = 0; i < Reloads; i++)
                    reload();

                Volatile.Write(ref finished, 1);
            });

            await Task.WhenAll(reader, writer);

            Assert.True(reads > 0, "The reader never ran, so nothing was raced.");
        }

        private sealed class StubText : ITextTableService
        {
            public string TableName => "en-US.bin";
            public string Locale { get; set; } = "enUS";
            public IReadOnlyList<string> AvailableLocales => ["enUS"];
            public int EntryCount => 0;

            public Task Initialise(IProgress<EngineProgress> progress) => Task.CompletedTask;

            public string GetText(uint id) => $"text:{id}";
        }
    }
}
