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
        static readonly ManualResetEvent ManualResetEvent = new ManualResetEvent(false);
        static readonly Stopwatch Stopwatch = new Stopwatch();
        static readonly ISynchronousServerSocket SynchronousServerSocket = new SynchronousServerSocket("127.0.0.1", 18880);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        static void Main()
        {
            /* Hide the console windows.
             * TODO: Will be removed and replaced with built-in .Net Core 3.0 feature. */
            ShowWindow(GetConsoleWindow(), 0);

            SynchronousServerSocket.On("btn-start-clicked", configurationJsonString =>
            {
                try
                {
                    CrawlerBot.OnEventBroadcast += OnStartProgressUpdated;
                    CrawlerBot.OnEventBroadcast += OnResourceVerified;
                    CrawlerBot.OnEventBroadcast += OnStopped;
                    CrawlerBot.StartWorking(new Configurations(configurationJsonString));
                    RedrawEvery(TimeSpan.FromSeconds(1));
                }
                catch (Exception exception)
                {
                    Redraw(new Frame
                    {
                        StatusText = exception.Message,
                        RestrictHumanInteraction = false
                    });
                }

                void OnStopped(Event @event)
                {
                    if (@event.EventType != EventType.Stopped) return;
                    Redraw(new Frame { StatusText = @event.Message });

                    if (!CrawlerState.Completed.HasFlag(CrawlerBot.CrawlerState)) return;
                    _constantRedrawTask.Wait();
                    Redraw(new Frame { RestrictHumanInteraction = false });
                }
                void OnStartProgressUpdated(Event @event)
                {
                    if (@event.EventType != EventType.StartProgressUpdated)
                    {
                        CrawlerBot.OnEventBroadcast -= OnStartProgressUpdated;
                        return;
                    }
                    Redraw(new Frame { StatusText = @event.Message });
                }
            });
            SynchronousServerSocket.On("btn-close-clicked", _ =>
            {
                StopWorking();
                ManualResetEvent.Set();
                ManualResetEvent.Dispose();
            });
            SynchronousServerSocket.On("btn-stop-clicked", _ => { StopWorking(); });
            GuiProcess.Start();
            ManualResetEvent.WaitOne();
            SynchronousServerSocket.Dispose();
            GuiProcess.Close();

            void StopWorking()
            {
                try
                {
                    CrawlerBot.OnEventBroadcast -= OnResourceVerified;
                    CrawlerBot.OnEventBroadcast += OnStopProgressUpdated;
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
                    Redraw(new Frame { StatusText = @event.Message });
                }
            }
            void OnResourceVerified(Event @event)
            {
                if (@event.EventType != EventType.ResourceVerified) return;
                Redraw(new Frame { StatusText = @event.Message });
            }
        }

        static void Redraw(Frame frame)
        {
            SynchronousServerSocket.Send(new Message
            {
                Text = "redraw",
                Payload = JsonConvert.SerializeObject(frame)
            });
        }

        static void RedrawEvery(TimeSpan timeSpan)
        {
            Stopwatch.Restart();
            _constantRedrawTask = Task.Run(() =>
            {
                while (!(CrawlerState.Completed).HasFlag(CrawlerBot.CrawlerState))
                {
                    Redraw(new Frame
                    {
                        CrawlerState = CrawlerBot.CrawlerState,
                        VerifiedUrlCount = CrawlerBot.Statistics?.VerifiedUrlCount,
                        ValidUrlCount = CrawlerBot.Statistics?.ValidUrlCount,
                        BrokenUrlCount = CrawlerBot.Statistics?.BrokenUrlCount,
                        MillisecondsAveragePageLoadTime = CrawlerBot.Statistics?.MillisecondsAveragePageLoadTime,
                        RemainingWorkload = CrawlerBot.RemainingWorkload,
                        ElapsedTime = Stopwatch.Elapsed.ToString("hh' : 'mm' : 'ss")
                    });
                    Thread.Sleep(timeSpan);
                }
                Stopwatch.Stop();
            });
        }

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}