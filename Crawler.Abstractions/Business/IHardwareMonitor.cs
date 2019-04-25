using System;

namespace Helix.Crawler.Abstractions
{
    public interface IHardwareMonitor
    {
        bool IsRunning { get; }

        event Action<double> OnHighCpuUsage;
        event Action<double> OnLowCpuUsage;

        void StartMonitoring(double millisecondSampleDuration = 10000, float highCpuUsageThreshold = 0.65f,
            float lowCpuUsageThreshold = 0.45f);

        void StopMonitoring();
    }
}