namespace NexusForever.SpellWorks.Core.Models.Filter
{
    /// <summary>
    /// How close two numbers have to be before a filter calls them equal.
    /// </summary>
    /// <remarks>
    /// Every numeric constraint compares in <see cref="double"/>, while the columns behind them are mostly
    /// single-precision: <c>0.1f</c> widens to 0.100000001490116…, so a bare <c>==</c> against the 0.1 the
    /// user typed is false for reasons that have nothing to do with the data. The same slip makes an
    /// inclusive threshold exclusive at exactly the bound that was typed, which is the one value a user
    /// picks deliberately.
    ///
    /// The tolerance is <em>relative</em>, with an absolute floor of itself: the error in a float grows with
    /// its magnitude - around 0.008 at 123456.78 - so a fixed absolute epsilon small enough to be honest
    /// about 0.1 cannot cover a large column at all. <see cref="Default"/> sits an order of magnitude above
    /// float's own precision (about 1.2e-7), so it absorbs the conversion and nothing else.
    ///
    /// Whole-number columns are unaffected in practice: every integer a <c>uint</c> column can hold is
    /// exact in a double, and the slack around it is far narrower than the gap to the next integer.
    /// </remarks>
    public static class NumberTolerance
    {
        /// <summary>The epsilon a filter uses unless it is given another.</summary>
        public const double Default = 1e-6;

        public static bool Equal(double actual, double value, double epsilon) =>
            Math.Abs(actual - value) <= Slack(actual, value, epsilon);

        /// <summary>As <c>actual &gt;= value</c>, with a value on the bound counted as on it.</summary>
        public static bool AtLeast(double actual, double value, double epsilon) =>
            actual >= value - Slack(actual, value, epsilon);

        /// <summary>As <c>actual &lt;= value</c>, with a value on the bound counted as on it.</summary>
        public static bool AtMost(double actual, double value, double epsilon) =>
            actual <= value + Slack(actual, value, epsilon);

        /// <summary>
        /// How far apart the two may be and still count as equal.
        /// </summary>
        /// <remarks>
        /// A negative or unreadable epsilon reads as none rather than as an error: it arrives from a box in
        /// the setup and from a file anybody can hand-edit, and a comparison that answered everything would
        /// be far worse than one that answers only exact matches.
        /// </remarks>
        private static double Slack(double actual, double value, double epsilon)
        {
            if (double.IsNaN(epsilon) || epsilon <= 0)
                return 0;

            double magnitude = Math.Max(Math.Abs(actual), Math.Abs(value));

            return double.IsInfinity(magnitude) || double.IsNaN(magnitude)
                ? epsilon
                : epsilon * Math.Max(1d, magnitude);
        }
    }
}
