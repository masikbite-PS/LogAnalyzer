namespace LogAnalyzer.Models;

public class SubscriptionSummary
{
    public string CallId { get; set; } = "";
    public string Subscriber { get; set; } = "";
    public string Target { get; set; } = "";
    public string Event { get; set; } = "";
    public DateTime StartTime { get; set; }
    public int SubscribeCount { get; set; }
    public int NotifyCount { get; set; }
    public string State { get; set; } = "";
    public string FinalStatus { get; set; } = "";
    public string SourceFile { get; set; } = "";
}
