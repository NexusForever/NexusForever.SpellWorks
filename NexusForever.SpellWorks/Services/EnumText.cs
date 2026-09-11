namespace NexusForever.SpellWorks.Services
{
    /// <summary>
    /// Renders a game-table id as its enum name.
    /// </summary>
    /// <remarks>
    /// Game table columns are <c>uint</c> while the matching enums have assorted underlying types
    /// (<c>Class</c> is a <c>byte</c>), so the value is always boxed through <see cref="Enum.ToObject"/>
    /// rather than cast - a direct cast throws for any enum that is not <c>int</c>-backed.
    /// </remarks>
    public static class EnumText
    {
        public static string Name<T>(uint value) where T : struct, Enum
        {
            // ToObject truncates rather than overflowing, so a value too large for the underlying type
            // simply fails the IsDefined check below and falls back to the number.
            object boxed = Enum.ToObject(typeof(T), value);

            return Enum.IsDefined(typeof(T), boxed) ? boxed.ToString() : value.ToString();
        }

        public static string Name<T>(uint? value) where T : struct, Enum
        {
            return value.HasValue ? Name<T>(value.Value) : "";
        }

        /// <summary>
        /// The named bits of a <c>[Flags]</c> enum, in declaration order, as (name, value) pairs.
        /// </summary>
        /// <remarks>
        /// The zero member is skipped: it names the absence of every flag, so offering it as something to
        /// tick would be offering a no-op. This is what lets a mask field render a picker rather than
        /// demanding the user type <c>0x06</c>.
        /// </remarks>
        public static IReadOnlyList<(string Name, uint Value)> Bits<T>() where T : struct, Enum
        {
            List<(string, uint)> bits = [];

            foreach (T member in Enum.GetValues<T>())
            {
                uint value = Convert.ToUInt32(member);
                if (value != 0)
                    bits.Add((member.ToString(), value));
            }

            return bits;
        }

        /// <summary>
        /// Decompose <paramref name="value"/> into the names of the bits it sets, plus whatever is left over.
        /// </summary>
        /// <remarks>
        /// Leftover bits are reported as hex rather than dropped - the client data carries bits no enum here
        /// names yet, and silently hiding them would make the picker lie about what the filter matches.
        /// </remarks>
        public static string Flags<T>(uint value) where T : struct, Enum
        {
            if (value == 0)
                return "None";

            List<string> names = [];
            uint remaining = value;

            foreach ((string name, uint bit) in Bits<T>())
            {
                if ((remaining & bit) != bit)
                    continue;

                names.Add(name);
                remaining &= ~bit;
            }

            if (remaining != 0)
                names.Add($"0x{remaining:X}");

            return string.Join(" | ", names);
        }
    }
}
