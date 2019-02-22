using System;
using System.Collections.Generic;

namespace Helix.Core
{
    public sealed class StateMachine<TState, TCommand> where TState : Enum where TCommand : Enum
    {
        readonly Dictionary<Transition<TState, TCommand>, TState> _possibleTransitions;

        public TState CurrentState { get; private set; }

        public StateMachine(Dictionary<Transition<TState, TCommand>, TState> possibleTransitions, TState initialState)
        {
            CurrentState = initialState;
            _possibleTransitions = possibleTransitions;
        }

        public void MoveNext(TCommand command) { CurrentState = GetNext(command); }

        public bool TryGetNext(TCommand command, out TState nextState)
        {
            var transition = new Transition<TState, TCommand>(CurrentState, command);
            return _possibleTransitions.TryGetValue(transition, out nextState);
        }

        TState GetNext(TCommand command)
        {
            var transition = new Transition<TState, TCommand>(CurrentState, command);
            if (!_possibleTransitions.TryGetValue(transition, out var nextState))
                throw new InvalidOperationException("Invalid transition: " + CurrentState + " -> " + command);
            return nextState;
        }
    }
}