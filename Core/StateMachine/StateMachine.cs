using System;
using System.Collections.Generic;

namespace Helix.Core
{
    public sealed class StateMachine<TState, TCommand> where TState : Enum where TCommand : Enum
    {
        readonly Dictionary<Transition<TState, TCommand>, TState> _possibleTransitions;
        readonly object _stateTransitionLock;

        public TState CurrentState { get; private set; }

        public StateMachine(Dictionary<Transition<TState, TCommand>, TState> possibleTransitions, TState initialState)
        {
            CurrentState = initialState;
            _stateTransitionLock = new object();
            _possibleTransitions = possibleTransitions;
        }

        public bool TryMoveNext(TCommand command)
        {
            lock (_stateTransitionLock)
            {
                if (!TryGetNext(command, out var nextState)) return false;
                CurrentState = nextState;
                return true;
            }
        }

        bool TryGetNext(TCommand command, out TState nextState)
        {
            var transition = new Transition<TState, TCommand>(CurrentState, command);
            return _possibleTransitions.TryGetValue(transition, out nextState);
        }
    }
}