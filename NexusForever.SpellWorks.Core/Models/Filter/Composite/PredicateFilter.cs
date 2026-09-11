
namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// A filter over an arbitrary lambda. The escape hatch for constraints whose predicate cannot be named
    /// ahead of time - the generic game-table cell filters, which only know their column at runtime.
    /// </summary>
    public sealed class PredicateFilter<T> : IModelFilter<T>
    {
        private readonly Func<T, bool> _predicate;

        public PredicateFilter(Func<T, bool> predicate)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        public bool Filter(T model) => _predicate(model);
    }
}
