using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Helix.Crawler;
using Helix.Crawler.Abstractions;
using Helix.IPC;
using Helix.IPC.Abstractions;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Helix.Gui
{
    public static class GuiController
    {
        static bool _closeButtonWasClicked;
        static Task _constantRedrawTask;
        static Process _sqLiteProcess;
        static readonly Process GuiProcess;
        static readonly ManualResetEvent ManualResetEvent;
        static readonly object OperationLock;
        static readonly Stopwatch Stopwatch;
        static readonly ISynchronousServerSocket SynchronousServerSocket;

        static GuiController()
        {
            GuiProcess = new Process { StartInfo = { FileName = Configurations.PathToElectronJsExecutable } };
            ManualResetEvent = new ManualResetEvent(false);
            OperationLock = new object();
            Stopwatch = new Stopwatch();
            SynchronousServerSocket = new SynchronousServerSocket(IPAddress.Loopback.ToString(), Configurations.GuiControllerPort);
        }

        static void Main()
        {
            /* Hide the console windows.
             * TODO: Will be removed and replaced with built-in .NET Core 3.0 feature. */
            ShowWindow(GetConsoleWindow(), 0);

            SynchronousServerSocket.OnReceived += message =>
            {
                var method = typeof(GuiController).GetMethod(message.Text, BindingFlags.NonPublic | BindingFlags.Static);
                method.Invoke(null, string.IsNullOrWhiteSpace(message.Payload) ? null : new object[] { message.Payload });
            };

            GuiProcess.Start();
            ManualResetEvent.WaitOne();

            _sqLiteProcess?.CloseMainWindow();
            _sqLiteProcess?.Close();
            SynchronousServerSocket.Dispose();
            GuiProcess.CloseMainWindow();
            GuiProcess.Close();
        }

        #region Action

        [UsedImplicitly]
        static void Close()
        {
            _closeButtonWasClicked = true;
            Redraw(new Frame
            {
                ShowWaitingOverlay = true,
                DisableStopButton = true,
                DisableCloseButton = true
            });
            if (!CrawlerState.Completed.HasFlag(CrawlerBot.CrawlerState)) StopWorking();
            ManualResetEvent.Set();
            ManualResetEvent.Dispose();
        }

        [UsedImplicitly]
        static void Preview()
        {
            if (!Monitor.TryEnter(OperationLock)) return;
            try
            {
                if (_sqLiteProcess != null)
                {
                    const int swRestore = 9;
                    if (IsIconic(_sqLiteProcess.MainWindowHandle)) ShowWindow(_sqLiteProcess.MainWindowHandle, swRestore);

                    /* Due to a limitation of WinAPI SetForegroundWindow() method,
                     * an ALT keypress is required before calling that WinAPI in order for it to work properly.
                     *
                     * See more:
                     * https://stackoverflow.com/questions/20444735/issue-with-setforegroundwindow-in-net
                     * https://www.roelvanlisdonk.nl/2014/09/05/reliable-bring-external-process-window-to-foreground-without-c/
                     * https://stackoverflow.com/questions/10740346/setforegroundwindow-only-working-while-visual-studio-is-open
                     */
                    const int alt = 0xA4;
                    const int extendedKey = 0x1;
                    keybd_event(alt, 0x45, extendedKey | 0, 0);

                    SetForegroundWindow(_sqLiteProcess.MainWindowHandle);

                    /* Release the ALT key */
                    const int keyup = 0x2;
                    keybd_event(alt, 0x45, extendedKey | keyup, 0);
                }
                else
                {
                    _sqLiteProcess = Process.Start(
                        Configurations.PathToSqLiteBrowserExecutable,
                        $"-R -t DataTransferObjects {Configurations.PathToReportFile}" // TODO: Remove hard-coded [DataTransferObjects]
                    );

                    /* Wait for MainWindowHandle to be available.
                     * TODO: Waiting for a fixed amount of time is not a good idea. Need to find another solution. */
                    Thread.Sleep(2000);

                    Task.Run(() =>
                    {
                        _sqLiteProcess.WaitForExit();
                        _sqLiteProcess = null;
                    });
                }
            }
            finally { Monitor.Exit(OperationLock); }
        }

        [UsedImplicitly]
        static void Start(string configurationJsonString)
        {
            _sqLiteProcess?.CloseMainWindow();
            _sqLiteProcess?.Close();

            CrawlerBot.OnEventBroadcast += OnStartProgressUpdated;
            CrawlerBot.OnEventBroadcast += OnReportFileCreated;
            CrawlerBot.OnEventBroadcast += OnResourceVerified;
            CrawlerBot.OnEventBroadcast += OnStopped;

            Redraw(new Frame
            {
                DisableMainButton = true,
                DisableStopButton = true,
                DisableCloseButton = true,
                DisableConfigurationPanel = true,
                DisablePreviewButton = true,
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

            void OnStartProgressUpdated(Event @event)
            {
                if (@event.EventType != EventType.StartProgressUpdated)
                {
                    CrawlerBot.OnEventBroadcast -= OnStartProgressUpdated;
                    return;
                }
                Redraw(new Frame { StatusText = @event.Message });
            }
            void OnReportFileCreated(Event @event)
            {
                if (@event.EventType != EventType.ReportFileCreated) return;
                CrawlerBot.OnEventBroadcast -= OnReportFileCreated;
                Redraw(new Frame { DisablePreviewButton = false });
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
                    DisableCloseButton = _closeButtonWasClicked,
                    ShowWaitingOverlay = _closeButtonWasClicked,
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
        }

        [UsedImplicitly]
        static void Stop()
        {
            Redraw(new Frame
            {
                ShowWaitingOverlay = true,
                DisableStopButton = true,
                DisableCloseButton = true
            });
            StopWorking();
        }

        #endregion

        #region Windows API

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr handle);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr handle);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        #endregion

        #region Helper

        static void OnResourceVerified(Event @event)
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

        static void Redraw(Frame frame) { SynchronousServerSocket.Send(new Message { Payload = JsonConvert.SerializeObject(frame) }); }

        static void StopWorking()
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

        #endregion
    }
}