using Helix.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Helix.Implementations
{
    public static class ServiceLocator
    {
        static ServiceProvider _serviceProvider;

        public static TService Get<TService>() { return _serviceProvider.GetService<TService>(); }

        public static void RegisterServices(Configurations configurations)
        {
            if (_serviceProvider != null) return;
            _serviceProvider = new ServiceCollection()
                .AddTransient<IResourceCollector, ResourceCollector>()
                .AddTransient<IResourceVerifier, ResourceVerifier>()
                .AddTransient<IResourceProcessor, ResourceProcessor>()
                .AddTransient<IResourceScope, ResourceScope>()
                .AddSingleton(configurations)
                .BuildServiceProvider();
        }
    }
}