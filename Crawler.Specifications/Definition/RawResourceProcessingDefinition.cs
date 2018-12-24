using System;
using Helix.Crawler.Abstractions;
using Helix.Specifications.Core;

namespace Helix.Crawler.Specifications
{
    internal class RawResourceProcessingDefinition : TheoryData<IRawResource, IResource, bool, Type>
    {
        public RawResourceProcessingDefinition() { CreateResourceFromRawResource(); }

        void CreateResourceFromRawResource() { }
    }
}