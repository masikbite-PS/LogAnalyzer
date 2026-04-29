using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogAnalyzer.Models;
using LogAnalyzer.Services;

namespace LogAnalyzer.ViewModels;

public class SipFlowGroup
{
    public string CallId { get; set; } = "";
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

    [ObservableProperty]
    private string mergedCallFlowDiagram = "";

    private readonly SipKnowledgeBase _kb = new();
    private readonly SipCallFlowDiagramBuilder _diagramBuilder = new();

    public void SetData(List<SipMessage> messages, string sipCallId, string? partnerCallId = null,
        IEnumerable<string>? partnerSipCallIds = null)
    {
        SipFlowGroups.Clear();
        SelectedMessage = null;
        MergedCallFlowDiagram = "";
        StatusMessage = "";

        if (string.IsNullOrWhiteSpace(sipCallId) && string.IsNullOrWhiteSpace(partnerCallId))
        {
            StatusMessage = "No SIP Call-ID provided";
            return;
        }

        // Build full set of Call-IDs to show
        var allCallIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(sipCallId)) allCallIds.Add(sipCallId);
        if (!string.IsNullOrWhiteSpace(partnerCallId)) allCallIds.Add(partnerCallId);
        if (partnerSipCallIds != null)
            foreach (var id in partnerSipCallIds) if (!string.IsNullOrWhiteSpace(id)) allCallIds.Add(id);

        // Filter messages by Call-ID - exact match preferred
        var filtered = messages
            .Where(m => allCallIds.Contains(m.CallId))
            .OrderBy(m => m.Timestamp)
            .ToList();

        // If no exact match, try substring match on primary call ID
        if (filtered.Count == 0)
        {
            filtered = messages
                .Where(m => allCallIds.Any(id => m.CallId.Contains(id, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(m => m.Timestamp)
                .ToList();
        }

        if (filtered.Count == 0)
        {
            StatusMessage = $"No SIP messages found for Call-ID: {sipCallId}";
            return;
        }

        var hasInvite = filtered.Any(m =>
            m.CallId.Equals(sipCallId, StringComparison.OrdinalIgnoreCase) &&
            m.SipMethod.Equals("INVITE", StringComparison.OrdinalIgnoreCase));

        if (!hasInvite)
        {
            StatusMessage = "No INVITE found for this Call-ID — call flow not available";
            return;
        }

        // Group by Call-ID
        var groups = filtered.GroupBy(m => m.CallId).ToList();

        int totalMessages = 0;
        foreach (var group in groups)
        {
            var flowGroup = new SipFlowGroup
            {
                CallId = group.Key
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

        // Build merged diagram
        MergedCallFlowDiagram = _diagramBuilder.Build(filtered);

        // Update status
        var dialogCount = groups.Count;
        StatusMessage = $"Found {dialogCount} SIP dialog(s), {totalMessages} message(s) total";
    }

    [RelayCommand]
    public void ExportToTextile()
    {
        if (string.IsNullOrWhiteSpace(MergedCallFlowDiagram))
        {
            MessageBox.Show("No data to export", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var textile = GenerateTextileFormat();
        var dialog = new TextileExportDialog(textile);
        dialog.ShowDialog();
    }

    private string GenerateTextileFormat()
    {
        var sb = new StringBuilder();

        sb.AppendLine("h3. SIP Call Flow with Session Details");
        sb.AppendLine();

        foreach (var group in SipFlowGroups)
        {
            sb.AppendLine($"h4. Dialog: {EscapeTextile(group.CallId)}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(group.FlowSummary))
            {
                sb.AppendLine(group.FlowSummary);
                sb.AppendLine();
            }

            sb.AppendLine("|_.#|_.Time|_.From|_.To|_.Method|_.SDP|");
            foreach (var msg in group.Messages)
            {
                var sdpInfo = SdpParser.Parse(msg.RawBody);
                var from = msg.Direction == "Received" ? ExtractIp(msg.RemoteAddress) : "PBX";
                var to   = msg.Direction == "Received" ? "PBX" : ExtractIp(msg.RemoteAddress);
                sb.AppendLine($"|{msg.SequenceNumber}" +
                              $"|{msg.Timestamp:HH:mm:ss.fff}" +
                              $"|{EscapeTextile(from)}" +
                              $"|{EscapeTextile(to)}" +
                              $"|{EscapeTextile(msg.SipMethod)}" +
                              $"|{EscapeTextile(sdpInfo.Summary)}|");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string EscapeTextile(string text) =>
        string.IsNullOrEmpty(text) ? "" : text.Replace("|", "&#124;");

    private static string ExtractIp(string remoteAddress)
    {
        if (string.IsNullOrWhiteSpace(remoteAddress)) return "";
        return remoteAddress.Split(':')[0];
    }
}
