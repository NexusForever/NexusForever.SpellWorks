using Microsoft.Extensions.DependencyInjection;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Effect;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Core.Static;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Models
{
    /// <summary>
    /// The models each wrap one game-table row and resolve the rows it points at.
    /// </summary>
    public class SpellModelGraphTests
    {
        private static async Task<ServiceProvider> Container(SyntheticArchive archive)
        {
            var tables = new GameTableService(archive.AsArchiveService());
            await tables.Initialise(new ProgressRecorder());

            return new ServiceCollection()
                .AddSingleton<IGameTableService>(tables)
                .AddSingleton<ITextTableService>(new NamingTextTable())
                .AddSingleton<ISpellTooltipParseService, SpellTooltipParseService>()
                .AddTransient<ISpellModel, SpellModel>()
                .AddTransient<ISpellBaseModel, SpellBaseModel>()
                .AddTransient<ISpellEffectModel, SpellEffectModel>()
                .AddTransient<ISpellProcModel, SpellProcModel>()
                .AddSpellEffectData()
                .AddSingleton<ISpellModelService, SpellModelService>()
                .BuildServiceProvider();
        }

        // ------------------------------------------------------------------ proc model

        [Fact]
        public void A_proc_reads_its_type_and_target_out_of_the_effect_row()
        {
            var proc = new SpellProcModel();

            proc.Initialise(new Spell4EffectsEntry { DataBits00 = 3, DataBits01 = 777 });

            Assert.Equal((ProcType)3, proc.ProcType);
            Assert.Equal(777u, proc.SpellId);
        }

        // ------------------------------------------------------------------ effect model

        [Fact]
        public async Task An_effect_exposes_the_columns_the_grid_shows()
        {
            ServiceProvider provider = await Container(new SyntheticArchive());
            var effect = provider.GetRequiredService<ISpellEffectModel>();

            var entry = new Spell4EffectsEntry
            {
                Id           = 1,
                SpellId      = 100,
                EffectType   = SpellEffectType.Damage,
                TargetFlags  = 6,
                DamageType   = (NexusForever.Game.Static.Spell.DamageType)2,
                DelayTime    = 250,
                TickTime     = 100,
                DurationTime = 5000,
                Flags        = 3
            };

            effect.Initialise(entry);

            Assert.Same(entry, effect.Entry);
            Assert.Equal(SpellEffectType.Damage, effect.Type);
            Assert.Equal(6u, effect.TargetFlags);
            Assert.Equal(2u, effect.DamageType);
            Assert.Equal(250u, effect.DelayTime);
            Assert.Equal(100u, effect.TickTime);
            Assert.Equal(5000u, effect.DurationTime);
            Assert.Equal(3u, effect.Flags);
        }

        [Fact]
        public async Task An_effect_picks_up_the_projection_registered_for_its_type()
        {
            ServiceProvider provider = await Container(new SyntheticArchive());
            var effect = provider.GetRequiredService<ISpellEffectModel>();

            effect.Initialise(new Spell4EffectsEntry { EffectType = SpellEffectType.VitalModifier });

            Assert.IsType<VitalModifierSpellEffectColumnData>(effect.ColumnData);
            ISpellEffectRowData row = Assert.IsType<VitalModifierSpellEffectRowData>(Assert.Single(effect.RowData));
            Assert.Same(effect.Entry, row.Entry);
        }

        [Fact]
        public async Task An_effect_type_with_no_projection_falls_back_to_the_raw_columns()
        {
            ServiceProvider provider = await Container(new SyntheticArchive());
            var effect = provider.GetRequiredService<ISpellEffectModel>();

            effect.Initialise(new Spell4EffectsEntry { EffectType = (SpellEffectType)9999, DataBits00 = 42 });

            // An unrecognised effect still gets the generic projection, so the grid shows raw bits rather
            // than failing.
            Assert.IsType<DefaultSpellEffectColumnData>(effect.ColumnData);
            ISpellEffectRowData row = Assert.IsType<DefaultSpellEffectRowData>(Assert.Single(effect.RowData));
            Assert.Equal("42", row.Data00);
            Assert.Equal("Data00", effect.ColumnData.Data00ColumnName);
        }

        // ------------------------------------------------------------------ base model

        [Fact]
        public async Task A_base_spell_resolves_the_rows_it_points_at()
        {
            var archive = new SyntheticArchive()
                .With("Spell4HitResults", new Spell4HitResultsEntry { Id = 5, Flags = 3 })
                .With("Spell4TargetMechanics", new Spell4TargetMechanicsEntry { Id = 6, TargetType = 2, Flags = 1 })
                .With("Spell4TargetAngle", new Spell4TargetAngleEntry { Id = 7, TargetAngle = 45f })
                .With("Spell4Prerequisites", new Spell4PrerequisitesEntry { Id = 8 })
                .With("Spell4ValidTargets", new Spell4ValidTargetsEntry { Id = 9, TargetBitmask = 0xFF })
                .With("Spell4SpellTypes", new Spell4SpellTypesEntry { Id = 10 })
                .With("Spell4Base", new Spell4BaseEntry { Id = 1 });

            ServiceProvider provider = await Container(archive);
            var model = provider.GetRequiredService<ISpellBaseModel>();

            model.Initialise(new Spell4BaseEntry
            {
                Id                          = 1,
                LocalizedTextIdName         = 100,
                Spell4HitResultId           = 5,
                Spell4TargetMechanicId      = 6,
                Spell4TargetAngleId         = 7,
                Spell4PrerequisiteId        = 8,
                Spell4ValidTargetId         = 9,
                Spell4BaseIdPrerequisiteSpell = 1,
                Spell4SpellTypesIdSpellType = 10
            });

            Assert.Equal(5u, model.HitResult.Id);
            Assert.Equal(6u, model.TargetMechanics.Id);
            Assert.Equal(7u, model.TargetAngle.Id);
            Assert.Equal(8u, model.Prerequisites.Id);
            Assert.Equal(9u, model.ValidTargets.Id);
            Assert.Equal(10u, model.SpellType.Id);
            Assert.Equal(1u, model.PrerequisiteSpell.Id);
        }

        [Fact]
        public async Task A_base_spell_reads_its_name_from_the_text_table()
        {
            ServiceProvider provider = await Container(new SyntheticArchive());
            var model = provider.GetRequiredService<ISpellBaseModel>();

            model.Initialise(new Spell4BaseEntry { Id = 1, LocalizedTextIdName = 100 });

            Assert.Equal("text:100", model.Name);
        }

        [Fact]
        public async Task A_base_spell_pointing_at_nothing_resolves_to_nothing()
        {
            ServiceProvider provider = await Container(new SyntheticArchive());
            var model = provider.GetRequiredService<ISpellBaseModel>();

            model.Initialise(new Spell4BaseEntry { Id = 1 });

            Assert.Null(model.HitResult);
            Assert.Null(model.TargetAngle);
            Assert.Null(model.Prerequisites);
            Assert.Null(model.ValidTargets);
            Assert.Null(model.CastGroup);
            Assert.Null(model.PositionalAoe);
            Assert.Null(model.AoeGroup);
            Assert.Null(model.PrerequisiteSpell);
            Assert.Null(model.SpellType);
        }

        // ------------------------------------------------------------------ spell model

        [Fact]
        public async Task A_spell_reads_its_description_and_tooltip_from_the_text_table()
        {
            var archive = new SyntheticArchive().With("Spell4Base", new Spell4BaseEntry { Id = 1 });
            ServiceProvider provider = await Container(archive);
            await provider.GetRequiredService<ISpellModelService>().Initialise(new ProgressRecorder());

            var spell = provider.GetRequiredService<ISpellModel>();
            spell.Initialise(new Spell4Entry
            {
                Id                                   = 7,
                Spell4BaseIdBaseSpell                = 1,
                Description                          = "Arcane Missile",
                LocalizedTextIdActionBarTooltip      = 200
            });

            Assert.Equal(7u, spell.Id);
            Assert.Equal("Arcane Missile", spell.Description);
            Assert.Equal("text:200", spell.ActionBarTooltip);
            Assert.NotNull(spell.SpellBaseModel);
        }

        private sealed class NamingTextTable : ITextTableService
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
