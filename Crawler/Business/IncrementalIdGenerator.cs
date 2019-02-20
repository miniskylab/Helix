using System.Threading;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public class IncrementalIdGenerator : IIncrementalIdGenerator
    {
        int _currentId;

        public int GetNext() { return Interlocked.Increment(ref _currentId); }
    }
}