namespace NexusForever.SpellWorks.Services
{
    /// <summary>
    /// Detaches a pane into a real OS window - not an in-page floating group and not <c>window.open</c>,
    /// either of which costs a window handle, per-monitor placement and a taskbar entry.
    /// </summary>
    public interface IPopoutHost
    {
        int OpenCount { get; }

        /// <summary>Soft cap; each pop-out is a WebView2 instance costing some RAM.</summary>
        int Cap { get; }

        /// <summary>Open <paramref name="viewId"/> in its own window; returns its pane key, or null if capped.</summary>
        string Popout(string viewId);

        /// <summary>Close a pop-out and return its view to the main window.</summary>
        void Dock(string key);

        void Close(string key);

        /// <summary>Close every pop-out for shutdown, keeping each registered so the workspace saves it.</summary>
        void CloseAll();
    }

    /// <summary>
    /// The OS window one pop-out lives in, as much of it as <see cref="IPopoutHost"/> touches.
    /// </summary>
    /// <remarks>
    /// The host does the bookkeeping - the cap, the keys, and putting the workspace back when a window
    /// goes away - and none of that needs a real window. Naming the little that does, keeps the bookkeeping
    /// testable instead of hidden behind a WPF type.
    /// </remarks>
    public interface IPopoutWindow
    {
        double Left { get; set; }

        double Top { get; set; }

        /// <summary>Raised when the window is gone, however it went - the user's close button included.</summary>
        event EventHandler Closed;

        void Show();

        void Close();
    }

    public interface IPopoutWindowFactory
    {
        /// <summary>
        /// Where the cascade is measured from: the main window's top-left, or a sensible spot when there
        /// is no main window to ask.
        /// </summary>
        (double Left, double Top) Anchor { get; }

        IPopoutWindow Create(string paneKey, string viewId);
    }
}
