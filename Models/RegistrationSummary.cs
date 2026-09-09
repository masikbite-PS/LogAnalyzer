namespace LogAnalyzer.Models;

public class RegistrationSummary
{
    public string CallId { get; set; } = "";
    public string User { get; set; } = "";
    public DateTime StartTime { get; set; }
    public string FinalStatus { get; set; } = "";
    public int MessageCount { get; set; }
    public string SourceFile { get; set; } = "";
}
