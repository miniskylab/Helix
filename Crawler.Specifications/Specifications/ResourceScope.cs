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
        void CouldIdentifyInternalResources(Configurations configurations, IResource resource, bool expectedResult, Type expectedException)
        {
            if (configurations != null) ServiceLocator.AddOrReplaceServices(new ServiceDescriptor(typeof(Configurations), configurations));
            var resourceScope = ServiceLocator.Get<IResourceScope>();

            if (expectedException != null) Assert.Throws(expectedException, () => { resourceScope.IsInternalResource(resource); });
            else Assert.Equal(expectedResult, resourceScope.IsInternalResource(resource));
        }

        [Theory]
        [ClassData(typeof(StartUrlDefinition))]
        void CouldIdentifyStartUrl(Configurations configurations, string url, bool expectedResult, Type expectedException)
        {
            if (configurations != null) ServiceLocator.AddOrReplaceServices(new ServiceDescriptor(typeof(Configurations), configurations));
            var resourceScope = ServiceLocator.Get<IResourceScope>();

            if (expectedException != null) Assert.Throws(expectedException, () => { resourceScope.IsStartUrl(url); });
            else Assert.Equal(expectedResult, resourceScope.IsStartUrl(url));
        }

        [Theory]
        [ClassData(typeof(UriLocalizationDefinition))]
        void CouldLocalizeUri(Configurations configurations, Uri originalUri, Uri expectedLocalizedUri, Type expectedException)
        {
            if (configurations != null) ServiceLocator.AddOrReplaceServices(new ServiceDescriptor(typeof(Configurations), configurations));
            var resourceScope = ServiceLocator.Get<IResourceScope>();

            if (expectedException != null) Assert.Throws(expectedException, () => { resourceScope.Localize(originalUri); });
            else Assert.Equal(expectedLocalizedUri, resourceScope.Localize(originalUri));
        }
    }
}