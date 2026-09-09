using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services;

public class SipCallFlowDiagramBuilder
{
    private const int TimeWidth = 18;
    private const int FromWidth = 18;
    private const int ToWidth = 18;
    private const int MessageWidth = 14;
    private const int ExpiresWidth = 20;

    public string Build(IEnumerable<SipMessage> messages)
    {
        var msgList = messages.OrderBy(m => m.Timestamp).ToList();
        if (msgList.Count == 0)
            return "";

        var sb = new StringBuilder();
        var hasExpires = msgList.Any(m => !string.IsNullOrEmpty(m.Expires));

        sb.AppendLine("Merged Call Flow with Session Details");
        sb.AppendLine(new string('═', TimeWidth + FromWidth + ToWidth + MessageWidth + (hasExpires ? ExpiresWidth : 0) + 50));
        sb.AppendLine($"{"Time".PadRight(TimeWidth)}{"From".PadRight(FromWidth)}{"To".PadRight(ToWidth)}{"Message".PadRight(MessageWidth)}{(hasExpires ? "Expires".PadRight(ExpiresWidth) : "")}SDP");
        sb.AppendLine(new string('─', TimeWidth + FromWidth + ToWidth + MessageWidth + (hasExpires ? ExpiresWidth : 0) + 50));

        foreach (var msg in msgList)
        {
            var sdpInfo = SdpParser.Parse(msg.RawBody);
            var fromLabel = GetParticipantLabel(msg, isDest: false);
            var toLabel = GetParticipantLabel(msg, isDest: true);
            var method = msg.SipMethod.Length > 12 ? msg.SipMethod.Substring(0, 12) : msg.SipMethod;
            var expiresLabel = hasExpires ? FormatExpires(msg.Expires).PadRight(ExpiresWidth) : "";

            var line = $"{msg.Timestamp:HH:mm:ss.fff}".PadRight(TimeWidth)
                     + fromLabel.PadRight(FromWidth)
                     + toLabel.PadRight(ToWidth)
                     + method.PadRight(MessageWidth)
                     + expiresLabel
                     + sdpInfo.Summary;

            sb.AppendLine(line);
        }

        sb.AppendLine(new string('─', TimeWidth + FromWidth + ToWidth + MessageWidth + (hasExpires ? ExpiresWidth : 0) + 50));

        return sb.ToString().TrimEnd();
    }

    private string FormatExpires(string expires)
    {
        if (string.IsNullOrEmpty(expires))
            return "";

        return expires == "0" ? "0 (ended)" : expires;
    }

    private string GetParticipantLabel(SipMessage msg, bool isDest)
    {
        if (msg.Direction == "Received")
        {
            var label = isDest ? "PBX" : ExtractIp(msg.RemoteAddress);
            return label;
        }
        else
        {
            var label = isDest ? ExtractIp(msg.RemoteAddress) : "PBX";
            return label;
        }
    }

    private string ExtractIp(string remoteAddress)
    {
        if (string.IsNullOrWhiteSpace(remoteAddress))
            return "";

        var parts = remoteAddress.Split(':');
        return parts[0];
    }
}
