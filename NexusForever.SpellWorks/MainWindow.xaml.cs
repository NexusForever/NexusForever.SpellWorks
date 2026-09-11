using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using NexusForever.SpellWorks.Components;
using NexusForever.SpellWorks.Services;

namespace NexusForever.SpellWorks
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public partial class MainWindow : Window
    {
        public MainWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();

            WindowWorkArea.Attach(this);

            WebView.Services = serviceProvider;
            WebView.RootComponents.Add(new RootComponent
            {
                Selector      = "#app",
                ComponentType = typeof(Shell),
                Parameters    = new Dictionary<string, object>
                {
                    [nameof(Shell.Bridge)] = new WindowBridge(this)
                }
            });

            // Quitting the main window quits the app, pop-outs included. Left to themselves they would outlive
            // it, and closing the last one by hand would drop it from the workspace before the save on exit
            // could record it.
            IPopoutHost popouts = serviceProvider.GetRequiredService<IPopoutHost>();
            Closing += (_, _) => popouts.CloseAll();
        }
    }
}
