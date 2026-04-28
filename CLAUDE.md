# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Branch Information

⚠️ **This is the `pbx-2024.2` branch**, targeting PBX version **2024.2**. 

- **For PBX 2026.0**: See the `main` branch
- **Key files to adapt for 2024.2 differences** (when real logs are tested): `Services/LogParser.cs`, `Services/SqlParser.cs`, `Services/CallAnalyzer.cs`, `Services/SipLogParser.cs`

## Project Overview

**PBX Log Analyzer** is a WPF desktop application for analyzing telephone PBX system logs. It helps users trace call activity by searching for a specific Call ID across PBX log files, then enriches the results with SQL database records (Calls, Agents, etc.) and SIP protocol messages for deeper analysis.

- **Framework**: .NET 8.0 WPF (Windows Presentation Foundation)
- **Language**: C#
- **Current Version**: 1.3.0
- **Target Platform**: Windows (x64)

## Build & Development

### Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 or equivalent C# IDE

### Build Commands

```bash
# Debug build
dotnet build

# Release build (creates standalone executable)
dotnet publish LogAnalyzer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish

# The executable will be at: publish/LogAnalyzer.exe
```

### Run the Application

```bash
# From project root
dotnet run

# Or directly execute the built binary
.\bin\Debug\net8.0-windows\LogAnalyzer.exe
```

### Code Style & Dependencies

- **MVVM Framework**: CommunityToolkit.Mvvm (v8.4.2) for ObservableObject/RelayCommand
- **File Dialogs**: Ookii.Dialogs.Wpf (v5.0.1) for native folder browser
- **Implicit Usings**: Enabled (no need to type full System.* namespaces)
- **Nullable**: Enabled (use ? for nullable reference types)

## Architecture

### High-Level Flow

1. **User selects folders** (PBX logs and optional SIP logs) and enters a CallID
2. **LogFileScanner** scans the PBX folder for text log files
3. **CallAnalyzer** finds all log entries related to the CallID (within ±5 minute time window for context)
4. Results displayed across 4 tabs:
   - **Log Analysis**: Raw log entries grouped by call
   - **SQL Data**: Enriched database records (Calls, Agents, etc.)
   - **SIP Messages**: SIP protocol messages if SIP folder provided
   - **Scripts**: IVR script traces if available

### Key Components

#### Models (`Models/`)
- **CallInfo**: Represents a single call with metadata (calling/called numbers, duration, user login, etc.)
- **LogEntry**: Parsed log line with timestamp, thread ID, level, component, message
- **SipMessage**: SIP protocol message with method, direction, timestamp, raw body
- **SdpInfo**: Session Description Protocol data extracted from SIP messages
- **SqlDataRow**: Generic SQL record with field name/value/type/description

#### Services (`Services/`)
- **CallAnalyzer** (`CallAnalyzer.cs`): Core logic—finds CallID matches, expands time window, extracts related entries
- **LogFileScanner** (`LogFileScanner.cs`): Recursively finds all `.log` files in a folder
- **LogParser** (`LogParser.cs`): Regex-based parser for PBX log format: `YYYY-MM-DD HH:MM:SS.fff <ThreadID> <Level> <Component> <Message>`
- **SipLogParser** (`SipLogParser.cs`): Extracts SIP messages from text logs, groups by Call-ID
- **SipCallFlowDiagramBuilder** (`SipCallFlowDiagramBuilder.cs`): Generates ASCII call flow diagrams from SIP messages
- **SdpParser** (`SdpParser.cs`): Parses SDP session information from SIP message bodies
- **SqlParser** (`SqlParser.cs`): Parses SQL table records embedded in log output
- **MarkdownTableParser** (`MarkdownTableParser.cs`): Parses markdown table references to populate SQL field descriptions
- **TableDefinitionService** (`TableDefinitionService.cs`): Loads embedded table schema references
- **SipKnowledgeBase** (`SipKnowledgeBase.cs`): Maps SIP method codes and status codes to human-readable names

#### ViewModels (`ViewModels/`)
- **MainViewModel** (`MainViewModel.cs`): Top-level coordinator—manages folder/CallID input, orchestrates analysis, binds to MainWindow
- **SipViewModel** (`SipViewModel.cs`): Manages SIP message display, call flow diagram generation
- **SqlDataViewModel** (`SqlDataViewModel.cs`): Handles SQL record retrieval, table selection, export functionality
- **ScriptsViewModel** (`ScriptsViewModel.cs`): Displays IVR script entries related to the call

#### UI (`*.xaml` / `*.xaml.cs`)
- **MainWindow.xaml**: Four-tab layout (Log Analysis, SQL Data, SIP Messages, Scripts)
- **TextileExportDialog.xaml**: Export options dialog for Textile format

### Data Flow

1. User enters CallID and folders → **MainViewModel.AnalyzeCommand**
2. **LogFileScanner** finds all .log files
3. **LogParser** parses each file into LogEntry objects
4. **CallAnalyzer** filters for CallID + time window
5. Parallel parsers extract:
   - SIP messages (SipLogParser + SipCallFlowDiagramBuilder)
   - SQL records (SqlParser)
   - IVR scripts (pattern matching in ScriptsViewModel)
6. Results bound to ViewModels and displayed in tabs

## Key Patterns & Conventions

### Regex Parsers
- All parsers use compiled Regex for performance (see `LogParser.LogLineRegex`, `SipLogParser` patterns)
- Regexes defined as static readonly fields for reuse

### MVVM via CommunityToolkit
- **ObservableObject**: Base class for ViewModels; use `[ObservableProperty]` attribute to auto-generate property change notifications
- **RelayCommand**: Use `[RelayCommand]` attribute for command handlers (e.g., `SelectPbxFolder()` → `SelectPbxFolderCommand`)
- **ObservableCollection**: Used for dynamic lists (LogEntries, SqlRecords, etc.)

### Async/Progress Reporting
- Long-running operations (file scanning, parsing) use `async Task` with `CancellationToken` support
- Progress is reported via `IProgress<int>` parameter (0-100 range)
- UI updates use `ObservableProperty` binding

### Error Handling
- Parsers generally skip malformed lines silently (try/catch with `catch { }`)
- No exceptions thrown to user unless it's a critical blocker (e.g., invalid folder)

## Release Process

Releases are automated via GitHub Actions (`.github/workflows/release.yml`):
1. Push a git tag in format `v*.*.*` (e.g., `v1.3.1`)
2. GitHub Actions builds release binary with `dotnet publish` (Release config, win-x64, self-contained, single file)
3. Release notes auto-generated from commits
4. Binary `LogAnalyzer.exe` attached to GitHub release

To create a release:
```bash
git tag v1.3.1
git push origin v1.3.1
```

## Project Structure

```
LogAnalyzer/
├── App.xaml(.cs)              # WPF app entry point
├── MainWindow.xaml(.cs)        # Main UI window
├── LogAnalyzer.csproj          # Project file
├── Models/                     # Data classes
│   ├── CallInfo.cs
│   ├── LogEntry.cs
│   ├── SipMessage.cs
│   └── ...
├── Services/                   # Business logic (parsers, analyzers)
│   ├── LogParser.cs
│   ├── SipLogParser.cs
│   ├── CallAnalyzer.cs
│   └── ...
├── ViewModels/                 # MVVM ViewModels
│   ├── MainViewModel.cs
│   ├── SipViewModel.cs
│   └── ...
├── Converters/                 # WPF value converters
├── Data/                       # Embedded resources (table schemas, icons)
└── .github/workflows/          # CI/CD
    └── release.yml
```

## Common Development Tasks

### Adding a New Parser
1. Create a new service class in `Services/` that implements parsing logic
2. Use compiled Regex for performance
3. Return a structured list (e.g., `List<SipMessage>`)
4. Handle malformed input gracefully (skip or log)
5. Add progress reporting if long-running

### Adding a New Tab
1. Create a new ViewModel in `ViewModels/` (inherit from ObservableObject)
2. Create a new UserControl or TabItem in MainWindow.xaml
3. Bind the new ViewModel to the control
4. Populate the control during analysis in MainViewModel.Analyze()

### Updating Table Schema References
1. Modify the corresponding .md file in `Data/`
2. Mark as EmbeddedResource in `LogAnalyzer.csproj`
3. The schema is loaded by TableDefinitionService at runtime

## Testing Notes

No automated test suite currently in place. Manual testing workflow:
1. Build the app
2. Select PBX log folder (contains .log files with PBX traces)
3. Optionally select SIP folder
4. Enter a CallID from the logs
5. Verify results in all 4 tabs
6. Test export functionality (Textile format)

Key areas to verify:
- Call matching accuracy (should find all related entries within ±5 min)
- SQL record enrichment (Calls, Agents, etc. should populate)
- SIP message grouping and diagram generation
- Export output formatting
