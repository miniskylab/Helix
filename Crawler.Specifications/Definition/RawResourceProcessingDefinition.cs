using System;
using Helix.Crawler.Abstractions;
using Helix.Specifications;

namespace Helix.Crawler.Specifications
{
    class RawResourceProcessingDefinition : TheoryDescription<RawResource, Resource, bool, Type>
    {
        public RawResourceProcessingDefinition()
        {
            CreateResourceFromRawResource();
            ReturnFalseIfResourceCannotBeCreatedFromRawResource();
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
                true
            );
            AddTheoryDescription(
                new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "/with-leading-slash" },
                new Resource { ParentUri = new Uri("http://www.helix.com"), Uri = new Uri("http://www.helix.com/with-leading-slash") },
                true
            );
            AddTheoryDescription(
                new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "//www.sanity.com" },
                new Resource { ParentUri = new Uri("http://www.helix.com"), Uri = new Uri("http://www.sanity.com") },
                true
            );
        }

        void CreateResourceFromRawResource()
        {
            AddTheoryDescription(
                new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http://www.helix.com/anything" },
                new Resource { ParentUri = new Uri("http://www.helix.com"), Uri = new Uri("http://www.helix.com/anything") },
                true
            );
        }

        void FragmentsAreStrippedFromTheCreatedResource()
        {
            AddTheoryDescription(
                new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http://www.helix.com/anything#fragment" },
                new Resource { ParentUri = new Uri("http://www.helix.com"), Uri = new Uri("http://www.helix.com/anything") },
                true
            );
        }

        void OnlySupportHttpAndHttpsSchemes()
        {
            AddTheoryDescription(new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "ftp://www.sanity.com" });
            AddTheoryDescription(new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "mailto://www.sanity.com" });
            AddTheoryDescription(new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "telnet://www.sanity.com" });
            AddTheoryDescription(new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "file://www.sanity.com" });
        }

        void ReturnFalseIfResourceCannotBeCreatedFromRawResource()
        {
            AddTheoryDescription(new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http:///anything" });
        }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p4: typeof(ArgumentNullException)); }
    }
}