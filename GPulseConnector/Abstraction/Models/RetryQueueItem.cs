using System;

namespace GPulseConnector.Abstraction.Models
{
    public class RetryQueueItem
    {
        public long Id { get; set; }
        public string PayloadType { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public string? PayloadHash { get; set; }
        public int AttemptCount { get; set; } = 0;
        public DateTime CreatedOnUtc { get; set; } = DateTime.UtcNow;
        public string? LastError { get; set; }
    }

    
}
