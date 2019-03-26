using System;
using System.Collections.Generic;

namespace Helix.Core
{
    public sealed class Transition<TState, TCommand> where TState : Enum where TCommand : Enum
    {
        readonly TCommand _command;
        readonly TState _currentState;

        public Transition(TState currentState, TCommand command)
        {
            _currentState = currentState;
            _command = command;
        }

        public override bool Equals(object obj)
        {
            return obj is Transition<TState, TCommand> other && _currentState.Equals(other._currentState) &&
                   _command.Equals(other._command);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<TCommand>.Default.GetHashCode(_command) * 397) ^
                       EqualityComparer<TState>.Default.GetHashCode(_currentState);
            }
        }
    }
}