using System;
using System.Collections.Generic;
using Helix.Crawler.Abstractions;
using Helix.Specifications;

namespace Helix.Crawler.Specifications
{
    class RawResourceExtractionDefinition : TheoryDescription<HtmlDocument, IList<RawResource>, Type>
    {
        public RawResourceExtractionDefinition()
        {
            ExtractRawResourcesFromHtmlDocument();
            OnlySupportHttpAndHttpsSchemes();
            ThrowExceptionIfArgumentNull();
        }

        void ExtractRawResourcesFromHtmlDocument()
        {
            AddTheoryDescription(
                new HtmlDocument
                {
                    Url = "http://www.helix.com",
                    Text = @"
                        <html>
                            <body>
                                <a href=""http://www.sanity.com/""></a>
                                <a href=""http://192.168.1.2""></a>
                            </body>
                        </html>"
                },
                new List<RawResource>
                {
                    new RawResource { ParentUrl = "http://www.helix.com", Url = "http://www.sanity.com/" },
                    new RawResource { ParentUrl = "http://www.helix.com", Url = "http://192.168.1.2" },
                }
            );
        }

        void OnlySupportHttpAndHttpsSchemes()
        {
            AddTheoryDescription(new HtmlDocument
                {
                    Url = "http://www.helix.com",
                    Text = @"
                        <html>
                            <body>
                                <a href=""ftp://www.sanity.com""></a>
                                <a href=""mailto://www.sanity.com""></a>
                                <a href=""telnet://www.sanity.com""></a>
                                <a href=""file://www.sanity.com""></a>
                            </body>
                        </html>"
                },
                new List<RawResource>()
            );
        }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p3: typeof(ArgumentNullException)); }
    }
}