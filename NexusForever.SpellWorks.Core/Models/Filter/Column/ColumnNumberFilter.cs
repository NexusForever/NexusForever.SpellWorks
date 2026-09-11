namespace NexusForever.SpellWorks.Core.Models.Filter.Column
{
    /// <summary>How a numeric column constraint compares.</summary>
    public enum NumberMatch
    {
        Equals,
        AtLeast,
        AtMost,
        MaskAll,
        MaskAny
    }

    /// <summary>
    /// One constraint on one numeric column of one game table row.
    /// </summary>
    /// <remarks>
    /// The row arrives as <see cref="object"/> because a column predicate has nothing to say about what
    /// owns the row - the same constraint on <c>Spell4Effects</c> is asked by the spell browser, the
    /// effects grid and the procs grid alike. <see cref="RowMatchFilter{T}"/> is what binds it to an
    /// element; this class only ever sees the row it was handed.
    ///
    /// <see cref="Read"/> is a compiled accessor from <c>ITableCatalog.Columns</c>, so evaluating one of
    /// these costs a delegate call rather than reflection.
    ///
    /// The mask readings exist because these columns carry packed bitfields as readily as numbers -
    /// <c>DataBits00</c> is a spell id in one effect type and a flag set in the next - so the column is
    /// offered both readings rather than the schema guessing which it is.
    /// </remarks>
    public sealed class ColumnNumberFilter : IModelFilter<object>
    {
        public required Func<object, double> Read { get; init; }

        public double Value { get; init; }

        public NumberMatch Match { get; init; }

        /// <summary>
        /// How close the column has to be to <see cref="Value"/> to count as equal to it.
        /// </summary>
        /// <remarks>
        /// A float column cannot hold the decimal that was typed, so an exact comparison is the one reading
        /// of "=" that can never be true. <see cref="NumberTolerance"/> says what the slack is worth; the
        /// setup exposes it, because how close is close enough belongs to the data rather than to the
        /// comparison.
        /// </remarks>
        public double Epsilon { get; init; } = NumberTolerance.Default;

        public bool Filter(object row)
        {
            if (row == null)
                return false;

            double actual = Read(row);

            return Match switch
            {
                NumberMatch.AtLeast => NumberTolerance.AtLeast(actual, Value, Epsilon),
                NumberMatch.AtMost  => NumberTolerance.AtMost(actual, Value, Epsilon),
                NumberMatch.MaskAll => Mask(actual, MaskMode.All),
                NumberMatch.MaskAny => Mask(actual, MaskMode.Any),
                _                   => NumberTolerance.Equal(actual, Value, Epsilon)
            };
        }

        /// <summary>
        /// A mask reading of a column read as a <see cref="double"/>. A value outside <see cref="uint"/> -
        /// a negative column, or one too large - has no bits to test and so satisfies no mask.
        /// </summary>
        private bool Mask(double actual, MaskMode mode)
        {
            if (actual < 0 || actual > uint.MaxValue || Value < 0 || Value > uint.MaxValue)
                return false;

            return mode.Matches((uint)actual, (uint)Value);
        }
    }
}
