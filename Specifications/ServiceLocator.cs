using Helix.Crawler;
using Helix.Crawler.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Helix.Specifications
{
    public static class ServiceLocator
    {
        static ServiceCollection _serviceCollection;
        static ServiceProvider _serviceProvider;

        static ServiceLocator() { Reset(); }

        public static void AddOrReplaceServices(params ServiceDescriptor[] services)
        {
            foreach (var service in services)
            {
                _serviceCollection.RemoveAll(service.GetType());
                _serviceCollection.Add(service);
            }
            _serviceProvider = _serviceCollection.BuildServiceProvider();
        }

        public static void Dispose()
        {
            _serviceProvider?.Dispose();
            _serviceProvider = null;
        }

        public static TService Get<TService>() { return _serviceProvider.GetService<TService>(); }

        public static void Reset()
        {
            Dispose();
            _serviceCollection = new ServiceCollection();
            _serviceProvider = _serviceCollection.Add(new[]
            {
                new ServiceDescriptor(typeof(IResourceCollector), typeof(ResourceCollector), ServiceLifetime.Transient),
                new ServiceDescriptor(typeof(IResourceVerifier), typeof(ResourceVerifier), ServiceLifetime.Transient),
                new ServiceDescriptor(typeof(IResourceProcessor), typeof(ResourceProcessor), ServiceLifetime.Transient),
                new ServiceDescriptor(typeof(IResourceScope), typeof(ResourceScope), ServiceLifetime.Transient),
                new ServiceDescriptor(typeof(Configurations), new Configurations()),
                new ServiceDescriptor(typeof(IMemory), typeof(Memory), ServiceLifetime.Singleton),
                new ServiceDescriptor(typeof(IRawResource), typeof(RawResource), ServiceLifetime.Transient),
                new ServiceDescriptor(typeof(IResource), typeof(Resource), ServiceLifetime.Transient),
                new ServiceDescriptor(typeof(IVerificationResult), typeof(VerificationResult), ServiceLifetime.Transient)
            }).BuildServiceProvider();
        }
    }
}