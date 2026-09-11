using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NexusForever.SpellWorks.Services
{
    /// <summary>
    /// Keeps a borderless window's maximized size inside the monitor work area.
    /// </summary>
    /// <remarks>
    /// A <c>WindowStyle=None</c> window is maximized by the shell to the full monitor rectangle rather than
    /// the work area, so it covers the taskbar and tears against it. This answers
    /// <c>WM_GETMINMAXINFO</c> ourselves with the work area of whichever monitor the window is currently on.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public static class WindowWorkArea
    {
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int MONITOR_DEFAULTTONEAREST = 0x0002;

        private const int ABM_GETSTATE = 0x0004;
        private const int ABM_GETTASKBARPOS = 0x0005;
        private const int ABS_AUTOHIDE = 0x0001;

        private const int ABE_LEFT = 0;
        private const int ABE_TOP = 1;
        private const int ABE_RIGHT = 2;
        private const int ABE_BOTTOM = 3;

        /// <summary>Attach the clamp to <paramref name="window"/>. Safe to call before the handle exists.</summary>
        public static void Attach(Window window)
        {
            if (window == null)
                return;

            var helper = new WindowInteropHelper(window);
            if (helper.Handle != IntPtr.Zero)
            {
                Hook(helper.Handle);
                return;
            }

            window.SourceInitialized += (_, _) => Hook(new WindowInteropHelper(window).Handle);
        }

        private static void Hook(IntPtr handle)
        {
            HwndSource source = HwndSource.FromHwnd(handle);
            source?.AddHook(WndProc);
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_GETMINMAXINFO)
                return IntPtr.Zero;

            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
                return IntPtr.Zero;

            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref info))
                return IntPtr.Zero;

            MINMAXINFO minMax = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            // Both are monitor-relative, which is what the shell expects here even on secondary displays.
            (int x, int y, int width, int height) = MaximisedBounds.For(
                Rect(info.rcMonitor), Rect(info.rcWork), AutoHideEdge(info.rcMonitor));

            minMax.ptMaxPosition.x = x;
            minMax.ptMaxPosition.y = y;
            minMax.ptMaxSize.x     = width;
            minMax.ptMaxSize.y     = height;

            Marshal.StructureToPtr(minMax, lParam, true);

            handled = true;
            return IntPtr.Zero;
        }

        private static ScreenRect Rect(RECT rect) => new(rect.left, rect.top, rect.right, rect.bottom);

        /// <summary>
        /// The edge an auto-hiding taskbar occupies on this monitor, or <c>null</c> when there is not one.
        /// </summary>
        private static TaskbarEdge? AutoHideEdge(RECT monitor)
        {
            var data = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>() };
            if ((SHAppBarMessage(ABM_GETSTATE, ref data).ToInt32() & ABS_AUTOHIDE) == 0)
                return null;

            if (SHAppBarMessage(ABM_GETTASKBARPOS, ref data) == IntPtr.Zero)
                return null;

            if (!MaximisedBounds.Overlaps(Rect(monitor), Rect(data.rc)))
                return null;

            return data.uEdge switch
            {
                ABE_LEFT   => TaskbarEdge.Left,
                ABE_TOP    => TaskbarEdge.Top,
                ABE_RIGHT  => TaskbarEdge.Right,
                ABE_BOTTOM => TaskbarEdge.Bottom,
                _          => null
            };
        }

        #region Native

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public int uEdge;
            public RECT rc;
            public int lParam;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHAppBarMessage(int message, ref APPBARDATA data);

        #endregion
    }
}
