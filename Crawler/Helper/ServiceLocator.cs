using Helix.Crawler.Abstractions;
using Helix.Persistence;
using Helix.Persistence.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Helix.Crawler
{
    static class ServiceLocator
    {
        static ServiceProvider _serviceProvider;

        public static void Dispose()
        {
            _serviceProvider?.Dispose();
            _serviceProvider = null;
        }

        public static TService Get<TService>() { return _serviceProvider.GetService<TService>(); }

        public static void RegisterServices(Configurations configurations)
        {
            if (_serviceProvider?.GetService<Configurations>() != null) return;
            _serviceProvider?.Dispose();
            _serviceProvider = new ServiceCollection()
                .AddTransient<IWebBrowser, ChromiumWebBrowser>()
                .AddTransient<IRawResourceExtractor, RawResourceExtractor>()
                .AddTransient<IRawResourceVerifier, RawResourceVerifier>()
                .AddTransient<IRawResourceProcessor, RawResourceProcessor>()
                .AddTransient<IResourceScope, ResourceScope>()
                .AddTransient<IPersistenceProvider, PersistenceProvider>()
                .AddSingleton<ILogger, Logger>()
                .AddSingleton<IReportWriter, ReportWriter>()
                .AddSingleton<IMemory, Memory>()
                .AddSingleton<IManagement, Management>()
                .AddSingleton(configurations)
                .BuildServiceProvider();
        }
    }
}