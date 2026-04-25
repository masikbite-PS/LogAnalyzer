namespace LogAnalyzer.Services;

public enum CallFlowPattern
{
    BasicCall,
    CancelledCall,
    BusyCall,
    FailedCall,
    Unknown
}

public class SipKnowledgeBase
{
    private static readonly Dictionary<string, string> MethodDescriptions = new()
    {
        { "INVITE", "Initiate a session (call setup)" },
        { "ACK", "Acknowledge successful final response to INVITE" },
        { "BYE", "Terminate a session" },
        { "CANCEL", "Cancel an in-progress INVITE transaction" },
        { "REGISTER", "Register a user agent with a registrar" },
        { "OPTIONS", "Query server capabilities" },
        { "REFER", "Transfer a call to another party" },
        { "SUBSCRIBE", "Request event notifications" },
        { "NOTIFY", "Deliver event notifications" },
        { "INFO", "Send information in an existing session" },
        { "UPDATE", "Modify session parameters without ending" },
        { "PRACK", "Acknowledge provisional responses" }
    };

    private static readonly Dictionary<string, string> ResponseDescriptions = new()
    {
        // 1xx Provisional
        { "100", "Trying - server received INVITE, processing" },
        { "180", "Ringing - user agent alerted" },
        { "181", "Call Is Being Forwarded" },
        { "182", "Queued" },
        { "183", "Session Progress - early media" },

        // 2xx Success
        { "200", "OK - request successful" },
        { "202", "Accepted" },

        // 3xx Redirection
        { "300", "Multiple Choices" },
        { "301", "Moved Permanently" },
        { "302", "Moved Temporarily" },
        { "305", "Use Proxy" },
        { "380", "Alternative Service" },

        // 4xx Client Error
        { "400", "Bad Request" },
        { "401", "Unauthorized" },
        { "402", "Payment Required" },
        { "403", "Forbidden" },
        { "404", "Not Found" },
        { "405", "Method Not Allowed" },
        { "406", "Not Acceptable" },
        { "407", "Proxy Authentication Required" },
        { "408", "Request Timeout" },
        { "410", "Gone" },
        { "413", "Request Entity Too Large" },
        { "414", "Request-URI Too Long" },
        { "415", "Unsupported Media Type" },
        { "416", "Unsupported URI Scheme" },
        { "420", "Bad Extension" },
        { "421", "Extension Required" },
        { "423", "Interval Too Brief" },
        { "480", "Temporarily Unavailable" },
        { "481", "Call/Transaction Does Not Exist" },
        { "482", "Loop Detected" },
        { "483", "Too Many Hops" },
        { "484", "Address Incomplete" },
        { "485", "Ambiguous" },
        { "486", "Busy Here" },
        { "487", "Request Terminated" },
        { "488", "Not Acceptable Here" },
        { "491", "Request Pending" },
        { "493", "Undecipherable" },

        // 5xx Server Error
        { "500", "Server Internal Error" },
        { "501", "Not Implemented" },
        { "502", "Bad Gateway" },
        { "503", "Service Unavailable" },
        { "504", "Server Time-out" },
        { "505", "Version Not Supported" },
        { "513", "Message Too Large" },

        // 6xx Global Failure
        { "600", "Busy Everywhere" },
        { "603", "Decline" },
        { "604", "Does Not Exist Anywhere" },
        { "606", "Not Acceptable" }
    };

    public string GetMethodDescription(string method)
    {
        return MethodDescriptions.TryGetValue(method.ToUpper(), out var desc) ? desc : $"Unknown method: {method}";
    }

    public string GetResponseDescription(string code)
    {
        return ResponseDescriptions.TryGetValue(code, out var desc) ? desc : $"Unknown response: {code}";
    }

    public string GetResponseClass(string code)
    {
        if (code.Length < 1) return "Unknown";
        return code[0] switch
        {
            '1' => "Provisional",
            '2' => "Success",
            '3' => "Redirection",
            '4' => "Client Error",
            '5' => "Server Error",
            '6' => "Global Failure",
            _ => "Unknown"
        };
    }

    public CallFlowPattern DetectPattern(IEnumerable<Models.SipMessage> messages)
    {
        var msgList = messages.OrderBy(m => m.Timestamp).ToList();
        if (msgList.Count == 0) return CallFlowPattern.Unknown;

        var methods = msgList.Select(m => m.SipMethod.ToUpper()).ToList();

        // BasicCall: INVITE → 100 → 180 → 200 → ACK → [BYE → 200]
        if (methods.Any(m => m == "INVITE") &&
            methods.Any(m => m == "100") &&
            methods.Any(m => m == "200 OK") &&
            methods.Any(m => m == "ACK"))
        {
            return CallFlowPattern.BasicCall;
        }

        // CancelledCall: INVITE → CANCEL
        if (methods.Any(m => m == "INVITE") && methods.Any(m => m == "CANCEL"))
        {
            return CallFlowPattern.CancelledCall;
        }

        // BusyCall: INVITE → 486
        if (methods.Any(m => m == "INVITE") && methods.Any(m => m == "486"))
        {
            return CallFlowPattern.BusyCall;
        }

        // FailedCall: INVITE → 4xx/5xx
        if (methods.Any(m => m == "INVITE") &&
            methods.Any(m => m.StartsWith("4") || m.StartsWith("5")))
        {
            return CallFlowPattern.FailedCall;
        }

        return CallFlowPattern.Unknown;
    }

    public string GetPatternDescription(CallFlowPattern pattern)
    {
        return pattern switch
        {
            CallFlowPattern.BasicCall => "Successful call: INVITE → 100 Trying → 180 Ringing → 200 OK → ACK → [BYE]",
            CallFlowPattern.CancelledCall => "Cancelled call: INVITE → CANCEL",
            CallFlowPattern.BusyCall => "Busy: INVITE → 486 Busy Here",
            CallFlowPattern.FailedCall => "Failed call: INVITE → error response",
            _ => "Unknown call pattern"
        };
    }

    public string BuildCallFlowSummary(IEnumerable<Models.SipMessage> messages)
    {
        var msgList = messages.OrderBy(m => m.Timestamp).ToList();
        if (msgList.Count == 0) return "No SIP messages";

        var flowParts = new List<string>();
        foreach (var msg in msgList)
        {
            var method = msg.SipMethod.ToUpper().Trim();
            if (string.IsNullOrEmpty(method)) continue;

            if (method.StartsWith("SIP/2.0"))
            {
                // Response: extract code
                var parts = method.Split(' ');
                if (parts.Length >= 2)
                {
                    var code = parts[1];
                    flowParts.Add(code + " " + GetResponseDescription(code).Split(" -")[0]);
                }
            }
            else
            {
                // Request method
                flowParts.Add(method);
            }
        }

        return string.Join(" → ", flowParts);
    }
}
