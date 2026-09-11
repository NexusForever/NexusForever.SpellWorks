namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// How a bitmask constraint is satisfied.
    /// </summary>
    public enum MaskMode
    {
        /// <summary>Every bit in the mask must be set.</summary>
        All,

        /// <summary>At least one bit in the mask must be set.</summary>
        Any
    }

    public static class MaskModeExtensions
    {
        /// <summary>
        /// Whether <paramref name="value"/> satisfies <paramref name="mask"/>. An empty mask is no constraint.
        /// </summary>
        public static bool Matches(this MaskMode mode, uint value, uint mask)
        {
            if (mask == 0)
                return true;

            return mode == MaskMode.All ? (value & mask) == mask : (value & mask) != 0;
        }
    }
}
