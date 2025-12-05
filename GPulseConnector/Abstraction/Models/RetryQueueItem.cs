using System;
using System.ComponentModel.DataAnnotations.Schema;
namespace GPulseConnector.Abstraction.Models
{
    [Table("retry_queue_items")]
    
   public class RetryQueueItem
    {
        [Column("id")]
        public long Id { get; set; }
        [Column("payload_type")]
        public string PayloadType { get; set; } = string.Empty;
        [Column("payload_json")]
        public string PayloadJson { get; set; } = string.Empty;
        [Column("payload_hash")]
        public string? PayloadHash { get; set; }
        [Column("attempt_count")]
        public int AttemptCount { get; set; } = 0;
        [Column("created_on_utc")]
        public DateTime CreatedOnUtc { get; set; } = DateTime.UtcNow;
        [Column("last_error")]
        public string? LastError { get; set; }
    }

    
}
