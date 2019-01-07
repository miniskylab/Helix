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
        void CouldIdentifyInternalResources(Configurations configurations, Resource resource, bool expectedIdentificationResult,
            Type expectedExceptionType)
        {
            if (configurations != null) ServiceLocator.AddOrReplaceServices(new ServiceDescriptor(typeof(Configurations), configurations));
            var resourceScope = ServiceLocator.Get<IResourceScope>();

            if (expectedExceptionType != null) Assert.Throws(expectedExceptionType, () => { resourceScope.IsInternalResource(resource); });
            else Assert.Equal(expectedIdentificationResult, resourceScope.IsInternalResource(resource));
        }

        [Theory]
        [ClassData(typeof(StartUrlDefinition))]
        void CouldIdentifyStartUrl(Configurations configurations, string url, bool expectedIdentificationResult, Type expectedExceptionType)
        {
            if (configurations != null) ServiceLocator.AddOrReplaceServices(new ServiceDescriptor(typeof(Configurations), configurations));
            var resourceScope = ServiceLocator.Get<IResourceScope>();

            if (expectedExceptionType != null) Assert.Throws(expectedExceptionType, () => { resourceScope.IsStartUrl(url); });
            else Assert.Equal(expectedIdentificationResult, resourceScope.IsStartUrl(url));
        }

        [Theory]
        [ClassData(typeof(UriLocalizationDefinition))]
        void CouldLocalizeUri(Configurations configurations, Uri originalUri, Uri expectedLocalizedUri, Type expectedExceptionType)
        {
            if (configurations != null) ServiceLocator.AddOrReplaceServices(new ServiceDescriptor(typeof(Configurations), configurations));
            var resourceScope = ServiceLocator.Get<IResourceScope>();

            if (expectedExceptionType != null) Assert.Throws(expectedExceptionType, () => { resourceScope.Localize(originalUri); });
            else Assert.Equal(expectedLocalizedUri, resourceScope.Localize(originalUri));
        }
    }
}