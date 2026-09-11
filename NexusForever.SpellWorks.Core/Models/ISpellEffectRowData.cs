using NexusForever.GameTable.Model;

namespace NexusForever.SpellWorks.Core.Models
{
    public interface ISpellEffectRowData
    {
        Spell4EffectsEntry Entry { get; set; }

        string Data00 { get; }
        string Data01 { get; }
        string Data02 { get; }
        string Data03 { get; }
        string Data04 { get; }
        string Data05 { get; }
        string Data06 { get; }
        string Data07 { get; }
        string Data08 { get; }
        string Data09 { get; }

        /// <summary>
        /// The Data column indices whose value is a Spell4 id the reader can follow, in column order.
        /// Empty for effect types that cross-reference nothing.
        /// </summary>
        IReadOnlyList<int> HyperlinkedDataIndices { get; }

        /// <summary>The raw value behind a Data column, by index, for following a cross-reference.</summary>
        uint DataBits(int index);
    }
}