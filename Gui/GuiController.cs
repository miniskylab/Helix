using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler;
using Helix.Crawler.Abstractions;
using Helix.IPC;
using Newtonsoft.Json;

namespace Helix.Gui
{
    public static class GuiController
    {
        static readonly List<Task> BackgroundTasks = new List<Task>();
        static readonly IpcSocket IpcSocket = new IpcSocket("127.0.0.1", 18880);
        static readonly ManualResetEvent ManualResetEvent = new ManualResetEvent(false);
        static readonly Stopwatch Stopwatch = new Stopwatch();

        static void Main()
        {
            IpcSocket.On("btn-start-clicked", configurationJsonString =>
            {
                if (CrawlerBot.Memory.CrawlerState != CrawlerState.Ready) return;
                var configurations = new Configurations(configurationJsonString);
                CrawlerBot.OnWebBrowserOpened += openedWebBrowserCount =>
                {
                    RedrawGui($"Opening web browsers ... ({openedWebBrowserCount}/{configurations.WebBrowserCount})");
                };
                CrawlerBot.OnWebBrowserClosed += closedWebBrowserCount =>
                {
                    RedrawGui($"Closing web browsers ... ({closedWebBrowserCount}/{configurations.WebBrowserCount})");
                };
                CrawlerBot.OnStopped += isAllWorkDone =>
                {
                    RedrawGui(isAllWorkDone ? "Done." : "Stopped.");
                    Stopwatch.Stop();
                };
                CrawlerBot.OnResourceVerified += verificationResult => Task.Run(() =>
                {
                    RedrawGui($"{verificationResult.HttpStatusCode} - {verificationResult.RawResource.Url}");
                });
                CrawlerBot.OnExceptionOccurred += exception => { RedrawGui(exception.Message); };
                CrawlerBot.StartWorking(configurations);
                RedrawGuiEvery(TimeSpan.FromSeconds(1));
                Stopwatch.Restart();
            });
            IpcSocket.On("btn-close-clicked", _ =>
            {
                Stopwatch.Stop();
                CrawlerBot.StopWorking();
                Task.WhenAll(BackgroundTasks).Wait();
                CrawlerBot.Dispose();
                ManualResetEvent.Set();
            });

            ManualResetEvent.WaitOne();
            IpcSocket.Send(new IpcMessage { Text = "shutdown" });
            IpcSocket.Dispose();
        }

        static void RedrawGui(string statusText = null)
        {
            IpcSocket.Send(new IpcMessage
            {
                Text = "redraw",
                Payload = JsonConvert.SerializeObject(new ViewModel
                {
                    CrawlerState = CrawlerBot.Memory.CrawlerState,
                    VerifiedUrlCount = null,
                    ValidUrlCount = null,
                    BrokenUrlCount = null,
                    RemainingUrlCount = CrawlerBot.Memory.RemainingUrlCount,
                    ElapsedTime = Stopwatch.Elapsed.ToString("hh' : 'mm' : 'ss"),
                    StatusText = statusText
                })
            });
        }

        static void RedrawGuiEvery(TimeSpan timeSpan)
        {
            BackgroundTasks.Add(Task.Run(() =>
            {
                while (!CrawlerBot.Memory.CancellationToken.IsCancellationRequested)
                {
                    RedrawGui();
                    Thread.Sleep(timeSpan);
                }
            }, CrawlerBot.Memory.CancellationToken));
        }
    }
}