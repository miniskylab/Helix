using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler.Abstractions;

namespace Helix.Crawler
{
    // TODO: Use win32 api
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
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    var startTime = DateTime.UtcNow;
                    var startCpuUtilization = GetCpuUtilization();
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    var endTime = DateTime.UtcNow;
                    var endCpuUtilization = GetCpuUtilization();

                    var totalElapsedTime = endTime - startTime;
                    var totalCpuUtilization = endCpuUtilization.Keys.Intersect(startCpuUtilization.Keys)
                        .Aggregate(TimeSpan.Zero, (totalProcessorTime, processId) =>
                        {
                            var processorTime = endCpuUtilization[processId] - startCpuUtilization[processId];
                            return totalProcessorTime + processorTime;
                        });

                    const float compensationRate = 1.3f;
                    cpuUtilizationSamples.Add(totalCpuUtilization * compensationRate / (Environment.ProcessorCount * totalElapsedTime));
                    if (cpuUtilizationSamples.Count < TimeSpan.FromMilliseconds(millisecondSampleDuration).TotalSeconds) continue;
                    CheckCpuUtilization();
                    cpuUtilizationSamples.Clear();

                    void CheckCpuUtilization()
                    {
                        var averageCpuUtilization = cpuUtilizationSamples.Average();
                        if (averageCpuUtilization >= highCpuUsageThreshold) OnHighCpuUsage?.Invoke(averageCpuUtilization);
                        else if (averageCpuUtilization < lowCpuUsageThreshold) OnLowCpuUsage?.Invoke(averageCpuUtilization);
                    }
                    IDictionary<int, TimeSpan> GetCpuUtilization()
                    {
                        var cpuUtilization = new Dictionary<int, TimeSpan>();
                        foreach (var process in Process.GetProcesses())
                        {
                            try
                            {
                                var totalProcessorTime = process.TotalProcessorTime;
                                cpuUtilization.Add(process.Id, totalProcessorTime);
                            }
                            catch (Win32Exception)
                            {
                                /* Ignore processes which we don't have permission to inspect. */
                            }
                            catch (InvalidOperationException)
                            {
                                /* A process might exit before the attempt to get its TotalProcessorTime.
                                 * If that happens an InvalidOperationException will be thrown and such processes will be ignored. */
                            }
                        }
                        return cpuUtilization;
                    }
                }
            }, _cancellationTokenSource.Token);
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