using System.Linq;
using System.Reflection;
using Autofac.Core;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Module = Autofac.Module;

namespace Helix.Core
{
    public class Log4NetModule : Module
    {
        static FileAppender _rollingFileAppender;

        public static void ConfigureLog4Net()
        {
            var hierarchy = (Hierarchy) LogManager.GetRepository(Assembly.GetEntryAssembly());
            if (hierarchy.Configured) return;

            var patternLayout = new PatternLayout { ConversionPattern = "[%date] [%5level] [%4thread] [%logger] - %message%newline" };
            patternLayout.ActivateOptions();

            _rollingFileAppender = new RollingFileAppender
            {
                AppendToFile = false,
                Layout = patternLayout,
                MaximumFileSize = "1GB",
                MaxSizeRollBackups = -1,
                PreserveLogFileNameExtension = true,
                RollingStyle = RollingFileAppender.RollingMode.Once
            };
            hierarchy.Root.AddAppender(_rollingFileAppender);
            hierarchy.Root.Level = Level.Info;
            hierarchy.Configured = true;
        }

        public static void CreateNewLogFile(string logFileName)
        {
            _rollingFileAppender.File = logFileName;
            _rollingFileAppender.ActivateOptions();
        }

        protected override void AttachToComponentRegistration(IComponentRegistry _, IComponentRegistration componentRegistration)
        {
            componentRegistration.Preparing += (__, preparingEventArgs) =>
            {
                preparingEventArgs.Parameters = preparingEventArgs.Parameters.Union(
                    new[]
                    {
                        new ResolvedParameter(
                            (parameterInfo, ___) => parameterInfo.ParameterType == typeof(ILog),
                            (parameterInfo, ___) => LogManager.GetLogger(parameterInfo.Member.DeclaringType)
                        )
                    });
            };
        }
    }
}