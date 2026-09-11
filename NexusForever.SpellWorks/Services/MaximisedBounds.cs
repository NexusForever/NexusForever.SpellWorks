namespace NexusForever.SpellWorks.Services
{
    /// <summary>A rectangle in screen pixels.</summary>
    public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    /// <summary>Which edge of a monitor an auto-hiding taskbar sits on.</summary>
    public enum TaskbarEdge
    {
        Left,
        Top,
        Right,
        Bottom
    }

    /// <summary>
    /// Where a borderless window should sit when maximized.
    /// </summary>
    /// <remarks>
    /// The arithmetic is separated from the message hook that feeds it, because the hook needs a real HWND
    /// and a real monitor while the arithmetic is what actually decides whether the taskbar stays visible.
    /// </remarks>
    public static class MaximisedBounds
    {
        /// <summary>
        /// The position and size to report for <c>WM_GETMINMAXINFO</c>, in coordinates relative to
        /// <paramref name="monitor"/>.
        /// </summary>
        /// <param name="autoHideEdge">
        /// The edge an auto-hiding taskbar occupies, or <c>null</c> when the taskbar does not auto-hide.
        /// </param>
        public static (int X, int Y, int Width, int Height) For(ScreenRect monitor, ScreenRect work, TaskbarEdge? autoHideEdge)
        {
            ScreenRect reserved = Reserve(work, autoHideEdge);

            return (reserved.Left - monitor.Left,
                    reserved.Top - monitor.Top,
                    reserved.Width,
                    reserved.Height);
        }

        /// <summary>
        /// An auto-hiding taskbar only reappears while some pixel of its edge is uncovered, and the work
        /// area does not account for it - so give that edge a pixel back.
        /// </summary>
        public static ScreenRect Reserve(ScreenRect work, TaskbarEdge? edge)
        {
            return edge switch
            {
                TaskbarEdge.Left   => work with { Left = work.Left + 1 },
                TaskbarEdge.Top    => work with { Top = work.Top + 1 },
                TaskbarEdge.Right  => work with { Right = work.Right - 1 },
                TaskbarEdge.Bottom => work with { Bottom = work.Bottom - 1 },
                _                  => work
            };
        }

        /// <summary>Whether a taskbar rectangle overlaps the monitor it would steal a pixel from.</summary>
        public static bool Overlaps(ScreenRect monitor, ScreenRect taskbar)
        {
            return taskbar.Right > monitor.Left
                && taskbar.Left < monitor.Right
                && taskbar.Bottom > monitor.Top
                && taskbar.Top < monitor.Bottom;
        }
    }
}
