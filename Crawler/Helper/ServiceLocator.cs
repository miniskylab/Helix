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
    internal static class ServiceLocator
    {
        static HttpClient _httpClient;
        static ServiceProvider _serviceProvider;
        static readonly ServiceProvider ApplicationWideSingletonServiceProvider;
        static readonly IServiceCollection ServiceCollection;

        static ServiceLocator()
        {
            ServiceCollection = new ServiceCollection()
                .AddTransient<IHtmlRenderer, HtmlRenderer>()
                .AddTransient<IResourceExtractor, ResourceExtractor>()
                .AddTransient<IResourceVerifier, ResourceVerifier>()
                .AddTransient<IResourceEnricher, ResourceEnricher>()
                .AddTransient<IResourceScope, ResourceScope>()
                .AddSingleton<IEventBroadcaster, EventBroadcaster>()
                .AddSingleton<IIncrementalIdGenerator, IncrementalIdGenerator>()
                .AddSingleton<IStatistics, Statistics>()
                .AddSingleton<INetworkServicePool, NetworkServicePool>()
                .AddSingleton<IReportWriter, ReportWriter>()
                .AddSingleton<IMemory, Memory>()
                .AddSingleton<IScheduler, Scheduler>()
                .AddSingleton<IHardwareMonitor, HardwareMonitor>()
                .AddSingleton<IHttpContentTypeToResourceTypeDictionary, HttpContentTypeToResourceTypeDictionary>();

            var applicationWideSingletonServiceCollection = new ServiceCollection()
                .AddSingleton(_ => (ILogger) Activator.CreateInstance(typeof(Logger)));
            ApplicationWideSingletonServiceProvider = applicationWideSingletonServiceCollection.BuildServiceProvider();

            foreach (var applicationWideSingletonServiceDescriptor in applicationWideSingletonServiceCollection)
                ServiceCollection.AddSingleton(
                    applicationWideSingletonServiceDescriptor.ServiceType,
                    ApplicationWideSingletonServiceProvider.GetService(applicationWideSingletonServiceDescriptor.ServiceType)
                );

            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                ApplicationWideSingletonServiceProvider.GetService<ILogger>().LogInfo("Disposing application-wide singleton services ...");
                ApplicationWideSingletonServiceProvider.GetService<ILogger>().Dispose();
                ApplicationWideSingletonServiceProvider.Dispose();
            };
        }

        public static void DisposeServices()
        {
            _serviceProvider?.Dispose();
            _serviceProvider = null;
            _httpClient?.Dispose();
            _httpClient = null;
        }

        public static TService Get<TService>() where TService : class
        {
            return _serviceProvider?.GetService<TService>() ?? ApplicationWideSingletonServiceProvider.GetService<TService>();
        }

        public static void InitializeServices(Configurations configurations)
        {
            if (_serviceProvider?.GetService<Configurations>() != null) throw new InvalidConstraintException();
            DisposeServices();
            _serviceProvider = ServiceCollection
                .AddTransient(_ => CreateAndConfigureWebBrowser())
                .AddTransient(_ => CreateAndConfigureSqLitePersistence())
                .AddSingleton(CreateAndConfigureHttpClient())
                .AddSingleton(configurations)
                .BuildServiceProvider();

            IWebBrowser CreateAndConfigureWebBrowser()
            {
                return new ChromiumWebBrowser(
                    Configurations.PathToChromiumExecutable,
                    Configurations.WorkingDirectory,
                    configurations.HttpRequestTimeout.TotalSeconds,
                    configurations.UseIncognitoWebBrowser,
                    configurations.UseHeadlessWebBrowsers,
                    (1920, 1080)
                );
            }
            HttpClient CreateAndConfigureHttpClient()
            {
                if (_httpClient != null) return _httpClient;
                IWebBrowser webBrowser = new ChromiumWebBrowser(
                    Configurations.PathToChromiumExecutable,
                    Configurations.WorkingDirectory
                );
                var userAgentString = webBrowser.GetUserAgentString();
                webBrowser.Dispose();

                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                _httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
                _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
                _httpClient.DefaultRequestHeaders.Upgrade.ParseAdd("1");
                _httpClient.DefaultRequestHeaders.Pragma.ParseAdd("no-cache");
                _httpClient.DefaultRequestHeaders.CacheControl = CacheControlHeaderValue.Parse("no-cache");
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgentString);
                _httpClient.Timeout = configurations.HttpRequestTimeout;
                return _httpClient;
            }
            ISqLitePersistence<VerificationResult> CreateAndConfigureSqLitePersistence()
            {
                return new SqLitePersistence<VerificationResult>(Configurations.PathToReportFile);
            }
        }
    }
}