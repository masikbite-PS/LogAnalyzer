using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services;

public class SipCallFlowDiagramBuilder
{
    private const int ColumnWidth = 24;
    private const int InnerWidth = ColumnWidth - 2;

    public string Build(IEnumerable<SipMessage> messages)
    {
        var msgList = messages.OrderBy(m => m.Timestamp).ToList();
        if (msgList.Count == 0)
            return "";

        // Identify participants
        var uniqueAddresses = msgList
            .Select(m => m.RemoteAddress)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct()
            .OrderBy(a => msgList.First(m => m.RemoteAddress == a).Timestamp)
            .ToList();

        if (uniqueAddresses.Count == 0)
            return "";

        var leftParticipant = uniqueAddresses.ElementAtOrDefault(0) ?? "Caller";
        var rightParticipant = uniqueAddresses.ElementAtOrDefault(1);
        var hasRight = rightParticipant != null;

        var sb = new StringBuilder();

        // Build header
        sb.AppendLine(BuildHeaderLine(leftParticipant, "PBX", rightParticipant));
        sb.AppendLine(BuildSeparatorLine());

        // Build message lines
        foreach (var msg in msgList)
        {
            sb.AppendLine(BuildMessageLine(msg, leftParticipant, rightParticipant));
            sb.AppendLine(BuildIdleLine());
        }

        return sb.ToString().TrimEnd();
    }

    private string BuildHeaderLine(string left, string center, string? right)
    {
        var leftPart = left.Length > ColumnWidth ? left.Substring(0, ColumnWidth - 1) : left.PadRight(ColumnWidth);
        var centerPart = center.PadRight(ColumnWidth);
        var rightPart = right != null
            ? (right.Length > ColumnWidth ? right.Substring(0, ColumnWidth - 1) : right.PadRight(ColumnWidth))
            : new string(' ', ColumnWidth);

        return $"{leftPart}{centerPart}{rightPart}";
    }

    private string BuildSeparatorLine()
    {
        var left = "    |".PadRight(ColumnWidth);
        var center = "|".PadRight(ColumnWidth);
        var right = "|";
        return $"{left}{center}{right}";
    }

    private string BuildIdleLine()
    {
        var left = " ".PadRight(ColumnWidth - 1) + "|";
        var center = " ".PadRight(ColumnWidth - 1) + "|";
        var right = " ".PadRight(ColumnWidth - 1) + "|";
        return $"{left}{center}{right}";
    }

    private string BuildMessageLine(SipMessage msg, string leftParticipant, string? rightParticipant)
    {
        var time = msg.Timestamp.ToString("HH:mm:ss");
        var method = msg.SipMethod.Substring(0, Math.Min(msg.SipMethod.Length, 15));

        // Determine direction and sides
        var isToRight = msg.RemoteAddress == rightParticipant;
        var isReceived = msg.Direction == "Received";

        var sb = new StringBuilder();
        sb.Append(time);

        if (!isToRight)
        {
            // Left ↔ PBX
            if (isReceived)
            {
                // Left → PBX
                var arrow = BuildArrow(method, InnerWidth, rightward: true);
                sb.Append("|");
                sb.Append(arrow);
                sb.Append("|");
                sb.Append(new string(' ', ColumnWidth - 1));
                sb.Append("|");
            }
            else
            {
                // PBX → Left
                var arrow = BuildArrow(method, InnerWidth, rightward: false);
                sb.Append("|");
                sb.Append(arrow);
                sb.Append("|");
                sb.Append(new string(' ', ColumnWidth - 1));
                sb.Append("|");
            }
        }
        else if (rightParticipant != null)
        {
            // PBX ↔ Right
            sb.Append("|");
            sb.Append(new string(' ', ColumnWidth - 1));
            sb.Append("|");

            if (isReceived)
            {
                // Right → PBX
                var arrow = BuildArrow(method, InnerWidth, rightward: false);
                sb.Append(arrow);
            }
            else
            {
                // PBX → Right
                var arrow = BuildArrow(method, InnerWidth, rightward: true);
                sb.Append(arrow);
            }
            sb.Append("|");
        }

        return sb.ToString();
    }

    private string BuildArrow(string method, int width, bool rightward)
    {
        var label = method.Length > width - 6 ? method.Substring(0, width - 6) : method;
        var totalDashes = width - label.Length - 3; // 3 = space + space + arrowhead
        var half = totalDashes / 2;

        if (rightward)
        {
            var remaining = totalDashes - half - 1;
            return new string('-', half) + $" {label} " + new string('-', remaining) + ">";
        }
        else
        {
            var remaining = totalDashes - half - 1;
            return "<" + new string('-', half) + $" {label} " + new string('-', remaining);
        }
    }
}
