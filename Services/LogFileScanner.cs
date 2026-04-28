using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services
{
    public class LogFileScanner
    {
        private readonly LogParser _parser = new();

        public async Task<List<LogEntry>> ScanFilesAsync(
            IEnumerable<string> filePaths,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var entries = new List<LogEntry>();
            int totalFiles = 0;
            int processedFiles = 0;

            var fileList = new List<string>(filePaths);
            totalFiles = fileList.Count;

            foreach (var filePath in fileList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var reader = new StreamReader(filePath);
                    string? line;
                    LogEntry? lastEntry = null;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        var entry = _parser.ParseLogLine(line, Path.GetFileName(filePath));
                        if (entry != null)
                        {
                            entries.Add(entry);
                            lastEntry = entry;
                        }
                        else if (lastEntry != null)
                        {
                            lastEntry.SipRawBody = lastEntry.SipRawBody.Length > 0
                                ? lastEntry.SipRawBody + "\n" + line
                                : line;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading {filePath}: {ex.Message}");
                }

                processedFiles++;
                progress?.Report((processedFiles * 100) / Math.Max(totalFiles, 1));
            }

            return entries;
        }

        public List<string> GetLogFiles(string folderPath)
        {
            var files = new List<string>();
            if (!Directory.Exists(folderPath))
                return files;

            foreach (var file in Directory.GetFiles(folderPath, "*.log", SearchOption.AllDirectories))
            {
                files.Add(file);
            }

            return files;
        }
    }
}
