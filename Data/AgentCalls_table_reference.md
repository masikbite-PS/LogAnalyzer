## Columns

| Name | Type | Null | Key | Default | Description |
|------|------|------|-----|---------|-------------|
| AgentID | varchar(64) | | | | The user number. Also takes into account the Lines.guidOwnerAgent value. |
| CallDirection | int | | | | The call direction. Possible values:<br/>1 — Outgoing<br/>2 — Incoming |
| CallRef | nvarchar(128) | | | | The unique identifier of the call within the PBX. The same as Calls.Id. |
| CallQueuesSeqNumber | int | | | | The queue sequence number within the call. Contains 0 if the call does not have related records in the CallsQueues table. |
| Supervisors | varchar(1000) | YES | | null | Comma-separated list of user numbers that should review the call. If not null, considered as Marked in History plugin. |
| CalledNumber | varchar(64) | | | | The called number. Corresponds to Calls.CalledNumber if user initiated, or CallsQueues.CalledNumber if user received. |
| CalledNumberInCNF | varchar(64) | | | '' | The called number in Complete Number Format. Corresponds to AgentCalls.CalledNumber. |
| CallingNumber | varchar(64) | | | | The calling number. Corresponds to Calls.CallingNumber. |
| DisconnectReason | int | | | | The PBX call disconnect reason code. Possible values:<br/>129 — transfer call<br/>16 — normal clearing<br/>17 — User busy<br/>Other ISDN codes. Corresponds to Calls.DisconnectReason. Contains 0 if forwarded. |
| DisconnectSource | int | | | | The call disconnect source. Possible values:<br/>0 — Unknown<br/>1 — Calling party<br/>2 — Called party<br/>3 — PBX<br/>Corresponds to Calls.DisconnectSource. Contains 1 if forwarded, 2 if transferred. |
| DurationSec | int | YES | | null | The speaking duration in seconds. Sum of speaking, hold, and wait-on-hold durations. |
| StartDateTime | datetime | | | | The call start time. Corresponds to Calls.StartTime if user initiated, or CallsQueues.StartTime if received. |
| WaveFile | varchar(255) | | | | The recording file name. Corresponds to last CallsQueues.RecordFile entry. |
| WaitTimeSec | int | YES | | null | The waiting in queue duration in seconds. Corresponds to Calls.WaitingDuration if initiated, or CallsQueues.WaitingDuration if received. |
| AcceptTimeSec | int | YES | | null | The ringing duration in seconds. Corresponds to Calls.AlertingDuration if initiated, or CallsQueues.AlertingDuration if received. |
| TrunkNumber | varchar(64) | YES | | null | The outgoing service number for outbound calls. Otherwise null. |
| HotlineNumber | varchar(64) | YES | | null | The hotline service number if call is to hotline. Otherwise null. |
| InitialDialedNumber | varchar(64) | YES | | null | The original called number if it differs from actual. Otherwise null. |
| ForwardedTo | varchar(64) | YES | | null | The number to which the call is transferred. Null if not transferred. |
| AcSource | int | YES | | null | The autocall initiator source identifier. Custom numbers start from 1000. Possible values:<br/>0 — Clarity SDK default<br/>1 — Predictive Dialer<br/>null — simple call. Corresponds to Calls.AcSource. |
| ACCallingNumber | varchar(64) | YES | | null | Corresponds to Calls.ACCallingNumber. |
| ACCalledNumber | varchar(64) | YES | | null | Corresponds to Calls.ACCalledNumber. |
| PartnerAgentID | varchar(64) | YES | | null | The user number of the partner. |
| Comment | varchar(1024) | YES | | null | Comment to call history item, edited from CC. |
| HistoryContactId | bigint | YES | FK | null | History contact identifier. |
| Cost | decimal(19,4) | YES | | null | Cost of call. Filled only for outbound calls using outbound cost calculation feature. |
| ConferenceId | uniqueidentifier | YES | | null | Id of related user conference. Null if not a conference call. |
| MediaType | int | | | 0 | Type of media. Possible values:<br/>0 — call<br/>1 — live chat<br/>2 — task |
