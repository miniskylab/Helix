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
        static Process _sqLiteProcess;
        static readonly Process GuiProcess = new Process { StartInfo = { FileName = "ui/electron.exe" } };
        static readonly ManualResetEvent ManualResetEvent = new ManualResetEvent(false);
        static readonly object OperationLock = new object();
        static readonly Stopwatch Stopwatch = new Stopwatch();
        static readonly ISynchronousServerSocket SynchronousServerSocket = new SynchronousServerSocket("127.0.0.1", 18880);

        [DllImport("USER32.DLL", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("User32.dll")]
        static extern bool IsIconic(IntPtr handle);

        static void Main()
        {
            /* Hide the console windows.
             * TODO: Will be removed and replaced with built-in .NET Core 3.0 feature. */
            ShowWindow(GetConsoleWindow(), 0);

            var closeButtonWasClicked = false;
            SynchronousServerSocket.On("btn-start-clicked", configurationJsonString =>
            {
                CrawlerBot.OnEventBroadcast += OnStartProgressUpdated;
                CrawlerBot.OnEventBroadcast += OnResourceVerified;
                CrawlerBot.OnEventBroadcast += OnStopped;

                Redraw(new Frame
                {
                    DisableMainButton = true,
                    DisableStopButton = true,
                    DisableCloseButton = true,
                    DisableConfigurationPanel = true,
                    BorderColor = BorderColor.Normal,
                    MainButtonFunctionality = MainButtonFunctionality.Start
                });
                if (CrawlerBot.TryStart(new Configurations(configurationJsonString)))
                {
                    Redraw(new Frame
                    {
                        // DisableMainButton = false,
                        DisableStopButton = false,
                        DisableCloseButton = false,
                        MainButtonFunctionality = MainButtonFunctionality.Pause
                    });
                    RedrawEvery(TimeSpan.FromSeconds(1));
                }
                else
                {
                    Redraw(new Frame
                    {
                        DisableMainButton = false,
                        DisableCloseButton = false,
                        MainButtonFunctionality = MainButtonFunctionality.Start
                    });
                }

                void OnStopped(Event @event)
                {
                    if (@event.EventType != EventType.Stopped) return;
                    Redraw(new Frame
                    {
                        DisableStopButton = true,
                        DisableMainButton = false,
                        DisableConfigurationPanel = false,
                        MainButtonFunctionality = MainButtonFunctionality.Start,
                        DisableCloseButton = closeButtonWasClicked,
                        ShowWaitingOverlay = closeButtonWasClicked,
                        BorderColor = CrawlerBot.CrawlerState == CrawlerState.Faulted ? BorderColor.Error : BorderColor.Normal,
                        StatusText = CrawlerBot.CrawlerState == CrawlerState.Faulted
                            ? "One or more errors occurred. Check the logs for more details."
                            : CrawlerBot.CrawlerState == CrawlerState.RanToCompletion
                                ? "The crawling task has completed."
                                : $"{@event.Message}."
                    });

                    _constantRedrawTask?.Wait();
                    if (CrawlerBot.CrawlerState == CrawlerState.Faulted) return;
                    Redraw(new Frame
                    {
                        VerifiedUrlCount = CrawlerBot.Statistics?.VerifiedUrlCount,
                        ValidUrlCount = CrawlerBot.Statistics?.ValidUrlCount,
                        BrokenUrlCount = CrawlerBot.Statistics?.BrokenUrlCount,
                        MillisecondsAveragePageLoadTime = CrawlerBot.Statistics?.MillisecondsAveragePageLoadTime,
                        RemainingWorkload = CrawlerBot.RemainingWorkload,
                        ElapsedTime = Stopwatch.Elapsed.ToString("hh' : 'mm' : 'ss")
                    });
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
                void RedrawEvery(TimeSpan timeSpan)
                {
                    _constantRedrawTask = Task.Run(() =>
                    {
                        Stopwatch.Restart();
                        while (!CrawlerState.Completed.HasFlag(CrawlerBot.CrawlerState))
                        {
                            Redraw(new Frame
                            {
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
            });
            SynchronousServerSocket.On("btn-close-clicked", _ =>
            {
                closeButtonWasClicked = true;
                Redraw(new Frame
                {
                    ShowWaitingOverlay = true,
                    DisableStopButton = true,
                    DisableCloseButton = true
                });
                if (!CrawlerState.Completed.HasFlag(CrawlerBot.CrawlerState)) StopWorking();
                ManualResetEvent.Set();
                ManualResetEvent.Dispose();
            });
            SynchronousServerSocket.On("btn-stop-clicked", _ =>
            {
                Redraw(new Frame
                {
                    ShowWaitingOverlay = true,
                    DisableStopButton = true,
                    DisableCloseButton = true
                });
                StopWorking();
            });
            SynchronousServerSocket.On("btn-preview-clicked", _ =>
            {
                if (!Monitor.TryEnter(OperationLock)) return;
                try
                {
                    if (_sqLiteProcess != null)
                    {
                        const int swRestore = 9;
                        if (IsIconic(_sqLiteProcess.MainWindowHandle)) ShowWindow(_sqLiteProcess.MainWindowHandle, swRestore);
                        SetForegroundWindow(_sqLiteProcess.MainWindowHandle);
                    }
                    else
                    {
                        _sqLiteProcess = Process.Start("sqlite-browser/DB Browser for SQLite.exe");
                        var sqLiteProcessMainWindowHandle = IntPtr.Zero;
                        while (sqLiteProcessMainWindowHandle == IntPtr.Zero)
                        {
                            sqLiteProcessMainWindowHandle = FindWindow(null, "DB Browser for SQLite");
                            Thread.Sleep(100);
                        }
                        Task.Run(() =>
                        {
                            _sqLiteProcess.WaitForExit();
                            _sqLiteProcess = null;
                        });
                    }
                }
                finally { Monitor.Exit(OperationLock); }
            });

            GuiProcess.Start();
            ManualResetEvent.WaitOne();
            lock (OperationLock)
            {
                _sqLiteProcess?.CloseMainWindow();
                _sqLiteProcess?.Close();
            }
            SynchronousServerSocket.Dispose();
            GuiProcess.CloseMainWindow();
            GuiProcess.Close();

            void StopWorking()
            {
                try
                {
                    CrawlerBot.OnEventBroadcast -= OnResourceVerified;
                    CrawlerBot.OnEventBroadcast += OnStopProgressUpdated;
                    CrawlerBot.Stop();

                    var waitingTime = TimeSpan.FromMinutes(1);
                    if (_constantRedrawTask == null || _constantRedrawTask.Wait(waitingTime)) return;

                    var errorMessage = $"Constant redrawing task failed to finish after {waitingTime.TotalSeconds} seconds.";
                    File.AppendAllText(
                        Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location), "debug.log"),
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
                Redraw(new Frame
                {
                    VerifiedUrlCount = CrawlerBot.Statistics?.VerifiedUrlCount,
                    ValidUrlCount = CrawlerBot.Statistics?.ValidUrlCount,
                    BrokenUrlCount = CrawlerBot.Statistics?.BrokenUrlCount,
                    RemainingWorkload = CrawlerBot.RemainingWorkload,
                    StatusText = @event.Message
                });
            }
        }

        static void Redraw(Frame frame) { SynchronousServerSocket.Send(new Message { Payload = JsonConvert.SerializeObject(frame) }); }

        [DllImport("User32.dll")]
        static extern bool SetForegroundWindow(IntPtr handle);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}