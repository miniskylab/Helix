using System;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using Autofac;
using Autofac.Core;
using Helix.Bot.Abstractions;
using Helix.Persistence;
using Helix.Persistence.Abstractions;
using Helix.WebBrowser;
using Helix.WebBrowser.Abstractions;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

namespace Helix.Bot
{
    public abstract class Application
    {
        static Application() { ConfigureLog4Net(); }

        static void ConfigureLog4Net()
        {
            var patternLayout = new PatternLayout
            {
                ConversionPattern = "[%date] [%5level] [%4thread] [%logger] - %message%newline"
            };
            patternLayout.ActivateOptions();

            var hierarchy = (Hierarchy) LogManager.GetRepository(Assembly.GetEntryAssembly());
            var rollingFileAppender = new RollingFileAppender
            {
                File = Configurations.PathToLogFile,
                AppendToFile = false,
                PreserveLogFileNameExtension = true,
                RollingStyle = RollingFileAppender.RollingMode.Size,
                MaxSizeRollBackups = -1,
                MaximumFileSize = "1GB",
                Layout = patternLayout
            };
            rollingFileAppender.ActivateOptions();
            hierarchy.Root.AddAppender(rollingFileAppender);
            hierarchy.Root.Level = Level.Info;
            hierarchy.Configured = true;
        }

        protected static class ServiceLocator
        {
            static IContainer _serviceContainer;

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

                #region Local Functions

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
                        return new SqLitePersistence<VerificationResult>(Configurations.PathToReportFile);
                    }
                }
                void RegisterSingletonServices()
                {
                    containerBuilder.RegisterInstance(configurations).AsSelf();
                    containerBuilder.RegisterInstance(Activator.CreateInstance<EventBroadcaster>()).As<IEventBroadcaster>();

                    containerBuilder.RegisterType<IncrementalIdGenerator>().As<IIncrementalIdGenerator>().SingleInstance();
                    containerBuilder.RegisterType<Statistics>().As<IStatistics>().SingleInstance();
                    containerBuilder.RegisterType<ReportWriter>().As<IReportWriter>().SingleInstance();
                    containerBuilder.RegisterType<ResourceVerifier>().As<IResourceVerifier>().SingleInstance();
                    containerBuilder.RegisterType<HardwareMonitor>().As<IHardwareMonitor>().SingleInstance();
                    containerBuilder.RegisterType<BrokenLinkCollectionWorkflow>().As<IBrokenLinkCollectionWorkflow>().SingleInstance();

                    containerBuilder.Register(_ => CreateAndConfigureHttpClient()).SingleInstance();

                    HttpClient CreateAndConfigureHttpClient()
                    {
                        IWebBrowser webBrowser = new ChromiumWebBrowser(
                            Configurations.PathToChromiumExecutable,
                            Configurations.WorkingDirectory
                        );
                        var userAgentString = webBrowser.GetUserAgentString();
                        webBrowser.Dispose();

                        var httpClient = new HttpClient();
                        httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                        httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
                        httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
                        httpClient.DefaultRequestHeaders.Upgrade.ParseAdd("1");
                        httpClient.DefaultRequestHeaders.Pragma.ParseAdd("no-cache");
                        httpClient.DefaultRequestHeaders.CacheControl = CacheControlHeaderValue.Parse("no-cache");
                        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgentString);
                        httpClient.Timeout = configurations.HttpRequestTimeout;
                        return httpClient;
                    }
                }

                #endregion
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