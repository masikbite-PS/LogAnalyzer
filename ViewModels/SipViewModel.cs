using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LogAnalyzer.Models;
using LogAnalyzer.Services;

namespace LogAnalyzer.ViewModels;

public class SipFlowGroup
{
    public string CallId { get; set; } = "";
    public string ShortCallId { get; set; } = "";
    public string FlowSummary { get; set; } = "";
    public ObservableCollection<SipMessage> Messages { get; set; } = new();
}

public partial class SipViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<SipFlowGroup> sipFlowGroups = new();

    [ObservableProperty]
    private SipMessage? selectedMessage;

    [ObservableProperty]
    private string statusMessage = "";

    private readonly SipKnowledgeBase _kb = new();

    public void SetData(List<SipMessage> messages, string callId, string? partnerCallId)
    {
        SipFlowGroups.Clear();
        SelectedMessage = null;

        if (string.IsNullOrWhiteSpace(callId) && string.IsNullOrWhiteSpace(partnerCallId))
        {
            StatusMessage = "No search criteria provided";
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

        if (filtered.Count == 0)
        {
            StatusMessage = $"No SIP messages found for Call-ID: {callId}";
            return;
        }

        // Group by Call-ID
        var groups = filtered.GroupBy(m => m.CallId).ToList();

        int totalMessages = 0;
        foreach (var group in groups)
        {
            var flowGroup = new SipFlowGroup
            {
                CallId = group.Key,
                ShortCallId = group.Key.Length > 18 ? group.Key.Substring(0, 18) + "..." : group.Key
            };

            var groupMessages = group.OrderBy(m => m.Timestamp).ToList();

            // Assign sequence numbers per group
            for (int i = 0; i < groupMessages.Count; i++)
            {
                groupMessages[i].SequenceNumber = i + 1;
                flowGroup.Messages.Add(groupMessages[i]);
            }

            // Build CallFlow summary for this group
            flowGroup.FlowSummary = _kb.BuildCallFlowSummary(groupMessages);

            SipFlowGroups.Add(flowGroup);
            totalMessages += groupMessages.Count;
        }

        // Update status
        var dialogCount = groups.Count;
        StatusMessage = $"Found {dialogCount} SIP dialog(s), {totalMessages} message(s) total";
    }
}
