using System;

namespace LogAnalyzer.Models
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string ThreadId { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string SourceFile { get; set; } = string.Empty;
        public string SipRawBody { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"[{Level,6}] {Timestamp:HH:mm:ss.fff} {Component,-40} {Message}";
        }
    }
}
