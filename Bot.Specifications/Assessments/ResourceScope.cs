using System;
using Helix.Bot.Abstractions;
using Helix.Specifications;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Helix.Bot.Specifications
{
    public class ResourceScope : AbstractSpecifications
    {
        [Theory]
        [ClassData(typeof(InternalResourceDescription))]
        void IdentifyInternalResources(Configurations configurations, Resource inputResource, bool expectedIdentificationResult,
            Type expectedExceptionType)
        {
            if (configurations != null) ServiceLocator.AddOrReplaceServices(new ServiceDescriptor(typeof(Configurations), configurations));
            var resourceScope = ServiceLocator.Get<IResourceScope>();

            if (expectedExceptionType != null)
                Assert.Throws(
                    expectedExceptionType,
                    () => { resourceScope.IsInternalResource(inputResource); }
                );
            else Assert.Equal(expectedIdentificationResult, resourceScope.IsInternalResource(inputResource));
        }

        [Theory]
        [ClassData(typeof(StartUriDescription))]
        void IdentifyStartUri(Configurations configurations, Uri inputUri, bool expectedIdentificationResult, Type expectedExceptionType)
        {
            if (configurations != null) ServiceLocator.AddOrReplaceServices(new ServiceDescriptor(typeof(Configurations), configurations));
            var resourceScope = ServiceLocator.Get<IResourceScope>();

            if (expectedExceptionType != null) Assert.Throws(expectedExceptionType, () => { resourceScope.IsStartUri(inputUri); });
            else Assert.Equal(expectedIdentificationResult, resourceScope.IsStartUri(inputUri));
        }

        [Theory]
        [ClassData(typeof(UriLocalizationDescription))]
        void LocalizeUri(Configurations configurations, Uri inputUri, Uri expectedOutputUri, Type expectedExceptionType)
        {
            if (configurations != null) ServiceLocator.AddOrReplaceServices(new ServiceDescriptor(typeof(Configurations), configurations));
            var resourceScope = ServiceLocator.Get<IResourceScope>();

            if (expectedExceptionType != null) Assert.Throws(expectedExceptionType, () => { resourceScope.Localize(inputUri); });
            else Assert.StrictEqual(expectedOutputUri, resourceScope.Localize(inputUri));
        }
    }
}