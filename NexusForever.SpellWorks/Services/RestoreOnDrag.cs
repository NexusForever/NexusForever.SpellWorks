namespace NexusForever.SpellWorks.Services
{
    /// <summary>
    /// Where a maximized window should land when the user drags its title bar to un-maximize it.
    /// </summary>
    /// <remarks>
    /// Restoring on its own drops the window back at the bounds it had before it was maximized, which are
    /// usually nowhere near the cursor - so the window jumps out from under the pointer and the drag that
    /// follows starts from the wrong place. The shell's own chrome instead keeps the grab point on the
    /// title bar: the same fraction along it horizontally, and the same offset from the top.
    ///
    /// The arithmetic is separated from <see cref="WindowBridge"/> because that needs a live window and a
    /// live mouse, while this is what actually decides where the window ends up.
    /// </remarks>
    public static class RestoreOnDrag
    {
        /// <summary>
        /// The top-left the restored window should take, in the same units as the inputs.
        /// </summary>
        /// <param name="windowOrigin">Top-left of the window as it is now, maximized, in screen space.</param>
        /// <param name="grab">Where the cursor sits inside that window.</param>
        /// <param name="maximisedWidth">Width of the window as it is now.</param>
        /// <param name="restoredWidth">Width it will have once restored.</param>
        public static (double Left, double Top) Origin(
            (double X, double Y) windowOrigin,
            (double X, double Y) grab,
            double maximisedWidth,
            double restoredWidth)
        {
            // A window with no width to speak of gives no meaningful grab fraction; centre on the cursor.
            double fraction = maximisedWidth > 0
                ? Math.Clamp(grab.X / maximisedWidth, 0d, 1d)
                : 0.5d;

            double left = windowOrigin.X + grab.X - fraction * restoredWidth;

            // The vertical offset is kept as it is, so the cursor stays on the title bar rather than
            // landing below it - which is what would let go of the drag.
            return (left, windowOrigin.Y);
        }
    }
}
