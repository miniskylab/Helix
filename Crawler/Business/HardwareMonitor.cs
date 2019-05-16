using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;
using Helix.Persistence.Abstractions;

namespace Helix.Crawler
{
    public class HardwareMonitor : IHardwareMonitor
    {
        CancellationTokenSource _cancellationTokenSource;
        readonly ILogger _logger;
        Task _samplingTask;

        public bool IsRunning { get; private set; }

        public event Action<int?, int?> OnHighCpuOrMemoryUsage;
        public event Action<int, int> OnLowCpuAndMemoryUsage;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public HardwareMonitor(ILogger logger) { _logger = logger; }

        public void StartMonitoring(double millisecondSampleDuration, float highCpuUsageThreshold, float lowCpuUsageThreshold,
            float highMemoryUsageThreshold, float lowMemoryUsageThreshold)
        {
            if (IsRunning) throw new Exception($"{nameof(HardwareMonitor)} is already running.");
            IsRunning = true;

            var cpuUsageSamples = new List<int>();
            _cancellationTokenSource = new CancellationTokenSource();
            _samplingTask = Task.Run(() =>
            {
                using (var performanceCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
                {
                    performanceCounter.NextValue();
                    while (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        const int millisecondSampleInterval = 1000;
                        Thread.Sleep(millisecondSampleInterval);

                        const float bufferRate = 2.2f; // TODO: to 1.3
                        cpuUsageSamples.Add((int) MathF.Ceiling(performanceCounter.NextValue() * bufferRate));

                        var millisecondTotalElapsedTime = cpuUsageSamples.Count * millisecondSampleInterval;
                        if (millisecondTotalElapsedTime < millisecondSampleDuration) continue;

                        CheckCpuAndMemoryUsage();
                        cpuUsageSamples.Clear();
                    }
                }
            }, _cancellationTokenSource.Token);

            void CheckCpuAndMemoryUsage()
            {
                var averageCpuUsage = (int) Math.Ceiling(cpuUsageSamples.Average());
                var memoryUsage = GetMemoryUsage();
                var highCpuUsage = averageCpuUsage >= highCpuUsageThreshold;
                var lowCpuUsage = averageCpuUsage < lowCpuUsageThreshold;
                var highMemoryUsage = memoryUsage >= highMemoryUsageThreshold;
                var lowMemoryUsage = memoryUsage < lowMemoryUsageThreshold;

                if (highCpuUsage) OnHighCpuOrMemoryUsage?.Invoke(averageCpuUsage, null);
                else if (highMemoryUsage) OnHighCpuOrMemoryUsage?.Invoke(null, memoryUsage);
                else if (lowCpuUsage && lowMemoryUsage) OnLowCpuAndMemoryUsage?.Invoke(averageCpuUsage, memoryUsage);
            }
        }

        public void StopMonitoring()
        {
            if (!IsRunning) throw new Exception($"{nameof(HardwareMonitor)} is not running.");
            IsRunning = false;

            _cancellationTokenSource.Cancel();
            _samplingTask.Wait();

            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            _samplingTask.Dispose();
            _samplingTask = null;
        }

        int GetMemoryUsage()
        {
            var performanceInformation = new PerformanceInformation();
            if (!GetPerformanceInfo(out performanceInformation, Marshal.SizeOf(performanceInformation)))
                _logger.LogInfo($"Failed to get performance information. Default value used is: {performanceInformation}");

            var totalMemory = performanceInformation.PhysicalTotal.ToInt64();
            var consumedMemory = totalMemory - performanceInformation.PhysicalAvailable.ToInt64();
            return (int) Math.Round(100f * consumedMemory / totalMemory, 0);
        }

        [DllImport("psapi.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetPerformanceInfo([Out] out PerformanceInformation performanceInformation, [In] int size);

        [StructLayout(LayoutKind.Sequential)]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
        struct PerformanceInformation
        {
            public readonly int Size;
            public readonly IntPtr CommitTotal;
            public readonly IntPtr CommitLimit;
            public readonly IntPtr CommitPeak;
            public readonly IntPtr PhysicalTotal;
            public readonly IntPtr PhysicalAvailable;
            public readonly IntPtr SystemCache;
            public readonly IntPtr KernelTotal;
            public readonly IntPtr KernelPaged;
            public readonly IntPtr KernelNonPaged;
            public readonly IntPtr PageSize;
            public readonly int HandlesCount;
            public readonly int ProcessCount;
            public readonly int ThreadCount;
        }
    }
}