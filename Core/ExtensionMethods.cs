using System;
using log4net;

namespace Helix.Core
{
    public static class ExtensionMethods
    {
        public static void StateTransitionFailureEvent<TState, TCommand>(this ILog log, TState currentState, TCommand command)
        {
            log.Info($"Transition from state [{currentState}] via [{Enum.GetName(typeof(TCommand), command)}] command failed.");
        }
    }
}