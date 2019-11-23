using System;

namespace Helix.Bot.Abstractions
{
    public class HttpRedirectException : Exception
    {
        public HttpRedirectException(string message) : base(message) { }
    }
}