namespace LogAnalyzer.Models;

public class CallSummary
{
    public string CallId { get; set; } = "";
    public string CallingNumber { get; set; } = "";
    public string CalledNumber { get; set; } = "";
    public DateTime StartTime { get; set; }
    public string SourceFile { get; set; } = "";
}
