using System;

namespace Helix
{
    public class Resource
    {
        public Uri ParentUri { get; }
        public Uri Uri { get; }

        public Resource(Uri uri, Uri parentUri = null)
        {
            if (!string.IsNullOrWhiteSpace(uri.Fragment))
                uri = new Uri(uri.OriginalString.Replace(uri.Fragment, string.Empty).ToLower());

            var originalString = uri.OriginalString.ToLower();
            if (parentUri != null && originalString.StartsWith("/"))
            {
                var baseString = originalString.StartsWith("//")
                    ? $"{parentUri.Scheme}:"
                    : $"{parentUri.Scheme}://{parentUri.Host}:{parentUri.Port}";
                uri = new Uri($"{baseString}/{originalString}");
            }

            ParentUri = parentUri;
            Uri = uri;
        }
    }
}