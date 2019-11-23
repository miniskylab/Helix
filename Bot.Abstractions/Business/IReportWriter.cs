using System;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IReportWriter : IService, IDisposable
    {
        void WriteReport(VerificationResult verificationResult);
    }
}