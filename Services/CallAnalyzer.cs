using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LogAnalyzer.Models;
using LogAnalyzer.Services;

namespace LogAnalyzer.Services
{
    public class CallAnalyzer
    {
        public (List<LogEntry> entries, CallInfo info) AnalyzeCall(List<LogEntry> allEntries, string callId)
        {
            var matchedEntries = new List<LogEntry>();
            var sourceFiles = new HashSet<string>();

            // Search for patterns: CallID=<callId> or ID=<callId>
            var escapedCallId = Regex.Escape(callId);
            var pattern = $"(CallID|ID)\\s*=\\s*['\"]?{escapedCallId}['\"]?";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

            var initialMatches = allEntries.Where(e => regex.IsMatch(e.Message)).ToList();

            if (initialMatches.Count == 0)
            {
                // If no exact pattern match, try simple substring search
                initialMatches = allEntries.Where(e =>
                    e.Message.Contains(callId, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (initialMatches.Count == 0)
            {
                return (new List<LogEntry>(), new CallInfo { CallId = callId });
            }

            // Find time boundaries from initial matches
            var minTime = initialMatches.Min(e => e.Timestamp);
            var maxTime = initialMatches.Max(e => e.Timestamp);

            // Expand time window: 5 minutes before first match, 5 minutes after last match
            var timeWindowBefore = TimeSpan.FromMinutes(5);
            var timeWindowAfter = TimeSpan.FromMinutes(5);

            var searchStart = minTime.Add(-timeWindowBefore);
            var searchEnd = maxTime.Add(timeWindowAfter);

            // Collect all entries within the time window
            matchedEntries = allEntries.Where(e =>
                e.Timestamp >= searchStart && e.Timestamp <= searchEnd
            ).OrderBy(e => e.Timestamp).ToList();

            // Extract partner channel IDs from Switch statements
            var partnerChannelIds = ExtractPartnerChannelIds(matchedEntries);

            // Add traces from partner channels
            if (partnerChannelIds.Count > 0)
            {
                var partnerMatches = new List<LogEntry>();
                foreach (var partnerId in partnerChannelIds)
                {
                    var partnerEntries = allEntries.Where(e =>
                        e.Message.Contains(partnerId, StringComparison.OrdinalIgnoreCase)
                    ).ToList();

                    if (partnerEntries.Count > 0)
                    {
                        var partnerMinTime = partnerEntries.Min(e => e.Timestamp);
                        var partnerMaxTime = partnerEntries.Max(e => e.Timestamp);

                        var partnerSearchStart = partnerMinTime.Add(-timeWindowBefore);
                        var partnerSearchEnd = partnerMaxTime.Add(timeWindowAfter);

                        var partnerWindowEntries = allEntries.Where(e =>
                            e.Timestamp >= partnerSearchStart && e.Timestamp <= partnerSearchEnd
                        ).ToList();

                        partnerMatches.AddRange(partnerWindowEntries);
                    }
                }

                // Merge and deduplicate entries
                matchedEntries = matchedEntries.Union(partnerMatches)
                    .OrderBy(e => e.Timestamp)
                    .ToList();
            }

            // Collect source files
            foreach (var entry in matchedEntries)
            {
                sourceFiles.Add(entry.SourceFile);
            }

            // Extract call info
            var callInfo = ExtractCallInfo(matchedEntries, callId, sourceFiles, minTime, maxTime);
            callInfo.PartnerChannelIds = partnerChannelIds.ToList();

            // Enrich partner channel via Calls.PartnerPhysicalId → PhysicalCalls.
            // Reliable when Switch trace heuristics miss (e.g. long queue wait).
            if (!string.IsNullOrEmpty(callInfo.PartnerPhysicalId))
            {
                var partnerCols = FindPhysicalCallRecord(allEntries, callInfo.PartnerPhysicalId);
                if (partnerCols != null &&
                    partnerCols.TryGetValue("ChannelNumber", out var partnerCh) &&
                    !string.IsNullOrWhiteSpace(partnerCh) && partnerCh != "null" &&
                    !callInfo.PartnerChannelIds.Contains(partnerCh))
                {
                    callInfo.PartnerChannelIds.Add(partnerCh);
                }
            }

            return (matchedEntries, callInfo);
        }

        // Resolves the partner SIP Call-ID using PhysicalCalls.StartTime (UTC) plus the
        // Calls UTC↔system delta, then matching the nearest INVITE in time. Replaces
        // the legacy ±N min number heuristic which fails when calls wait long in queue.
        public void EnrichSipPartner(CallInfo info, List<LogEntry> allEntries,
            List<SipMessage>? sipMessages, string primarySipCallId)
        {
            if (sipMessages == null || string.IsNullOrEmpty(info.PartnerPhysicalId)) return;
            var partnerCols = FindPhysicalCallRecord(allEntries, info.PartnerPhysicalId);
            if (partnerCols == null) return;

            var delta = GetUtcToSystemDelta(allEntries, info.CallId);
            if (!delta.HasValue) return;

            if (!partnerCols.TryGetValue("StartTime", out var partnerUtcStr) ||
                !TryParsePbxDateTime(partnerUtcStr, out var partnerUtc)) return;

            var partnerSysStart = partnerUtc + delta.Value;
            var foundSipId = FindPartnerSipCallIdByPhysicalCallStart(
                sipMessages, partnerSysStart, primarySipCallId);
            if (!string.IsNullOrEmpty(foundSipId) &&
                !info.PartnerSipCallIds.Contains(foundSipId))
                info.PartnerSipCallIds.Add(foundSipId);
        }

        private Dictionary<string, string>? FindPhysicalCallRecord(
            List<LogEntry> allEntries, string physicalId)
        {
            foreach (var entry in allEntries)
            {
                if (!entry.Message.Contains("insert into PhysicalCalls", StringComparison.OrdinalIgnoreCase))
                    continue;
                var (table, cols) = _sqlParser.ParseInsertStatement(entry.Message);
                if (!table.Equals("PhysicalCalls", StringComparison.OrdinalIgnoreCase)) continue;
                if (cols.TryGetValue("Id", out var id) &&
                    id.Equals(physicalId, StringComparison.OrdinalIgnoreCase))
                    return cols;
            }
            return null;
        }

        private TimeSpan? GetUtcToSystemDelta(List<LogEntry> allEntries, string? callsId)
        {
            if (string.IsNullOrWhiteSpace(callsId)) return null;
            foreach (var entry in allEntries)
            {
                if (!entry.Message.Contains("insert into Calls", StringComparison.OrdinalIgnoreCase))
                    continue;
                var (table, cols) = _sqlParser.ParseInsertStatement(entry.Message);
                if (!table.Equals("Calls", StringComparison.OrdinalIgnoreCase)) continue;
                if (!cols.TryGetValue("Id", out var id) ||
                    !id.Equals(callsId, StringComparison.OrdinalIgnoreCase)) continue;
                if (cols.TryGetValue("StartTime", out var utcStr) &&
                    cols.TryGetValue("ServerStartDateTime", out var sysStr) &&
                    TryParsePbxDateTime(utcStr, out var utc) &&
                    TryParsePbxDateTime(sysStr, out var sys))
                    return sys - utc;
                return null;
            }
            return null;
        }

        private static readonly string[] PbxDateFormats = new[]
        {
            "yyyyMMdd HH:mm:ss", "yyyyMMdd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.fff"
        };

        private static bool TryParsePbxDateTime(string s, out DateTime result)
        {
            if (DateTime.TryParseExact(s, PbxDateFormats,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out result)) return true;
            return DateTime.TryParse(s, out result);
        }

        private static string? FindPartnerSipCallIdByPhysicalCallStart(
            List<SipMessage> sipMessages, DateTime partnerStartSystem, string primarySipCallId)
        {
            var window = TimeSpan.FromSeconds(2);
            var lo = partnerStartSystem - window;
            var hi = partnerStartSystem + window;
            return sipMessages
                .Where(m => !m.CallId.Equals(primarySipCallId, StringComparison.OrdinalIgnoreCase))
                .Where(m => m.SipMethod.Equals("INVITE", StringComparison.OrdinalIgnoreCase))
                .Where(m => m.Timestamp >= lo && m.Timestamp <= hi)
                .OrderBy(m => Math.Abs((m.Timestamp - partnerStartSystem).Ticks))
                .Select(m => m.CallId)
                .FirstOrDefault();
        }

        private HashSet<string> ExtractPartnerChannelIds(List<LogEntry> entries)
        {
            var partnerIds = new HashSet<string>();
            var partnerPattern = new Regex(@"Switch\s+ChannelID\s*=\s*[""']?(\d+)[""']?\s+partnerChannelID\s*=\s*[""']?(\d+)[""']?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            foreach (var entry in entries)
            {
                var match = partnerPattern.Match(entry.Message);
                if (match.Success)
                {
                    var partnerId = match.Groups[2].Value;
                    if (!string.IsNullOrEmpty(partnerId))
                    {
                        partnerIds.Add(partnerId);
                    }
                }
            }

            return partnerIds;
        }

        private CallInfo ExtractCallInfo(List<LogEntry> entries, string callId, HashSet<string> sourceFiles,
            DateTime minMatchTime, DateTime maxMatchTime)
        {
            var info = new CallInfo
            {
                CallId = callId,
                SourceFiles = sourceFiles.OrderBy(f => f).ToList(),
                StartTime = entries.Count > 0 ? entries.Min(e => e.Timestamp) : null,
                EndTime = entries.Count > 0 ? entries.Max(e => e.Timestamp) : null
                // Duration is NOT calculated from log timestamps.
                // It must come exclusively from the Calls table's Duration column (SQL INSERT).
            };

            // Extract info from SQL INSERT line if present
            var sqlEntry = entries.FirstOrDefault(e =>
                e.Message.Contains("insert into Calls", StringComparison.OrdinalIgnoreCase));

            if (sqlEntry != null)
            {
                ExtractFromSql(sqlEntry.Message, info);
            }

            // Extract channel and partner IDs from all entries mentioning the callId
            var relevantEntries = entries.Where(e =>
                e.Message.Contains(callId, StringComparison.OrdinalIgnoreCase) ||
                e.Message.Contains("ChannelNumber") ||
                e.Message.Contains("PartnerPhysicalId")
            ).ToList();

            foreach (var entry in relevantEntries)
            {
                if (string.IsNullOrEmpty(info.ChannelNumber))
                {
                    var channelMatch = Regex.Match(entry.Message, @"ChannelNumber[=\s]+(\d+)");
                    if (channelMatch.Success)
                        info.ChannelNumber = channelMatch.Groups[1].Value;

                    var channelMatch2 = Regex.Match(entry.Message, @"Channel[=\s]+(\d+)");
                    if (channelMatch2.Success && string.IsNullOrEmpty(info.ChannelNumber))
                        info.ChannelNumber = channelMatch2.Groups[1].Value;
                }

                if (string.IsNullOrEmpty(info.PartnerPhysicalId))
                {
                    var partnerMatch = Regex.Match(entry.Message,
                        "PartnerPhysicalId\\s*=\\s*['\"]?([0-9A-Fa-f-]+)['\"]?");
                    if (partnerMatch.Success)
                        info.PartnerPhysicalId = partnerMatch.Groups[1].Value;
                }
            }

            return info;
        }

        private static readonly Dictionary<string, string> CallTypeNames = new()
        {
            { "1", "Inbound" }, { "2", "Outbound" }, { "3", "Internal" }, { "0", "Other" }
        };

        private readonly SqlParser _sqlParser = new();

        private void ExtractFromSql(string sqlMessage, CallInfo info)
        {
            var (_, columns) = _sqlParser.ParseInsertStatement(sqlMessage);

            if (columns.TryGetValue("CallingNumber", out var calling))
                info.CallingNumber = calling;

            if (columns.TryGetValue("CalledNumber", out var called))
                info.CalledNumber = called;

            if (columns.TryGetValue("ChannelNumber", out var channel))
                info.ChannelNumber = channel;

            if (columns.TryGetValue("OwnPhysicalId", out var own))
                info.OwnPhysicalId = own;

            if (columns.TryGetValue("PartnerPhysicalId", out var partner))
                info.PartnerPhysicalId = partner;

            if (columns.TryGetValue("UserLogin", out var user))
                info.UserLogin = user;

            if (columns.TryGetValue("ServerStartDateTime", out var sdt) && !string.IsNullOrEmpty(sdt))
                info.ServerStartDateTime = sdt;
            else if (columns.TryGetValue("StartTime", out var st))
                info.ServerStartDateTime = st;

            if (columns.TryGetValue("CallType", out var callType) &&
                CallTypeNames.TryGetValue(callType, out var typeName))
                info.CallTypeName = $"{typeName} ({callType})";

            if (columns.TryGetValue("Duration", out var dur) && long.TryParse(dur, out var durMs))
                info.Duration = durMs;
        }

    }
}
