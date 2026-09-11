using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusForever.SpellWorks.Core.Configuration;
using NexusForever.SpellWorks.Core;
using NexusForever.SpellWorks.Services;

namespace NexusForever.SpellWorks
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public partial class App : Application
    {
        private IConfiguration _configuration;
        private IServiceProvider _serviceProvider;
        private string _configurationError;

        public App()
        {
            (_configuration, _configurationError) = ConfigurationLoader.Load(AppContext.BaseDirectory);

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.Configure<SpelllWorksConfiguration>(_configuration);

            // A WebView has no console, so Blazor's own warnings and failures go to a file next to the exe.
            services.AddLogging(builder => builder
                .SetMinimumLevel(LogLevel.Warning)
                .AddProvider(new FileLoggerProvider(Path.Combine(AppContext.BaseDirectory, "SpellWorks.log"))));

            services.AddWpfBlazorWebView();
#if DEBUG
            services.AddBlazorWebViewDeveloperTools();
#endif

            services.AddSpellWorksCore();
            services.AddSpellWorksPlatform();

            services.AddSpellWorksWorkspace();
            services.AddSpellWorksWindowing();

            services.AddTransient<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // A slip in one handler should not take the whole inspector down with it; it is logged instead.
            DispatcherUnhandledException += (_, args) =>
            {
                FileLoggerProvider.Write(Path.Combine(AppContext.BaseDirectory, "SpellWorks.log"), "UNHANDLED " + args.Exception);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                FileLoggerProvider.Write(Path.Combine(AppContext.BaseDirectory, "SpellWorks.log"), "FATAL " + args.ExceptionObject);

            // One shared user data folder, so every BlazorWebView reuses a single browser and GPU process.
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER",
                Path.Combine(Path.GetTempPath(), "NexusForever.SpellWorks", "WebView2"));

            if (_configurationError != null)
            {
                FileLoggerProvider.Write(Path.Combine(AppContext.BaseDirectory, "SpellWorks.log"), _configurationError);
                _serviceProvider.GetRequiredService<WorkspaceState>().ConfigurationError = _configurationError;
            }

            _serviceProvider.GetRequiredService<WorkspaceStore>().Load();

            MainWindow mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider.GetRequiredService<WorkspaceStore>().Save();
            base.OnExit(e);
        }
    }
}
