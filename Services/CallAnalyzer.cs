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
        public (List<LogEntry> entries, CallInfo info) AnalyzeCall(
            List<LogEntry> allEntries, string callId, List<SipMessage>? sipMessages = null)
        {
            var sourceFiles = new HashSet<string>();

            // Find initial matches by callId in Message or SipRawBody
            var escapedCallId = Regex.Escape(callId);
            var pattern = $"(CallID|ID)\\s*=\\s*['\"]?{escapedCallId}['\"]?";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

            var initialMatches = allEntries.Where(e =>
                regex.IsMatch(e.Message) || regex.IsMatch(e.SipRawBody)).ToList();

            if (initialMatches.Count == 0)
            {
                initialMatches = allEntries.Where(e =>
                    e.Message.Contains(callId, StringComparison.OrdinalIgnoreCase) ||
                    e.SipRawBody.Contains(callId, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (initialMatches.Count == 0)
            {
                return (new List<LogEntry>(), new CallInfo { CallId = callId });
            }

            var minTime = initialMatches.Min(e => e.Timestamp);
            var maxTime = initialMatches.Max(e => e.Timestamp);

            List<LogEntry> matchedEntries;
            HashSet<string> channelIds = new();
            HashSet<string> partnerSipCallIds = new();

            // Channel-based filtering when SIP messages are available
            var sipNumbers = ExtractSipNumbers(sipMessages, callId);
            if (sipNumbers.Count > 0)
            {
                channelIds = FindChannelIds(allEntries, sipNumbers);
                partnerSipCallIds = FindPartnerSipCallIds(sipMessages!, callId);
            }

            // Use INVITE timestamp as the lower bound (–5 s) to exclude unrelated pre-call traces
            var primaryInvite = sipMessages?.FirstOrDefault(m =>
                m.CallId.Equals(callId, StringComparison.OrdinalIgnoreCase) &&
                m.SipMethod.Equals("INVITE", StringComparison.OrdinalIgnoreCase));
            var inviteStart = primaryInvite != null
                ? primaryInvite.Timestamp.AddSeconds(-5)
                : minTime;

            if (channelIds.Count > 0)
            {
                // Filter strictly by channel IDs and SIP Call-IDs, starting from INVITE time
                matchedEntries = allEntries.Where(e =>
                    e.Timestamp >= inviteStart &&
                    (MatchesChannel(e.Message, channelIds) ||
                    (e.SipRawBody.Length > 0 && (
                        e.SipRawBody.Contains(callId, StringComparison.OrdinalIgnoreCase) ||
                        partnerSipCallIds.Any(p => e.SipRawBody.Contains(p, StringComparison.OrdinalIgnoreCase))
                    )))
                ).OrderBy(e => e.Timestamp).ToList();

                // Find partner channels from Switch statements and add their entries
                var partnerChannelIds = ExtractPartnerChannelIds(matchedEntries);
                if (partnerChannelIds.Count > 0)
                {
                    foreach (var pid in partnerChannelIds) channelIds.Add(pid);
                    var partnerEntries = allEntries.Where(e =>
                        e.Timestamp >= inviteStart && MatchesChannel(e.Message, partnerChannelIds));
                    matchedEntries = matchedEntries.Union(partnerEntries)
                        .OrderBy(e => e.Timestamp).ToList();
                }

                // Extract refs first so SQL INSERT entries get added to matchedEntries
                var callInfo = new CallInfo { CallId = callId };
                callInfo.PartnerChannelIds = channelIds.ToList();
                callInfo.PartnerSipCallIds = partnerSipCallIds.ToList();
                ExtractCallRefs(allEntries, ref matchedEntries, callInfo, minTime, maxTime);

                // Now ExtractCallInfo can find SQL INSERT entries pulled in above
                var fullInfo = ExtractCallInfo(matchedEntries, callId, sourceFiles, minTime, maxTime);
                fullInfo.PartnerChannelIds = callInfo.PartnerChannelIds;
                fullInfo.PartnerSipCallIds = callInfo.PartnerSipCallIds;
                fullInfo.LogicalCallRef = callInfo.LogicalCallRef;
                fullInfo.PhysicalCallRef = callInfo.PhysicalCallRef;
                fullInfo.StatCallRef = callInfo.StatCallRef;
                fullInfo.InviteStartTime = inviteStart;
                if (!string.IsNullOrEmpty(fullInfo.StatCallRef))
                    ExtractCallsQueuesData(allEntries, fullInfo);
                foreach (var entry in matchedEntries) sourceFiles.Add(entry.SourceFile);
                fullInfo.SourceFiles = sourceFiles.OrderBy(f => f).ToList();
                return (matchedEntries, fullInfo);
            }

            // Fallback: time-window approach (±5 min)
            var timeWindowBefore = TimeSpan.FromMinutes(5);
            var timeWindowAfter = TimeSpan.FromMinutes(5);
            var searchStart = minTime.Add(-timeWindowBefore);
            var searchEnd = maxTime.Add(timeWindowAfter);

            matchedEntries = allEntries.Where(e =>
                e.Timestamp >= searchStart && e.Timestamp <= searchEnd
            ).OrderBy(e => e.Timestamp).ToList();

            var partnerIds = ExtractPartnerChannelIds(matchedEntries);
            if (partnerIds.Count > 0)
            {
                var partnerMatches = new List<LogEntry>();
                foreach (var partnerId in partnerIds)
                {
                    var partnerEntries = allEntries.Where(e =>
                        e.Message.Contains(partnerId, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (partnerEntries.Count > 0)
                    {
                        var ps = partnerEntries.Min(e => e.Timestamp).Add(-timeWindowBefore);
                        var pe = partnerEntries.Max(e => e.Timestamp).Add(timeWindowAfter);
                        partnerMatches.AddRange(allEntries.Where(e => e.Timestamp >= ps && e.Timestamp <= pe));
                    }
                }
                matchedEntries = matchedEntries.Union(partnerMatches).OrderBy(e => e.Timestamp).ToList();
            }

            foreach (var entry in matchedEntries) sourceFiles.Add(entry.SourceFile);
            var info = ExtractCallInfo(matchedEntries, callId, sourceFiles, minTime, maxTime);
            info.PartnerChannelIds = partnerIds.ToList();
            ExtractCallRefs(allEntries, ref matchedEntries, info, minTime, maxTime);
            return (matchedEntries, info);
        }

        private static readonly Regex LogicalCallRefRegex = new(
            @"logicalCallRef=([0-9A-Fa-f]{8}(?:-[0-9A-Fa-f]{4}){3}-[0-9A-Fa-f]{12})",
            RegexOptions.Compiled);

        private static readonly Regex PhysicalCallRefRegex = new(
            @"physicalCallRef=([0-9A-Fa-f]{8}(?:-[0-9A-Fa-f]{4}){3}-[0-9A-Fa-f]{12})",
            RegexOptions.Compiled);

        private static readonly Regex StatCallRefValueRegex = new(
            @"value='([0-9A-Fa-f]{8}(?:-[0-9A-Fa-f]{4}){3}-[0-9A-Fa-f]{12})'",
            RegexOptions.Compiled);

        private static void ExtractCallRefs(
            List<LogEntry> allEntries, ref List<LogEntry> matchedEntries, CallInfo callInfo,
            DateTime searchFrom, DateTime searchTo)
        {
            DateTime? logicalRefFoundAt = null;

            // Only look within the exact SIP Call-ID time range (no expansion)
            foreach (var entry in matchedEntries
                .Where(e => e.Timestamp >= searchFrom && e.Timestamp <= searchTo))
            {
                if (callInfo.LogicalCallRef == null)
                {
                    var m = LogicalCallRefRegex.Match(entry.Message);
                    if (m.Success)
                    {
                        callInfo.LogicalCallRef = m.Groups[1].Value;
                        logicalRefFoundAt = entry.Timestamp;
                    }
                }
                if (callInfo.PhysicalCallRef == null)
                {
                    var m = PhysicalCallRefRegex.Match(entry.Message);
                    if (m.Success) callInfo.PhysicalCallRef = m.Groups[1].Value;
                }
                if (callInfo.LogicalCallRef != null && callInfo.PhysicalCallRef != null) break;
            }

            if (callInfo.LogicalCallRef == null || logicalRefFoundAt == null) return;

            // Search only within a short window after the logicalCallRef was first seen
            var statSearchFrom = logicalRefFoundAt.Value;
            var statSearchTo = statSearchFrom.AddSeconds(5);

            var statEntry = allEntries.FirstOrDefault(e =>
                e.Timestamp >= statSearchFrom &&
                e.Timestamp <= statSearchTo &&
                e.Component.Contains("SetStatCallRef", StringComparison.OrdinalIgnoreCase) &&
                e.Message.Contains(callInfo.LogicalCallRef, StringComparison.OrdinalIgnoreCase));

            if (statEntry != null)
            {
                var m = StatCallRefValueRegex.Match(statEntry.Message);
                if (m.Success)
                {
                    callInfo.StatCallRef = m.Groups[1].Value;
                    if (!matchedEntries.Contains(statEntry))
                        matchedEntries = matchedEntries.Append(statEntry).OrderBy(e => e.Timestamp).ToList();

                    // Also pull in SQL INSERT entries for this StatCallRef so ExtractCallInfo can populate CallInfo
                    var statCallRef = callInfo.StatCallRef;
                    var currentMatched = matchedEntries;
                    var sqlEntries = allEntries.Where(e =>
                        e.Message.Contains("insert into Calls", StringComparison.OrdinalIgnoreCase) &&
                        e.Message.Contains(statCallRef, StringComparison.OrdinalIgnoreCase) &&
                        !currentMatched.Contains(e)).ToList();
                    if (sqlEntries.Count > 0)
                        matchedEntries = matchedEntries.Concat(sqlEntries).OrderBy(e => e.Timestamp).ToList();
                }
            }
        }

        private static bool MatchesChannel(string message, HashSet<string> channelIds)
        {
            foreach (var id in channelIds)
            {
                var prefix = $"id={id}";
                if (message.StartsWith(prefix) &&
                    (message.Length == prefix.Length ||
                     message[prefix.Length] == ' ' ||
                     message[prefix.Length] == '['))
                    return true;
            }
            return false;
        }

        private static HashSet<string> ExtractSipNumbers(List<SipMessage>? sipMessages, string callId)
        {
            var numbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (sipMessages == null) return numbers;

            var relevant = sipMessages.Where(m =>
                m.CallId.Equals(callId, StringComparison.OrdinalIgnoreCase) ||
                m.CallId.Contains(callId, StringComparison.OrdinalIgnoreCase));

            foreach (var m in relevant)
            {
                if (!string.IsNullOrEmpty(m.FromNumber)) numbers.Add(NormalizeNumber(m.FromNumber));
                if (!string.IsNullOrEmpty(m.ToNumber)) numbers.Add(NormalizeNumber(m.ToNumber));
            }
            return numbers;
        }

        private static HashSet<string> FindChannelIds(List<LogEntry> allEntries, HashSet<string> normalizedNumbers)
        {
            var channelIds = new HashSet<string>();
            var idRegex = new Regex(@"\bid=(\d+)", RegexOptions.Compiled);

            foreach (var entry in allEntries)
            {
                var msg = entry.Message;
                if (!normalizedNumbers.Any(n => ContainsNumber(msg, n))) continue;
                var m = idRegex.Match(msg);
                if (m.Success) channelIds.Add(m.Groups[1].Value);
            }
            return channelIds;
        }

        private static HashSet<string> FindPartnerSipCallIds(
            List<SipMessage> sipMessages, string primaryCallId)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var invite = sipMessages.FirstOrDefault(m =>
                m.CallId.Equals(primaryCallId, StringComparison.OrdinalIgnoreCase) &&
                m.SipMethod.Equals("INVITE", StringComparison.OrdinalIgnoreCase));

            if (invite == null) return result;

            string searchNumber;
            if (invite.Direction.Equals("Received", StringComparison.OrdinalIgnoreCase))
                searchNumber = NormalizeNumber(invite.FromNumber);
            else
                searchNumber = NormalizeNumber(invite.ToNumber);

            if (string.IsNullOrEmpty(searchNumber)) return result;

            // Window: 5 s before primary INVITE → 5 s after primary BYE
            var bye = sipMessages.LastOrDefault(m =>
                m.CallId.Equals(primaryCallId, StringComparison.OrdinalIgnoreCase) &&
                m.SipMethod.Equals("BYE", StringComparison.OrdinalIgnoreCase));
            var windowStart = invite.Timestamp.AddSeconds(-5);
            var windowEnd = (bye?.Timestamp ?? invite.Timestamp).AddSeconds(5);

            foreach (var msg in sipMessages)
            {
                if (msg.CallId.Equals(primaryCallId, StringComparison.OrdinalIgnoreCase)) continue;
                if (!msg.SipMethod.Equals("INVITE", StringComparison.OrdinalIgnoreCase)) continue;
                if (msg.Timestamp < windowStart || msg.Timestamp > windowEnd) continue;

                string candidate;
                if (invite.Direction.Equals("Received", StringComparison.OrdinalIgnoreCase))
                    candidate = NormalizeNumber(msg.FromNumber);
                else
                    candidate = NormalizeNumber(msg.ToNumber);

                if (candidate.Equals(searchNumber, StringComparison.OrdinalIgnoreCase))
                    result.Add(msg.CallId);
            }

            return result;
        }

        private static string NormalizeNumber(string n)
        {
            n = n.Trim();
            if (n.StartsWith("+")) return n.Substring(1);
            if (n.StartsWith("00")) return n.Substring(2);
            return n;
        }

        private static bool ContainsNumber(string message, string normalizedNumber)
        {
            if (string.IsNullOrEmpty(normalizedNumber) || normalizedNumber.Length < 3) return false;
            // Match both +49... and 0049... variants
            return message.Contains(normalizedNumber, StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("+" + normalizedNumber, StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("00" + normalizedNumber, StringComparison.OrdinalIgnoreCase);
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

        private void ExtractCallsQueuesData(List<LogEntry> allEntries, CallInfo info)
        {
            var statRef = info.StatCallRef!;
            var cqEntries = allEntries
                .Where(e => e.Message.Contains("insert into CallsQueues", StringComparison.OrdinalIgnoreCase)
                         && e.Message.Contains(statRef, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Timestamp)
                .ToList();

            foreach (var entry in cqEntries)
            {
                var (_, cols) = _sqlParser.ParseInsertStatement(entry.Message);
                if (!cols.TryGetValue("CallId", out var cqCallId) ||
                    !cqCallId.Equals(statRef, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (cols.TryGetValue("UserLogin", out var user) && !string.IsNullOrWhiteSpace(user) && user != "null")
                    info.UserLogin = user;

                if (cols.TryGetValue("ChannelNumber", out var ch) && !string.IsNullOrWhiteSpace(ch) && ch != "null"
                    && !info.CallsQueuesChannelIds.Contains(ch))
                    info.CallsQueuesChannelIds.Add(ch);
            }
        }

        private void ExtractFromSql(string sqlMessage, CallInfo info)
        {
            var (_, columns) = _sqlParser.ParseInsertStatement(sqlMessage);

            if (columns.TryGetValue("CallingNumber", out var calling))
                info.CallingNumber = calling;

            if (columns.TryGetValue("CalledNumber", out var called))
                info.CalledNumber = called;

            if (columns.TryGetValue("ChannelNumber", out var channel))
                info.ChannelNumber = channel;

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
