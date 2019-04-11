using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Helix.Crawler
{
    public class HardwareMonitor
    {
        public async Task<double> GetCpuUsageEvery(TimeSpan timeSpan)
        {
            var startTime = DateTime.UtcNow;
            var startTotalProcessorTime = Process.GetCurrentProcess().TotalProcessorTime; // TODO: all processes
            await Task.Delay(timeSpan);

            var endTime = DateTime.UtcNow;
            var endTotalProcessorTime = Process.GetCurrentProcess().TotalProcessorTime;
            var totalProcessorTime = endTotalProcessorTime - startTotalProcessorTime;
            var totalElapsedTime = endTime - startTime;
            return totalProcessorTime / (Environment.ProcessorCount * totalElapsedTime);
        }
    }
}