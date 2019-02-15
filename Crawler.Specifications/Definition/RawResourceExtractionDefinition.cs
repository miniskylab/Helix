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
            IgnoreAnchorTagsWithoutHrefAttribute();
            IgnoreAnchorTagsWithEmptyOrWhiteSpaceOnlyHrefAttributeValue();
            ThrowExceptionIfArgumentNull();
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
                                <a href=""//www.sanity.com""></a>
                                <a href=""http://www.sanity.com/""></a>
                                <a href=""ftp://www.sanity.com""></a>
                                <a href=""/with-leading-slash""></a>
                                <a href=""without-leading-slash""></a>
                                <a href=""http://192.168.1.2""></a>
                            </body>
                        </html>"
                },
                new List<RawResource>
                {
                    new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "//www.sanity.com" },
                    new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http://www.sanity.com/" },
                    new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "ftp://www.sanity.com" },
                    new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "/with-leading-slash" },
                    new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "without-leading-slash" },
                    new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http://192.168.1.2" }
                }
            );
        }

        void IgnoreAnchorTagsWithEmptyOrWhiteSpaceOnlyHrefAttributeValue()
        {
            AddTheoryDescription(new HtmlDocument
                {
                    Uri = new Uri("http://www.helix.com"),
                    Text = @"
                        <html>
                            <body>
                                <a href=""""></a>
                                <a href="" ""></a>
                            </body>
                        </html>"
                },
                new List<RawResource>()
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

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p3: typeof(ArgumentNullException)); }
    }
}