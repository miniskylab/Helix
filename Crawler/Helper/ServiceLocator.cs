using System;
using System.Data;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using Helix.Crawler.Abstractions;
using Helix.Persistence;
using Helix.Persistence.Abstractions;
using Helix.WebBrowser;
using Helix.WebBrowser.Abstractions;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Microsoft.Extensions.DependencyInjection;

namespace Helix.Crawler
{
    internal static class ServiceLocator
    {
        static HttpClient _httpClient;
        static ServiceProvider _immortalServiceProvider;
        static IServiceCollection _serviceCollection;
        static ServiceProvider _serviceProvider;

        static ServiceLocator()
        {
            ConfigureLogging();
            RegisterServices();
            SetupImmortalServices();
            DisposeImmortalServicesWhenQuiting();

            void ConfigureLogging()
            {
                var patternLayout = new PatternLayout { ConversionPattern = "[%date] [%5level] [%4thread] [%logger] - %message%newline" };
                patternLayout.ActivateOptions();

                var hierarchy = (Hierarchy) LogManager.GetRepository(Assembly.GetEntryAssembly());
                var rollingFileAppender = new RollingFileAppender
                {
                    File = $"logs\\{nameof(Helix)}.{DateTime.Now:yyyyMMdd-HHmmss}.log",
                    AppendToFile = false,
                    PreserveLogFileNameExtension = true,
                    RollingStyle = RollingFileAppender.RollingMode.Size,
                    MaxSizeRollBackups = -1,
                    MaximumFileSize = "1GB",
                    Layout = patternLayout
                };
                rollingFileAppender.ActivateOptions();
                hierarchy.Root.AddAppender(rollingFileAppender);
                hierarchy.Root.Level = Level.Debug;
                hierarchy.Configured = true;
            }
            void RegisterServices()
            {
                _serviceCollection = new ServiceCollection()
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
            }
            void SetupImmortalServices()
            {
                var immortalServiceCollection = new ServiceCollection()
                    .AddSingleton(_ => LogManager.GetLogger(Assembly.GetEntryAssembly(), nameof(ServiceLocator)));
                _immortalServiceProvider = immortalServiceCollection.BuildServiceProvider();

                foreach (var immortalServiceDescriptor in immortalServiceCollection)
                    _serviceCollection.AddSingleton(
                        immortalServiceDescriptor.ServiceType,
                        _immortalServiceProvider.GetService(immortalServiceDescriptor.ServiceType)
                    );
            }
            void DisposeImmortalServicesWhenQuiting()
            {
                AppDomain.CurrentDomain.ProcessExit += (_, __) =>
                {
                    _immortalServiceProvider.GetService<ILog>().Info("Disposing immortal services ...");
                    _immortalServiceProvider.Dispose();
                };
            }
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
            return _serviceProvider?.GetService<TService>() ?? _immortalServiceProvider.GetService<TService>();
        }

        public static void SetupAndConfigureServices(Configurations configurations)
        {
            if (_serviceProvider?.GetService<Configurations>() != null) throw new InvalidConstraintException();
            DisposeServices();
            _serviceProvider = _serviceCollection
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
                var sqLitePersistence = new SqLitePersistence<VerificationResult>(Configurations.PathToReportFile);
                Get<IEventBroadcaster>().Broadcast(new Event { EventType = EventType.ReportFileCreated });
                return sqLitePersistence;
            }
        }
    }
}