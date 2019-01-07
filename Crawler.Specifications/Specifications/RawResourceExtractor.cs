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
        void CouldExtractRawResourcesFromHtmlDocument(HtmlDocument htmlDocument, IList<RawResource> expectedRawResources,
            Type expectedExceptionType)
        {
            var rawResourceExtractedEventRaiseCount = 0;
            var rawResourceExtractor = ServiceLocator.Get<IRawResourceExtractor>();
            rawResourceExtractor.OnRawResourceExtracted += extractedRawResource =>
            {
                Assert.Single(
                    expectedRawResources ?? new List<RawResource>(),
                    expectedRawResource =>
                        expectedRawResource.Url == extractedRawResource.Url &&
                        expectedRawResource.ParentUrl == extractedRawResource.ParentUrl
                );
                Interlocked.Increment(ref rawResourceExtractedEventRaiseCount);
            };

            if (expectedExceptionType != null)
            {
                Assert.True(rawResourceExtractedEventRaiseCount == 0);
                Assert.Throws(expectedExceptionType, () => { rawResourceExtractor.ExtractRawResourcesFrom(htmlDocument); });
            }
            else
            {
                rawResourceExtractor.ExtractRawResourcesFrom(htmlDocument);
                Assert.Equal(expectedRawResources.Count, rawResourceExtractedEventRaiseCount);
            }
        }
    }
}