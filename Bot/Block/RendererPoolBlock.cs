using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Helix.Bot.Abstractions;
using log4net;

namespace Helix.Bot
{
    public class RendererPoolBlock : TransformBlock<IHtmlRenderer, IHtmlRenderer>, IRendererPoolBlock
    {
        readonly Counter _counter;

        public BufferBlock<Event> Events { get; }

        public override Task Completion => Task.WhenAll(base.Completion, Events.Completion);

        public RendererPoolBlock(IHardwareMonitor hardwareMonitor, Func<IHtmlRenderer> getHtmlRenderer, ILog log)
        {
            _log = log;
            _hardwareMonitor = hardwareMonitor;

            _counter = new Counter();
            Events = new BufferBlock<Event>(new DataflowBlockOptions { EnsureOrdered = true });

            CreateHtmlRenderer();
            CreateAndDestroyHtmlRenderersAdaptively();

            #region Local Functions

            void CreateHtmlRenderer()
            {
                if (!this.Post(getHtmlRenderer()))
                    log.Info($"Failed to post newly created {nameof(HtmlRenderer)} to {nameof(RendererPoolBlock)}.");

                _counter.CreatedHtmlRenderCount++;
            }
            void CreateAndDestroyHtmlRenderersAdaptively()
            {
                hardwareMonitor.OnLowCpuAndMemoryUsage += (averageCpuUsage, memoryUsage) =>
                {
                    if (OutputCount > 0 || _counter.CreatedHtmlRenderCount == Configurations.MaxHtmlRendererCount) return;
                    CreateHtmlRenderer();

                    log.Info(
                        $"Low CPU usage ({averageCpuUsage}%) and low memory usage ({memoryUsage}%) detected. " +
                        $"Browser count increased from {_counter.CreatedHtmlRenderCount - 1} to {_counter.CreatedHtmlRenderCount}."
                    );
                };
                hardwareMonitor.OnHighCpuOrMemoryUsage += (averageCpuUsage, memoryUsage) =>
                {
                    if (_counter.CreatedHtmlRenderCount == 1) return;
                    this.Receive().Dispose();
                    _counter.CreatedHtmlRenderCount--;

                    if (averageCpuUsage == null && memoryUsage == null)
                        throw new ArgumentException(nameof(averageCpuUsage), nameof(memoryUsage));

                    if (averageCpuUsage != null && memoryUsage != null)
                        log.Info(
                            $"High CPU usage ({averageCpuUsage}%) and high memory usage ({memoryUsage}%) detected. " +
                            $"Browser count decreased from {_counter.CreatedHtmlRenderCount + 1} to {_counter.CreatedHtmlRenderCount}."
                        );
                    else if (averageCpuUsage != null)
                        log.Info(
                            $"High CPU usage ({averageCpuUsage}%) detected. " +
                            $"Browser count decreased from {_counter.CreatedHtmlRenderCount + 1} to {_counter.CreatedHtmlRenderCount}."
                        );
                    else
                        log.Info(
                            $"High memory usage ({memoryUsage}%) detected. " +
                            $"Browser count decreased from {_counter.CreatedHtmlRenderCount + 1} to {_counter.CreatedHtmlRenderCount}."
                        );
                };
            }

            #endregion
        }

        public void Activate() { _hardwareMonitor.StartMonitoring(); }

        public override void Complete()
        {
            _hardwareMonitor.StopMonitoring();
            DisposeHtmlRenderers();
            CheckMemoryLeak();

            base.Complete();
            base.Completion.Wait();
            Events.Complete();

            #region Local Functions

            void DisposeHtmlRenderers()
            {
                var htmlRendererDisposalTasks = new List<Task>();
                while (TryReceive(null, out var htmlRenderer))
                {
                    var closureHtmlRenderer = htmlRenderer;
                    htmlRendererDisposalTasks.Add(Task.Run(() =>
                    {
                        closureHtmlRenderer.Dispose();
                        Interlocked.Increment(ref _counter.DisposedHtmlRendererCount);

                        var stopProgressReportEvent = new StopProgressReportEvent
                        {
                            Message = $"Closing web browsers ({_counter.DisposedHtmlRendererCount}/{_counter.CreatedHtmlRenderCount}) ..."
                        };
                        if (!Events.Post(stopProgressReportEvent) && !Events.Completion.IsCompleted)
                            _log.Error($"Failed to post data to buffer block named [{nameof(Events)}].");
                    }));
                }

                Task.WhenAll(htmlRendererDisposalTasks).Wait();
                foreach (var htmlRendererDisposalTask in htmlRendererDisposalTasks)
                    htmlRendererDisposalTask.Dispose();
            }
            void CheckMemoryLeak()
            {
                if (_counter.DisposedHtmlRendererCount == _counter.CreatedHtmlRenderCount) return;
                var resourceName = $"{nameof(HtmlRenderer)}{(_counter.CreatedHtmlRenderCount > 1 ? "s" : string.Empty)}";
                var disposedCountText = _counter.DisposedHtmlRendererCount == 0 ? "none" : $"only {_counter.DisposedHtmlRendererCount}";
                _log.Warn(
                    "Orphaned resources detected! " +
                    $"{_counter.CreatedHtmlRenderCount} {resourceName} were created but {disposedCountText} could be found and disposed."
                );
            }

            #endregion
        }

        protected override IHtmlRenderer Transform(IHtmlRenderer htmlRenderer) { return htmlRenderer; }

        class Counter
        {
            public int CreatedHtmlRenderCount;

            public int DisposedHtmlRendererCount;
        }

        #region Injected Services

        readonly ILog _log;
        readonly IHardwareMonitor _hardwareMonitor;

        #endregion
    }
}