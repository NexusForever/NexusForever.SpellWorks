using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Services
{
    public class SpellModelFilterServiceTests
    {
        private static readonly ISpellModel Missile = SpellModelBuilder.A(1, "Arcane Missile").Build();
        private static readonly ISpellModel Wave = SpellModelBuilder.A(2, "Healing Wave").Build();

        [Fact]
        public void Keeps_only_the_models_every_filter_accepts()
        {
            var service = new SpellModelFilterService();

            ISpellModel[] kept = service.Filter(
                [new SpellModelTextSearchFilter { Query = "Missile" }],
                [Missile, Wave]).ToArray();

            Assert.Equal([Missile], kept);
        }

        [Fact]
        public void Applies_every_filter_not_just_the_first()
        {
            var service = new SpellModelFilterService();

            ISpellModel[] kept = service.Filter(
                [
                    new SpellModelTextSearchFilter { Query = "a" },
                    new SpellModelIdFilter { IdPrefix = "2" }
                ],
                [Missile, Wave]).ToArray();

            Assert.Equal([Wave], kept);
        }

        [Fact]
        public void With_no_filters_everything_survives()
        {
            var service = new SpellModelFilterService();

            Assert.Equal([Missile, Wave], service.Filter([], [Missile, Wave]).ToArray());
        }

        [Fact]
        public void With_no_models_nothing_is_returned()
        {
            var service = new SpellModelFilterService();

            Assert.Empty(service.Filter([new SpellModelHasProcsFilter()], []));
        }
    }

    public class SpellTooltipParseServiceTests
    {
        [Fact]
        public void Resolves_the_tooltip_from_the_text_table()
        {
            var text = new RecordingTextTable { Text = "Deals 100 damage." };
            var service = new SpellTooltipParseService(text);

            ISpellModel spell = SpellModelBuilder.A(1).Build();
            spell.Entry.LocalizedTextIdActionBarTooltip = 42;

            Assert.Equal("Deals 100 damage.", service.Parse(spell));
            Assert.Equal(42u, text.Requested);
        }

        private sealed class RecordingTextTable : ITextTableService
        {
            public string Text { get; set; } = "";
            public uint Requested { get; private set; }

            public string TableName => "en-US.bin";
            public string Locale { get; set; } = "enUS";
            public IReadOnlyList<string> AvailableLocales => ["enUS"];
            public int EntryCount => 1;

            public Task Initialise(IProgress<EngineProgress> progress) => Task.CompletedTask;

            public string GetText(uint id)
            {
                Requested = id;
                return Text;
            }
        }
    }

    public class ResourceServiceTests
    {
        [Fact]
        public async Task Initialises_each_stage_in_dependency_order()
        {
            List<string> order = [];

            var service = new ResourceService(
                new OrderedArchive(order),
                new OrderedText(order),
                new OrderedTables(order),
                new OrderedModels(order));

            await service.Initialise(new ProgressRecorder());

            // Text and game tables both need the archive mounted; the models need the tables.
            Assert.Equal(["archive", "text", "tables", "models"], order);
        }

        private sealed class OrderedArchive(List<string> order) : IArchiveService
        {
            public IArchiveReader MainArchive => null;
            public IReadOnlyList<IArchiveReader> LocalisationArchives => [];
            public string PatchPath { get; set; }
            public string ArchiveName => null;

            public Task Initialise()
            {
                order.Add("archive");
                return Task.CompletedTask;
            }
        }

        private sealed class OrderedText(List<string> order) : ITextTableService
        {
            public string TableName => null;
            public string Locale { get; set; }
            public IReadOnlyList<string> AvailableLocales => [];
            public int EntryCount => 0;

            public Task Initialise(IProgress<EngineProgress> progress)
            {
                order.Add("text");
                return Task.CompletedTask;
            }

            public string GetText(uint id) => "";
        }

        private sealed class OrderedTables(List<string> order) : IGameTableService
        {
            public Task Initialise(IProgress<EngineProgress> progress)
            {
                order.Add("tables");
                return Task.CompletedTask;
            }

            public NexusForever.GameTable.GameTable<Spell4Entry> Spell4 => null;
            public NexusForever.GameTable.GameTable<Spell4AoeTargetConstraintsEntry> Spell4AoeTargetConstraints => null;
            public NexusForever.GameTable.GameTable<Spell4BaseEntry> Spell4Base => null;
            public NexusForever.GameTable.GameTable<Spell4CCConditionsEntry> Spell4CCConditions => null;
            public NexusForever.GameTable.GameTable<Spell4CastResultEntry> Spell4CastResult => null;
            public NexusForever.GameTable.GameTable<Spell4ClientMissileEntry> Spell4ClientMissile => null;
            public NexusForever.GameTable.GameTable<Spell4ConditionsEntry> Spell4Conditions => null;
            public NexusForever.GameTable.GameTable<Spell4EffectGroupListEntry> Spell4EffectGroupList => null;
            public NexusForever.GameTable.GameTable<Spell4EffectModificationEntry> Spell4EffectModification => null;
            public NexusForever.GameTable.GameTable<Spell4EffectsEntry> Spell4Effects => null;
            public NexusForever.GameTable.GameTable<Spell4GroupListEntry> Spell4GroupList => null;
            public NexusForever.GameTable.GameTable<Spell4HitResultsEntry> Spell4HitResults => null;
            public NexusForever.GameTable.GameTable<Spell4ModificationEntry> Spell4Modification => null;
            public NexusForever.GameTable.GameTable<Spell4PrerequisitesEntry> Spell4Prerequisites => null;
            public NexusForever.GameTable.GameTable<Spell4ReagentEntry> Spell4Reagent => null;
            public NexusForever.GameTable.GameTable<Spell4RunnerEntry> Spell4Runner => null;
            public NexusForever.GameTable.GameTable<Spell4ServiceTokenCostEntry> Spell4ServiceTokenCost => null;
            public NexusForever.GameTable.GameTable<Spell4SpellTypesEntry> Spell4SpellTypes => null;
            public NexusForever.GameTable.GameTable<Spell4StackGroupEntry> Spell4StackGroup => null;
            public NexusForever.GameTable.GameTable<Spell4TagEntry> Spell4Tag => null;
            public NexusForever.GameTable.GameTable<Spell4TargetAngleEntry> Spell4TargetAngle => null;
            public NexusForever.GameTable.GameTable<Spell4TargetMechanicsEntry> Spell4TargetMechanics => null;
            public NexusForever.GameTable.GameTable<Spell4TelegraphEntry> Spell4Telegraph => null;
            public NexusForever.GameTable.GameTable<Spell4ThresholdsEntry> Spell4Thresholds => null;
            public NexusForever.GameTable.GameTable<Spell4TierRequirementsEntry> Spell4TierRequirements => null;
            public NexusForever.GameTable.GameTable<Spell4ValidTargetsEntry> Spell4ValidTargets => null;
            public NexusForever.GameTable.GameTable<Spell4VisualEntry> Spell4Visual => null;
            public NexusForever.GameTable.GameTable<Spell4VisualGroupEntry> Spell4VisualGroup => null;
            public NexusForever.GameTable.GameTable<SpellCoolDownEntry> SpellCoolDown => null;
            public NexusForever.GameTable.GameTable<SpellEffectTypeEntry> SpellEffectType => null;
            public NexusForever.GameTable.GameTable<SpellLevelEntry> SpellLevel => null;
            public NexusForever.GameTable.GameTable<SpellPhaseEntry> SpellPhase => null;
        }

        private sealed class OrderedModels(List<string> order) : ISpellModelService
        {
            public Dictionary<uint, ISpellBaseModel> SpellBaseModels { get; } = [];
            public Dictionary<uint, ISpellModel> SpellModels { get; } = [];
            public Dictionary<uint, List<ISpellEffectModel>> SpellEffectModels { get; } = [];
            public Dictionary<uint, List<ISpellProcModel>> SpellProcModels { get; } = [];
            public Dictionary<uint, List<uint>> SpellProcReferences { get; } = [];
            public Dictionary<SpellEffectType, EffectTypeUsage> EffectTypeUsages { get; } = [];

            public void Reset()
            {
            }

            public Task Initialise(IProgress<EngineProgress> progress)
            {
                order.Add("models");
                return Task.CompletedTask;
            }
        }
    }

    public class SpellProcModelTests
    {
        [Fact]
        public void Reads_the_proc_target_and_type_out_of_the_effect_row()
        {
            var model = new SpellProcModel();

            model.Initialise(new Spell4EffectsEntry
            {
                EffectType = SpellEffectType.Proc,
                DataBits00 = 3,
                DataBits01 = 777
            });

            Assert.Equal(777u, model.SpellId);
        }
    }
}
