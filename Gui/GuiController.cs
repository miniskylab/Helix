using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Helix.Bot;
using Helix.Bot.Abstractions;
using Helix.Core;
using Helix.IPC;
using Helix.IPC.Abstractions;
using JetBrains.Annotations;
using log4net;
using Newtonsoft.Json;

namespace Helix.Gui
{
    public static class GuiController
    {
        static BrokenLinkCollector _brokenLinkCollector;
        static Configurations _configurations;
        static Task _elapsedTimeUpdateTask;
        static bool _isClosing;
        static Process _reportViewerProcess;
        static readonly ISynchronousServerSocket CommunicationSocketToGui;
        static readonly Stopwatch ElapsedTimeStopwatch;
        static readonly Process GuiProcess;
        static readonly ILog Log;
        static readonly ManualResetEvent ManualResetEvent;
        static readonly object OperationLock;

        static GuiController()
        {
            Log4NetModule.ConfigureLog4Net();

            Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            GuiProcess = new Process { StartInfo = { FileName = Configurations.PathToElectronJsExecutable } };
            ManualResetEvent = new ManualResetEvent(false);
            OperationLock = new object();
            ElapsedTimeStopwatch = new Stopwatch();
            CommunicationSocketToGui = new SynchronousServerSocket(IPAddress.Loopback.ToString(), Configurations.GuiControllerPort);
        }

        static void Main()
        {
            CommunicationSocketToGui.OnReceived += message =>
            {
                var method = typeof(GuiController).GetMethod(message.Text, BindingFlags.NonPublic | BindingFlags.Static);
                method?.Invoke(null, string.IsNullOrWhiteSpace(message.Payload) ? null : new object[] { message.Payload });
            };

            GuiProcess.Start();
            ManualResetEvent.WaitOne();

            _reportViewerProcess?.CloseMainWindow();
            _reportViewerProcess?.Close();

            CommunicationSocketToGui.Dispose();
            GuiProcess.CloseMainWindow();
            GuiProcess.Close();
        }

        #region Action

        [UsedImplicitly]
        static void Close()
        {
            _isClosing = true;
            Redraw(new Frame
            {
                ShowWaitingOverlay = true,
                DisableStopButton = true,
                DisableCloseButton = true
            });

            StopWorking();
            ManualResetEvent.Set();
            ManualResetEvent.Dispose();
        }

        [UsedImplicitly]
        static void Preview()
        {
            if (!Monitor.TryEnter(OperationLock)) return;
            try
            {
                if (_reportViewerProcess != null) RestoreOrSetForegroundWindow(_reportViewerProcess);
                else
                {
                    if (_configurations == null)
                    {
                        Log.Error("Cannot preview the report because there is no configurations provided.");
                        return;
                    }

                    _reportViewerProcess = Process.Start(
                        Configurations.PathToSqLiteBrowserExecutable,
                        $"-R -t VerificationResults {_configurations.PathToReportFile}" // TODO: Remove hard-coded [VerificationResults]
                    );

                    /* Wait for MainWindowHandle to be available.
                     * TODO: Waiting for a fixed amount of time is not a good idea. Need to find another solution. */
                    Thread.Sleep(2000);

                    Task.Run(() =>
                    {
                        _reportViewerProcess.WaitForExit();
                        _reportViewerProcess = null;
                    });
                }
            }
            finally { Monitor.Exit(OperationLock); }
        }

        static void OpenOutputDirectory()
        {
            if (!Monitor.TryEnter(OperationLock)) return;
            try
            {
                if (_configurations == null)
                {
                    Log.Error("Cannot open output directory because there is no configurations provided.");
                    return;
                }
                Process.Start("explorer.exe", _configurations.OutputDirectory);
            }
            finally { Monitor.Exit(OperationLock); }
        }

        [UsedImplicitly]
        static void Start(string configurationJsonString)
        {
            _configurations = new Configurations(configurationJsonString);

            CreateNewLogFile();
            CloseReportViewerIfOpen();
            CreateAndConfigureBot();
            DisableGui();
            TryStartBot();

            #region Local Functions

            void CreateNewLogFile()
            {
                Log4NetModule.CreateNewLogFile(_configurations.PathToLogFile);
                Log.Info($"Accepted: {_configurations.StartUri}");
            }
            void CloseReportViewerIfOpen()
            {
                _reportViewerProcess?.CloseMainWindow();
                _reportViewerProcess?.Close();
            }
            void CreateAndConfigureBot()
            {
                _brokenLinkCollector = new BrokenLinkCollector();
                _brokenLinkCollector.OnEventBroadcast += OnStartProgressUpdated;
                _brokenLinkCollector.OnEventBroadcast += OnWorkflowActivated;
                _brokenLinkCollector.OnEventBroadcast += OnResourceProcessed;
                _brokenLinkCollector.OnEventBroadcast += OnWorkflowCompleted;
            }
            void DisableGui()
            {
                Redraw(new Frame
                {
                    DisableMainButton = true,
                    DisableStopButton = true,
                    DisableCloseButton = true,
                    DisablePreviewButton = true,
                    DisableConfigurationPanel = true,
                    BorderColor = BorderColor.Normal,
                    DisableOpenOutputDirectoryButton = true
                });
            }
            void TryStartBot()
            {
                if (_brokenLinkCollector.TryStart(_configurations))
                {
                    Redraw(new Frame
                    {
                        DisableStopButton = false,
                        DisableCloseButton = false
                    });
                    UpdateElapsedTimeOnGuiEvery(TimeSpan.FromSeconds(1));
                }
                else
                {
                    Redraw(new Frame
                    {
                        DisableMainButton = false,
                        DisableCloseButton = false
                    });
                }
            }
            void OnStartProgressUpdated(Event @event)
            {
                if (@event is StartProgressReportEvent)
                {
                    Redraw(new Frame { StatusText = @event.Message });
                    return;
                }
                _brokenLinkCollector.OnEventBroadcast -= OnStartProgressUpdated;
            }
            void OnWorkflowActivated(Event @event)
            {
                if (!(@event is WorkflowActivatedEvent)) return;

                _brokenLinkCollector.OnEventBroadcast -= OnWorkflowActivated;
                Redraw(new Frame
                {
                    DisablePreviewButton = false,
                    DisableOpenOutputDirectoryButton = false
                });
            }
            static void OnResourceProcessed(Event @event)
            {
                if (!(@event is ResourceProcessedEvent resourceProcessedEvent)) return;
                Redraw(new Frame
                {
                    StatusText = resourceProcessedEvent.Message,
                    ValidUrlCount = resourceProcessedEvent.ValidUrlCount,
                    BrokenUrlCount = resourceProcessedEvent.BrokenUrlCount,
                    VerifiedUrlCount = resourceProcessedEvent.VerifiedUrlCount,
                    RemainingWorkload = resourceProcessedEvent.RemainingWorkload,
                    MillisecondsAveragePageLoadTime = resourceProcessedEvent.MillisecondsAveragePageLoadTime
                });
            }
            static void OnWorkflowCompleted(Event @event)
            {
                if (!(@event is WorkflowCompletedEvent)) return;

                _elapsedTimeUpdateTask?.Wait();
                Redraw(new Frame
                {
                    DisableStopButton = true,
                    DisableMainButton = false,
                    DisableConfigurationPanel = false,
                    DisableCloseButton = _isClosing,
                    ShowWaitingOverlay = _isClosing,
                    BorderColor = _brokenLinkCollector.BotState == BotState.Faulted ? BorderColor.Error : BorderColor.Normal,
                    ElapsedTime = ElapsedTimeStopwatch.Elapsed.ToString("hh' : 'mm' : 'ss"),
                    StatusText = _brokenLinkCollector.BotState switch
                    {
                        BotState.Faulted => "One or more errors occurred. Check the <a href=# id='btn-view-log'>log</a> for more details.",
                        BotState.RanToCompletion => "The crawling task has completed.",
                        _ => $"{@event.Message}."
                    }
                });
            }
            void UpdateElapsedTimeOnGuiEvery(TimeSpan timeSpan)
            {
                _elapsedTimeUpdateTask = Task.Run(() =>
                {
                    ElapsedTimeStopwatch.Restart();
                    while (!BotState.Completed.HasFlag(_brokenLinkCollector.BotState))
                    {
                        Redraw(new Frame { ElapsedTime = ElapsedTimeStopwatch.Elapsed.ToString("hh' : 'mm' : 'ss") });
                        Thread.Sleep(timeSpan);
                    }
                    ElapsedTimeStopwatch.Stop();
                });
            }

            #endregion
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

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr handle);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        #endregion

        #region Helper

        static void Redraw(Frame frame) { CommunicationSocketToGui.Send(new Message { Payload = JsonConvert.SerializeObject(frame) }); }

        static void StopWorking()
        {
            try
            {
                _brokenLinkCollector.OnEventBroadcast += OnStopProgressUpdated;
                _brokenLinkCollector.Stop();
                _brokenLinkCollector.Dispose();

                var waitingTime = TimeSpan.FromMinutes(1);
                if (_elapsedTimeUpdateTask == null || _elapsedTimeUpdateTask.Wait(waitingTime)) return;
                Log.Error($"Constant redrawing task failed to finish after {waitingTime.TotalSeconds} seconds.");
            }
            catch (Exception exception)
            {
                Log.Error("One or more errors occurred when stopping working.", exception);
            }

            #region Local Functions

            void OnStopProgressUpdated(Event @event)
            {
                if (!(@event is StopProgressReportEvent)) return;
                Redraw(new Frame { WaitingOverlayProgressText = @event.Message });
            }

            #endregion
        }

        static void RestoreOrSetForegroundWindow(Process process)
        {
            const int swRestore = 9;
            if (IsIconic(process.MainWindowHandle)) ShowWindow(process.MainWindowHandle, swRestore);

            /* Due to a limitation of WinAPI SetForegroundWindow() method,
             * an ALT keypress is required before calling that WinAPI in order for it to work properly.
             *
             * See more:
             * https://stackoverflow.com/questions/20444735/issue-with-setforegroundwindow-in-net
             * https://www.roelvanlisdonk.nl/2014/09/05/reliable-bring-external-process-window-to-foreground-without-c/
             * https://stackoverflow.com/questions/10740346/setforegroundwindow-only-working-while-visual-studio-is-open
             */
            const int altKey = 0xA4;
            const int extendedKey = 0x1;
            keybd_event(altKey, 0x45, extendedKey | 0, 0);

            SetForegroundWindow(process.MainWindowHandle);

            /* Release the ALT key */
            const int keyUp = 0x2;
            keybd_event(altKey, 0x45, extendedKey | keyUp, 0);
        }

        #endregion
    }
}