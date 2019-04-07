using System;
using System.Collections.Generic;
using System.Threading;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Xunit;

namespace Helix.Crawler.Specifications
{
    public class ResourceExtractor : AbstractSpecifications
    {
        [Theory]
        [ClassData(typeof(ResourceExtractionDefinition))]
        void ExtractResourcesFromHtmlDocument(HtmlDocument htmlDocument, IList<Resource> expectedOutputResources,
            Type expectedExceptionType)
        {
            var resourceExtractedEventRaiseCount = 0;
            var resourceExtractor = ServiceLocator.Get<IResourceExtractor>();
            if (expectedExceptionType != null)
            {
                Assert.True(resourceExtractedEventRaiseCount == 0);
                Assert.Throws(
                    expectedExceptionType,
                    () => resourceExtractor.ExtractResourcesFrom(htmlDocument, OnResourceExtracted)
                );
            }
            else
            {
                resourceExtractor.ExtractResourcesFrom(htmlDocument, OnResourceExtracted);
                Assert.Equal(expectedOutputResources.Count, resourceExtractedEventRaiseCount);
            }

            void OnResourceExtracted(Resource extractedResource)
            {
                Assert.Single(
                    expectedOutputResources ?? new List<Resource>(),
                    expectedOutputResource => expectedOutputResource?.ParentUri == extractedResource?.ParentUri &&
                                              expectedOutputResource?.OriginalUrl == extractedResource?.OriginalUrl
                );
                Interlocked.Increment(ref resourceExtractedEventRaiseCount);
            }
        }

        [Fact]
        void RaiseArgumentNullExceptionIfCallbackIsNull()
        {
            var resourceExtractor = ServiceLocator.Get<IResourceExtractor>();
            var htmlDocument = new HtmlDocument
            {
                Uri = new Uri("http://www.helix.com"),
                Text = @"<html>
                            <body>
                                <a href=""/anything""></a>
                                <a href=""//www.sanity.com""></a>
                            </body>
                        </html>"
            };
            Assert.Throws<ArgumentNullException>(() => resourceExtractor.ExtractResourcesFrom(htmlDocument, null));
            Assert.Throws<ArgumentNullException>(() => resourceExtractor.ExtractResourcesFrom(null, null));
        }
    }
}