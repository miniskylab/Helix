using System;
using System.Collections.Generic;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Xunit;

namespace Helix.Crawler.Specifications
{
    public class HtmlParser : AbstractSpecifications
    {
        [Theory]
        [ClassData(typeof(UrlExtractionDefinition))]
        void CouldPExtractUrlsFromHtml(string html, IList<string> expectedExtractedUrls, Type expectedExceptionType)
        {
            var urlCollectedEventRaiseCount = 0;
            var htmlParser = ServiceLocator.Get<IHtmlParser>();
            htmlParser.OnUrlCollected += url =>
            {
                Assert.Contains(url, expectedExtractedUrls ?? new List<string>());
                urlCollectedEventRaiseCount++;
            };

            if (expectedExceptionType != null)
            {
                Assert.True(urlCollectedEventRaiseCount == 0);
                Assert.Throws(expectedExceptionType, () => { htmlParser.ExtractUrlsFrom(html); });
            }
            else
            {
                htmlParser.ExtractUrlsFrom(html);
                Assert.Equal(expectedExtractedUrls.Count, urlCollectedEventRaiseCount);
            }
        }
    }
}