using System.Collections.Concurrent;
using Helix.Bot.Abstractions;

namespace Bot.Business
{
    public class ProcessedUrlRegister : IProcessedUrlRegister
    {
        readonly ConcurrentDictionary<string, bool> _processedUrls;

        public ProcessedUrlRegister() { _processedUrls = new ConcurrentDictionary<string, bool>(); }

        public bool IsRegistered(string url) { return _processedUrls.ContainsKey(url); }

        public bool TryRegister(string url) { return _processedUrls.TryAdd(url, false); }
    }
}