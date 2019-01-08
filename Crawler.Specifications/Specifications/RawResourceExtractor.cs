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
                    expectedOutputRawResource => expectedOutputRawResource.Url == extractedRawResource.Url &&
                                                 expectedOutputRawResource.ParentUrl == extractedRawResource.ParentUrl
                );
                Interlocked.Increment(ref rawResourceExtractedEventRaiseCount);
            }

            if (expectedExceptionType != null)
            {
                Assert.True(rawResourceExtractedEventRaiseCount == 0);
                Assert.Throws(
                    expectedExceptionType,
                    () => { rawResourceExtractor.ExtractRawResourcesFrom(htmlDocument, OnRawResourceExtracted); }
                );
            }
            else
            {
                rawResourceExtractor.ExtractRawResourcesFrom(htmlDocument, OnRawResourceExtracted);
                Assert.Equal(expectedOutputRawResources.Count, rawResourceExtractedEventRaiseCount);
            }
        }
    }
}