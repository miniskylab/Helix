using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Helix.Specifications
{
    public static class ServiceLocator
    {
        static ServiceProvider _serviceProvider;

        public static void Dispose()
        {
            _serviceProvider?.Dispose();
            _serviceProvider = null;
        }

        public static TService Get<TService>() { return _serviceProvider.GetService<TService>(); }

        public static void RegisterServices(params ServiceDescriptor[] serviceDescriptors)
        {
            Dispose();
            _serviceProvider = new ServiceCollection().Add(serviceDescriptors).BuildServiceProvider();
        }
    }
}