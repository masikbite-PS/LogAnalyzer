using System.Collections.Generic;

namespace LogAnalyzer.Models;

public class SdpInfo
{
    public bool HasSdp { get; set; }
    public string MediaType { get; set; } = "";
    public string MediaPort { get; set; } = "";
    public string ConnectionIp { get; set; } = "";
    public string Direction { get; set; } = "";
    public List<string> Codecs { get; set; } = new();

    public string Summary =>
        HasSdp ? $"{MediaType}: {string.Join(", ", Codecs)} → {ConnectionIp}:{MediaPort}" : "-";
}
