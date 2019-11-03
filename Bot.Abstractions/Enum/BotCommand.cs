namespace Helix.Bot.Abstractions
{
    public enum BotCommand
    {
        Initialize,
        Run,
        Stop,
        Abort,
        MarkAsRanToCompletion,
        MarkAsCancelled,
        MarkAsFaulted,
        Pause,
        Resume
    }
}