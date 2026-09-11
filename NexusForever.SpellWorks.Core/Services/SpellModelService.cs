using Microsoft.Extensions.DependencyInjection;
using NexusForever.GameTable.Model;
using NexusForever.Game.Static.Spell;
using NexusForever.SpellWorks.Core.Models;

namespace NexusForever.SpellWorks.Core.Services
{
    /// <summary>
    /// The spell graph, projected from the game tables once per load.
    /// </summary>
    /// <remarks>
    /// Every dictionary here is <em>published</em> rather than filled in place: a load builds a new one and
    /// assigns it, and <see cref="Reset"/> assigns an empty one. The app reads these from the thread pool -
    /// a grid projects its rows there, the command palette searches there - while a reload writes them, and
    /// a <see cref="Dictionary{TKey, TValue}"/> being cleared or resized under an enumerator is undefined:
    /// "Collection was modified" if you are lucky, a torn read or a spin inside a bucket chain if you are
    /// not. Swapping the reference costs nothing per read and means a reader always holds a complete
    /// dictionary - the one it started with, or a newer one - never one being filled in underneath it.
    /// </remarks>
    public class SpellModelService : ISpellModelService
    {
        public Dictionary<uint, ISpellBaseModel> SpellBaseModels { get; private set; } = [];
        public Dictionary<uint, ISpellModel> SpellModels { get; private set; } = [];
        public Dictionary<uint, List<ISpellEffectModel>> SpellEffectModels { get; private set; } = [];
        public Dictionary<uint, List<ISpellProcModel>> SpellProcModels { get; private set; } = [];
        public Dictionary<uint, List<uint>> SpellProcReferences { get; private set; } = [];
        public Dictionary<SpellEffectType, EffectTypeUsage> EffectTypeUsages { get; private set; } = [];

        #region Dependency Injection

        private readonly IGameTableService _gameTableService;
        private readonly IServiceProvider _serviceProvider;

        public SpellModelService(
            IGameTableService gameTableService,
            IServiceProvider serviceProvider)
        {
            _gameTableService = gameTableService;
            _serviceProvider = serviceProvider;
        }

        #endregion

        /// <summary>
        /// Drop every model ahead of a reload.
        /// </summary>
        /// <remarks>
        /// Each dictionary is replaced with an empty one rather than emptied. Work already under way - a
        /// projection that read <see cref="SpellModels"/> a moment ago - finishes against the collection it
        /// was handed, which is the whole point: its answer is stale, and the pane that asked for it
        /// discards it, but nothing crashes and nothing reads a dictionary mid-clear.
        /// </remarks>
        public void Reset()
        {
            SpellBaseModels     = [];
            SpellModels         = [];
            SpellEffectModels   = [];
            SpellProcModels     = [];
            SpellProcReferences = [];
            EffectTypeUsages    = [];
        }

        /// <summary>
        /// Rebuild the graph from the loaded game tables.
        /// </summary>
        /// <remarks>
        /// Each stage builds its own dictionary and publishes it when it is complete. The ordering is not
        /// cosmetic: <see cref="SpellModel.Initialise"/> resolves its base row, effects and procs back
        /// through this service, so those have to be visible before the spells are built - and the spells
        /// are published last, so a reader walking <see cref="SpellModels"/> goes from the empty set
        /// <see cref="Reset"/> left to the whole new graph in one assignment.
        /// </remarks>
        public Task Initialise(IProgress<EngineProgress> progress)
        {
            progress.Report(new EngineProgress("Loading Spell Models..."));

            Reset();

            InitialiseBaseSpellModels();
            InitialiseSpellEffectModels();
            InitialiseSpellProcsModels();

            // must happen last, requires effects and procs to be initialised
            InitialiseSpells();

            return Task.CompletedTask;
        }

        private void InitialiseBaseSpellModels()
        {
            Dictionary<uint, ISpellBaseModel> models = [];

            foreach (Spell4BaseEntry item in _gameTableService.Spell4Base.Entries)
            {
                var model = _serviceProvider.GetService<ISpellBaseModel>();
                model.Initialise(item);
                models.Add(model.Entry.Id, model);
            }

            SpellBaseModels = models;
        }

        private void InitialiseSpells()
        {
            Dictionary<uint, ISpellModel> models = [];

            foreach (Spell4Entry item in _gameTableService.Spell4.Entries)
            {
                var model = _serviceProvider.GetService<ISpellModel>();
                model.Initialise(item);
                models.Add(item.Id, model);
            }

            SpellModels = models;
        }

        /// <summary>
        /// Build the per-spell effect lists and, in the same pass, the reverse index from effect type back to
        /// the spells using it. There are over 100k effect rows, so the reverse index rides along here rather than
        /// walking the table a second time.
        /// </summary>
        private void InitialiseSpellEffectModels()
        {
            // Spell ids are collected in a set because a spell may carry several effects of one type and must
            // still count once; the row tally alongside it is what counts them all.
            Dictionary<SpellEffectType, (HashSet<uint> Spells, int Rows)> usage = [];
            Dictionary<uint, List<ISpellEffectModel>> effects = [];

            foreach (var spellEffectsBySpellId in _gameTableService.Spell4Effects.Entries
                .GroupBy(e => e.SpellId))
            {
                var effectList = new List<ISpellEffectModel>();
                effects.Add(spellEffectsBySpellId.Key, effectList);

                foreach (Spell4EffectsEntry entry in spellEffectsBySpellId)
                {
                    var model = _serviceProvider.GetService<ISpellEffectModel>();
                    model.Initialise(entry);
                    effectList.Add(model);

                    if (!usage.TryGetValue(entry.EffectType, out (HashSet<uint> Spells, int Rows) counts))
                        counts = ([], 0);

                    counts.Spells.Add(entry.SpellId);
                    usage[entry.EffectType] = (counts.Spells, counts.Rows + 1);
                }
            }

            Dictionary<SpellEffectType, EffectTypeUsage> usages = [];

            foreach ((SpellEffectType type, (HashSet<uint> spells, int rows)) in usage)
            {
                usages.Add(type, new EffectTypeUsage
                {
                    Type           = type,
                    SpellIds       = [.. spells.Order()],
                    EffectRowCount = rows
                });
            }

            SpellEffectModels = effects;
            EffectTypeUsages  = usages;
        }

        private void InitialiseSpellProcsModels()
        {
            Dictionary<uint, List<ISpellProcModel>> procs = [];
            Dictionary<uint, List<uint>> references = [];

            foreach (var spellEffectsBySpellId in _gameTableService.Spell4Effects.Entries
                .GroupBy(e => e.SpellId))
            {
                var procsList = new List<ISpellProcModel>();
                procs.Add(spellEffectsBySpellId.Key, procsList);

                foreach (Spell4EffectsEntry spellEffectEntry in spellEffectsBySpellId
                    .Where(e => e.EffectType == SpellEffectType.Proc))
                {
                    var procModel = _serviceProvider.GetService<ISpellProcModel>();
                    procModel.Initialise(spellEffectEntry);
                    procsList.Add(procModel);

                    if (!references.TryGetValue(spellEffectEntry.DataBits01, out List<uint> referencedBy))
                    {
                        referencedBy = [];
                        references.Add(spellEffectEntry.DataBits01, referencedBy);
                    }

                    referencedBy.Add(spellEffectEntry.SpellId);
                }

            }

            SpellProcModels     = procs;
            SpellProcReferences = references;
        }
    }
}
