using Helix.Crawler;
using Helix.Crawler.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Helix.Specifications
{
    public class ServiceLocator
    {
        ServiceCollection _serviceCollection;
        ServiceProvider _serviceProvider;

        ServiceLocator()
        {
            Dispose();
            _serviceCollection = new ServiceCollection();
            _serviceProvider = _serviceCollection.Add(new[]
            {
                new ServiceDescriptor(typeof(IRawResourceExtractor), typeof(RawResourceExtractor), ServiceLifetime.Transient),
                new ServiceDescriptor(typeof(IResourceVerifier), typeof(ResourceVerifier), ServiceLifetime.Transient),
                new ServiceDescriptor(typeof(IRawResourceProcessor), typeof(RawResourceProcessor), ServiceLifetime.Transient),
                new ServiceDescriptor(typeof(IResourceScope), typeof(ResourceScope), ServiceLifetime.Transient),
                new ServiceDescriptor(typeof(Configurations), new Configurations()),
                new ServiceDescriptor(typeof(IMemory), typeof(Memory), ServiceLifetime.Singleton),
                new ServiceDescriptor(typeof(IManagement), typeof(Management), ServiceLifetime.Singleton)
            }).BuildServiceProvider();
        }

        public void AddOrReplaceServices(params ServiceDescriptor[] services)
        {
            foreach (var service in services)
            {
                _serviceCollection.RemoveAll(service.GetType());
                _serviceCollection.Add(service);
            }
            _serviceProvider = _serviceCollection.BuildServiceProvider();
        }

        public static ServiceLocator CreateNewInstance() { return new ServiceLocator(); }

        public void Dispose()
        {
            _serviceCollection = null;
            _serviceProvider?.Dispose();
            _serviceProvider = null;
        }

        public TService Get<TService>() { return _serviceProvider.GetService<TService>(); }
    }
}