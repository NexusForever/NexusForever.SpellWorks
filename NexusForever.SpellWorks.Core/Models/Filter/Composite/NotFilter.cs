
namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// Negation of one child. A condition with its <c>!</c> toggle set compiles to this.
    /// </summary>
    public sealed class NotFilter<T> : IModelFilter<T>
    {
        public IModelFilter<T> Inner { get; }

        public NotFilter(IModelFilter<T> inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool Filter(T model) => !Inner.Filter(model);
    }
}
