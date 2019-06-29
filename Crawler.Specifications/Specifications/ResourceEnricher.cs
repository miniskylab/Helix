using System;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Xunit;

namespace Helix.Crawler.Specifications
{
    public class ResourceEnricher : AbstractSpecifications
    {
        [Theory]
        [ClassData(typeof(ResourceEnrichmentDefinition))]
        void EnrichResource(Resource resource, Resource expectedOutputResource, Type expectedExceptionType)
        {
            var resourceEnricher = ServiceLocator.Get<IResourceEnricher>();
            if (expectedExceptionType != null)
                Assert.Throws(expectedExceptionType, () => { resourceEnricher.Enrich(resource); });
            else
            {
                resourceEnricher.Enrich(resource);
                Assert.StrictEqual(expectedOutputResource?.ParentUri, resource?.ParentUri);
                Assert.Equal(expectedOutputResource?.OriginalUrl, resource?.OriginalUrl);
                Assert.StrictEqual(expectedOutputResource?.Uri, resource?.Uri);
                Assert.Equal(expectedOutputResource?.StatusCode, resource?.StatusCode);
                Assert.Equal(expectedOutputResource?.AbsoluteUrl, resource?.AbsoluteUrl);
                Assert.Equal(expectedOutputResource?.IsBroken, resource?.IsBroken);
                Assert.Equal(expectedOutputResource?.Uri?.Fragment, resource?.Uri?.Fragment);
            }
        }
    }
}