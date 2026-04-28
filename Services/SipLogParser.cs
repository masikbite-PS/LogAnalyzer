using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services;

public class SipLogParser
{
    private static readonly Regex CallIdRegex = new(
        @"^Call-ID:\s*(.+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline
    );

    private static readonly Regex SipRequestRegex = new(
        @"^([A-Z]+)\s+sip:",
        RegexOptions.Compiled
    );

    private static readonly Regex SipStatusRegex = new(
        @"^SIP/2\.0\s+(\d{3})\s+(.*)",
        RegexOptions.Compiled
    );

    private static readonly Regex DirectionRegex = new(
        @"(Sent message to|Received message from)\s+([\d.]+:\d+)",
        RegexOptions.Compiled
    );

    private static readonly Regex FromRegex = new(
        @"^From:.*?<sip:([^@>;]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline
    );

    private static readonly Regex ToRegex = new(
        @"^To:.*?<sip:([^@>;]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline
    );

    public async Task<List<SipMessage>> ParseAsync(string folderPath, IProgress<int>? progress = null)
    {
        var messages = new List<SipMessage>();

        if (!Directory.Exists(folderPath))
            return messages;

        var logFiles = GetLogFiles(folderPath).ToList();
        int processedFiles = 0;

        foreach (var filePath in logFiles)
        {
            try
            {
                var fileMessages = await ParseFileAsync(filePath);
                messages.AddRange(fileMessages);
            }
            catch { }

            processedFiles++;
            progress?.Report((processedFiles * 100) / Math.Max(logFiles.Count, 1));
        }

        return messages;
    }

    private IEnumerable<string> GetLogFiles(string folderPath)
    {
        try
        {
            return Directory.EnumerateFiles(folderPath, "*.log", SearchOption.AllDirectories);
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    private async Task<List<SipMessage>> ParseFileAsync(string filePath)
    {
        var messages = new List<SipMessage>();
        string? currentHeaderLine = null;
        var bodyBuilder = new List<string>();
        var logLineRegex = LogParser.LogLineRegex;

        try
        {
            using (var reader = new StreamReader(filePath))
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    var match = logLineRegex.Match(line);

                    if (match.Success)
                    {
                        var component = match.Groups[4].Value;

                        if (component.Contains("Sip", StringComparison.OrdinalIgnoreCase) && (line.Contains("Sent message") || line.Contains("Received message")))
                        {
                            // This is a SIP header line
                            // Flush previous message if any
                            if (currentHeaderLine != null && bodyBuilder.Count > 0)
                            {
                                var msg = BuildSipMessage(currentHeaderLine, bodyBuilder, filePath);
                                if (msg != null)
                                    messages.Add(msg);
                            }

                            currentHeaderLine = line;
                            bodyBuilder.Clear();
                        }
                        else
                        {
                            // Different log line (not SIP) — may indicate end of SIP message
                            // But only flush if we have accumulated something
                            if (currentHeaderLine != null && bodyBuilder.Count > 0)
                            {
                                var msg = BuildSipMessage(currentHeaderLine, bodyBuilder, filePath);
                                if (msg != null)
                                    messages.Add(msg);
                                currentHeaderLine = null;
                                bodyBuilder.Clear();
                            }
                        }
                    }
                    else
                    {
                        // Continuation line (not a log header)
                        if (currentHeaderLine != null)
                        {
                            bodyBuilder.Add(line);
                        }
                    }
                }

                // Flush last message
                if (currentHeaderLine != null && bodyBuilder.Count > 0)
                {
                    var msg = BuildSipMessage(currentHeaderLine, bodyBuilder, filePath);
                    if (msg != null)
                        messages.Add(msg);
                }
            }
        }
        catch { }

        return messages;
    }

    private SipMessage? BuildSipMessage(string headerLine, List<string> bodyLines, string sourceFile)
    {
        try
        {
            var match = LogParser.LogLineRegex.Match(headerLine);
            if (!match.Success)
                return null;

            var timestamp = DateTime.TryParse(match.Groups[1].Value, out var dt) ? dt : DateTime.MinValue;
            var threadId = match.Groups[2].Value;
            var level = match.Groups[3].Value;
            var message = match.Groups[5].Value;

            var dirMatch = DirectionRegex.Match(message);
            var direction = "Unknown";
            var remoteAddress = "";

            if (dirMatch.Success)
            {
                direction = dirMatch.Groups[1].Value.Contains("Sent") ? "Sent" : "Received";
                remoteAddress = dirMatch.Groups[2].Value;
            }

            var rawBody = string.Join("\n", bodyLines);

            // Extract Call-ID
            var callIdMatch = CallIdRegex.Match(rawBody);
            var callId = callIdMatch.Success ? callIdMatch.Groups[1].Value.Trim() : "";

            // Extract SIP method or status
            var sipMethod = ExtractSipMethod(bodyLines);

            var fromMatch = FromRegex.Match(rawBody);
            var toMatch = ToRegex.Match(rawBody);

            return new SipMessage
            {
                Timestamp = timestamp,
                ThreadId = threadId,
                Level = level,
                Direction = direction,
                RemoteAddress = remoteAddress,
                RawBody = rawBody,
                CallId = callId,
                SipMethod = sipMethod,
                SourceFile = sourceFile,
                FromNumber = fromMatch.Success ? fromMatch.Groups[1].Value.Trim() : "",
                ToNumber = toMatch.Success ? toMatch.Groups[1].Value.Trim() : ""
            };
        }
        catch
        {
            return null;
        }
    }

    private string ExtractSipMethod(List<string> bodyLines)
    {
        if (bodyLines.Count == 0)
            return "Unknown";

        var firstLine = bodyLines[0];

        // Try request format: "INVITE sip:..."
        var reqMatch = SipRequestRegex.Match(firstLine);
        if (reqMatch.Success)
            return reqMatch.Groups[1].Value;

        // Try response format: "SIP/2.0 200 OK"
        var statusMatch = SipStatusRegex.Match(firstLine);
        if (statusMatch.Success)
            return $"{statusMatch.Groups[1].Value} {statusMatch.Groups[2].Value}";

        return "Unknown";
    }
}
