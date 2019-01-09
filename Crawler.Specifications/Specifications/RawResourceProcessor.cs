using System;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Xunit;

namespace Helix.Crawler.Specifications
{
    public class RawResourceProcessor : AbstractSpecifications
    {
        [Theory]
        [ClassData(typeof(RawResourceProcessingDefinition))]
        void CouldProcessRawResourcesIntoResources(RawResource rawResource, Resource expectedOutputResource,
            bool expectedProcessingResult, Type expectedExceptionType)
        {
            var rawResourceProcessor = ServiceLocator.Get<IRawResourceProcessor>();
            if (expectedExceptionType != null)
                Assert.Throws(expectedExceptionType, () => { rawResourceProcessor.TryProcessRawResource(rawResource, out _); });
            else
            {
                var processingResult = rawResourceProcessor.TryProcessRawResource(rawResource, out var resource);
                Assert.Equal(expectedProcessingResult, processingResult);
                Assert.Equal(expectedOutputResource.Localized, resource.Localized);
                Assert.Equal(expectedOutputResource.ParentUri, resource.ParentUri);
                Assert.Equal(expectedOutputResource.Uri, resource.Uri);
            }
        }
    }
}