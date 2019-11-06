using System.Collections.Generic;

namespace Helix.Bot.Abstractions
{
    public class SuccessfulProcessingResult : ProcessingResult
    {
        public List<Resource> NewResources { get; set; }
    }

    public class FailedProcessingResult : ProcessingResult { }

    public abstract class ProcessingResult
    {
        public Resource ProcessedResource { get; set; }
    }
}