namespace NexusForever.SpellWorks.Core.Models
{
    /// <summary>
    /// One constraint over one element. Every pane's filtering is built from these, whatever it lists.
    /// </summary>
    /// <remarks>
    /// Generic because the grids are heterogeneous - spells, effects, procs, effect-type usages, table
    /// descriptors and raw cell arrays all get filtered, and only two of those are <see cref="ISpellModel"/>.
    /// Keeping one interface is what lets the composites in <c>Models.Filter</c> compose all of them.
    /// </remarks>
    public interface IModelFilter<in T>
    {
        bool Filter(T model);
    }

    /// <summary>
    /// A constraint over a spell. Carries no members of its own - it exists so the spell filters stay a
    /// named, closed family, which is what <c>ISpellModelFilterService</c> takes.
    /// </summary>
    public interface ISpellModelFilter : IModelFilter<ISpellModel>
    {
    }
}
