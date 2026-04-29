using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LogAnalyzer.Models;

namespace LogAnalyzer.ViewModels;

public partial class ScriptsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<LogEntry> scriptsEntries = new();

    [ObservableProperty]
    private string statusMessage = "Run analysis first";

    public void SetData(List<LogEntry> allEntries, string? channelNumber, List<string> partnerChannelIds)
    {
        StatusMessage = "Analyzing...";
        ScriptsEntries.Clear();

        var channelIds = new List<string>();
        if (!string.IsNullOrWhiteSpace(channelNumber))
            channelIds.Add(channelNumber);
        channelIds.AddRange(partnerChannelIds);

        if (channelIds.Count == 0)
        {
            StatusMessage = "No channel info available";
            return;
        }

        var filtered = allEntries
            .Where(e => e.Component.Contains("Scripts::", StringComparison.OrdinalIgnoreCase))
            .Where(e => channelIds.Any(id => e.Message.Contains(id, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(e => e.Timestamp)
            .ToList();

        foreach (var entry in filtered)
            ScriptsEntries.Add(entry);

        var channelInfo = string.Join(", ", channelIds);
        StatusMessage = $"Found {filtered.Count} Scripts:: trace(s) for channels: {channelInfo}";
    }
}
