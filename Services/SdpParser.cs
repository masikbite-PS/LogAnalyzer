using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services;

public static class SdpParser
{
    private static readonly Dictionary<int, string> StaticCodecs = new()
    {
        { 0, "PCMU" },
        { 3, "GSM" },
        { 8, "PCMA" },
        { 9, "G722" },
        { 18, "G729" }
    };

    private static readonly Dictionary<string, string> ClockRates = new()
    {
        { "PCMU", "8k" },
        { "GSM", "8k" },
        { "PCMA", "8k" },
        { "G722", "8k" },
        { "G729", "8k" },
        { "Opus", "48k" }
    };

    public static SdpInfo Parse(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            return new SdpInfo { HasSdp = false };

        var lines = rawBody.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        var blankLineIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                blankLineIndex = i;
                break;
            }
        }

        if (blankLineIndex == -1)
            return new SdpInfo { HasSdp = false };

        var sdpLines = lines.Skip(blankLineIndex + 1).ToList();

        if (sdpLines.Count == 0 || !sdpLines.Any(l => l.StartsWith("v=")))
            return new SdpInfo { HasSdp = false };

        var sdpInfo = new SdpInfo { HasSdp = true };

        var connectionMatch = Regex.Match(string.Join("\n", sdpLines), @"^c=IN IP4\s+([\d.]+)", RegexOptions.Multiline);
        if (connectionMatch.Success)
            sdpInfo.ConnectionIp = connectionMatch.Groups[1].Value;

        var mediaMatch = Regex.Match(string.Join("\n", sdpLines), @"^m=(\w+)\s+(\d+)", RegexOptions.Multiline);
        if (mediaMatch.Success)
        {
            sdpInfo.MediaType = mediaMatch.Groups[1].Value;
            sdpInfo.MediaPort = mediaMatch.Groups[2].Value;
        }

        var directionMatch = Regex.Match(string.Join("\n", sdpLines), @"^a=(sendrecv|sendonly|recvonly|inactive)", RegexOptions.Multiline);
        if (directionMatch.Success)
            sdpInfo.Direction = directionMatch.Groups[1].Value;

        var rtpmapMatches = Regex.Matches(string.Join("\n", sdpLines), @"^a=rtpmap:(\d+)\s+([\w-]+)/(\d+)", RegexOptions.Multiline);
        var rtpmapDict = new Dictionary<int, (string Name, int Clock)>();

        foreach (Match match in rtpmapMatches)
        {
            if (int.TryParse(match.Groups[1].Value, out var pt))
            {
                var name = match.Groups[2].Value;
                if (int.TryParse(match.Groups[3].Value, out var clock))
                {
                    rtpmapDict[pt] = (name, clock);
                }
            }
        }

        var mediaLine = sdpLines.FirstOrDefault(l => l.StartsWith("m="));
        if (!string.IsNullOrEmpty(mediaLine))
        {
            var parts = mediaLine.Split(' ');
            if (parts.Length > 3)
            {
                for (int i = 3; i < parts.Length; i++)
                {
                    if (int.TryParse(parts[i], out var pt))
                    {
                        string codecName = "";

                        if (rtpmapDict.ContainsKey(pt))
                        {
                            var (name, clock) = rtpmapDict[pt];
                            if (name.Equals("telephone-event", StringComparison.OrdinalIgnoreCase))
                                continue;
                            codecName = $"{name}/{(clock / 1000)}k";
                        }
                        else if (StaticCodecs.ContainsKey(pt))
                        {
                            var name = StaticCodecs[pt];
                            codecName = $"{name}/8k";
                        }

                        if (!string.IsNullOrEmpty(codecName) && !sdpInfo.Codecs.Contains(codecName))
                            sdpInfo.Codecs.Add(codecName);
                    }
                }
            }
        }

        return sdpInfo;
    }
}
