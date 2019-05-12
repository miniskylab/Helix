using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    public class HardwareMonitor : IHardwareMonitor
    {
        CancellationTokenSource _cancellationTokenSource;
        Task _samplingTask;

        public bool IsRunning { get; private set; }

        public event Action<double> OnHighCpuUsage;
        public event Action<double> OnLowCpuUsage;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public HardwareMonitor() { }

        public void StartMonitoring(double millisecondSampleDuration, float highCpuUsageThreshold, float lowCpuUsageThreshold)
        {
            if (IsRunning) throw new Exception($"{nameof(HardwareMonitor)} is already running.");
            IsRunning = true;

            var cpuUtilizationSamples = new List<double>();
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

                        const float bufferRate = 2.2f;
                        cpuUtilizationSamples.Add(MathF.Ceiling(MathF.Ceiling(performanceCounter.NextValue()) * bufferRate));

                        var millisecondTotalElapsedTime = cpuUtilizationSamples.Count * millisecondSampleInterval;
                        if (millisecondTotalElapsedTime < millisecondSampleDuration) continue;

                        CheckCpuUtilization();
                        cpuUtilizationSamples.Clear();
                    }
                }
            }, _cancellationTokenSource.Token);

            void CheckCpuUtilization()
            {
                var averageCpuUtilization = cpuUtilizationSamples.Average();
                if (averageCpuUtilization >= highCpuUsageThreshold) OnHighCpuUsage?.Invoke(averageCpuUtilization);
                else if (averageCpuUtilization < lowCpuUsageThreshold) OnLowCpuUsage?.Invoke(averageCpuUtilization);
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
    }
}