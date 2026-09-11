
namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// Disjunction: at least one child must match. The OR blocks of a query fold into one of these.
    /// </summary>
    /// <remarks>
    /// Empty means <em>true</em>, not false. The compiler never builds an empty one from a non-empty query -
    /// groups with no valid conditions are dropped upstream - so an empty one can only come from a
    /// construction mistake, and widening the grid is the less alarming way for that to fail than blanking it.
    /// </remarks>
    public sealed class AnyOfFilter<T> : IModelFilter<T>
    {
        public IReadOnlyList<IModelFilter<T>> Children { get; }

        public AnyOfFilter(IEnumerable<IModelFilter<T>> children)
        {
            Children = children as IReadOnlyList<IModelFilter<T>> ?? [.. children ?? []];
        }

        public AnyOfFilter(params IModelFilter<T>[] children)
            : this((IEnumerable<IModelFilter<T>>)children)
        {
        }

        public bool Filter(T model)
        {
            if (Children.Count == 0)
                return true;

            for (int i = 0; i < Children.Count; i++)
                if (Children[i].Filter(model))
                    return true;

            return false;
        }
    }
}
