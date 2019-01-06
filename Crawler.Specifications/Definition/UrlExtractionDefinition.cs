using System;
using System.Collections.Generic;
using Helix.Specifications.Core;

namespace Helix.Crawler.Specifications
{
    class UrlExtractionDefinition : TheoryDescription<string, IList<string>, Type>
    {
        public UrlExtractionDefinition()
        {
            ExtractUrlsFromHtmlString();
            ThrowExceptionIfArgumentNull();
        }

        void ExtractUrlsFromHtmlString()
        {
            AddTheoryDescription("", new List<string>());
            AddTheoryDescription(@"
                <html>
                    <body>
                        <a href=""http://www.helix.com""></a>
                        <a href=""http://www.sanity.com/""></a>
                        <a href=""http://192.168.1.2""></a>
                    </body>
                </html>",
                new List<string>
                {
                    "http://www.helix.com",
                    "http://www.sanity.com/",
                    "http://192.168.1.2"
                }
            );
        }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p3: typeof(ArgumentNullException)); }
    }
}