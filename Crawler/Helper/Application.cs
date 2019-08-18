using System;
using System.Reflection;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

namespace Helix.Crawler
{
    public abstract class Application
    {
        static Application() { ConfigureLog4Net(); }

        static void ConfigureLog4Net()
        {
            var patternLayout = new PatternLayout
            {
                ConversionPattern = "[%date] [%5level] [%4thread] [%logger] - %message%newline"
            };
            patternLayout.ActivateOptions();

            var hierarchy = (Hierarchy) LogManager.GetRepository(Assembly.GetEntryAssembly());
            var rollingFileAppender = new RollingFileAppender
            {
                File = $"logs\\{nameof(Helix)}.{DateTime.Now:yyyyMMdd-HHmmss}.log",
                AppendToFile = false,
                PreserveLogFileNameExtension = true,
                RollingStyle = RollingFileAppender.RollingMode.Size,
                MaxSizeRollBackups = -1,
                MaximumFileSize = "1GB",
                Layout = patternLayout
            };
            rollingFileAppender.ActivateOptions();
            hierarchy.Root.AddAppender(rollingFileAppender);
            hierarchy.Root.Level = Level.Debug;
            hierarchy.Configured = true;
        }
    }
}