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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;

namespace CrawlerFrontendGui
{
    public class Gui : Controller
    {
        static BrowserWindow _gui;
        static readonly List<Task> BackgroundTasks = new List<Task>();
        static readonly object StaticLock = new object();
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

            ShowGui();
            Electron.IpcMain.OnSync("get-web-port", _ => BridgeSettings.WebPort);
        }

        [UsedImplicitly]
        public void ConfigureServices(IServiceCollection services) { services.AddMvcCore().AddJsonFormatters(); }

        [HttpPost("btn-close-clicked")]
        public IActionResult OnBtnCloseClicked()
        {
            lock (StaticLock)
            {
                Stopwatch.Stop();
                Crawler.StopWorking();
                Task.WhenAll(BackgroundTasks).Wait();
                Crawler.Dispose();
                Electron.App.Quit();
                return Ok();
            }
        }

        [HttpPost("btn-start-clicked")]
        public IActionResult OnBtnStartClicked([FromBody] string configurationJsonString)
        {
            lock (StaticLock)
            {
                if (Crawler.State != CrawlerState.Ready) return BadRequest();
                var configurations = new Configurations(configurationJsonString);
                Crawler.OnWebBrowserOpened += openedWebBrowserCount =>
                {
                    RedrawGui($"Opening web browsers ... ({openedWebBrowserCount}/{configurations.WebBrowserCount})");
                };
                Crawler.OnStopped += isAllWorkDone =>
                {
                    RedrawGui(isAllWorkDone ? "Done." : "Stopped.");
                    Stopwatch.Stop();
                };
                Crawler.StartWorking(configurations);
                RedrawGuiEvery(TimeSpan.FromSeconds(1));
                Stopwatch.Restart();
                return Ok();
            }
        }

        static void Main(string[] args)
        {
            new WebHostBuilder()
                .UseStartup<Gui>()
                .UseKestrel()
                .UseElectron(args)
                .Build()
                .Run();
        }

        static void RedrawGui(string statusText = null)
        {
            Electron.IpcMain.Send(_gui, "redraw", JsonConvert.SerializeObject(new ViewModel
            {
                CrawlerState = Crawler.State,
                VerifiedUrlCount = Crawler.VerifiedUrlCount,
                ValidUrlCount = null,
                BrokenUrlCount = null,
                RemainingUrlCount = Crawler.RemainingUrlCount,
                IdleWebBrowserCount = Crawler.IdleWebBrowserCount,
                ElapsedTime = Stopwatch.Elapsed.ToString("hh' : 'mm' : 'ss"),
                StatusText = statusText
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

        static void ShowGui()
        {
            if (_gui != null) return;
            _gui = Electron.WindowManager.CreateWindowAsync(new BrowserWindowOptions
            {
                Width = 500,
                Height = 700,
                Show = false,
                Center = true,
                Fullscreenable = false,
                Maximizable = false,
                Resizable = false,
                Title = "Helix"
            }).Result;

            _gui.SetMenuBarVisibility(false);
            _gui.OnReadyToShow += () => _gui.Show();
        }
    }
}