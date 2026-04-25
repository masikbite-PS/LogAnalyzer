# AgentCalls

This table contains information about the users' calls.

## Columns

| Name | Type | Null | Key | Default | Description |
|------|------|------|-----|---------|-------------|
| SeqNumber | int |  | PK |  | The unique identifier of the user's call. |
| AgentID | varchar(64) |  |  |  | The user number. It also takes into account the Lines.guidOwnerAgent value. |
| CallDirection | int |  |  |  | The call direction. Possible values: 1 — Outgoing; 2 — Incoming. |
| CallRef | nvarchar(128) |  |  |  | The unique identifier of the call within the PBX. Same as Calls.Id. |
| CallQueuesSeqNumber | int |  |  |  | Queue sequence number within the call. 0 if no related records in CallsQueues. |
| Supervisors | varchar(1000) | YES |  | null | Comma-separated list of user numbers who should review the call. If not null → marked in History plugin. |
| CalledNumber | varchar(64) |  |  |  | Called number. Matches Calls.CalledNumber (outgoing) or CallsQueues.CalledNumber (incoming). |
| CalledNumberInCNF | varchar(64) |  |  | '' | Called number in Complete Number Format. Corresponds to CalledNumber. |
| CallingNumber | varchar(64) |  |  |  | Calling number. Corresponds to Calls.CallingNumber. |
| DisconnectReason | int |  |  |  | PBX disconnect reason. Values: 129 — transfer; 16 — normal clearing; 17 — busy; others — ISDN codes. 0 if forwarded. |
| DisconnectSource | int |  |  |  | Disconnect source: 0 — Unknown; 1 — Calling party; 2 — Called party; 3 — PBX. 1 if forwarded, 2 if transferred. |
| DurationSec | int | YES |  | null | Total duration (talk + hold + hold-wait). |
| StartDateTime | datetime |  |  |  | Call start time. From Calls or CallsQueues depending on direction. |
| WaveFile | varchar(255) |  |  |  | Recording filename. Depends on last CallsQueues entry or direct queue record. |
| WaitTimeSec | int | YES |  | null | Queue waiting duration. From Calls or CallsQueues. |
| AcceptTimeSec | int | YES |  | null | Ringing duration. From Calls or CallsQueues. |
| TrunkNumber | varchar(64) | YES |  | null | Outgoing service number (for outbound calls). |
| HotlineNumber | varchar(64) | YES |  | null | Hotline service number if applicable. |
| InitialDialedNumber | varchar(64) | YES |  | null | Original called number if different. |
| ForwardedTo | varchar(64) | YES |  | null | Target number if call was transferred. |
| AcSource | int | YES |  | null | Autocall source: 0 — SDK; 1 — Predictive Dialer; ≥1000 — custom. Null for simple calls. |
| ACCallingNumber | varchar(64) | YES |  | null | Auto-calling number (Calls.ACCallingNumber). |
| ACCalledNumber | varchar(64) | YES |  | null | Auto-called number (Calls.ACCalledNumber). |
| PartnerAgentID | varchar(64) | YES |  | null | Partner user number. Depends on call direction and related tables. |
| Comment | varchar(1024) | YES |  | null | Comment added from CC. |
| HistoryContactId | bigint | YES | FK | null | History contact identifier. |
| Cost | decimal(19,4) | YES |  | null | Call cost (outbound only). |
| ConferenceId | uniqueidentifier | YES |  | null | Related conference ID (UserConferences.Id). Null if not a conference call. |
| MediaType | int |  |  | 0 | Media type: 0 — call; 1 — live chat; 2 — task. |