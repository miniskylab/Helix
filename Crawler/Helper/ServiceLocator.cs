using System;
using System.Data;
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
        static HttpClient _httpClient;
        static readonly ServiceProvider _singletonServiceProvider;
        static readonly IServiceCollection _transientServiceCollection;
        static ServiceProvider _transientServiceProvider;

        static ServiceLocator()
        {
            _transientServiceCollection = new ServiceCollection()
                .AddTransient<IHtmlRenderer, HtmlRenderer>()
                .AddTransient<IResourceExtractor, ResourceExtractor>()
                .AddTransient<IResourceVerifier, ResourceVerifier>()
                .AddTransient<IResourceProcessor, ResourceProcessor>()
                .AddTransient<IResourceScope, ResourceScope>()
                .AddSingleton<IIncrementalIdGenerator, IncrementalIdGenerator>()
                .AddSingleton<IStatistics, Statistics>()
                .AddSingleton<IServicePool, ServicePool>()
                .AddSingleton<IReportWriter, ReportWriter>()
                .AddSingleton<IMemory, Memory>()
                .AddSingleton<IScheduler, Scheduler>()
                .AddSingleton<IHardwareMonitor, HardwareMonitor>();

            var singletonServiceCollection = new ServiceCollection()
                .AddSingleton<IPersistenceProvider, PersistenceProvider>()
                .AddSingleton<IWebBrowserProvider, WebBrowserProvider>()
                .AddSingleton<IEventBroadcaster, EventBroadcaster>()
                .AddSingleton<ILogger, Logger>();
            _singletonServiceProvider = singletonServiceCollection.BuildServiceProvider();

            foreach (var singletonServiceDescriptor in singletonServiceCollection)
                _transientServiceCollection.AddSingleton(
                    singletonServiceDescriptor.ServiceType,
                    _singletonServiceProvider.GetService(singletonServiceDescriptor.ServiceType)
                );

            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                _singletonServiceProvider.GetService<ILogger>().LogInfo("Disposing application-wide singleton services ...");
                _singletonServiceProvider.Dispose();
            };
        }

        public static void CreateTransientServices(Configurations configurations)
        {
            if (_transientServiceProvider?.GetService<Configurations>() != null) throw new InvalidConstraintException();
            _transientServiceProvider?.Dispose();
            _transientServiceProvider = _transientServiceCollection
                .AddSingleton(CreateAndConfigureHttpClient())
                .AddSingleton(configurations)
                .BuildServiceProvider();

            HttpClient CreateAndConfigureHttpClient()
            {
                if (_httpClient != null) return _httpClient;
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");
                _httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("*");
                _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("*");
                _httpClient.DefaultRequestHeaders.CacheControl = CacheControlHeaderValue.Parse("no-cache");
                _httpClient.DefaultRequestHeaders.Pragma.ParseAdd("no-cache");
                _httpClient.DefaultRequestHeaders.Upgrade.ParseAdd("1");
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(configurations.UserAgent);
                _httpClient.Timeout = configurations.HttpRequestTimeout;
                return _httpClient;
            }
        }

        public static void DisposeTransientServices()
        {
            _transientServiceProvider?.Dispose();
            _transientServiceProvider = null;
            _httpClient?.Dispose();
            _httpClient = null;
        }

        public static TService Get<TService>() where TService : class
        {
            return _transientServiceProvider?.GetService<TService>() ?? _singletonServiceProvider.GetService<TService>();
        }
    }
}