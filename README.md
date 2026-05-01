# PBX Log Analyzer

A Windows desktop tool for tracing a single call across PBX text logs and SIP traces. Given a Call-ID, it gathers the related PBX traces, the SQL records the call produced (Calls, CallsQueues, AgentCalls, PhysicalCalls, ...), the raw SIP dialog, and the IVR script traces — all in one window.

This branch (`main`) targets **PBX 2026.0**. For PBX 2024.2 use the `pbx-2024.2` branch.

## Download

Grab the latest `LogAnalyzer-v2026.0.X.exe` from the [Releases page](https://github.com/masikbite-PS/LogAnalyzer/releases). The executable is self-contained — no .NET runtime install required.

## Inputs

The window has three inputs:

| Field | Required | What goes in |
|---|---|---|
| **PBX Folder** | One of the two | Folder with PBX `.log` files |
| **SIP Folder** | One of the two | Folder with raw SIP traces (`.log`) |
| **CallID** | yes | The call identifier to search for. In PBX 2026.0 this is the same string for SQL `Calls.Id` and the SIP `Call-ID` header — outgoing calls store a GUID, incoming calls store the inbound `xxx@host` Call-ID |

At least one folder must be selected. Empty folders flash a red border on Analyze.

## Tabs

### Log Analysis
PBX traces matching the CallID, plus all SIP messages for the primary and partner Call-IDs, sorted by timestamp. SIP entries appear as `SIP <Sent|Received> <METHOD>` with the full raw body in the message column. Time window: 5 seconds before the first CallID match → 5 seconds after the last.

The left side shows extracted Call Info: start time, from/to numbers, user, call type, duration, channel, partner physical id, source files.

### SQL Data
SQL `INSERT` statements found in the logs, parsed and grouped by table. Pick the table from the dropdown.

- **Calls** — the primary record matching the entered CallID
- **PhysicalCalls** — both the call's own (`Calls.OwnPhysicalId`) and partner (`Calls.PartnerPhysicalId`) records
- **CallsQueues / AgentCalls / AgentLogins / AgentStates / AgentServices / UserBindingDevice** — every record referencing the CallID

Right-click a row to copy `Field = Value`, just `Field`, or just `Value`. Multi-select with Ctrl+Click. The selection is highlighted in soft yellow so you can still see text selection within a cell.

`Export to Textile` produces Redmine-friendly tables.

### SIP Messages
Grouped by SIP `Call-ID`. The primary group corresponds to the entered CallID; partner groups are resolved automatically via `Calls.PartnerPhysicalId` → `PhysicalCalls.StartTime` (UTC) plus the `Calls` UTC↔system delta — this works even when the call waited a long time in queue before pickup.

Each group shows the message list, the call-flow summary, and SDP details. `Export to Textile` exports the merged call flow.

### Scripts
IVR script traces (`Scripts::*` components) for the call's channel and any partner channels. Bounded to the same time window as Log Analysis. `Export scripts` saves the visible list as plain log lines.

## Typical workflow

1. Pick the PBX folder, optionally the SIP folder.
2. Paste the CallID, click **Analyze**.
3. Read **Call Info** on the left of the Log Analysis tab to confirm the right call was matched.
4. Drill in:
   - SQL Data → start with `Calls`, then `CallsQueues` for queue history, `PhysicalCalls` for both legs.
   - SIP Messages → check the call flow and SDP for media/codec issues.
   - Scripts → check IVR routing decisions.
5. Use the per-tab `Export to Textile` / `Export scripts` buttons to attach evidence to a Redmine ticket.

## Building from source

Requires .NET 8 SDK.

```
dotnet build
dotnet run
```

Release build:

```
dotnet publish LogAnalyzer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

Tagging `v2026.0.X` on `main` triggers a GitHub Actions release that publishes `LogAnalyzer-v2026.0.X.exe`.
