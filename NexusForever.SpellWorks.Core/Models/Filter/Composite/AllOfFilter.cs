
namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// Conjunction: every child must match. One filter group renders as one of these.
    /// </summary>
    /// <remarks>
    /// Empty means true, which is the identity for AND and matches what an unconstrained form should do.
    /// </remarks>
    public sealed class AllOfFilter<T> : IModelFilter<T>
    {
        public IReadOnlyList<IModelFilter<T>> Children { get; }

        public AllOfFilter(IEnumerable<IModelFilter<T>> children)
        {
            Children = children as IReadOnlyList<IModelFilter<T>> ?? [.. children ?? []];
        }

        public AllOfFilter(params IModelFilter<T>[] children)
            : this((IEnumerable<IModelFilter<T>>)children)
        {
        }

        public bool Filter(T model)
        {
            for (int i = 0; i < Children.Count; i++)
                if (!Children[i].Filter(model))
                    return false;

            return true;
        }
    }
}
