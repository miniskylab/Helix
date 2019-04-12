using System;

namespace Helix.Crawler.Abstractions
{
    public interface IHardwareMonitor : IDisposable
    {
        event Action OnHighCpuUsage;
        event Action OnLowCpuUsage;

        void StartMonitoring(double millisecondSampleDuration = 30000, float highCpuUsageThreshold = 0.8f,
            float lowCpuUsageThreshold = 0.6f);

        void StopMonitoring();
    }
}