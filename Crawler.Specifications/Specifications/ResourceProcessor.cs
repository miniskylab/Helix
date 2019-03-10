using System;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Xunit;

namespace Helix.Crawler.Specifications
{
    public class ResourceProcessor : AbstractSpecifications
    {
        [Theory]
        [ClassData(typeof(ResourceProcessingDefinition))]
        void CouldProcessResource(Resource resource, Resource expectedOutputResource, Type expectedExceptionType)
        {
            var resourceProcessor = ServiceLocator.Get<IResourceProcessor>();
            if (expectedExceptionType != null)
                Assert.Throws(expectedExceptionType, () => { resourceProcessor.Enrich(resource); });
            else
            {
                resourceProcessor.Enrich(resource);
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