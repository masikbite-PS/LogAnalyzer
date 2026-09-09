using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogAnalyzer.Models;
using LogAnalyzer.Services;

namespace LogAnalyzer.ViewModels;

public partial class SipRegistrationViewModel : ObservableObject
{
    private readonly SipLogParser _sipParser = new();
    private readonly SipCallFlowDiagramBuilder _diagramBuilder = new();
    private List<SipMessage> _allMessages = new();

    [ObservableProperty]
    private bool isScanning = false;

    [ObservableProperty]
    private int progressValue = 0;

    [ObservableProperty]
    private string statusMessage = "Select a log folder and click Scan to list registrations";

    [ObservableProperty]
    private RegistrationSummary? selectedRegistration;

    [ObservableProperty]
    private string chainDiagram = "";

    public ObservableCollection<RegistrationSummary> Registrations { get; } = new();

    public async Task ScanAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            StatusMessage = "Please select a log folder first";
            return;
        }

        IsScanning = true;
        ProgressValue = 0;
        Registrations.Clear();
        SelectedRegistration = null;
        ChainDiagram = "";
        StatusMessage = "Scanning for REGISTER messages...";

        try
        {
            var progress = new Progress<int>(p => ProgressValue = p);
            _allMessages = await _sipParser.ParseAsync(folderPath, progress);
            var summaries = _sipParser.BuildRegistrationSummaries(_allMessages);

            foreach (var summary in summaries)
            {
                Registrations.Add(summary);
            }

            StatusMessage = $"Found {Registrations.Count} registration(s)";
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

    partial void OnSelectedRegistrationChanged(RegistrationSummary? value)
    {
        if (value == null)
        {
            ChainDiagram = "";
            return;
        }

        var chain = _allMessages
            .Where(m => m.CallId.Equals(value.CallId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.Timestamp)
            .ToList();

        ChainDiagram = _diagramBuilder.Build(chain);
    }

    [RelayCommand]
    private void CopyCallId(RegistrationSummary? registration)
    {
        var target = registration ?? SelectedRegistration;
        if (target == null || string.IsNullOrWhiteSpace(target.CallId))
            return;

        Clipboard.SetText(target.CallId);
        StatusMessage = $"Copied Call-ID: {target.CallId}";
    }
}
