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
        static IEventBroadcaster _eventBroadcaster;
        static HttpClient _httpClient;
        static ILogger _logger;
        static bool _objectDisposed;
        static ServiceProvider _serviceProvider;

        static ServiceLocator()
        {
            _objectDisposed = true;
            SetupApplicationWideSingletonServices();

            void SetupApplicationWideSingletonServices()
            {
                _logger = Activator.CreateInstance<Logger>();
                _eventBroadcaster = Activator.CreateInstance<EventBroadcaster>();
                AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
                {
                    _logger.Dispose();
                    _logger = null;
                    _eventBroadcaster = null;
                };
            }
        }

        public static void CreateSessionScopedServices(Configurations configurations)
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(ServiceLocator));
            if (_serviceProvider?.GetService<Configurations>() != null) throw new InvalidConstraintException();
            _serviceProvider?.Dispose();
            _serviceProvider = GetSupportServiceCollection()
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
                .AddSingleton(GetHttpClient(configurations))
                .AddSingleton(configurations)
                .BuildServiceProvider();
        }

        public static void DisposeSessionScopedAndSupportServices()
        {
            if (_objectDisposed) return;
            _serviceProvider?.Dispose();
            _serviceProvider = null;
            _httpClient.Dispose();
            _httpClient = null;
            _objectDisposed = true;
        }

        public static TService Get<TService>()
        {
            if (_objectDisposed) throw new ObjectDisposedException(nameof(ServiceLocator));
            return _serviceProvider.GetService<TService>();
        }

        public static bool TryCreateSupportServices()
        {
            if (!_objectDisposed) return false;
            _objectDisposed = false;
            _serviceProvider = GetSupportServiceCollection().BuildServiceProvider();
            return true;
        }

        static HttpClient GetHttpClient(Configurations configurations)
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

        static IServiceCollection GetSupportServiceCollection()
        {
            return new ServiceCollection()
                .AddSingleton<IPersistenceProvider, PersistenceProvider>()
                .AddSingleton<IWebBrowserProvider, WebBrowserProvider>()
                .AddSingleton(_eventBroadcaster)
                .AddSingleton(_logger);
        }
    }
}