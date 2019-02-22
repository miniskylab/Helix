using System;
using System.Collections.Generic;

namespace Helix.Core
{
    public sealed class Transition<TState, TCommand> where TState : Enum where TCommand : Enum
    {
        readonly TCommand Command;
        readonly TState CurrentState;

        public Transition(TState currentState, TCommand command)
        {
            CurrentState = currentState;
            Command = command;
        }

        public override bool Equals(object obj)
        {
            return obj is Transition<TState, TCommand> other && CurrentState.Equals(other.CurrentState) && Command.Equals(other.Command);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<TCommand>.Default.GetHashCode(Command) * 397) ^
                       EqualityComparer<TState>.Default.GetHashCode(CurrentState);
            }
        }
    }
}