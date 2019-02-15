using System;
using Helix.Crawler.Abstractions;
using Helix.Specifications;

namespace Helix.Crawler.Specifications
{
    class RawResourceProcessingDefinition : TheoryDescription<RawResource, Resource, HttpStatusCode, Type>
    {
        public RawResourceProcessingDefinition()
        {
            CreateResourceFromRawResource();
            DetectMalformedUris();
            ConvertRelativeUrlToAbsoluteUrl();
            OnlySupportHttpAndHttpsSchemes();
            FragmentsAreStrippedFromTheCreatedResource();
            ThrowExceptionIfArgumentNull();
        }

        void ConvertRelativeUrlToAbsoluteUrl()
        {
            AddTheoryDescription(
                new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "without-leading-slash" },
                new Resource { ParentUri = new Uri("http://www.helix.com"), Uri = new Uri("http://www.helix.com/without-leading-slash") },
                HttpStatusCode.OK
            );
            AddTheoryDescription(
                new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "/with-leading-slash" },
                new Resource { ParentUri = new Uri("http://www.helix.com"), Uri = new Uri("http://www.helix.com/with-leading-slash") },
                HttpStatusCode.OK
            );
            AddTheoryDescription(
                new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "//www.sanity.com" },
                new Resource { ParentUri = new Uri("http://www.helix.com"), Uri = new Uri("http://www.sanity.com") },
                HttpStatusCode.OK
            );
        }

        void CreateResourceFromRawResource()
        {
            AddTheoryDescription(
                new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http://www.helix.com/anything" },
                new Resource { ParentUri = new Uri("http://www.helix.com"), Uri = new Uri("http://www.helix.com/anything") },
                HttpStatusCode.OK
            );
        }

        void DetectMalformedUris()
        {
            AddTheoryDescription(
                new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http:///malformed-uri" },
                p3: HttpStatusCode.MalformedUri
            );
        }

        void FragmentsAreStrippedFromTheCreatedResource()
        {
            AddTheoryDescription(
                new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http://www.helix.com/anything#fragment" },
                new Resource { ParentUri = new Uri("http://www.helix.com"), Uri = new Uri("http://www.helix.com/anything") },
                HttpStatusCode.OK
            );
        }

        void OnlySupportHttpAndHttpsSchemes()
        {
            AddTheoryDescription(
                new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "ftp://www.sanity.com" },
                p3: HttpStatusCode.UriSchemeNotSupported
            );
            AddTheoryDescription(
                new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "mailto://www.sanity.com" },
                p3: HttpStatusCode.UriSchemeNotSupported
            );
            AddTheoryDescription(
                new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "telnet://www.sanity.com" },
                p3: HttpStatusCode.UriSchemeNotSupported
            );
            AddTheoryDescription(
                new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "file://www.sanity.com" },
                p3: HttpStatusCode.UriSchemeNotSupported
            );
            AddTheoryDescription(
                new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "tel:12345678" },
                p3: HttpStatusCode.UriSchemeNotSupported
            );
        }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p4: typeof(ArgumentNullException)); }
    }
}