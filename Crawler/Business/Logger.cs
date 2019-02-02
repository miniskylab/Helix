using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Helix.Core;
using Helix.Persistence;

namespace Helix.Crawler
{
    public class Logger : FileLogger
    {
        [Obsolete(ErrorMessage.UseDependencyInjection, true)]
        public Logger() : base(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "helix_errors.log")) { }

        public override void LogException(Exception exception)
        {
            switch (exception)
            {
                case OperationCanceledException operationCanceledException:
                    if (operationCanceledException.CancellationToken.IsCancellationRequested) return;
                    break;
                case AggregateException aggregateException:
                    var thereIsNoUnhandledInnerException = !aggregateException.InnerExceptions.Any(innerException =>
                    {
                        if (!(innerException is OperationCanceledException operationCanceledException)) return false;
                        return !operationCanceledException.CancellationToken.IsCancellationRequested;
                    });
                    if (thereIsNoUnhandledInnerException) return;
                    break;
            }
            base.LogException(exception);
        }
    }
}