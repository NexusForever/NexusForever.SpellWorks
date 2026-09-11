using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NexusForever.SpellWorks.Core;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Services
{
    /// <summary>
    /// The workspace registration, shared by the app and by tests.
    /// </summary>
    public class WorkspaceRegistrationTests
    {
        private static ServiceProvider Provider()
        {
            var services = new ServiceCollection();
            services.Configure<Core.Configuration.SpelllWorksConfiguration>(_ => { });
            services.AddSpellWorksCore();
            services.AddSingleton<IArchiveMounter>(new StubMounter());
            services.AddSingleton<IDriveProbe>(new StubDrives());
            services.AddSpellWorksWorkspace();
            services.AddSpellWorksWindowing();

            return services.BuildServiceProvider();
        }

        [Theory]
        [InlineData(typeof(WorkspaceState))]
        [InlineData(typeof(WorkspaceStore))]
        [InlineData(typeof(PaletteIndex))]
        [InlineData(typeof(RowSource))]
        public void Every_workspace_service_resolves(Type service)
        {
            Assert.NotNull(Provider().GetService(service));
        }

        [Fact]
        public void The_windowing_adapters_resolve_the_real_host_over_a_real_window_factory()
        {
            // The host is the covered half and the factory is the WPF half; the app has to get both, and
            // the host has to be the one that actually does the bookkeeping.
            ServiceProvider provider = Provider();

            Assert.IsType<PopoutHost>(provider.GetService<IPopoutHost>());
            Assert.IsType<WpfPopoutWindowFactory>(provider.GetService<IPopoutWindowFactory>());
            Assert.IsType<FolderPicker>(provider.GetService<IFolderPicker>());
        }

        [Fact]
        public void The_pop_out_host_is_shared_because_the_cap_counts_every_window()
        {
            ServiceProvider provider = Provider();

            Assert.Same(provider.GetService<IPopoutHost>(), provider.GetService<IPopoutHost>());
        }

        [Fact]
        public void The_workspace_is_shared_because_a_pop_out_must_see_the_same_one()
        {
            // A scoped registration in a BlazorWebView is per-window, which would give each pop-out its own.
            ServiceProvider provider = Provider();

            Assert.Same(provider.GetService<WorkspaceState>(), provider.GetService<WorkspaceState>());
            Assert.Same(provider.GetService<RowSource>(), provider.GetService<RowSource>());
        }

        [Fact]
        public void The_store_persists_the_state_it_was_registered_with()
        {
            ServiceProvider provider = Provider();

            Assert.NotNull(provider.GetRequiredService<WorkspaceStore>());
            Assert.Empty(provider.GetRequiredService<WorkspaceStore>().RestorablePopouts);
        }

        private sealed class StubMounter : IArchiveMounter
        {
            public bool Exists(string path) => false;

            public IArchiveReader Mount(string indexPath, string coreDataPath) => null;
        }

        private sealed class StubDrives : IDriveProbe
        {
            public IReadOnlyList<ProbedDrive> Drives() => [];

            public bool FileExists(string path) => false;
        }
    }
}
