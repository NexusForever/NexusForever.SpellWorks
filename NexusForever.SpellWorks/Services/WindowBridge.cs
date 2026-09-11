using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace NexusForever.SpellWorks.Services
{
    /// <summary>
    /// <see cref="IWindowBridge"/> over a real WPF window. Passed to the root component as a parameter,
    /// because a BlazorWebView's DI scope is per-window but has no way to reach the <see cref="Window"/>.
    /// </summary>
    /// <remarks>
    /// Excluded from coverage: every member drives a live <see cref="Window"/> or reads the global mouse
    /// state, neither of which exists without an interactive desktop session. Components depend on
    /// <see cref="IWindowBridge"/>, so their window-chrome bindings are covered against a test double.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public sealed class WindowBridge(Window window) : IWindowBridge
    {
        public Window Window { get; } = window;

        public void BeginDrag()
        {
            // WebView2 forwards the pointer event asynchronously, so by the time this runs the button may
            // already be up - DragMove throws in that case.
            if (Mouse.LeftButton != MouseButtonState.Pressed)
                return;

            if (Window.WindowState == WindowState.Maximized)
                RestoreUnderCursor();

            try
            {
                Window.DragMove();
            }
            catch (InvalidOperationException)
            {
            }
        }

        /// <summary>
        /// Un-maximize while keeping the title bar under the pointer, the way the shell's own chrome does.
        /// A plain restore drops the window back at its old bounds, leaving the cursor holding nothing.
        /// </summary>
        private void RestoreUnderCursor()
        {
            System.Windows.Point grab = Mouse.GetPosition(Window);

            // Left/Top are not updated while a window is maximized - they still describe the restore
            // bounds - so the maximized origin has to come from the visual itself.
            System.Windows.Point origin = ScreenOrigin();

            double maximisedWidth = Window.ActualWidth;
            double restoredWidth = Window.RestoreBounds.Width > 0 ? Window.RestoreBounds.Width : Window.Width;

            Window.WindowState = WindowState.Normal;

            if (double.IsNaN(restoredWidth) || restoredWidth <= 0)
                return;

            (double left, double top) = RestoreOnDrag.Origin(
                (origin.X, origin.Y), (grab.X, grab.Y), maximisedWidth, restoredWidth);

            Window.Left = left;
            Window.Top  = top;
        }

        /// <summary>The window's top-left in device-independent screen coordinates.</summary>
        private System.Windows.Point ScreenOrigin()
        {
            System.Windows.Point device = Window.PointToScreen(new System.Windows.Point(0, 0));

            PresentationSource source = PresentationSource.FromVisual(Window);
            return source == null ? device : source.CompositionTarget.TransformFromDevice.Transform(device);
        }

        public void ToggleMaximize()
        {
            Window.WindowState = Window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        public void Minimize() => Window.WindowState = WindowState.Minimized;

        public void Close() => Window.Close();
    }

    /// <summary>
    /// The real folder browser.
    /// </summary>
    /// <remarks>
    /// Excluded from coverage: <see cref="Microsoft.Win32.OpenFolderDialog.ShowDialog()"/> is modal and
    /// blocks on user input, so it cannot run unattended. Callers depend on <see cref="IFolderPicker"/>.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public sealed class FolderPicker : IFolderPicker
    {
        public string Pick(string title, string initialDirectory)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title            = title,
                InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : null
            };

            return dialog.ShowDialog() == true ? dialog.FolderName : null;
        }
    }
}
