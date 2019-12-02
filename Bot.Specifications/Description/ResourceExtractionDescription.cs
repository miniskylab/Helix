using System;
using System.Collections.Generic;
using Helix.Bot.Abstractions;
using Helix.Specifications;

namespace Helix.Bot.Specifications
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
            NeverReturnNull();
        }

        void ExtractResourcesFromHtmlDocument()
        {
            AddTheoryDescription(
                new HtmlDocument
                {
                    Uri = new Uri("http://www.helix.com"),
                    HtmlText = @"
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
                    Resource("//www.sanity.com"),
                    Resource("http://www.sanity.com/"),
                    Resource("ftp://www.sanity.com"),
                    Resource("/with-leading-slash"),
                    Resource("without-leading-slash"),
                    Resource("http://192.168.1.2")
                }
            );

            #region Local Functions

            static Resource Resource(string originalUrl) => new Resource(0, originalUrl, new Uri("http://www.helix.com"), true);

            #endregion
        }

        void IgnoreAnchorTagsWithHrefAttributeContainingEmptyOrWhiteSpaceCharactersOnly()
        {
            AddTheoryDescription(new HtmlDocument
                {
                    Uri = new Uri("http://www.helix.com"),
                    HtmlText = @"
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
                    HtmlText = @"
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
                    HtmlText = @"
                        <html>
                            <body>
                                <a></a>
                            </body>
                        </html>"
                },
                new List<Resource>()
            );
        }

        void NeverReturnNull()
        {
            AddTheoryDescription(new HtmlDocument
                {
                    Uri = new Uri("http://www.helix.com"),
                    HtmlText = @"
                        <html>
                            <body>
                            </body>
                        </html>"
                },
                new List<Resource>()
            );
        }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p3: typeof(ArgumentNullException)); }
    }
}