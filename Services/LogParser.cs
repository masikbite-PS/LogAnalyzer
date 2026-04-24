using System;
using System.Text.RegularExpressions;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services
{
    public class LogParser
    {
        private static readonly Regex LogLineRegex = new(
            @"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})\s+(\S+)\s+(\w+)\s+(\S+)\s+(.*)",
            RegexOptions.Compiled
        );

        public LogEntry? ParseLogLine(string line, string sourceFile)
        {
            var match = LogLineRegex.Match(line);
            if (!match.Success)
                return null;

            if (!DateTime.TryParse(match.Groups[1].Value, out var timestamp))
                return null;

            return new LogEntry
            {
                Timestamp = timestamp,
                ThreadId = match.Groups[2].Value,
                Level = match.Groups[3].Value,
                Component = match.Groups[4].Value,
                Message = match.Groups[5].Value,
                SourceFile = sourceFile
            };
        }
    }
}
