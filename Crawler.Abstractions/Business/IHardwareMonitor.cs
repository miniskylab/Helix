using System;

namespace Helix.Crawler.Abstractions
{
    public interface IHardwareMonitor : IDisposable
    {
        event Action<double> OnHighCpuUsage;
        event Action<double> OnLowCpuUsage;

        void StartMonitoring(double millisecondSampleDuration = 10000, float highCpuUsageThreshold = 0.8f,
            float lowCpuUsageThreshold = 0.6f);

        void StopMonitoring();
    }
}