using System;
using Helix.Core;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public sealed class RawResourceProcessor : IRawResourceProcessor
    {
        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public RawResourceProcessor() { }

        public bool TryProcessRawResource(RawResource rawResource, out Resource resource)
        {
            if (rawResource == null) throw new ArgumentNullException();

            resource = null;
            if (!Uri.TryCreate(rawResource.Url, UriKind.Absolute, out var uri)) return false;
            StripFragmentFrom(ref uri);

            resource = new Resource { ParentUri = rawResource.ParentUri, Uri = uri, HttpStatusCode = rawResource.HttpStatusCode };
            return true;
        }

        static void StripFragmentFrom(ref Uri uri)
        {
            if (string.IsNullOrWhiteSpace(uri.Fragment)) return;
            uri = new Uri(uri.AbsoluteUri.Replace(uri.Fragment, string.Empty));
        }
    }
}