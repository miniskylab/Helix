using System;
using System.Collections.Generic;
using Helix.Bot.Abstractions;
using Helix.Specifications;
using Xunit;

namespace Helix.Bot.Specifications
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
                    Assert.Equal(expectedOutputResources[index].OriginalUri, extractedResources[index].OriginalUri);
                    Assert.StrictEqual(expectedOutputResources[index].ParentUri, extractedResources[index].ParentUri);
                    Assert.Equal(
                        expectedOutputResources[index].IsExtractedFromHtmlDocument,
                        extractedResources[index].IsExtractedFromHtmlDocument
                    );
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