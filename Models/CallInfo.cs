using System;
using System.Collections.Generic;

namespace LogAnalyzer.Models
{
    public class CallInfo
    {
        public string CallId { get; set; } = string.Empty;
        public string? CallingNumber { get; set; }
        public string? CalledNumber { get; set; }
        public string? ChannelNumber { get; set; }
        public string? OwnPhysicalId { get; set; }
        public string? PartnerPhysicalId { get; set; }
        public long? Duration { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        // From SQL Calls record
        public string? ServerStartDateTime { get; set; }
        public string? UserLogin { get; set; }
        public string? CallTypeName { get; set; }

        public List<string> SourceFiles { get; set; } = new();
        public List<string> PartnerChannelIds { get; set; } = new();
        public List<string> PartnerSipCallIds { get; set; } = new();
        public List<string> CallsQueuesChannelIds { get; set; } = new();

        // Extracted from channel logs when CallId is not provided
        public string? LogicalCallRef { get; set; }
        public string? PhysicalCallRef { get; set; }
        public string? StatCallRef { get; set; }
        public DateTime? InviteStartTime { get; set; }
    }
}
