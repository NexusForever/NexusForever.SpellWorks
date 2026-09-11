using Microsoft.Extensions.DependencyInjection;
using NexusForever.SpellWorks.Services.Filtering;

namespace NexusForever.SpellWorks.Services
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Register the workspace. One registration path shared by the app and by tests.
        /// </summary>
        /// <remarks>
        /// Anything two windows must agree on is a singleton - a scoped registration in a BlazorWebView is
        /// per-window, which would silently give each pop-out its own copy.
        /// </remarks>
        public static IServiceCollection AddSpellWorksWorkspace(this IServiceCollection services)
        {
            services.AddSingleton<WorkspaceState>();

            // The preferences are the workspace's own object, resolved so that the services reading one of
            // them - the filter schemas and their float tolerance - see the live value rather than a copy.
            services.AddSingleton(provider => provider.GetRequiredService<WorkspaceState>().Preferences);
            services.AddSingleton<WorkspaceStore>();
            services.AddSingleton<PaletteIndex>();
            services.AddSingleton<FilterSchemaRegistry>();
            services.AddSingleton<RowSource>();

            return services;
        }

        /// <summary>
        /// Register the windowing adapters.
        /// </summary>
        /// <remarks>
        /// Separated from the workspace the way Core separates its platform services: everything here
        /// needs a live desktop, so a test registers its own <see cref="IPopoutWindowFactory"/> and gets
        /// the real <see cref="PopoutHost"/> over it rather than a stand-in for the host itself.
        /// </remarks>
        public static IServiceCollection AddSpellWorksWindowing(this IServiceCollection services)
        {
            services.AddSingleton<IPopoutHost, PopoutHost>();
            services.AddSingleton<IPopoutWindowFactory, WpfPopoutWindowFactory>();
            services.AddSingleton<IFolderPicker, FolderPicker>();

            return services;
        }
    }
}
