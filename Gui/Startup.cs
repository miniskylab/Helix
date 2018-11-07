using System;
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
                ElapsedTime = Stopwatch.Elapsed.ToString("hh' : 'mm' : 'ss")
            }));
        }

        static void RedrawGuiEvery(TimeSpan timeSpan)
        {
            Task.Run(() =>
            {
                while (!Crawler.CancellationToken.IsCancellationRequested)
                {
                    RedrawGui();
                    Thread.Sleep(timeSpan);
                }
            }, Crawler.CancellationToken);
        }

        static void SetupGuiEventHandlers()
        {
            Electron.IpcMain.On("btnStartClicked", configurationJsonString =>
            {
                if (Crawler.State != CrawlerState.Ready) return;
                Task.Run(() => { Crawler.StartWorking(new Configurations((string) configurationJsonString)); });
                RedrawGuiEvery(TimeSpan.FromSeconds(1));
                Stopwatch.Start();
            });
            Electron.IpcMain.On("btnCloseClicked", _ =>
            {
                Stopwatch.Stop();
                Crawler.StopWorking();
                Electron.App.Quit();
            });
        }

        static async void ShowGui()
        {
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
            _gui.OnReadyToShow += () => _gui.Show();
        }
    }
}