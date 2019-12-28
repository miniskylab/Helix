using System;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IHardwareMonitor : IService
    {
        event Action<int?, int?> OnHighCpuOrMemoryUsage;

        event Action<int, int> OnLowCpuAndMemoryUsage;

        void StartMonitoring(double millisecondSampleDuration = 10000, float highCpuUsageThreshold = 65, float lowCpuUsageThreshold = 45,
            float highMemoryUsageThreshold = 90, float lowMemoryUsageThreshold = 75);

        void StopMonitoring();
    }
}