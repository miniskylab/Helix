using System;
using System.Collections.Generic;
using System.Threading;
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
        static readonly Dictionary<string, object> LockMap;

        static ServiceLocator()
        {
            _objectDisposed = false;
            LockMap = new Dictionary<string, object>
            {
                { $"{nameof(Get)}", new object() },
                { $"{nameof(AddSingleton)}", new object() }
            };
        }

        public static void AddSingleton(Configurations configurations)
        {
            lock (LockMap[nameof(AddSingleton)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ServiceLocator));
                if (_serviceProvider?.GetService<Configurations>() != null) return;
                _serviceProvider?.Dispose();
                _serviceProvider = new ServiceCollection()
                    .AddTransient<IHtmlRenderer, HtmlRenderer>()
                    .AddTransient<IResourceExtractor, ResourceExtractor>()
                    .AddTransient<IResourceVerifier, ResourceVerifier>()
                    .AddTransient<IResourceProcessor, ResourceProcessor>()
                    .AddTransient<IResourceScope, ResourceScope>()
                    .AddSingleton<IIncrementalIdGenerator, IncrementalIdGenerator>()
                    .AddSingleton<IStatistics, Statistics>()
                    .AddSingleton<IPersistenceProvider, PersistenceProvider>()
                    .AddSingleton<IWebBrowserProvider, WebBrowserProvider>()
                    .AddSingleton<IServicePool, ServicePool>()
                    .AddSingleton<ILogger, Logger>()
                    .AddSingleton<IReportWriter, ReportWriter>()
                    .AddSingleton<IMemory, Memory>()
                    .AddSingleton<IScheduler, Scheduler>()
                    .AddSingleton(configurations)
                    .BuildServiceProvider();
            }
        }

        public static void Dispose()
        {
            try
            {
                foreach (var lockObject in LockMap.Values) Monitor.Enter(lockObject);
                if (_objectDisposed) return;
                _serviceProvider?.Dispose();
                _serviceProvider = null;
                _objectDisposed = true;
            }
            finally
            {
                foreach (var lockObject in LockMap.Values) Monitor.Exit(lockObject);
            }
        }

        public static TService Get<TService>()
        {
            lock (LockMap[nameof(Get)])
            {
                if (_objectDisposed) throw new ObjectDisposedException(nameof(ServiceLocator));
                return _serviceProvider.GetService<TService>();
            }
        }
    }
}