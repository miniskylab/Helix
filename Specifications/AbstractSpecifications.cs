using System;

namespace Helix.Specifications
{
    public abstract class AbstractSpecifications : IDisposable
    {
        protected AbstractSpecifications() { ServiceLocator.Reset(); }

        public void Dispose() { ServiceLocator.Dispose(); }
    }
}