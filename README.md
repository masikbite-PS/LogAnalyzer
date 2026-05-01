# PBX Log Analyzer — PBX 2024.2

A Windows desktop tool for tracing a single call across PBX 2024.2 logs. Given a CallID and/or a SIP Call-ID, it gathers the related PBX traces, the SQL records the call produced (Calls, CallsQueues, AgentCalls, PhysicalCalls, ...), the SIP dialog, and the IVR script traces — all in one window.

This branch (`pbx-2024.2`) targets **PBX 2024.2** specifically. For PBX 2026.0 use the `main` branch. Differences from `main`:
- Single log folder — SIP messages are interleaved with PBX traces in the same files (component `Sip::Engine::LogHook`).
- Two CallID inputs — analysis follows one of three isolated scenarios depending on which fields are filled.
- Versioning: `2024.2.X`.

## Download

Grab the latest `LogAnalyzer-v2024.2.X.exe` from the [Releases page](https://github.com/masikbite-PS/LogAnalyzer/releases). The executable is self-contained — no .NET runtime install required.

## Inputs

| Field | Required | What goes in |
|---|---|---|
| **Folder** | yes | Folder with PBX 2024.2 `.log` files (SIP traces are inside the same files) |
| **CallID (from Calls)** | one of the two | The PBX SQL `Calls.Id` value (typically a GUID like `659ED2D7-C285-...`) |
| **Call-ID (from SIP)** | one of the two | The SIP `Call-ID` header value (`xxx@host` form) |

At least one of the two CallID fields must be filled. Empty folder flashes a red border on Analyze.

## Three analysis scenarios

The combination of inputs selects an isolated code path:

### Scenario A: CallID-only
Only **CallID (from Calls)** filled. Everything is driven from the SQL `Calls` record:

- Channel set comes from `Calls.ChannelNumber` plus `CallsQueues.ChannelNumber`.
- Time bounds come from `Calls.ServerStartDateTime` and `Calls.Duration`.
- Partner channel comes from the `Statistic::Manager::FoundPartner` trace.
- SIP partner detection is not performed (no SIP Call-ID supplied).

### Scenario B: SIP-only
Only **Call-ID (from SIP)** filled. Analysis works backwards from the SIP dialog:

- Phone numbers extracted from the SIP INVITE → channel `id=NNN` resolved from PBX traces.
- StatCallRef chain auto-detected from `Call::Call::SetStatCallRef` traces, time-bounded to the SIP message range.
- Partner channel and partner SIP Call-ID resolved precisely via `Calls.PartnerPhysicalId` → `PhysicalCalls.StartTime` (UTC) plus the `Calls` UTC↔system delta — this works even when the call waited a long time in queue before pickup.

### Scenario C: Both
Both fields filled. Non-SIP tabs (Log Analysis, SQL Data, Scripts) follow the stable CallID-only path; SIP Messages uses the entered SIP Call-ID plus the partner SIP Call-ID derived from `PhysicalCalls`.

The split keeps each path isolated — debugging one scenario won't regress the others.

## Tabs

### Log Analysis
PBX traces matching the call, sorted by timestamp. The left side shows extracted Call Info: start time, from/to numbers, user, call type, duration, channel, partner physical id, source files. Time window: 5 seconds before INVITE → end of call + 5 seconds.

### SQL Data
SQL `INSERT` statements parsed and grouped by table. Pick the table from the dropdown.

- **Calls** — the primary record matching the CallID
- **PhysicalCalls** — both own (`Calls.OwnPhysicalId`) and partner (`Calls.PartnerPhysicalId`) records
- **CallsQueues / AgentCalls / AgentLogins / AgentStates / AgentServices / UserBindingDevice** — every record referencing the call

Right-click a row to copy `Field = Value`, just `Field`, or just `Value`. Multi-select with Ctrl+Click. The selection is highlighted in soft yellow so you can still see text selection within a cell.

`Export to Textile` produces Redmine-friendly tables.

### SIP Messages
Grouped by SIP `Call-ID`. The primary group corresponds to the entered SIP Call-ID; partner groups are resolved automatically (see Scenario B / C). Each group shows the message list, the call-flow summary, and SDP details. `Export to Textile` exports the merged call flow.

### Scripts
IVR script traces (`Scripts::*` components) for the call's channel and any partner channels. Bounded to the same time window as Log Analysis. `Export scripts` saves the visible list as plain log lines.

## Typical workflow

1. Pick the log folder.
2. Paste the CallID and/or SIP Call-ID, click **Analyze**.
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

Tagging `v2024.2.X` on `pbx-2024.2` triggers a GitHub Actions release that publishes `LogAnalyzer-v2024.2.X.exe`.
