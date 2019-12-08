using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using log4net;

namespace Helix.Core
{
    public static class ExtensionMethods
    {
        public static bool IsAcknowledgingOperationCancelledException(this Exception exception, CancellationToken cancellationToken)
        {
            switch (exception)
            {
                case OperationCanceledException operationCanceledException:
                {
                    var cancellationRequested = operationCanceledException.CancellationToken.IsCancellationRequested;
                    var cancellationTokenIsTheSame = operationCanceledException.CancellationToken.Equals(cancellationToken);
                    if (cancellationRequested && cancellationTokenIsTheSame) return true;
                    break;
                }
                case AggregateException aggregateException:
                {
                    var allInnerExceptionsAreAcknowledgingOperationCancelledException = aggregateException.Flatten().InnerExceptions.All(
                        innerException =>
                        {
                            if (!(innerException is OperationCanceledException operationCanceledException)) return false;
                            var cancellationRequested = operationCanceledException.CancellationToken.IsCancellationRequested;
                            var cancellationTokenIsTheSame = operationCanceledException.CancellationToken.Equals(cancellationToken);
                            return cancellationRequested && cancellationTokenIsTheSame;
                        }
                    );
                    if (allInnerExceptionsAreAcknowledgingOperationCancelledException) return true;
                    break;
                }
            }
            return false;
        }

        public static bool IsCompilerGenerated(this Type type) => type.GetCustomAttribute(typeof(CompilerGeneratedAttribute), true) != null;

        public static void StateTransitionFailureEvent<TState, TCommand>(this ILog log, TState currentState, TCommand command)
        {
            log.Info($"Transition from state [{currentState}] via [{Enum.GetName(typeof(TCommand), command)}] command failed.");
        }

        public static Uri StripFragment(this Uri uri)
        {
            return string.IsNullOrWhiteSpace(uri.Fragment) ? uri : new UriBuilder(uri) { Fragment = string.Empty }.Uri;
        }
    }
}