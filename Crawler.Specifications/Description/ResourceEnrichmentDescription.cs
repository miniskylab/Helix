using System;
using Helix.Crawler.Abstractions;
using Helix.Specifications;

namespace Helix.Crawler.Specifications
{
    internal class ResourceEnrichmentDescription : TheoryDescription<Resource, Resource, Type>
    {
        public ResourceEnrichmentDescription()
        {
            CreateUriFromOriginalUrl();

            DetectMalformedUris();
            ConvertRelativeToAbsoluteUrl();
            SupportOnlyHttpAndHttpsSchemes();
            StripFragments();

            ThrowExceptionIfArgumentNull();
        }

        void ConvertRelativeToAbsoluteUrl()
        {
            var parentUri = new Uri("http://www.helix.com");
            var relativeUrl = "without-leading-slash";
            var absoluteUrl = "http://www.helix.com/without-leading-slash";
            AddTheoryDescription(
                new Resource { ParentUri = parentUri, OriginalUrl = relativeUrl },
                new Resource { ParentUri = parentUri, OriginalUrl = relativeUrl, Uri = new Uri(absoluteUrl) }
            );

            relativeUrl = "/with-leading-slash";
            absoluteUrl = "http://www.helix.com/with-leading-slash";
            AddTheoryDescription(
                new Resource { ParentUri = parentUri, OriginalUrl = relativeUrl },
                new Resource { ParentUri = parentUri, OriginalUrl = relativeUrl, Uri = new Uri(absoluteUrl) }
            );

            relativeUrl = "//www.sanity.com";
            absoluteUrl = "http://www.sanity.com";
            AddTheoryDescription(
                new Resource { ParentUri = parentUri, OriginalUrl = relativeUrl },
                new Resource { ParentUri = parentUri, OriginalUrl = relativeUrl, Uri = new Uri(absoluteUrl) }
            );
        }

        void CreateUriFromOriginalUrl()
        {
            var parentUri = new Uri("http://www.helix.com");
            const string originalUrl = "http://www.helix.com/anything";
            AddTheoryDescription(
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl },
                new Resource
                {
                    ParentUri = parentUri,
                    OriginalUrl = originalUrl,
                    Uri = new Uri("http://www.helix.com/anything"),
                    StatusCode = 0
                }
            );
        }

        void DetectMalformedUris()
        {
            var parentUri = new Uri("http://www.helix.com");
            var originalUrl = "http:///malformed-uri";
            AddTheoryDescription(
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl },
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl, StatusCode = StatusCode.MalformedUri }
            );

            parentUri = new Uri("https://www.helix.com");
            originalUrl = "http:/incompatible-scheme";
            AddTheoryDescription(
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl },
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl, StatusCode = StatusCode.MalformedUri }
            );
        }

        void StripFragments()
        {
            var parentUri = new Uri("http://www.helix.com");
            const string originalUrl = "http://www.helix.com/anything#fragment";
            var uriWithoutFragment = new Uri("http://www.helix.com/anything");
            AddTheoryDescription(
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl },
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl, Uri = uriWithoutFragment }
            );
        }

        void SupportOnlyHttpAndHttpsSchemes()
        {
            const StatusCode statusCode = StatusCode.UriSchemeNotSupported;
            var parentUri = new Uri("http://www.helix.com");

            var originalUrl = "ftp://www.sanity.com";
            AddTheoryDescription(
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl },
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl, Uri = new Uri(originalUrl), StatusCode = statusCode }
            );

            originalUrl = "mailto://www.sanity.com";
            AddTheoryDescription(
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl },
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl, Uri = new Uri(originalUrl), StatusCode = statusCode }
            );

            originalUrl = "telnet://www.sanity.com";
            AddTheoryDescription(
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl },
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl, Uri = new Uri(originalUrl), StatusCode = statusCode }
            );

            originalUrl = "file://www.sanity.com";
            AddTheoryDescription(
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl },
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl, Uri = new Uri(originalUrl), StatusCode = statusCode }
            );

            originalUrl = "tel:12345678";
            AddTheoryDescription(
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl },
                new Resource { ParentUri = parentUri, OriginalUrl = originalUrl, Uri = new Uri(originalUrl), StatusCode = statusCode }
            );
        }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p3: typeof(ArgumentNullException)); }
    }
}