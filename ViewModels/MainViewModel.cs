using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogAnalyzer.Models;
using LogAnalyzer.Services;
using Ookii.Dialogs.Wpf;

namespace LogAnalyzer.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly LogFileScanner _scanner = new();
        private readonly CallAnalyzer _analyzer = new();
        private CancellationTokenSource? _cancellationTokenSource;
        private List<LogEntry> _allScannedEntries = new();

        [ObservableProperty]
        private string pbxFolderPath = "";

        [ObservableProperty]
        private string sipFolderPath = "";

        [ObservableProperty]
        private string callId = "";

        [ObservableProperty]
        private int progressValue = 0;

        [ObservableProperty]
        private string statusMessage = "Ready";

        [ObservableProperty]
        private bool isAnalyzing = false;

        [ObservableProperty]
        private CallInfo? callInfo;

        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        public ObservableCollection<LogLevelFilter> LogLevelFilters { get; } = new()
        {
            new LogLevelFilter { Level = "All", IsSelected = true },
            new LogLevelFilter { Level = "Debug" },
            new LogLevelFilter { Level = "Info" },
            new LogLevelFilter { Level = "Warning" },
            new LogLevelFilter { Level = "Error" }
        };

        public SqlDataViewModel SqlDataViewModel { get; } = new();

        public SipViewModel SipViewModel { get; } = new();

        public ScriptsViewModel ScriptsViewModel { get; } = new();

        [RelayCommand]
        private void SelectPbxFolder()
        {
            var dialog = new VistaFolderBrowserDialog();
            if (dialog.ShowDialog() == true)
            {
                PbxFolderPath = dialog.SelectedPath;
            }
        }

        [RelayCommand]
        private void SelectSipFolder()
        {
            var dialog = new VistaFolderBrowserDialog();
            if (dialog.ShowDialog() == true)
            {
                SipFolderPath = dialog.SelectedPath;
            }
        }

        [RelayCommand]
        private async Task Analyze()
        {
            if (string.IsNullOrWhiteSpace(CallId))
            {
                StatusMessage = "Please enter a CallID";
                return;
            }

            if (string.IsNullOrWhiteSpace(PbxFolderPath) && string.IsNullOrWhiteSpace(SipFolderPath))
            {
                StatusMessage = "Please select at least one folder";
                return;
            }

            IsAnalyzing = true;
            ProgressValue = 0;
            LogEntries.Clear();
            CallInfo = null;

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                var progress = new Progress<int>(p => ProgressValue = p);

                // Collect all log files
                var allFiles = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(PbxFolderPath))
                {
                    allFiles.AddRange(_scanner.GetLogFiles(PbxFolderPath));
                }
                if (!string.IsNullOrWhiteSpace(SipFolderPath))
                {
                    allFiles.AddRange(_scanner.GetLogFiles(SipFolderPath));
                }

                StatusMessage = $"Scanning {allFiles.Count} files...";

                // Scan all files
                var allEntries = await _scanner.ScanFilesAsync(allFiles, progress, _cancellationTokenSource.Token);
                _allScannedEntries = allEntries;

                StatusMessage = $"Scanning complete. Analyzing call {CallId}...";

                // Analyze the call
                var (filteredEntries, callInfo) = _analyzer.AnalyzeCall(allEntries, CallId);

                CallInfo = callInfo;
                SqlDataViewModel.SetData(allEntries, CallId, callInfo.PartnerPhysicalId ?? "");
                ScriptsViewModel.SetData(allEntries, callInfo.ChannelNumber, callInfo.PartnerChannelIds);
                foreach (var entry in filteredEntries)
                {
                    LogEntries.Add(entry);
                }

                // Parse SIP messages
                if (!string.IsNullOrWhiteSpace(SipFolderPath))
                {
                    StatusMessage = "Parsing SIP messages...";
                    var sipParser = new SipLogParser();
                    var sipMessages = await sipParser.ParseAsync(SipFolderPath, progress);
                    SipViewModel.SetData(sipMessages, CallId, callInfo.PartnerPhysicalId);
                }
                else
                {
                    SipViewModel.SetData(new(), CallId, callInfo.PartnerPhysicalId);
                }

                StatusMessage = $"Found {filteredEntries.Count} log entries in {callInfo.SourceFiles.Count} files";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Analysis cancelled";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsAnalyzing = false;
                _cancellationTokenSource?.Dispose();
            }
        }

        [RelayCommand]
        private void ExportLogs()
        {
            if (LogEntries.Count == 0)
            {
                StatusMessage = "No logs to export";
                return;
            }

            var dialog = new VistaSaveFileDialog
            {
                DefaultExt = "log",
                Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using var writer = new StreamWriter(dialog.FileName);
                    writer.WriteLine($"CallID: {CallInfo?.CallId}");
                    var durationStr = CallInfo?.Duration.HasValue == true
                        ? FormatDuration(CallInfo.Duration.Value)
                        : "N/A";
                    writer.WriteLine($"Duration: {durationStr}");
                    writer.WriteLine($"From: {CallInfo?.CallingNumber}");
                    writer.WriteLine($"To: {CallInfo?.CalledNumber}");
                    writer.WriteLine($"Channel: {CallInfo?.ChannelNumber}");
                    writer.WriteLine(new string('-', 80));

                    foreach (var entry in LogEntries)
                    {
                        writer.WriteLine(entry.ToString());
                    }

                    StatusMessage = $"Exported {LogEntries.Count} entries to {dialog.FileName}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void CancelAnalysis()
        {
            _cancellationTokenSource?.Cancel();
        }

        private static string FormatDuration(long milliseconds)
        {
            var totalSeconds = milliseconds / 1000;
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            return $"{minutes}m {seconds}s";
        }
    }

    public partial class LogLevelFilter : ObservableObject
    {
        [ObservableProperty]
        private string level = "";

        [ObservableProperty]
        private bool isSelected;
    }
}
