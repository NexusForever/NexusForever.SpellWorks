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
    /// Projecting the loaded game tables into the spell model graph.
    /// </summary>
    public class SpellModelServiceTests
    {
        private static async Task<SpellModelService> Service(SyntheticArchive archive)
        {
            var tables = new GameTableService(archive.AsArchiveService());
            await tables.Initialise(new ProgressRecorder());

            // The models resolve their own collaborators, so they need a real container - the same
            // registrations the app uses.
            ServiceProvider provider = new ServiceCollection()
                .AddSingleton<IGameTableService>(tables)
                .AddSingleton<ITextTableService>(new StubTextTableService())
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
        public async Task Builds_one_model_per_spell()
        {
            // Every Spell4 row is resolved against its base spell, so the base table has to agree.
            SpellModelService service = await Service(new SyntheticArchive()
                .With("Spell4Base", new Spell4BaseEntry { Id = 50 })
                .With("Spell4",
                    new Spell4Entry { Id = 1, Spell4BaseIdBaseSpell = 50 },
                    new Spell4Entry { Id = 2, Spell4BaseIdBaseSpell = 50 }));

            Assert.Equal(2, service.SpellModels.Count);
            Assert.Equal(1u, service.SpellModels[1].Id);
            Assert.Equal(50u, service.SpellModels[1].SpellBaseModel.Entry.Id);
        }

        [Fact]
        public async Task Builds_one_base_model_per_base_spell()
        {
            SpellModelService service = await Service(new SyntheticArchive()
                .With("Spell4Base", new Spell4BaseEntry { Id = 10 }));

            Assert.Single(service.SpellBaseModels);
            Assert.Equal(10u, service.SpellBaseModels[10].Entry.Id);
        }

        [Fact]
        public async Task Groups_effects_by_the_spell_that_owns_them()
        {
            SpellModelService service = await Service(new SyntheticArchive()
                .With("Spell4Effects",
                    new Spell4EffectsEntry { Id = 1, SpellId = 100 },
                    new Spell4EffectsEntry { Id = 2, SpellId = 100 },
                    new Spell4EffectsEntry { Id = 3, SpellId = 200 }));

            Assert.Equal(2, service.SpellEffectModels[100].Count);
            Assert.Single(service.SpellEffectModels[200]);
        }

        [Fact]
        public async Task Collects_only_proc_effects_as_procs()
        {
            SpellModelService service = await Service(new SyntheticArchive()
                .With("Spell4Effects",
                    new Spell4EffectsEntry { Id = 1, SpellId = 100, EffectType = SpellEffectType.Damage },
                    new Spell4EffectsEntry { Id = 2, SpellId = 100, EffectType = SpellEffectType.Proc, DataBits01 = 555 }));

            Assert.Single(service.SpellProcModels[100]);
            Assert.Equal(555u, service.SpellProcModels[100][0].SpellId);
        }

        [Fact]
        public async Task Records_which_spells_a_proc_points_at()
        {
            SpellModelService service = await Service(new SyntheticArchive()
                .With("Spell4Effects",
                    new Spell4EffectsEntry { Id = 1, SpellId = 100, EffectType = SpellEffectType.Proc, DataBits01 = 555 },
                    new Spell4EffectsEntry { Id = 2, SpellId = 200, EffectType = SpellEffectType.Proc, DataBits01 = 555 }));

            // Spell 555 is cast as a proc by both 100 and 200.
            Assert.Equal([100u, 200u], service.SpellProcReferences[555]);
        }

        [Fact]
        public async Task Reset_replaces_every_dictionary_rather_than_emptying_it()
        {
            SpellModelService service = await Service(new SyntheticArchive()
                .With("Spell4Base", new Spell4BaseEntry { Id = 1 })
                .With("Spell4", new Spell4Entry { Id = 1, Spell4BaseIdBaseSpell = 1 })
                .With("Spell4Effects", new Spell4EffectsEntry { Id = 1, SpellId = 1 }));

            // Each dictionary is published by assignment, so a caller that read one a moment ago keeps
            // the state it started with rather than watching it be emptied from another thread, as a grid
            // projecting on the thread pool would while Setup reloads. See ReloadUnderReadTests.
            Dictionary<uint, ISpellModel> held = service.SpellModels;

            service.Reset();

            Assert.NotSame(held, service.SpellModels);
            Assert.Single(held);

            Assert.Empty(service.SpellModels);
            Assert.Empty(service.SpellBaseModels);
            Assert.Empty(service.SpellEffectModels);
            Assert.Empty(service.SpellProcModels);
            Assert.Empty(service.SpellProcReferences);
            Assert.Empty(service.EffectTypeUsages);
        }

        [Fact]
        public async Task Indexes_every_effect_type_back_to_the_spells_that_use_it()
        {
            SpellModelService service = await Service(new SyntheticArchive()
                .With("Spell4Effects",
                    new Spell4EffectsEntry { Id = 1, SpellId = 300, EffectType = SpellEffectType.Damage },
                    new Spell4EffectsEntry { Id = 2, SpellId = 100, EffectType = SpellEffectType.Damage },
                    new Spell4EffectsEntry { Id = 3, SpellId = 200, EffectType = SpellEffectType.Damage },
                    new Spell4EffectsEntry { Id = 4, SpellId = 100, EffectType = SpellEffectType.Heal }));

            EffectTypeUsage damage = service.EffectTypeUsages[SpellEffectType.Damage];

            // Ascending, whatever order the table listed the rows in - the browser reads them straight out.
            Assert.Equal([100u, 200u, 300u], damage.SpellIds);
            Assert.Equal(3, damage.EffectRowCount);

            Assert.Equal([100u], service.EffectTypeUsages[SpellEffectType.Heal].SpellIds);
        }

        [Fact]
        public async Task Counts_a_spell_once_however_many_effects_of_one_type_it_carries()
        {
            SpellModelService service = await Service(new SyntheticArchive()
                .With("Spell4Effects",
                    new Spell4EffectsEntry { Id = 1, SpellId = 100, EffectType = SpellEffectType.Damage },
                    new Spell4EffectsEntry { Id = 2, SpellId = 100, EffectType = SpellEffectType.Damage },
                    new Spell4EffectsEntry { Id = 3, SpellId = 100, EffectType = SpellEffectType.Damage }));

            EffectTypeUsage damage = service.EffectTypeUsages[SpellEffectType.Damage];

            // The two columns say different things: one spell reaches for Damage, and it does so three times.
            Assert.Equal([100u], damage.SpellIds);
            Assert.Equal(3, damage.EffectRowCount);
        }

        [Fact]
        public async Task Leaves_an_effect_type_the_client_never_uses_out_of_the_index()
        {
            SpellModelService service = await Service(new SyntheticArchive()
                .With("Spell4Effects",
                    new Spell4EffectsEntry { Id = 1, SpellId = 100, EffectType = SpellEffectType.Damage }));

            // An empty row for every unused enum member would be noise the browser then has to filter back out.
            Assert.Single(service.EffectTypeUsages);
            Assert.False(service.EffectTypeUsages.ContainsKey(SpellEffectType.Heal));
        }

        [Fact]
        public async Task Rebuilds_the_effect_type_index_from_scratch_on_a_second_load()
        {
            var archive = new SyntheticArchive()
                .With("Spell4Effects",
                    new Spell4EffectsEntry { Id = 1, SpellId = 100, EffectType = SpellEffectType.Damage },
                    new Spell4EffectsEntry { Id = 2, SpellId = 200, EffectType = SpellEffectType.Damage });

            SpellModelService service = await Service(archive);
            await service.Initialise(new ProgressRecorder());

            // Initialise resets first, so a reload must not double the counts it already holds.
            EffectTypeUsage damage = service.EffectTypeUsages[SpellEffectType.Damage];

            Assert.Equal([100u, 200u], damage.SpellIds);
            Assert.Equal(2, damage.EffectRowCount);
        }

        [Fact]
        public async Task A_spell_whose_base_row_is_missing_fails_the_load()
        {
            // Client data always carries the base spell; if it did not, the model graph cannot be built.
            await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(new SyntheticArchive()
                .With("Spell4", new Spell4Entry { Id = 1, Spell4BaseIdBaseSpell = 999 })));
        }

        [Fact]
        public async Task Attaches_effects_and_procs_to_the_spell_that_owns_them()
        {
            SpellModelService service = await Service(new SyntheticArchive()
                .With("Spell4Base", new Spell4BaseEntry { Id = 5 })
                .With("Spell4", new Spell4Entry { Id = 100, Spell4BaseIdBaseSpell = 5 })
                .With("Spell4Effects",
                    new Spell4EffectsEntry { Id = 1, SpellId = 100, EffectType = SpellEffectType.Damage },
                    new Spell4EffectsEntry { Id = 2, SpellId = 100, EffectType = SpellEffectType.Proc, DataBits01 = 100 }));

            ISpellModel spell = service.SpellModels[100];

            Assert.Equal(2, spell.Effects.Count);
            Assert.Single(spell.Procs);
            Assert.Equal([100u], spell.ProcReferences);
        }

        [Fact]
        public async Task Reports_progress_when_it_starts()
        {
            var tables = new GameTableService(new SyntheticArchive().AsArchiveService());
            await tables.Initialise(new ProgressRecorder());

            ServiceProvider provider = new ServiceCollection()
                .AddSingleton<IGameTableService>(tables)
                .AddSingleton<ITextTableService>(new StubTextTableService())
                .AddSingleton<ISpellTooltipParseService, SpellTooltipParseService>()
                .AddTransient<ISpellModel, SpellModel>()
                .AddTransient<ISpellBaseModel, SpellBaseModel>()
                .AddTransient<ISpellEffectModel, SpellEffectModel>()
                .AddTransient<ISpellProcModel, SpellProcModel>()
                .AddSpellEffectData()
                .BuildServiceProvider();

            var progress = new ProgressRecorder();
            await new SpellModelService(tables, provider).Initialise(progress);

            Assert.Equal("Loading Spell Models...", progress.Reports[0].Message);
        }

        private sealed class StubTextTableService : ITextTableService
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
