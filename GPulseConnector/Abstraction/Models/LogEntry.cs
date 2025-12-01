using System;

namespace GPulseConnector.Abstraction.Models
{
    public class LogEntry
    {
        public int Id { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string Message { get; set; } = string.Empty;
        public string? Level { get; set; }
    }
}
