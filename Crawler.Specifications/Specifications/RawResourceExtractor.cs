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
            rawResourceExtractor.OnRawResourceExtracted += extractedRawResource =>
            {
                Assert.Single(
                    expectedOutputRawResources ?? new List<RawResource>(),
                    expectedOutputRawResource => expectedOutputRawResource.Url.Equals(extractedRawResource.Url) &&
                                                 expectedOutputRawResource.ParentUri.Equals(extractedRawResource.ParentUri)
                );
                Interlocked.Increment(ref rawResourceExtractedEventRaiseCount);
            };

            if (expectedExceptionType != null)
            {
                Assert.True(rawResourceExtractedEventRaiseCount == 0);
                Assert.Throws(
                    expectedExceptionType,
                    () => rawResourceExtractor.ExtractRawResourcesFrom(htmlDocument)
                );
            }
            else
            {
                rawResourceExtractor.ExtractRawResourcesFrom(htmlDocument);
                Assert.Equal(expectedOutputRawResources.Count, rawResourceExtractedEventRaiseCount);
            }
        }
    }
}