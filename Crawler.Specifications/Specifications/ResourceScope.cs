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
        void IdentifyInternalResources(Configurations configurations, Resource resource, bool expectedIdentificationResult,
            Type expectedExceptionType)
        {
            if (configurations != null) ServiceLocator.AddOrReplaceServices(new ServiceDescriptor(typeof(Configurations), configurations));
            var resourceScope = ServiceLocator.Get<IResourceScope>();

            if (expectedExceptionType != null) Assert.Throws(expectedExceptionType, () => { resourceScope.IsInternalResource(resource); });
            else Assert.Equal(expectedIdentificationResult, resourceScope.IsInternalResource(resource));
        }

        [Theory]
        [ClassData(typeof(StartUriDefinition))]
        void IdentifyStartUri(Configurations configurations, Uri uri, bool expectedIdentificationResult, Type expectedExceptionType)
        {
            if (configurations != null) ServiceLocator.AddOrReplaceServices(new ServiceDescriptor(typeof(Configurations), configurations));
            var resourceScope = ServiceLocator.Get<IResourceScope>();

            if (expectedExceptionType != null) Assert.Throws(expectedExceptionType, () => { resourceScope.IsStartUri(uri); });
            else Assert.Equal(expectedIdentificationResult, resourceScope.IsStartUri(uri));
        }

        [Theory]
        [ClassData(typeof(UriLocalizationDefinition))]
        void LocalizeUri(Configurations configurations, Uri originalUri, Uri expectedLocalizedUri, Type expectedExceptionType)
        {
            if (configurations != null) ServiceLocator.AddOrReplaceServices(new ServiceDescriptor(typeof(Configurations), configurations));
            var resourceScope = ServiceLocator.Get<IResourceScope>();

            if (expectedExceptionType != null) Assert.Throws(expectedExceptionType, () => { resourceScope.Localize(originalUri); });
            else Assert.StrictEqual(expectedLocalizedUri, resourceScope.Localize(originalUri));
        }
    }
}