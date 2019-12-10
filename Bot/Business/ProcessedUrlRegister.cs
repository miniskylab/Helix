using System.Collections.Concurrent;
using System.Collections.Generic;
using Helix.Bot.Abstractions;

namespace Bot.Business
{
    public class ProcessedUrlRegister : IProcessedUrlRegister
    {
        readonly ConcurrentDictionary<string, bool> _processedUrls;

        public ProcessedUrlRegister() { _processedUrls = new ConcurrentDictionary<string, bool>(); }

        public bool IsRegistered(string url) { return _processedUrls.ContainsKey(url); }

        public bool IsSavedToReportFile(string url)
        {
            if (!IsRegistered(url)) throw new KeyNotFoundException($"Url is not registered: {url}");
            return _processedUrls[url];
        }

        public void MarkAsSavedToReportFile(string url)
        {
            if (!IsRegistered(url)) throw new KeyNotFoundException($"Url is not registered: {url}");
            _processedUrls[url] = true;
        }

        public bool TryRegister(string url) { return _processedUrls.TryAdd(url, false); }
    }
}