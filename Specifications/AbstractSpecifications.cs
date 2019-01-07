using System;

namespace Helix.Specifications
{
    public abstract class AbstractSpecifications : IDisposable
    {
        protected ServiceLocator ServiceLocator;

        protected AbstractSpecifications() { ServiceLocator = ServiceLocator.CreateNewInstance(); }

        public void Dispose()
        {
            ServiceLocator?.Dispose();
            ServiceLocator = null;
        }
    }
}