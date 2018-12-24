using System;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Xunit;

namespace Helix.Crawler.Specifications
{
    public class ResourceProcessor : AbstractSpecifications
    {
        [Theory]
        [ClassData(typeof(RawResourceProcessingDefinition))]
        void CouldProcessRawResourcesIntoResources(IRawResource rawResource, IResource expectedResource, bool expectedProcessingResult,
            Type expectedException)
        {
            var resourceProcessor = ServiceLocator.Get<IResourceProcessor>();
            if (expectedException != null)
                Assert.Throws(expectedException, () => { resourceProcessor.TryProcessRawResource(rawResource, out _); });
            else
            {
                var processingResult = resourceProcessor.TryProcessRawResource(rawResource, out var resource);
                Assert.Equal(expectedProcessingResult, processingResult);
                Assert.StrictEqual(expectedResource, resource);
            }
        }
    }
}