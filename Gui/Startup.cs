using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CrawlerBackendBusiness;
using ElectronNET.API;
using ElectronNET.API.Entities;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;

namespace Gui
{
    [UsedImplicitly]
    class Startup
    {
        static BrowserWindow _gui;
        static readonly List<Task> BackgroundTasks = new List<Task>();
        static readonly Stopwatch Stopwatch = new Stopwatch();

        static FileServerOptions FileServerOptions
        {
            get
            {
                var workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                var webDirectory = Path.Combine(workingDirectory, "www");
                return new FileServerOptions
                {
                    FileProvider = new PhysicalFileProvider(webDirectory),
                    StaticFileOptions =
                    {
                        OnPrepareResponse = context =>
                        {
                            context.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                            context.Context.Response.Headers["Pragma"] = "no-cache";
                            context.Context.Response.Headers["Expires"] = "0";
                        }
                    }
                };
            }
        }

        [UsedImplicitly]
        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc();
            app.UseFileServer(FileServerOptions);

            SetupGuiEventHandlers();
            ShowGui();
        }

        [UsedImplicitly]
        public void ConfigureServices(IServiceCollection services) { services.AddMvcCore().AddJsonFormatters(); }

        static void KeepAlive()
        {
            BackgroundTasks.Add(Task.Run(() =>
            {
                while (!Crawler.CancellationToken.IsCancellationRequested)
                {
                    Electron.IpcMain.Send(_gui, "keep-alive");
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }, Crawler.CancellationToken));
        }

        static void Main(string[] args)
        {
            new WebHostBuilder()
                .UseStartup<Startup>()
                .UseKestrel()
                .UseElectron(args)
                .Build()
                .Run();
        }

        static void RedrawGui()
        {
            Electron.IpcMain.Send(_gui, "redraw", JsonConvert.SerializeObject(new ViewModel
            {
                CrawlerState = Crawler.State,
                VerifiedUrlCount = Crawler.VerifiedUrlCount,
                ValidUrlCount = null,
                BrokenUrlCount = null,
                RemainingUrlCount = Crawler.RemainingUrlCount,
                IdleWebBrowserCount = Crawler.IdleWebBrowserCount,
                ElapsedTime = Stopwatch.Elapsed.ToString("hh' : 'mm' : 'ss")
            }));
        }

        static void RedrawGuiEvery(TimeSpan timeSpan)
        {
            BackgroundTasks.Add(Task.Run(() =>
            {
                while (!Crawler.CancellationToken.IsCancellationRequested)
                {
                    RedrawGui();
                    Thread.Sleep(timeSpan);
                }
            }, Crawler.CancellationToken));
        }

        static void SetupGuiEventHandlers()
        {
            Electron.IpcMain.On("btnStartClicked", configurationJsonString =>
            {
                if (Crawler.State != CrawlerState.Ready) return;
                var configurations = new Configurations((string) configurationJsonString);
                Crawler.OnWebBrowserOpened += openedWebBrowserCount =>
                {
                    Electron.IpcMain.Send(_gui, "redraw", JsonConvert.SerializeObject(new ViewModel
                    {
                        StatusText = $"Openning web browsers ... ({openedWebBrowserCount}/{configurations.WebBrowserCount})"
                    }));
                };
                Task.Run(() => { Crawler.StartWorking(configurations); });
                RedrawGuiEvery(TimeSpan.FromSeconds(1));
                Stopwatch.Start();
            });
            Electron.IpcMain.On("btnCloseClicked", _ =>
            {
                Stopwatch.Stop();
                Crawler.StopWorking();
                Task.WhenAll(BackgroundTasks).Wait();
                Crawler.Dispose();
                Electron.App.Quit();
            });
        }

        static async void ShowGui()
        {
            if (_gui != null) return;
            _gui = await Electron.WindowManager.CreateWindowAsync(new BrowserWindowOptions
            {
                Width = 500,
                Height = 700,
                Show = false,
                Center = true,
                Fullscreenable = false,
                Maximizable = false,
                Resizable = false,
                Title = "Helix"
            });

            _gui.SetMenuBarVisibility(false);
            _gui.OnReadyToShow += () =>
            {
                KeepAlive();
                _gui.Show();
            };
        }
    }
}