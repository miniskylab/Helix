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

        public event Action OnHighCpuUsage;
        public event Action OnLowCpuUsage;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public HardwareMonitor() { }

        public void Dispose() { StopMonitoring(); }

        public void StartMonitoring(double millisecondSampleDuration = 30000, float highCpuUsageThreshold = 0.8f,
            float lowCpuUsageThreshold = 0.6f)
        {
            if (_samplingTask != null) throw new Exception($"{nameof(HardwareMonitor)} is already running.");

            var cpuUtilizationSamples = new List<double>();
            _cancellationTokenSource = new CancellationTokenSource();
            _samplingTask = Task.Run(() =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    var processes = Process.GetProcesses().Where(process => process.Id > 0).ToList();
                    var startTime = DateTime.UtcNow;
                    var startProcessorTimeSamples = GetProcessorTimeSamples(processes);
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    var endTime = DateTime.UtcNow;
                    var endProcessorTimeSamples = GetProcessorTimeSamples(processes);

                    var totalElapsedTime = endTime - startTime;
                    var allProcessesTotalProcessorTime = endProcessorTimeSamples.Keys.Intersect(startProcessorTimeSamples.Keys)
                        .Aggregate(TimeSpan.Zero, (totalProcessorTime, processId) =>
                        {
                            var processorTime = endProcessorTimeSamples[processId] - startProcessorTimeSamples[processId];
                            return totalProcessorTime + processorTime;
                        });

                    cpuUtilizationSamples.Add(allProcessesTotalProcessorTime / (Environment.ProcessorCount * totalElapsedTime));
                    if (cpuUtilizationSamples.Count < TimeSpan.FromMilliseconds(millisecondSampleDuration).TotalSeconds) continue;
                    CheckCpuUtilization(cpuUtilizationSamples.Average(), highCpuUsageThreshold, lowCpuUsageThreshold);
                    cpuUtilizationSamples.Clear();
                }
            }, _cancellationTokenSource.Token);
        }

        public void StopMonitoring()
        {
            if (_samplingTask == null) throw new Exception($"{nameof(HardwareMonitor)} is not running.");
            _cancellationTokenSource.Cancel();
            _samplingTask.Wait();

            _cancellationTokenSource = null;
            _samplingTask = null;
        }

        void CheckCpuUtilization(double averageCpuUtilization, float highCpuUsageThreshold, float lowCpuUsageThreshold)
        {
            if (averageCpuUtilization >= highCpuUsageThreshold) OnHighCpuUsage?.Invoke();
            else if (averageCpuUtilization < lowCpuUsageThreshold) OnLowCpuUsage?.Invoke();
        }

        static IDictionary<int, TimeSpan> GetProcessorTimeSamples(IEnumerable<Process> processes)
        {
            var processorTimeSamples = new Dictionary<int, TimeSpan>();
            foreach (var process in processes)
            {
                try
                {
                    var totalProcessorTime = process.TotalProcessorTime;
                    processorTimeSamples.Add(process.Id, totalProcessorTime);
                }
                catch (InvalidOperationException)
                {
                    /* A process might exit before the attempt to get its TotalProcessorTime.
                     * If that happens an InvalidOperationException will be thrown and such processes will be ignored. */
                }
            }
            return processorTimeSamples;
        }
    }
}