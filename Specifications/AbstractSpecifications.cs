using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Helix.Specifications
{
    public abstract class AbstractSpecifications : IDisposable
    {
        readonly ServiceCollection _serviceCollection;
        ServiceProvider _serviceProvider;

        protected AbstractSpecifications() { _serviceCollection = new ServiceCollection(); }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
            _serviceProvider = null;
        }

        protected TService Get<TService>() { return _serviceProvider.GetService<TService>(); }

        protected abstract void RegisterDefaultDependencies(ServiceCollection serviceCollection);

        protected void RegisterDependencies(params ServiceDescriptor[] dependencies)
        {
            _serviceCollection.Clear();
            RegisterDefaultDependencies(_serviceCollection);
            foreach (var dependency in dependencies) RegisterDependency(dependency);
            _serviceProvider = _serviceCollection.BuildServiceProvider();
        }

        void RegisterDependency(ServiceDescriptor dependency)
        {
            _serviceCollection.RemoveAll(dependency.ServiceType);
            _serviceCollection.Add(dependency);
        }
    }
}