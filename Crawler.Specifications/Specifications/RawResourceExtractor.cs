using System;
using System.Collections.Generic;
using System.Threading;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Xunit;

namespace Helix.Crawler.Specifications
{
    public class RawResourceExtractor : AbstractSpecifications
    {
        [Theory]
        [ClassData(typeof(RawResourceExtractionDefinition))]
        void CouldExtractRawResourcesFromHtmlDocument(HtmlDocument htmlDocument, IList<RawResource> expectedOutputRawResources,
            Type expectedExceptionType)
        {
            var rawResourceExtractedEventRaiseCount = 0;
            var rawResourceExtractor = ServiceLocator.Get<IRawResourceExtractor>();
            void OnRawResourceExtracted(RawResource extractedRawResource)
            {
                Assert.Single(
                    expectedOutputRawResources ?? new List<RawResource>(),
                    expectedOutputRawResource => expectedOutputRawResource.Url.Equals(extractedRawResource.Url) &&
                                                 expectedOutputRawResource.ParentUri.Equals(extractedRawResource.ParentUri)
                );
                Interlocked.Increment(ref rawResourceExtractedEventRaiseCount);
            }

            if (expectedExceptionType != null)
            {
                Assert.True(rawResourceExtractedEventRaiseCount == 0);
                Assert.Throws(
                    expectedExceptionType,
                    () => rawResourceExtractor.ExtractRawResourcesFrom(htmlDocument, OnRawResourceExtracted)
                );
            }
            else
            {
                rawResourceExtractor.ExtractRawResourcesFrom(htmlDocument, OnRawResourceExtracted);
                Assert.Equal(expectedOutputRawResources.Count, rawResourceExtractedEventRaiseCount);
            }
        }

        [Fact]
        void RaiseArgumentNullExceptionIfCallbackIsNull()
        {
            var rawResourceExtractor = ServiceLocator.Get<IRawResourceExtractor>();
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
            Assert.Throws<ArgumentNullException>(() => rawResourceExtractor.ExtractRawResourcesFrom(htmlDocument, null));
            Assert.Throws<ArgumentNullException>(() => rawResourceExtractor.ExtractRawResourcesFrom(null, null));
        }
    }
}