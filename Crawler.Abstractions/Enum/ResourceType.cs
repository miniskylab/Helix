using System;

namespace Helix.Crawler.Abstractions
{
    [Flags]
    public enum ResourceType
    {
        Unknown = 0,
        Css = 1 << 0,
        Image = 1 << 1,
        Audio = 1 << 2,
        Video = 1 << 3,
        Font = 1 << 4,
        Script = 1 << 5,
        Blob = 1 << 6,
        StaticAsset = Css | Image | Audio | Video | Font | Script | Blob,

        Html = 1 << 7
    }
}