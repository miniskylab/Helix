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
            ConvertRelativeUrlToAbsoluteUrl();
            OnlySupportHttpAndHttpsSchemes();
            IgnoreAnchorTagsWithoutHrefAttribute();
            ThrowExceptionIfArgumentNull();
        }

        void ConvertRelativeUrlToAbsoluteUrl()
        {
            const string htmlDocumentText = @"
                <html>
                    <body>
                        <a href=""without-leading-slash""></a>
                        <a href=""/with-leading-slash""></a>
                        <a href=""//www.sanity.com""></a>
                    </body>
                </html>";

            AddTheoryDescription(
                new HtmlDocument
                {
                    Uri = new Uri("http://www.helix.com"),
                    Text = htmlDocumentText
                },
                new List<RawResource>
                {
                    new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http://www.helix.com/without-leading-slash" },
                    new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http://www.helix.com/with-leading-slash" },
                    new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http://www.sanity.com" }
                }
            );
            AddTheoryDescription(
                new HtmlDocument
                {
                    Uri = new Uri("https://www.helix.com"),
                    Text = htmlDocumentText
                },
                new List<RawResource>
                {
                    new RawResource { ParentUri = new Uri("https://www.helix.com"), Url = "https://www.helix.com/without-leading-slash" },
                    new RawResource { ParentUri = new Uri("https://www.helix.com"), Url = "https://www.helix.com/with-leading-slash" },
                    new RawResource { ParentUri = new Uri("https://www.helix.com"), Url = "https://www.sanity.com" }
                }
            );
        }

        void ExtractRawResourcesFromHtmlDocument()
        {
            AddTheoryDescription(
                new HtmlDocument
                {
                    Uri = new Uri("http://www.helix.com"),
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
                    new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http://www.sanity.com/" },
                    new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http://192.168.1.2" }
                }
            );
        }

        void IgnoreAnchorTagsWithoutHrefAttribute()
        {
            AddTheoryDescription(new HtmlDocument
                {
                    Uri = new Uri("http://www.helix.com"),
                    Text = @"
                        <html>
                            <body>
                                <a></a>
                            </body>
                        </html>"
                },
                new List<RawResource>()
            );
        }

        void OnlySupportHttpAndHttpsSchemes()
        {
            AddTheoryDescription(new HtmlDocument
                {
                    Uri = new Uri("http://www.helix.com"),
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