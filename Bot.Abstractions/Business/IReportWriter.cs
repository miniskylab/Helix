using System;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IReportWriter : IService, IDisposable
    {
        void AddNew(params VerificationResult[] toBeAddedVerificationResults);

        void Update(params VerificationResult[] toBeUpdatedVerificationResults);

        void RemoveAndUpdate(params VerificationResult[] toBeUpdatedVerificationResults);
    }
}