namespace LogAnalyzer.Models;

public class SipMessage
{
    public DateTime Timestamp { get; set; }
    public string ThreadId { get; set; } = "";
    public string Level { get; set; } = "";
    public string Direction { get; set; } = "";
    public string RemoteAddress { get; set; } = "";
    public string RawBody { get; set; } = "";
    public string CallId { get; set; } = "";
    public string SipMethod { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public int SequenceNumber { get; set; }
}
