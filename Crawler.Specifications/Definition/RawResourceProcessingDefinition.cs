using System;
using Helix.Crawler.Abstractions;
using Helix.Specifications;
using Helix.Specifications.Core;

namespace Helix.Crawler.Specifications
{
    internal class RawResourceProcessingDefinition : TheoryDescription<IRawResource, IResource, bool, Type>
    {
        public RawResourceProcessingDefinition()
        {
            CreateResourceFromRawResource();
            ThrowExceptionIfArgumentNull();
        }

        void CreateResourceFromRawResource()
        {
            var rawResource = ServiceLocator.Get<IRawResource>();
            rawResource.ParentUrl = "http://www.helix.com";
            rawResource.Url = "http://www.helix.com/anything";

            var expectedOutputResource = ServiceLocator.Get<IResource>();
            expectedOutputResource.ParentUri = new Uri(rawResource.ParentUrl);
            expectedOutputResource.Uri = new Uri(rawResource.Url);

            AddTheoryDescription(rawResource, expectedOutputResource, true);
        }

        void ThrowExceptionIfArgumentNull() { AddTheoryDescription(null, p4: typeof(ArgumentNullException)); }
    }
}