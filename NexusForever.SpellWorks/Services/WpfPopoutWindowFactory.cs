using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace NexusForever.SpellWorks.Services
{
    /// <summary>
    /// Real <see cref="PopoutWindow"/>s, for <see cref="PopoutHost"/> to place and close.
    /// </summary>
    /// <remarks>
    /// Excluded from coverage: every member constructs or reads a live <see cref="Window"/>, which needs an
    /// interactive desktop session. What decides how many windows there may be, where each one lands and
    /// what happens to the workspace when one closes is <see cref="PopoutHost"/>, which is covered.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public sealed class WpfPopoutWindowFactory : IPopoutWindowFactory
    {
        #region Dependency Injection

        private readonly IServiceProvider _serviceProvider;

        public WpfPopoutWindowFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        #endregion

        public (double Left, double Top) Anchor
        {
            get
            {
                Window main = Application.Current?.MainWindow;
                return (main?.Left ?? 100, main?.Top ?? 100);
            }
        }

        public IPopoutWindow Create(string paneKey, string viewId) =>
            new WpfPopoutWindow(new PopoutWindow(_serviceProvider, paneKey, viewId));

        private sealed class WpfPopoutWindow(PopoutWindow window) : IPopoutWindow
        {
            public double Left
            {
                get => window.Left;
                set => window.Left = value;
            }

            public double Top
            {
                get => window.Top;
                set => window.Top = value;
            }

            public event EventHandler Closed
            {
                add    => window.Closed += value;
                remove => window.Closed -= value;
            }

            public void Show() => window.Show();

            public void Close() => window.Close();
        }
    }
}
