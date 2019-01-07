using System;
using System.Collections.Generic;
using Helix.Specifications;

namespace Helix.Crawler.Specifications
{
    class UrlExtractionDefinition : TheoryDescription<string, IList<string>, Type>
    {
        public UrlExtractionDefinition()
        {
            ExtractUrlsFromHtmlString();
            OnlySupportHttpAndHttpsSchemes();
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

        void OnlySupportHttpAndHttpsSchemes()
        {
            AddTheoryDescription(@"
                <html>
                    <body>
                        <a href=""ftp://www.helix.com""></a>
                        <a href=""mailto://www.helix.com""></a>
                        <a href=""telnet://www.helix.com""></a>
                        <a href=""file://www.helix.com""></a>
                    </body>
                </html>",
                new List<string>()
            );
        }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p3: typeof(ArgumentNullException)); }
    }
}