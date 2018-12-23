using System;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Helix.Crawler.Specifications
{
    public class ResourceScope : AbstractSpecifications
    {
        [Theory]
        [ClassData(typeof(InternalResourceDefinition))]
        void CouldIdentifyInternalResources(IResource resource, Configurations configurations, bool expectedResult, Type expectedException)
        {
            if (configurations != null) ServiceLocator.AddOrReplaceServices(new ServiceDescriptor(typeof(Configurations), configurations));
            var resourceScope = ServiceLocator.Get<IResourceScope>();

            if (expectedException != null) Assert.Throws(expectedException, () => { resourceScope.IsInternalResource(resource); });
            else Assert.Equal(expectedResult, resourceScope.IsInternalResource(resource));
        }

        [Theory]
        [ClassData(typeof(StartUrlDefinition))]
        void CouldIdentifyStartUrl(string url, Configurations configurations, bool expectedResult, Type expectedException)
        {
            if (configurations != null) ServiceLocator.AddOrReplaceServices(new ServiceDescriptor(typeof(Configurations), configurations));
            var resourceScope = ServiceLocator.Get<IResourceScope>();

            if (expectedException != null) Assert.Throws(expectedException, () => { resourceScope.IsStartUrl(url); });
            else Assert.Equal(expectedResult, resourceScope.IsStartUrl(url));
        }
    }
}