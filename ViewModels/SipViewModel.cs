using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LogAnalyzer.Models;
using LogAnalyzer.Services;

namespace LogAnalyzer.ViewModels;

public partial class SipViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<SipMessage> sipMessages = new();

    [ObservableProperty]
    private SipMessage? selectedMessage;

    [ObservableProperty]
    private string statusMessage = "";

    [ObservableProperty]
    private string callFlowSummary = "";

    private readonly SipKnowledgeBase _kb = new();

    public void SetData(List<SipMessage> messages, string callId, string? partnerCallId)
    {
        SipMessages.Clear();
        SelectedMessage = null;

        if (string.IsNullOrWhiteSpace(callId) && string.IsNullOrWhiteSpace(partnerCallId))
        {
            StatusMessage = "No search criteria provided";
            CallFlowSummary = "";
            return;
        }

        // Filter messages by Call-ID (SIP Call-ID header)
        var filtered = messages
            .Where(m =>
                (!string.IsNullOrWhiteSpace(callId) && m.CallId.Contains(callId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(partnerCallId) && m.CallId.Contains(partnerCallId, StringComparison.OrdinalIgnoreCase))
            )
            .OrderBy(m => m.Timestamp)
            .ToList();

        // Assign sequence numbers
        for (int i = 0; i < filtered.Count; i++)
        {
            filtered[i].SequenceNumber = i + 1;
        }

        foreach (var msg in filtered)
        {
            SipMessages.Add(msg);
        }

        // Build CallFlow summary
        CallFlowSummary = _kb.BuildCallFlowSummary(filtered);

        // Update status
        if (filtered.Count == 0)
        {
            StatusMessage = $"No SIP messages found for Call-ID: {callId}";
        }
        else
        {
            var pattern = _kb.DetectPattern(filtered);
            StatusMessage = $"Found {filtered.Count} SIP message(s) — {_kb.GetPatternDescription(pattern)}";
        }
    }
}
