
namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// The unconstrained filter. What an empty query compiles to, so callers never special-case null.
    /// </summary>
    public sealed class MatchAllFilter<T> : IModelFilter<T>
    {
        public static readonly MatchAllFilter<T> Instance = new();

        private MatchAllFilter()
        {
        }

        public bool Filter(T model) => true;
    }
}
