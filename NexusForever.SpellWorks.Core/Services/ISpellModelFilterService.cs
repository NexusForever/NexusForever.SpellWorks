using NexusForever.SpellWorks.Core.Models;

namespace NexusForever.SpellWorks.Core.Services
{
    public interface ISpellModelFilterService
    {
        /// <summary>
        /// Apply a conjunction of spell constraints. Takes <see cref="IModelFilter{T}"/> rather than
        /// <see cref="ISpellModelFilter"/> so a composite can be passed straight in - a whole boolean query
        /// arrives here as a single <c>AnyOfFilter</c>, and <c>IEnumerable</c> covariance keeps every existing
        /// <c>List&lt;ISpellModelFilter&gt;</c> caller compiling unchanged.
        /// </summary>
        IEnumerable<ISpellModel> Filter(IEnumerable<IModelFilter<ISpellModel>> filters, IEnumerable<ISpellModel> models);

        /// <summary>
        /// Apply one compiled filter to any sequence. The panes that list effects, procs, effect types,
        /// table descriptors or raw cells route through here - their predicates are named Core filters too,
        /// they are simply not <see cref="ISpellModel"/> ones.
        /// </summary>
        IEnumerable<T> Filter<T>(IModelFilter<T> filter, IEnumerable<T> models);
    }
}
