using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogAnalyzer.Models;
using LogAnalyzer.Services;

namespace LogAnalyzer.ViewModels;

public partial class CallSearchViewModel : ObservableObject
{
    private readonly SipLogParser _sipParser = new();

    [ObservableProperty]
    private bool isScanning = false;

    [ObservableProperty]
    private int progressValue = 0;

    [ObservableProperty]
    private string statusMessage = "Select a log folder and click Scan to list calls";

    [ObservableProperty]
    private CallSummary? selectedCall;

    public ObservableCollection<CallSummary> Calls { get; } = new();

    public async Task ScanAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            StatusMessage = "Please select a log folder first";
            return;
        }

        IsScanning = true;
        ProgressValue = 0;
        Calls.Clear();
        StatusMessage = "Scanning for INVITE messages...";

        try
        {
            var progress = new Progress<int>(p => ProgressValue = p);
            var messages = await _sipParser.ParseAsync(folderPath, progress);
            var summaries = _sipParser.BuildCallSummaries(messages);

            foreach (var summary in summaries)
            {
                Calls.Add(summary);
            }

            StatusMessage = $"Found {Calls.Count} call(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void CopyCallId(CallSummary? call)
    {
        var target = call ?? SelectedCall;
        if (target == null || string.IsNullOrWhiteSpace(target.CallId))
            return;

        Clipboard.SetText(target.CallId);
        StatusMessage = $"Copied Call-ID: {target.CallId}";
    }
}
