namespace NexusForever.SpellWorks.Services.Filtering
{
    /// <summary>
    /// Parsing helpers shared by every schema factory. Deliberately tolerant: a value that does not parse is
    /// a constraint the compiler drops and the form marks, never an exception.
    /// </summary>
    public static class FilterValue
    {
        public static bool TryEnum<T>(string value, out T result) where T : struct, Enum
        {
            result = default;

            if (string.IsNullOrWhiteSpace(value) || value == FilterFieldSchema.Any)
                return false;

            return Enum.TryParse(value.Trim(), out result);
        }

        /// <summary>
        /// Parse a bitmask or a plain number. Accepts decimal or <c>0x</c>-prefixed hex, as a mask field should.
        /// </summary>
        public static bool TryUInt(string value, out uint result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, null, out result);

            return uint.TryParse(value, out result);
        }

        /// <summary>
        /// Parse a threshold. Decimals are accepted and always read in the invariant culture, because a
        /// value typed here can end up written to <c>Workspace.json</c> and read back on another machine.
        /// </summary>
        public static bool TryNumber(string value, out double result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return double.TryParse(value.Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out result);
        }

        public static string Trimmed(string value) => value?.Trim() ?? "";

        /// <summary>
        /// The mask mode a condition asks for. <see cref="FilterOperator.MaskAny"/> means one bit is enough;
        /// anything else is an all-bits rule.
        /// </summary>
        public static Core.Models.Filter.MaskMode MaskMode(FilterCondition condition) =>
            condition.Operator == FilterOperator.MaskAny
                ? Core.Models.Filter.MaskMode.Any
                : Core.Models.Filter.MaskMode.All;
    }
}
