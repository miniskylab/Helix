using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler;
using Helix.Crawler.Abstractions;
using Helix.IPC;
using Helix.IPC.Abstractions;
using Newtonsoft.Json;

namespace Helix.Gui
{
    public static class GuiController
    {
        static Task _constantRedrawTask;
        static readonly Process GuiProcess = new Process { StartInfo = { FileName = "ui/electron.exe" } };
        static readonly IIpcSocket IpcSocket = new IpcSocket("127.0.0.1", 18880); // TODO: Dependency Injection?
        static readonly ManualResetEvent ManualResetEvent = new ManualResetEvent(false);
        static readonly Stopwatch Stopwatch = new Stopwatch();

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        static void Main()
        {
            /* Hide the console windows.
             * TODO: Will be removed and replaced with built-in .Net Core 3.0 feature. */
            ShowWindow(GetConsoleWindow(), 0);

            IpcSocket.On("btn-start-clicked", configurationJsonString =>
            {
                try
                {
                    CrawlerBot.EventBroadcast += OnStartProgressUpdated;
                    CrawlerBot.EventBroadcast += OnResourceVerified;
                    CrawlerBot.EventBroadcast += OnStopped;
                    CrawlerBot.StartWorking(new Configurations(configurationJsonString));
                    RedrawEvery(TimeSpan.FromSeconds(1));
                }
                catch (Exception exception) { Redraw(exception.Message, false); }

                void OnStopped(Event @event)
                {
                    if (@event.EventType != EventType.Stopped) return;
                    Redraw(@event.Message);

                    switch (CrawlerBot.CrawlerState)
                    {
                        case CrawlerState.RanToCompletion:
                        case CrawlerState.Faulted:
                            Redraw(restrictHumanInteraction: false);
                            break;
                    }
                }
                void OnStartProgressUpdated(Event @event)
                {
                    if (@event.EventType != EventType.StartProgressUpdated)
                    {
                        CrawlerBot.EventBroadcast -= OnStartProgressUpdated;
                        return;
                    }
                    Redraw(@event.Message, redrawEverything: false);
                }
            });
            IpcSocket.On("btn-close-clicked", _ =>
            {
                StopWorking();
                ManualResetEvent.Set();
                ManualResetEvent.Dispose();

                void StopWorking()
                {
                    try
                    {
                        CrawlerBot.EventBroadcast -= OnResourceVerified;
                        CrawlerBot.EventBroadcast += OnStopProgressUpdated;
                        CrawlerBot.StopWorking();

                        var waitingTime = TimeSpan.FromMinutes(1);
                        if (_constantRedrawTask == null || _constantRedrawTask.Wait(waitingTime)) return;

                        var errorMessage = $"Constant redrawing task failed to finish after {waitingTime.TotalSeconds} seconds.";
                        File.AppendAllText(
                            Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "debug.log"),
                            $"\r\n[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] {errorMessage}"
                        );
                    }
                    catch (Exception exception)
                    {
                        File.AppendAllText("debug.log", $"\r\n[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] {exception}");
                    }

                    void OnStopProgressUpdated(Event @event)
                    {
                        if (@event.EventType != EventType.StopProgressUpdated) return;
                        Redraw(@event.Message);
                    }
                }
            });
            GuiProcess.Start();
            ManualResetEvent.WaitOne();
            IpcSocket.Dispose();
            GuiProcess.Close();

            void OnResourceVerified(Event @event)
            {
                if (@event.EventType != EventType.ResourceVerified) return;
                Redraw(@event.Message);
            }
        }

        static void Redraw(string statusText = null, bool? restrictHumanInteraction = null, bool redrawEverything = true)
        {
            IpcSocket.Send(new IpcMessage
            {
                Text = "redraw",
                Payload = JsonConvert.SerializeObject(
                    redrawEverything
                        ? new Frame
                        {
                            CrawlerState = CrawlerBot.CrawlerState,
                            VerifiedUrlCount = CrawlerBot.Statistics?.VerifiedUrlCount,
                            ValidUrlCount = CrawlerBot.Statistics?.ValidUrlCount,
                            BrokenUrlCount = CrawlerBot.Statistics?.BrokenUrlCount,
                            MillisecondsAveragePageLoadTime = CrawlerBot.Statistics?.MillisecondsAveragePageLoadTime,
                            RemainingWorkload = CrawlerBot.RemainingWorkload,
                            ElapsedTime = Stopwatch.Elapsed.ToString("hh' : 'mm' : 'ss"),
                            RestrictHumanInteraction = restrictHumanInteraction,
                            StatusText = statusText
                        }
                        : new Frame { RestrictHumanInteraction = restrictHumanInteraction, StatusText = statusText }
                )
            });
        }

        static void RedrawEvery(TimeSpan timeSpan)
        {
            Stopwatch.Restart();
            _constantRedrawTask = Task.Run(() =>
            {
                while (!(CrawlerState.Completed).HasFlag(CrawlerBot.CrawlerState))
                {
                    Redraw();
                    Thread.Sleep(timeSpan);
                }
                Stopwatch.Stop();
            });
        }

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}