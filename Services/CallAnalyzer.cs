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
            List<LogEntry> allEntries, string? pbxCallId, string? sipCallId,
            List<SipMessage>? sipMessages = null)
        {
            var sourceFiles = new HashSet<string>();
            // logFilterId: used for PBX log entry matching (prefer PBX CallID, fall back to SIP Call-ID)
            // sipLookupId: used exclusively for SIP message lookups (phone number extraction, partner search)
            var logFilterId = pbxCallId ?? sipCallId ?? "";
            var sipLookupId = sipCallId;

            // Find initial matches by logFilterId in Message or SipRawBody
            var escapedCallId = Regex.Escape(logFilterId);
            var pattern = $"(CallID|ID)\\s*=\\s*['\"]?{escapedCallId}['\"]?";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

            var initialMatches = allEntries.Where(e =>
                regex.IsMatch(e.Message) || regex.IsMatch(e.SipRawBody)).ToList();

            if (initialMatches.Count == 0)
            {
                initialMatches = allEntries.Where(e =>
                    e.Message.Contains(logFilterId, StringComparison.OrdinalIgnoreCase) ||
                    e.SipRawBody.Contains(logFilterId, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (initialMatches.Count == 0)
            {
                return (new List<LogEntry>(), new CallInfo { CallId = logFilterId });
            }

            var minTime = initialMatches.Min(e => e.Timestamp);
            var maxTime = initialMatches.Max(e => e.Timestamp);

            List<LogEntry> matchedEntries;
            HashSet<string> channelIds = new();
            HashSet<string> partnerSipCallIds = new();

            // Channel-based filtering: only when sipLookupId is available (SIP Call-ID known)
            var sipNumbers = sipLookupId != null
                ? ExtractSipNumbers(sipMessages, sipLookupId)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (sipNumbers.Count > 0)
            {
                channelIds = FindChannelIds(allEntries, sipNumbers);
                partnerSipCallIds = FindPartnerSipCallIds(sipMessages!, sipLookupId!);
            }

            // Use INVITE timestamp as the lower bound (–5 s) to exclude unrelated pre-call traces
            var primaryInvite = sipLookupId != null ? sipMessages?.FirstOrDefault(m =>
                m.CallId.Equals(sipLookupId, StringComparison.OrdinalIgnoreCase) &&
                m.SipMethod.Equals("INVITE", StringComparison.OrdinalIgnoreCase)) : null;
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
                        (sipLookupId != null && e.SipRawBody.Contains(sipLookupId, StringComparison.OrdinalIgnoreCase)) ||
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
                var callInfo = new CallInfo { CallId = logFilterId };
                callInfo.PartnerChannelIds = channelIds.ToList();
                callInfo.PartnerSipCallIds = partnerSipCallIds.ToList();
                ExtractCallRefs(allEntries, ref matchedEntries, callInfo, minTime, maxTime);

                // Now ExtractCallInfo can find SQL INSERT entries pulled in above
                var fullInfo = ExtractCallInfo(matchedEntries, logFilterId, sourceFiles, minTime, maxTime);
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

            // For pbxCallId-only: Calls INSERT is the ONLY source of truth
            if (sipLookupId == null)
            {
                var sqlInsert = initialMatches.FirstOrDefault(e =>
                    e.Message.Contains("insert into Calls", StringComparison.OrdinalIgnoreCase));
                if (sqlInsert != null)
                {
                    // Combine Message + SipRawBody to handle multi-line INSERTs
                    var fullSql = string.IsNullOrEmpty(sqlInsert.SipRawBody)
                        ? sqlInsert.Message
                        : sqlInsert.Message + " " + sqlInsert.SipRawBody;
                    var (_, cols) = _sqlParser.ParseInsertStatement(fullSql);

                    if (cols.Count > 0)  // Successful parse
                    {
                        // Build CallInfo directly from parsed columns
                        var callInfo = new CallInfo { CallId = logFilterId };
                        if (cols.TryGetValue("CallingNumber", out var calling)) callInfo.CallingNumber = calling;
                        if (cols.TryGetValue("CalledNumber", out var called)) callInfo.CalledNumber = called;
                        if (cols.TryGetValue("ChannelNumber", out var channel)) callInfo.ChannelNumber = channel;
                        if (cols.TryGetValue("PartnerPhysicalId", out var partner)) callInfo.PartnerPhysicalId = partner;
                        if (cols.TryGetValue("UserLogin", out var user)) callInfo.UserLogin = user;
                        if (cols.TryGetValue("ServerStartDateTime", out var sdt)) callInfo.ServerStartDateTime = sdt;
                        else if (cols.TryGetValue("StartTime", out var st)) callInfo.ServerStartDateTime = st;
                        if (cols.TryGetValue("CallType", out var callType) &&
                            CallTypeNames.TryGetValue(callType, out var typeName))
                            callInfo.CallTypeName = $"{typeName} ({callType})";
                        if (cols.TryGetValue("Duration", out var dur) && long.TryParse(dur, out var durMs))
                            callInfo.Duration = durMs;

                        callInfo.StatCallRef = logFilterId;
                        ExtractCallsQueuesData(allEntries, callInfo);

                        // Extract exact time from ServerStartDateTime
                        if (DateTime.TryParse(callInfo.ServerStartDateTime, out var callStartTime))
                        {
                            var callStart = callStartTime;
                            var callEnd = callInfo.Duration.HasValue && callInfo.Duration.Value > 0
                                ? callStartTime.AddMilliseconds(callInfo.Duration.Value)
                                : callStartTime;

                            // Filter: use ChannelNumber if available, otherwise time only
                            channelIds = new HashSet<string>();
                            if (!string.IsNullOrEmpty(callInfo.ChannelNumber))
                                channelIds.Add(callInfo.ChannelNumber);
                            foreach (var ch in callInfo.CallsQueuesChannelIds)
                                channelIds.Add(ch);

                            if (channelIds.Count > 0)
                            {
                                matchedEntries = allEntries.Where(e =>
                                    e.Timestamp >= callStart && e.Timestamp <= callEnd &&
                                    MatchesChannel(e.Message, channelIds)
                                ).OrderBy(e => e.Timestamp).ToList();
                            }
                            else
                            {
                                matchedEntries = allEntries.Where(e =>
                                    e.Timestamp >= callStart && e.Timestamp <= callEnd
                                ).OrderBy(e => e.Timestamp).ToList();
                            }

                            if (!matchedEntries.Contains(sqlInsert))
                                matchedEntries = matchedEntries.Append(sqlInsert).OrderBy(e => e.Timestamp).ToList();

                            // Partner channels
                            var partnerChannelIds = ExtractPartnerChannelIds(matchedEntries);
                            if (partnerChannelIds.Count > 0)
                            {
                                foreach (var pid in partnerChannelIds) channelIds.Add(pid);
                                var partnerEntries = allEntries.Where(e =>
                                    e.Timestamp >= callStart && e.Timestamp <= callEnd &&
                                    MatchesChannel(e.Message, partnerChannelIds));
                                matchedEntries = matchedEntries.Union(partnerEntries)
                                    .OrderBy(e => e.Timestamp).ToList();
                            }

                            foreach (var entry in matchedEntries) sourceFiles.Add(entry.SourceFile);
                            callInfo.PartnerChannelIds = channelIds.ToList();
                            callInfo.StartTime = callStartTime;
                            callInfo.EndTime = callEnd;
                            callInfo.SourceFiles = sourceFiles.OrderBy(f => f).ToList();
                            return (matchedEntries, callInfo);
                        }
                    }
                }
            }

            // Fallback: time-window approach (±3 sec) — narrow to avoid capturing other calls
            var timeWindowBefore = TimeSpan.FromSeconds(3);
            var timeWindowAfter = TimeSpan.FromSeconds(3);
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
            var info = ExtractCallInfo(matchedEntries, logFilterId, sourceFiles, minTime, maxTime);
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

            // Only look at INVITEs within ±3 minutes of the primary INVITE to avoid matching
            // unrelated calls from the same number across the entire log history
            var windowStart = invite.Timestamp.AddMinutes(-3);
            var windowEnd = invite.Timestamp.AddMinutes(3);

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
