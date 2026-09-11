using NexusForever.SpellWorks.Core.Models;

namespace NexusForever.SpellWorks.Services.Filtering
{
    /// <summary>
    /// Which control the form draws for a field.
    /// </summary>
    public enum FilterControlKind
    {
        Text,
        Choice,

        /// <summary>A bare on/off constraint - no value, only presence and polarity.</summary>
        Toggle,

        /// <summary>A bitmask, entered as decimal or <c>0x</c> hex.</summary>
        Mask,

        /// <summary>A bitmask whose bits have names, picked rather than typed.</summary>
        Flags
    }

    /// <summary>
    /// One filterable field of one pane: everything the form needs to draw it and the compiler needs to
    /// turn a condition on it into a Core filter.
    /// </summary>
    /// <remarks>
    /// Split into an untyped base and a typed subclass because the form is element-agnostic - it renders
    /// labels and controls and never sees an <c>ISpellModel</c> - while the factory necessarily is not.
    /// The schema is a table of constructors: no predicate logic lives here, and none in Razor.
    /// </remarks>
    public abstract class FilterFieldSchema
    {
        /// <summary>The sentinel a choice field shows for "no constraint".</summary>
        public const string Any = "Any";

        /// <summary>Stable key, written into <c>Workspace.json</c>.</summary>
        public required string Key { get; init; }

        public required string Label { get; init; }

        /// <summary>
        /// The label with enough context to stand on its own, away from the card that titles it.
        /// </summary>
        /// <remarks>
        /// A card's title says which row its fields belong to, so inside one a bare name is unambiguous.
        /// The chips row and the promoted card have no such heading - "databits00" there could be any of
        /// three tables - so both read this instead.
        /// </remarks>
        public virtual string QualifiedLabel => Label;

        /// <summary>
        /// Whether switching this toggle on means "hide these" rather than "show only these".
        /// </summary>
        /// <remarks>
        /// Every toggle is phrased positively - <c>Deprecated</c> selects deprecated spells - because a
        /// filter that reads as its own negation cannot be chipped or negated readably. But the useful
        /// reading of both housekeeping toggles is the negative one, so the form seeds them negated and
        /// the user flips the <c>!</c> to get the other. Which toggles those are is the schema's to say,
        /// rather than a key comparison in Razor that would have to grow with every such field.
        /// </remarks>
        public bool SeedNegated { get; init; }

        /// <summary>The titled card this field sits under in the form.</summary>
        public required string GroupTitle { get; init; }

        public FilterControlKind Control { get; init; } = FilterControlKind.Text;

        public string Placeholder { get; init; }

        /// <summary>Choice options, <see cref="Any"/> first. Null for every other control.</summary>
        public IReadOnlyList<string> Options { get; init; }

        /// <summary>
        /// The named bits a <see cref="FilterControlKind.Flags"/> field offers, in declaration order. Null
        /// for every other control.
        /// </summary>
        /// <remarks>
        /// The condition still stores the assembled number, so a picked mask and a typed one persist and
        /// compile identically - the picker is a way of writing the value, not a different kind of value.
        /// </remarks>
        public IReadOnlyList<(string Name, uint Value)> Bits { get; init; }

        public IReadOnlyList<FilterOperator> AllowedOperators { get; init; } = [FilterOperator.Equals];

        /// <summary>
        /// What a condition on this field gets when it carries no operator, or one the field no longer
        /// offers. Coercing rather than dropping preserves as much of a stale saved condition as it can.
        /// </summary>
        public FilterOperator DefaultOperator => AllowedOperators[0];

        /// <summary>
        /// Whether the condition asks for nothing - an empty box, or a choice still on <see cref="Any"/>.
        /// Blank is not invalid: it is an untouched control, and it compiles away silently.
        /// </summary>
        public bool IsBlank(FilterCondition condition)
        {
            if (Control == FilterControlKind.Toggle)
                return false;

            return string.IsNullOrWhiteSpace(condition.Value) || condition.Value == Any;
        }

        /// <summary>
        /// Whether the condition would compile. False only for a value the field cannot parse - the form
        /// marks those rather than discarding them, so the user can see and fix what they typed.
        /// </summary>
        public bool IsValid(FilterCondition condition) => IsBlank(condition) || Build(condition) != null;

        /// <summary>The compiled filter, or null when the value does not parse.</summary>
        protected abstract object Build(FilterCondition condition);
    }

    /// <summary>
    /// One column of one linked game table row, offered by a flex card rather than written out by hand.
    /// </summary>
    /// <remarks>
    /// Untyped in the element, unlike <see cref="FilterFieldSchema{T}"/>, and deliberately so: a constraint
    /// on a column of a <c>Spell4Effects</c> row has nothing to say about what owns that row, so the very
    /// same field objects serve the spell browser, the effects grid and the procs grid. What differs
    /// between those panes is only how the rows are reached, which is the flex source's job.
    ///
    /// <see cref="Source"/> is also what the compiler groups by: every condition on one source within one
    /// filter group is answered by one row, so a block asking for two things asks them of the same effect.
    /// </remarks>
    public sealed class FilterColumnFieldSchema : FilterFieldSchema
    {
        /// <summary>The flex source this column belongs to - <c>"effects"</c>, <c>"base"</c>, …</summary>
        public required string Source { get; init; }

        /// <summary>The linked row's name - <c>"Effects"</c>, <c>"Base"</c>. Titles the card and the label.</summary>
        public required string SourceName { get; init; }

        /// <summary>Which linked row this column belongs to, said in full.</summary>
        public override string QualifiedLabel => $"{SourceName} · {Label}";

        /// <summary>
        /// Turns one condition into one constraint on the row, or returns null when the value does not
        /// parse. Never applies <see cref="FilterCondition.Negate"/> - polarity is the compiler's job.
        /// </summary>
        public required Func<FilterCondition, IModelFilter<object>> RowFactory { get; init; }

        protected override object Build(FilterCondition condition) => RowFactory(condition);
    }

    /// <summary>
    /// A field of a pane listing <typeparamref name="T"/>.
    /// </summary>
    public sealed class FilterFieldSchema<T> : FilterFieldSchema
    {
        /// <summary>
        /// Turns one condition into one Core filter, or returns null when the value does not parse. Never
        /// applies <see cref="FilterCondition.Negate"/> - polarity is the compiler's job, so that every
        /// field gets it identically.
        /// </summary>
        public required Func<FilterCondition, IModelFilter<T>> Factory { get; init; }

        protected override object Build(FilterCondition condition) => Factory(condition);
    }
}
