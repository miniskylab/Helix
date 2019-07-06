using System;
using System.Collections.Generic;
using Helix.Crawler.Abstractions;
using Helix.Specifications;

namespace Helix.Crawler.Specifications
{
    internal class ResourceExtractionDescription : TheoryDescription<HtmlDocument, IList<Resource>, Type>
    {
        public ResourceExtractionDescription()
        {
            ExtractResourcesFromHtmlDocument();

            IgnoreAnchorTagsWithoutHrefAttribute();
            IgnoreAnchorTagsWithHrefAttributeContainingEmptyOrWhiteSpaceCharactersOnly();
            IgnoreAnchorTagsWithHrefAttributeContainingJavaScriptCode();

            ThrowExceptionIfArgumentNull();
        }

        void ExtractResourcesFromHtmlDocument()
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
                new List<Resource>
                {
                    new Resource { ParentUri = new Uri("http://www.helix.com"), OriginalUrl = "//www.sanity.com" },
                    new Resource { ParentUri = new Uri("http://www.helix.com"), OriginalUrl = "http://www.sanity.com/" },
                    new Resource { ParentUri = new Uri("http://www.helix.com"), OriginalUrl = "ftp://www.sanity.com" },
                    new Resource { ParentUri = new Uri("http://www.helix.com"), OriginalUrl = "/with-leading-slash" },
                    new Resource { ParentUri = new Uri("http://www.helix.com"), OriginalUrl = "without-leading-slash" },
                    new Resource { ParentUri = new Uri("http://www.helix.com"), OriginalUrl = "http://192.168.1.2" }
                }
            );
        }

        void IgnoreAnchorTagsWithHrefAttributeContainingEmptyOrWhiteSpaceCharactersOnly()
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
                new List<Resource>()
            );
        }

        void IgnoreAnchorTagsWithHrefAttributeContainingJavaScriptCode()
        {
            AddTheoryDescription(new HtmlDocument
                {
                    Uri = new Uri("http://www.helix.com"),
                    Text = @"
                        <html>
                            <body>
                                <a href=""javascript:test()""></a>
                                <a href=""JavaScript:test()""></a>
                            </body>
                        </html>"
                },
                new List<Resource>()
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
                new List<Resource>()
            );
        }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p3: typeof(ArgumentNullException)); }
    }
}