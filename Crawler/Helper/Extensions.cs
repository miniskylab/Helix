using System;

namespace Helix.Crawler
{
    static class Extensions
    {
        public static string StripFragment(this string url)
        {
            var hashCharacterIndex = url.IndexOf("#", StringComparison.Ordinal);
            return hashCharacterIndex > 0 ? url.Substring(0, hashCharacterIndex) : url;
        }
    }
}