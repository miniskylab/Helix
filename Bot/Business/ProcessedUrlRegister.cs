using System.Collections.Concurrent;
using Helix.Bot.Abstractions;

namespace Bot.Business
{
    public class ProcessedUrlRegister : IProcessedUrlRegister
    {
        readonly ConcurrentDictionary<string, bool> _processedUrls;
    }
}