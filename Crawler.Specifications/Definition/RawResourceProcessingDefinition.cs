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
            FragmentsAreStrippedFromTheCreatedResource();
            ThrowExceptionIfArgumentNull();
        }

        void CreateResourceFromRawResource()
        {
            var rawResource = new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http://www.helix.com/anything" };
            var expectedOutputResource = new Resource { ParentUri = rawResource.ParentUri, Uri = new Uri(rawResource.Url) };
            AddTheoryDescription(rawResource, expectedOutputResource, true);
        }

        void FragmentsAreStrippedFromTheCreatedResource()
        {
            const string url = "http://www.helix.com/anything";
            var urlWithFragment = $"{url}#fragment";
            var rawResource = new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = urlWithFragment };
            var expectedOutputResource = new Resource { ParentUri = rawResource.ParentUri, Uri = new Uri(url) };
            AddTheoryDescription(rawResource, expectedOutputResource, true);
        }

        void ReturnFalseIfResourceCannotBeCreatedFromRawResource()
        {
            var rawResource = new RawResource { ParentUri = new Uri("http://www.helix.com"), Url = "http:///anything" };
            AddTheoryDescription(rawResource);
        }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p4: typeof(ArgumentNullException)); }
    }
}