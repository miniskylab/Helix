using System.Data;
using System.Net.Http;
using System.Net.Http.Headers;
using Autofac;
using Helix.Bot.Abstractions;
using Helix.Core;
using Helix.WebBrowser;
using Helix.WebBrowser.Abstractions;

namespace Helix.Bot
{
    public abstract class Bot
    {
        IContainer _serviceContainer;

        protected void DisposeServiceContainer()
        {
            _serviceContainer?.Dispose();
            _serviceContainer = null;
        }

        protected TWorkflow GetWorkflow<TWorkflow>() where TWorkflow : IWorkflow { return _serviceContainer.Resolve<TWorkflow>(); }

        protected void SetupServiceContainer(Configurations configurations)
        {
            if (_serviceContainer?.Resolve<Configurations>() != null) throw new InvalidConstraintException();

            var containerBuilder = DependencyInjection.GetDefaultContainerBuilder();
            RegisterTransientServicesRequiringConfigurations();
            RegisterSingletonServices();
            _serviceContainer = containerBuilder.Build();

            #region Local Functions

            void RegisterTransientServicesRequiringConfigurations()
            {
                containerBuilder.Register(_ => CreateAndConfigureWebBrowser()).As<IWebBrowser>();

                IWebBrowser CreateAndConfigureWebBrowser()
                {
                    return new ChromiumWebBrowser(
                        Configurations.PathToChromiumExecutable,
                        Configurations.WorkingDirectory,
                        Configurations.HttpRequestTimeout.TotalSeconds,
                        configurations.UseIncognitoWebBrowser,
                        configurations.UseHeadlessWebBrowsers,
                        (1920, 1080)
                    );
                }
            }
            void RegisterSingletonServices()
            {
                containerBuilder.RegisterInstance(configurations).AsSelf();

                containerBuilder.RegisterType<Statistics>().As<IStatistics>().SingleInstance();
                containerBuilder.RegisterType<ReportWriter>().As<IReportWriter>().SingleInstance();
                containerBuilder.RegisterType<HardwareMonitor>().As<IHardwareMonitor>().SingleInstance();
                containerBuilder.RegisterType<ResourceVerifier>().As<IResourceVerifier>().SingleInstance();
                containerBuilder.RegisterType<IncrementalIdGenerator>().As<IIncrementalIdGenerator>().SingleInstance();
                containerBuilder.RegisterType<BrokenLinkCollectionWorkflow>().As<IBrokenLinkCollectionWorkflow>().SingleInstance();

                containerBuilder.Register(_ => CreateAndConfigureHttpClient()).SingleInstance();

                static HttpClient CreateAndConfigureHttpClient()
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
                    httpClient.Timeout = Configurations.HttpRequestTimeout;
                    return httpClient;
                }
            }

            #endregion
        }
    }
}