using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Helix.Crawler.Abstractions;
using Helix.Persistence;
using Helix.Persistence.Abstractions;
using Helix.WebBrowser;
using Helix.WebBrowser.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Helix.Crawler
{
    static class ServiceLocator
    {
        static bool _objectDisposed;
        static ServiceProvider _serviceProvider;
        static readonly IEventBroadcaster EventBroadcaster;

        static ServiceLocator()
        {
            _objectDisposed = false;
            EventBroadcaster = Activator.CreateInstance<EventBroadcaster>();
            _serviceProvider = GetInfrastructureServiceCollection().BuildServiceProvider();
        }

        public static void Dispose()
        {
            if (_objectDisposed) return;
            _serviceProvider?.Dispose();
            _serviceProvider = null;
            _objectDisposed = true;
        }

        public static TService Get<TService>()
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(ServiceLocator));
            return _serviceProvider.GetService<TService>();
        }

        public static void RebuildUsingNew(Configurations configurations)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(ServiceLocator));
            if (_serviceProvider?.GetService<Configurations>() != null) return;
            _serviceProvider?.Dispose();
            _serviceProvider = GetInfrastructureServiceCollection()
                .AddTransient<IHtmlRenderer, HtmlRenderer>()
                .AddTransient<IResourceExtractor, ResourceExtractor>()
                .AddTransient<IResourceVerifier, ResourceVerifier>()
                .AddTransient<IResourceProcessor, ResourceProcessor>()
                .AddTransient<IResourceScope, ResourceScope>()
                .AddSingleton<IIncrementalIdGenerator, IncrementalIdGenerator>()
                .AddSingleton<IStatistics, Statistics>()
                .AddSingleton<IServicePool, ServicePool>()
                .AddSingleton<ILogger, Logger>()
                .AddSingleton<IReportWriter, ReportWriter>()
                .AddSingleton<IMemory, Memory>()
                .AddSingleton<IScheduler, Scheduler>()
                .AddSingleton(GetHttpClient(configurations))
                .AddSingleton(configurations)
                .BuildServiceProvider();
        }

        static HttpClient GetHttpClient(Configurations configurations)
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("*");
            httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("*");
            httpClient.DefaultRequestHeaders.CacheControl = CacheControlHeaderValue.Parse("no-cache");
            httpClient.DefaultRequestHeaders.Pragma.ParseAdd("no-cache");
            httpClient.DefaultRequestHeaders.Upgrade.ParseAdd("1");
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(configurations.UserAgent);
            return httpClient;
        }

        static IServiceCollection GetInfrastructureServiceCollection()
        {
            return new ServiceCollection()
                .AddSingleton<IPersistenceProvider, PersistenceProvider>()
                .AddSingleton<IWebBrowserProvider, WebBrowserProvider>()
                .AddSingleton(EventBroadcaster);
        }
    }
}