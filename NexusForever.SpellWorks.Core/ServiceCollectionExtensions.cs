using System.Reflection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Services;

namespace NexusForever.SpellWorks.Core
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Register the engine.
        /// </summary>
        /// <remarks>
        /// The I/O-bound implementations - the archive mounter and the drive probe
        /// </remarks>
        public static IServiceCollection AddSpellWorksCore(this IServiceCollection services)
        {
            services.TryAddMessenger();

            services.AddSingleton(TimeProvider.System);

            services.AddSingleton<IResourceService, ResourceService>();
            services.AddSingleton<IArchiveService, ArchiveService>();
            services.AddSingleton<ITextTableService, TextTableService>();
            services.AddSingleton<IGameTableService, GameTableService>();
            services.AddSingleton<ISpellTooltipParseService, SpellTooltipParseService>();

            services.AddSingleton<ISpellModelFilterService, SpellModelFilterService>();
            services.AddSingleton<ISpellModelService, SpellModelService>();
            services.AddSingleton<ITableCatalog, TableCatalog>();
            services.AddSingleton<IEngineHost, EngineHost>();
            services.AddSingleton<IInstallationProbe, InstallationProbe>();

            services.AddTransient<ISpellModel, SpellModel>();
            services.AddTransient<ISpellBaseModel, SpellBaseModel>();
            services.AddTransient<ISpellEffectModel, SpellEffectModel>();
            services.AddTransient<ISpellProcModel, SpellProcModel>();

            services.AddSpellEffectData();

            return services;
        }

        /// <summary>Register the implementations that read the real machine.</summary>
        public static IServiceCollection AddSpellWorksPlatform(this IServiceCollection services)
        {
            services.AddSingleton<IArchiveMounter, NexusArchiveMounter>();
            services.AddSingleton<IDriveProbe, DriveProbe>();

            return services;
        }

        /// <summary>
        /// Bind every <see cref="SpellEffectAttribute"/>-tagged effect projection against its effect type.
        /// </summary>
        public static IServiceCollection AddSpellEffectData(this IServiceCollection services)
        {
            foreach (Type type in typeof(ISpellEffectColumnData).Assembly.GetTypes())
            {
                var attribute = type.GetCustomAttribute<SpellEffectAttribute>();
                if (attribute == null)
                    continue;

                if (type.IsAssignableTo(typeof(ISpellEffectColumnData)))
                    services.AddKeyedTransient(typeof(ISpellEffectColumnData), attribute.Type, type);

                if (type.IsAssignableTo(typeof(ISpellEffectRowData)))
                    services.AddKeyedTransient(typeof(ISpellEffectRowData), attribute.Type, type);
            }

            return services;
        }

        private static void TryAddMessenger(this IServiceCollection services)
        {
            if (services.All(d => d.ServiceType != typeof(IMessenger)))
                services.AddSingleton<IMessenger, WeakReferenceMessenger>();
        }
    }
}
