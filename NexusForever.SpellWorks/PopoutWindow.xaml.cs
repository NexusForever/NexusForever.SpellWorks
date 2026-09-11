using System.Windows;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using NexusForever.SpellWorks.Components;
using NexusForever.SpellWorks.Services;

namespace NexusForever.SpellWorks
{
    /// <summary>
    /// One popped-out pane: its own OS window, its own BlazorWebView, its own root component.
    /// App-wide state still comes from the shared singleton container.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public partial class PopoutWindow : Window
    {
        public PopoutWindow(IServiceProvider serviceProvider, string paneKey, string viewId)
        {
            InitializeComponent();

            WindowWorkArea.Attach(this);

            WebView.Services = serviceProvider;
            WebView.RootComponents.Add(new RootComponent
            {
                Selector      = "#app",
                ComponentType = typeof(PopoutRoot),
                Parameters    = new Dictionary<string, object>
                {
                    [nameof(PopoutRoot.Bridge)]  = new WindowBridge(this),
                    [nameof(PopoutRoot.PaneKey)] = paneKey,
                    [nameof(PopoutRoot.ViewId)]  = viewId
                }
            });
        }
    }
}
