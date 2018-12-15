using System;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Helix.Crawler.Specifications
{
    public class ResourceScope
    {
        [Theory]
        [ClassData(typeof(InternalResourceDefinition))]
        void CouldIdentifyInternalResources(IResource resource, Configurations configurations, bool expectedResult, Type expectedException)
        {
            if (configurations != null) ServiceLocator.RegisterServices(new ServiceDescriptor(typeof(Configurations), configurations));
            var resourceScope = ServiceLocator.Get<IResourceScope>();
            if (expectedException != null) Assert.Throws(expectedException, () => { resourceScope.IsInternalResource(resource); });
            else Assert.Equal(resourceScope.IsInternalResource(resource), expectedResult);
        }
    }
}