using System;
using System.Collections.Generic;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Xunit;

namespace Helix.Crawler.Specifications
{
    public class ResourceExtractor : AbstractSpecifications
    {
        [Theory]
        [ClassData(typeof(ResourceExtractionDescription))]
        void ExtractResourcesFromHtmlDocument(HtmlDocument inputHtmlDocument, IList<Resource> expectedOutputResources,
            Type expectedExceptionType)
        {
            var resourceExtractor = ServiceLocator.Get<IResourceExtractor>();
            if (expectedExceptionType != null)
            {
                Assert.Throws(
                    expectedExceptionType,
                    () => resourceExtractor.ExtractResourcesFrom(inputHtmlDocument)
                );
            }
            else
            {
                var extractedResources = resourceExtractor.ExtractResourcesFrom(inputHtmlDocument);
                Assert.Equal(expectedOutputResources.Count, extractedResources.Count);
                for (var index = 0; index < expectedOutputResources.Count; index++)
                {
                    Assert.Equal(expectedOutputResources[index].OriginalUrl, extractedResources[index].OriginalUrl);
                    Assert.StrictEqual(expectedOutputResources[index].ParentUri, extractedResources[index].ParentUri);
                    Assert.Equal(expectedOutputResources[index].IsExtracted, extractedResources[index].IsExtracted);
                }
            }
        }

        [Fact]
        void RaiseArgumentNullExceptionIfCallbackIsNull()
        {
            var resourceExtractor = ServiceLocator.Get<IResourceExtractor>();
            Assert.Throws<ArgumentNullException>(() => resourceExtractor.ExtractResourcesFrom(null));
        }
    }
}