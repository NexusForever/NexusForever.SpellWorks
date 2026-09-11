using NexusForever.Game.Static.Spell;
using NexusForever.SpellWorks.Core.Models;

namespace NexusForever.SpellWorks.Core.Services
{
    public interface ISpellModelService
    {
        Dictionary<uint, ISpellBaseModel> SpellBaseModels { get; }
        Dictionary<uint, ISpellModel> SpellModels { get; }
        Dictionary<uint, List<ISpellEffectModel>> SpellEffectModels { get; }
        Dictionary<uint, List<ISpellProcModel>> SpellProcModels { get; }
        Dictionary<uint, List<uint>> SpellProcReferences { get; }

        /// <summary>
        /// Every effect type present in <c>Spell4Effects</c>, mapped to the spells that use it. The
        /// reverse of <see cref="SpellEffectModels"/>; a type absent from the table is absent here.
        /// </summary>
        Dictionary<SpellEffectType, EffectTypeUsage> EffectTypeUsages { get; }

        /// <summary>
        /// Clear every model dictionary ahead of a reload. The dictionaries themselves are never reassigned.
        /// </summary>
        void Reset();

        Task Initialise(IProgress<EngineProgress> progress);
    }
}
