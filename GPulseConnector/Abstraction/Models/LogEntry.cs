using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace GPulseConnector.Abstraction.Models
{
    [Table("log_entries")]
    public class LogEntry
    {
        [Column("id")]
        public int Id { get; set; }
        [Column("timestamp_utc")]
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        [Column("message")]
        public string Message { get; set; } = string.Empty;
        [Column("level")]       
        public string? Level { get; set; }
    }
}
