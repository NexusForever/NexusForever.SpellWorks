using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NexusForever.SpellWorks.Core.Configuration;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Services;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test
{
    /// <summary>
    /// The engine's registration. One shared path so the app and the tests cannot drift apart.
    /// </summary>
    public class ServiceCollectionExtensionsTests
    {
        private static ServiceProvider Provider(bool platform = true)
        {
            var services = new ServiceCollection();
            services.Configure<SpelllWorksConfiguration>(_ => { });
            services.AddSpellWorksCore();

            if (platform)
                services.AddSpellWorksPlatform();

            return services.BuildServiceProvider();
        }

        [Theory]
        [InlineData(typeof(IResourceService))]
        [InlineData(typeof(IArchiveService))]
        [InlineData(typeof(ITextTableService))]
        [InlineData(typeof(IGameTableService))]
        [InlineData(typeof(ISpellTooltipParseService))]
        [InlineData(typeof(ISpellModelFilterService))]
        [InlineData(typeof(ISpellModelService))]
        [InlineData(typeof(ITableCatalog))]
        [InlineData(typeof(IEngineHost))]
        [InlineData(typeof(IInstallationProbe))]
        [InlineData(typeof(IMessenger))]
        [InlineData(typeof(TimeProvider))]
        public void Every_engine_service_resolves(Type service)
        {
            Assert.NotNull(Provider().GetService(service));
        }

        [Fact]
        public void The_engine_services_are_shared_because_two_windows_must_agree()
        {
            ServiceProvider provider = Provider();

            Assert.Same(provider.GetService<IEngineHost>(), provider.GetService<IEngineHost>());
            Assert.Same(provider.GetService<ITableCatalog>(), provider.GetService<ITableCatalog>());
        }

        [Theory]
        [InlineData(typeof(ISpellModel))]
        [InlineData(typeof(ISpellBaseModel))]
        [InlineData(typeof(ISpellEffectModel))]
        [InlineData(typeof(ISpellProcModel))]
        public void A_model_is_handed_out_fresh_each_time_because_each_wraps_one_row(Type model)
        {
            ServiceProvider provider = Provider();

            Assert.NotNull(provider.GetService(model));
            Assert.NotSame(provider.GetService(model), provider.GetService(model));
        }

        [Fact]
        public void The_platform_registrations_are_what_read_the_real_machine()
        {
            // Without them the engine has no way to reach a disk, which is what lets a test substitute one.
            ServiceProvider withPlatform = Provider();
            Assert.NotNull(withPlatform.GetService<IArchiveMounter>());
            Assert.NotNull(withPlatform.GetService<IDriveProbe>());

            ServiceProvider without = Provider(platform: false);
            Assert.Null(without.GetService<IArchiveMounter>());
            Assert.Null(without.GetService<IDriveProbe>());
        }

        [Fact]
        public void A_messenger_registered_by_the_host_is_left_alone()
        {
            var messenger = new WeakReferenceMessenger();

            var services = new ServiceCollection();
            services.Configure<SpelllWorksConfiguration>(_ => { });
            services.AddSingleton<IMessenger>(messenger);
            services.AddSpellWorksCore();

            Assert.Same(messenger, services.BuildServiceProvider().GetService<IMessenger>());
        }

        [Fact]
        public void The_engine_can_be_built_and_mounted_without_touching_a_disk()
        {
            // The whole point of the seams: swap the two platform services and the engine runs anywhere.
            var services = new ServiceCollection();
            services.Configure<SpelllWorksConfiguration>(o => o.PatchPath = @"C:\WildStar\Patch");
            services.AddSpellWorksCore();
            services.AddSingleton<IArchiveMounter>(new Testing.FakeArchiveMounter()
                .With(Path.Combine(@"C:\WildStar\Patch", "ClientData.index")));
            services.AddSingleton<IDriveProbe>(new Testing.FakeDriveProbe());

            ServiceProvider provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetRequiredService<IEngineHost>());
            Assert.Equal(EngineState.Idle, provider.GetRequiredService<IEngineHost>().State);
        }

        [Fact]
        public void The_configured_patch_path_reaches_the_archive_service()
        {
            var services = new ServiceCollection();
            services.Configure<SpelllWorksConfiguration>(o => o.PatchPath = @"D:\Configured");
            services.AddSpellWorksCore();
            services.AddSpellWorksPlatform();

            Assert.Equal(@"D:\Configured", services.BuildServiceProvider()
                .GetRequiredService<IArchiveService>().PatchPath);
        }

        [Fact]
        public void The_configuration_holds_the_patch_path()
        {
            var configuration = new SpelllWorksConfiguration { PatchPath = @"C:\Patch" };

            Assert.Equal(@"C:\Patch", Options.Create(configuration).Value.PatchPath);
        }
    }
}
