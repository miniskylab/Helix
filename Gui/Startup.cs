using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CrawlerBackendBusiness;
using ElectronNET.API;
using ElectronNET.API.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Gui
{
    class Startup
    {
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

        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc();
            app.UseFileServer(FileServerOptions);

            ElectronSetupEventListeners();
            ElectronBootstrap();
        }

        public void ConfigureServices(IServiceCollection services) { services.AddMvcCore().AddJsonFormatters(); }

        static async void ElectronBootstrap()
        {
            var browserWindow = await Electron.WindowManager.CreateWindowAsync(new BrowserWindowOptions
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

            browserWindow.SetMenuBarVisibility(false);
            browserWindow.OnReadyToShow += () => browserWindow.Show();
        }

        static void ElectronSetupEventListeners()
        {
            Electron.IpcMain.On("btnStartClicked", configurationJsonString =>
            {
                if (Crawler.State != CrawlerState.Ready) return;
                Task.Run(() => { Crawler.StartWorking(new Configurations((string) configurationJsonString)); });
            });
            Electron.IpcMain.On("btnCloseClicked", _ =>
            {
                Crawler.StopWorking();
                Electron.App.Quit();
            });
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
    }
}