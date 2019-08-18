using System;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using Autofac;
using Autofac.Core;
using Helix.Crawler.Abstractions;
using Helix.Persistence;
using Helix.Persistence.Abstractions;
using Helix.WebBrowser;
using Helix.WebBrowser.Abstractions;
using log4net;

namespace Helix.Crawler
{
    public partial class CrawlerBot
    {
        static class ServiceLocator
        {
            static IEventBroadcaster _eventBroadcaster;
            static IContainer _serviceContainer;

            static ServiceLocator() { AppDomain.CurrentDomain.ProcessExit += (_, __) => { _eventBroadcaster?.Dispose(); }; }

            public static void DisposeServices()
            {
                _serviceContainer?.Dispose();
                _serviceContainer = null;
            }

            public static TService Get<TService>() where TService : class { return _serviceContainer?.Resolve<TService>(); }

            public static void SetupAndConfigureServices(Configurations configurations)
            {
                if (_serviceContainer?.Resolve<Configurations>() != null) throw new InvalidConstraintException();

                var containerBuilder = new ContainerBuilder();
                containerBuilder.RegisterModule<Log4NetModule>();
                RegisterTransientServicesByConvention();
                RegisterTransientServicesRequiringConfigurations();
                RegisterSingletonServices();
                _serviceContainer = containerBuilder.Build();

                void RegisterTransientServicesByConvention()
                {
                    var filteredTypes = Assembly.GetExecutingAssembly().GetTypes()
                        .Where(type => type.IsClass && !type.IsAbstract && !type.IsNested && !type.IsCompilerGenerated());
                    foreach (var filteredType in filteredTypes)
                    {
                        var matchingInterfaceType = filteredType.GetInterface($"I{filteredType.Name}");
                        if (matchingInterfaceType == null) continue;
                        containerBuilder.RegisterType(filteredType).As(matchingInterfaceType);
                    }
                }
                void RegisterTransientServicesRequiringConfigurations()
                {
                    containerBuilder.Register(_ => CreateAndConfigureWebBrowser()).As<IWebBrowser>();
                    containerBuilder.Register(_ => CreateAndConfigureSqLitePersistence()).As<ISqLitePersistence<VerificationResult>>();

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
                    ISqLitePersistence<VerificationResult> CreateAndConfigureSqLitePersistence()
                    {
                        var sqLitePersistence = new SqLitePersistence<VerificationResult>(Configurations.PathToReportFile);
                        Get<IEventBroadcaster>().Broadcast(new Event { EventType = EventType.ReportFileCreated });
                        return sqLitePersistence;
                    }
                }
                void RegisterSingletonServices()
                {
                    _eventBroadcaster?.Dispose();
                    _eventBroadcaster = Activator.CreateInstance<EventBroadcaster>();
                    containerBuilder.RegisterInstance(_eventBroadcaster)
                        .As<IEventBroadcaster>()
                        .ExternallyOwned();

                    containerBuilder.RegisterInstance(configurations).AsSelf().SingleInstance();
                    containerBuilder.RegisterType<IncrementalIdGenerator>().As<IIncrementalIdGenerator>().SingleInstance();
                    containerBuilder.RegisterType<Statistics>().As<IStatistics>().SingleInstance();
                    containerBuilder.RegisterType<ReportWriter>().As<IReportWriter>().SingleInstance();
                    containerBuilder.RegisterType<Memory>().As<IMemory>().SingleInstance();
                    containerBuilder.RegisterType<Scheduler>().As<IScheduler>().SingleInstance();
                    containerBuilder.RegisterType<HardwareMonitor>().As<IHardwareMonitor>().SingleInstance();
                    containerBuilder.Register(_ => CreateAndConfigureHttpClient()).SingleInstance();
                    containerBuilder.RegisterType<NetworkServicePool>().As<INetworkServicePool>()
                        .WithParameters(new[]
                        {
                            Parameter<IResourceExtractor>(),
                            Parameter<IResourceVerifier>(),
                            Parameter<IHtmlRenderer>()
                        })
                        .SingleInstance();

                    HttpClient CreateAndConfigureHttpClient()
                    {
                        IWebBrowser webBrowser = new ChromiumWebBrowser(
                            Configurations.PathToChromiumExecutable,
                            Configurations.WorkingDirectory
                        );
                        var userAgentString = webBrowser.GetUserAgentString();
                        webBrowser.Dispose();

                        var httpClient = new HttpClient();
                        httpClient.DefaultRequestHeaders.Accept.ParseAdd(
                            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                        httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
                        httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
                        httpClient.DefaultRequestHeaders.Upgrade.ParseAdd("1");
                        httpClient.DefaultRequestHeaders.Pragma.ParseAdd("no-cache");
                        httpClient.DefaultRequestHeaders.CacheControl = CacheControlHeaderValue.Parse("no-cache");
                        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgentString);
                        httpClient.Timeout = configurations.HttpRequestTimeout;
                        return httpClient;
                    }
                    ResolvedParameter Parameter<T>()
                    {
                        return new ResolvedParameter(
                            (parameterInfo, _) => parameterInfo.ParameterType == typeof(Func<T>),
                            (_, componentContext) =>
                            {
                                var copiedComponentContext = componentContext.Resolve<IComponentContext>();
                                return new Func<T>(copiedComponentContext.Resolve<T>);
                            });
                    }
                }
            }

            class Log4NetModule : Autofac.Module
            {
                protected override void AttachToComponentRegistration(IComponentRegistry _, IComponentRegistration componentRegistration)
                {
                    componentRegistration.Preparing += (__, preparingEventArgs) =>
                    {
                        preparingEventArgs.Parameters = preparingEventArgs.Parameters.Union(
                            new[]
                            {
                                new ResolvedParameter(
                                    (parameterInfo, ___) => parameterInfo.ParameterType == typeof(ILog),
                                    (parameterInfo, ___) => LogManager.GetLogger(parameterInfo.Member.DeclaringType)
                                )
                            });
                    };
                }
            }
        }
    }
}