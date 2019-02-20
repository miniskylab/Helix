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
        void CouldProcessRawResourcesIntoResources(RawResource rawResource, Resource expectedOutputResource, Type expectedExceptionType)
        {
            var rawResourceProcessor = ServiceLocator.Get<IRawResourceProcessor>();
            if (expectedExceptionType != null)
                Assert.Throws(expectedExceptionType, () => { rawResourceProcessor.ProcessRawResource(rawResource, out _); });
            else
            {
                rawResourceProcessor.ProcessRawResource(rawResource, out var resource);
                Assert.Equal(expectedOutputResource?.Localized, resource?.Localized);
                Assert.StrictEqual(expectedOutputResource?.ParentUri, resource?.ParentUri);
                Assert.StrictEqual(expectedOutputResource?.Uri, resource?.Uri);
                Assert.Equal(expectedOutputResource?.Uri?.Fragment, resource?.Uri?.Fragment);
            }
        }
    }
}