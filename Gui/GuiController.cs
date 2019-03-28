using System;
using System.Collections.Generic;
using System.Data;
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
        static readonly List<Task> BackgroundTasks = new List<Task>();
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
                if (CrawlerBot.CrawlerState != CrawlerState.WaitingToRun) return;
                var configurations = new Configurations(configurationJsonString);
                RedrawGui("Initializing ...");
                CrawlerBot.OnStopped += () =>
                {
                    Stopwatch.Stop();
                    switch (CrawlerBot.CrawlerState)
                    {
                        case CrawlerState.RanToCompletion:
                            RedrawGui("Done.");
                            break;
                        case CrawlerState.Cancelled:
                            RedrawGui("Cancelled.");
                            break;
                        case CrawlerState.Faulted:
                            RedrawGui("One or more errors occurred. Check the logs for more details.");
                            break;
                        default:
                            throw new InvalidConstraintException();
                    }
                };
                CrawlerBot.OnEventBroadcast += OnResourceVerified;
                CrawlerBot.StartWorking(configurations);
                RedrawGuiEvery(TimeSpan.FromSeconds(1));
                Stopwatch.Restart();
            });
            IpcSocket.On("btn-close-clicked", _ =>
            {
                try
                {
                    CrawlerBot.OnEventBroadcast -= OnResourceVerified;
                    CrawlerBot.OnEventBroadcast += OnShutdownStepChanged;
                    CrawlerBot.StopWorking();
                    RedrawGui("Waiting for background tasks to complete ...");
                    if (!Task.WhenAll(BackgroundTasks).Wait(TimeSpan.FromMinutes(1)))
                        File.AppendAllText(
                            Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "debug.log"),
                            $"\r\n[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] Waiting for background tasks to complete timed out after 60 seconds."
                        );
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
                catch (Exception exception)
                {
                    File.AppendAllText("debug.log", $"\r\n[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] {exception}");
                }
                finally { ManualResetEvent.Set(); }
            });
            GuiProcess.Start();
            ManualResetEvent.WaitOne();
            Stopwatch.Stop();
            IpcSocket.Dispose();
            GuiProcess.Close();

            void OnResourceVerified(Event @event)
            {
                if (@event.EventType != EventType.ResourceVerified) return;
                RedrawGui(@event.Message);
            }
            void OnShutdownStepChanged(Event @event)
            {
                if (@event.EventType != EventType.ShutdownStepChanged) return;
                RedrawGui(@event.Message);
            }
        }

        static void RedrawGui(string statusText = null)
        {
            IpcSocket.Send(new IpcMessage
            {
                Text = "redraw",
                Payload = JsonConvert.SerializeObject(
                    CrawlerBot.CrawlerState == CrawlerState.WaitingToRun
                        ? new Frame { StatusText = statusText }
                        : new Frame
                        {
                            CrawlerState = CrawlerBot.CrawlerState,
                            VerifiedUrlCount = CrawlerBot.Statistics?.VerifiedUrlCount,
                            ValidUrlCount = CrawlerBot.Statistics?.ValidUrlCount,
                            BrokenUrlCount = CrawlerBot.Statistics?.BrokenUrlCount,
                            AveragePageLoadTime = CrawlerBot.Statistics?.AveragePageLoadTime,
                            RemainingWorkload = CrawlerBot.RemainingWorkload,
                            ElapsedTime = Stopwatch.Elapsed.ToString("hh' : 'mm' : 'ss"),
                            StatusText = statusText
                        }
                )
            });
        }

        static void RedrawGuiEvery(TimeSpan timeSpan)
        {
            BackgroundTasks.Add(Task.Run(() =>
            {
                while (!(CrawlerState.WaitingToRun | CrawlerState.Completed).HasFlag(CrawlerBot.CrawlerState))
                {
                    RedrawGui();
                    Thread.Sleep(timeSpan);
                }
            }));
        }

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}