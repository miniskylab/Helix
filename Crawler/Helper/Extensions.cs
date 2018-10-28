using System;

namespace Crawler
{
    static class Extensions
    {
        public static string EnsureEndsWith(this string url, char character) { return $"{url.TrimEnd(character)}{character}"; }

        public static string StripFragment(this string url)
        {
            var hashCharacterIndex = url.IndexOf("#", StringComparison.Ordinal);
            return hashCharacterIndex > 0 ? url.Substring(0, hashCharacterIndex) : url;
        }
    }
}