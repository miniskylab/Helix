using System;

namespace Helix.Crawler.Abstractions
{
    [Flags]
    public enum ResourceType
    {
        Unknown = 0,

        Html = 1,
        Css = 1 << 1,
        Script = 1 << 2,
        Json = 1 << 3,
        Xml = 1 << 4,
        ServerSentEvent = 1 << 5,
        Text = 1 << 6,

        Image = 1 << 7,
        Audio = 1 << 8,
        Video = 1 << 9,
        Font = 1 << 10,
        Blob = 1 << 11
    }
}