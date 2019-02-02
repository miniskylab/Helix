using System;

namespace Helix.Persistence.Abstractions
{
    public interface ILogger : IDisposable
    {
        void LogException(Exception exception);

        void LogInfo(string info);
    }
}