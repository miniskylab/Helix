using System;
using Helix.Bot.Abstractions;
using Helix.Specifications;
using Xunit;

namespace Helix.Bot.Specifications
{
    public class ResourceEnricher : AbstractSpecifications
    {
        [Theory]
        [ClassData(typeof(ResourceEnrichmentDescription))]
        void EnrichResource(Resource inputResource, Resource expectedOutputResource, Type expectedExceptionType)
        {
            var resourceEnricher = ServiceLocator.Get<IResourceEnricher>();
            if (expectedExceptionType != null)
                Assert.Throws(expectedExceptionType, () => { resourceEnricher.Enrich(inputResource); });
            else
            {
                resourceEnricher.Enrich(inputResource);
                Assert.NotEqual(0, inputResource.Id);
                Assert.StrictEqual(expectedOutputResource?.ParentUri, inputResource.ParentUri);
                Assert.Equal(expectedOutputResource?.OriginalUrl, inputResource.OriginalUrl);
                Assert.StrictEqual(expectedOutputResource?.Uri, inputResource.Uri);
                Assert.Equal(expectedOutputResource?.StatusCode, inputResource.StatusCode);
                Assert.Equal(expectedOutputResource?.Uri?.Fragment, inputResource.Uri?.Fragment);
            }
        }
    }
}