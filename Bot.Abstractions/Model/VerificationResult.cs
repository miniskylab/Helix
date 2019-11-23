using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace Helix.Bot.Abstractions
{
    public class VerificationResult
    {
        [Key]
        public int Id { [UsedImplicitly] get; set; }

        [Required]
        public bool IsInternalResource { [UsedImplicitly] get; set; }

        public string ParentUrl { [UsedImplicitly] get; set; }

        [Required]
        public string ResourceType { [UsedImplicitly] get; set; }

        [Required]
        public StatusCode StatusCode { [UsedImplicitly] get; set; }

        public string StatusMessage { [UsedImplicitly] get; set; }

        [Required]
        public string VerifiedUrl { [UsedImplicitly] get; set; }
    }
}