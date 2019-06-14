using System;

namespace Helix.Crawler.Abstractions
{
    [Flags]
    public enum ResourceType
    {
        Unknown = 0,
        Css = 1 << 0,
        Pdf = 1 << 1,
        Image = 1 << 2,
        Audio = 1 << 3,
        Video = 1 << 4,
        Font = 1 << 5,
        Script = 1 << 6,
        Blob = 1 << 7,
        StaticAsset = Css | Image | Pdf | Audio | Video | Font | Script | Blob,

        Html = 1 << 8
    }
}