using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Helix.Crawler
{
    public class HardwareMonitor
    {
        CancellationTokenSource _cancellationTokenSource;
        Task _samplingTask;

        public event Action OnHighCpuUsage;
        public event Action OnLowCpuUsage;

        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public HardwareMonitor() { }

        public void Dispose() { StopMonitoring(); }

        public void StartMonitoring(double millisecondSampleInterval = 60000, float highCpuUsageThreshold = 0.8f,
            float lowCpuUsageThreshold = 0.6f)
        {
            if (_samplingTask != null) throw new Exception("Already monitoring.");

            var cpuUtilizationSamples = new List<double>();
            _cancellationTokenSource = new CancellationTokenSource();
            _samplingTask = Task.Run(() =>
            {
                while (_cancellationTokenSource.IsCancellationRequested)
                {
                    var processes = Process.GetProcesses();
                    var startTime = DateTime.UtcNow;
                    var startTotalProcessorTime = processes.Aggregate(
                        TimeSpan.Zero,
                        (accumulatedTotalProcessorTime, process) => accumulatedTotalProcessorTime + process.TotalProcessorTime
                    );
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    var endTime = DateTime.UtcNow;
                    var endTotalProcessorTime = processes.Aggregate(
                        TimeSpan.Zero,
                        (accumulatedTotalProcessorTime, process) => accumulatedTotalProcessorTime + process.TotalProcessorTime
                    );

                    var totalElapsedTime = endTime - startTime;
                    var totalProcessorTime = endTotalProcessorTime - startTotalProcessorTime;
                    cpuUtilizationSamples.Add(totalProcessorTime / (Environment.ProcessorCount * totalElapsedTime));
                    if (cpuUtilizationSamples.Count < TimeSpan.FromMilliseconds(millisecondSampleInterval).TotalSeconds) continue;

                    CheckCpuUtilization(cpuUtilizationSamples.Average(), highCpuUsageThreshold, lowCpuUsageThreshold);
                    cpuUtilizationSamples.Clear();
                }
            }, _cancellationTokenSource.Token);
        }

        public void StopMonitoring()
        {
            if (_samplingTask == null) throw new Exception("Not started monitoring.");
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
    }
}