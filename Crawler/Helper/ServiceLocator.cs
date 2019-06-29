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
        static ServiceProvider _transientServiceProvider;
        static readonly ServiceProvider SingletonServiceProvider;
        static readonly IServiceCollection TransientServiceCollection;

        static ServiceLocator()
        {
            TransientServiceCollection = new ServiceCollection()
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

            var singletonServiceCollection = new ServiceCollection()
                .AddSingleton(_ => (ILogger) Activator.CreateInstance(typeof(Logger)));
            SingletonServiceProvider = singletonServiceCollection.BuildServiceProvider();

            foreach (var singletonServiceDescriptor in singletonServiceCollection)
                TransientServiceCollection.AddSingleton(
                    singletonServiceDescriptor.ServiceType,
                    SingletonServiceProvider.GetService(singletonServiceDescriptor.ServiceType)
                );

            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                SingletonServiceProvider.GetService<ILogger>().LogInfo("Disposing application-wide singleton services ...");
                SingletonServiceProvider.GetService<ILogger>().Dispose();
                SingletonServiceProvider.Dispose();
            };
        }

        public static void CreateTransientServices(Configurations configurations)
        {
            if (_transientServiceProvider?.GetService<Configurations>() != null) throw new InvalidConstraintException();
            DisposeTransientServices();
            _transientServiceProvider = TransientServiceCollection
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

        public static void DisposeTransientServices()
        {
            _transientServiceProvider?.Dispose();
            _transientServiceProvider = null;
            _httpClient?.Dispose();
            _httpClient = null;
        }

        public static TService Get<TService>() where TService : class
        {
            return _transientServiceProvider?.GetService<TService>() ?? SingletonServiceProvider.GetService<TService>();
        }
    }
}