## Columns

| Name | Type | Null | Key | Default | Description |
|------|------|------|-----|---------|-------------|
| SeqNumber | int | | | | Sequential numeric identifier of the call record. |
| Id | nvarchar(128) | | PK | | Unique identifier of the call inside PBX. |
| SessionId | nvarchar(128) | | | | Groups related calls belonging to the same session. During transfer, new call SessionId = original call Id. |
| StartTime | datetime | | | | Call start timestamp (UTC). |
| ServerStartDateTime | datetime | | | | Call start timestamp (server local time). |
| Duration | int | | | | Total call lifetime in PBX in milliseconds. |
| CallType | int | | | | Call direction. Possible values:<br/>1 — Inbound<br/>2 — Outbound<br/>3 — Internal<br/>0 — Other (Predictive dialer / PBX generated) |
| CallingNumber | varchar(64) | YES | | | Call initiator identity. Always callable back. |
| CalledNumber | varchar(64) | YES | | | Original target number. May differ from actual connected destination if forwarding occurred. |
| ChannelNumber | int | | | | Channel used to start the call. |
| IsTrunk | int | | | | Channel type. Possible values:<br/>1 — Trunk channel<br/>0 — Internal line |
| LineId | int | YES | | | Unique PBX line identifier. |
| ACSource | int | YES | | | Autocall source. Possible values:<br/>0 — Client or PBX default<br/>1 — Predictive dialer<br/>2 — Callback call |
| ACQueueNumber | varchar(64) | YES | | | Effective queue number for autocall. |
| ACCalledNumber | varchar(64) | YES | | | Autocall destination number. |
| ACCallingNumber | varchar(64) | YES | | | Caller ID displayed to destination (predictive dialer visible number). |
| ACScriptProcessingDuration | int | YES | | | Autocall script execution duration in milliseconds. |
| ACProceeedingDuration | int | YES | | | Time between leaving PBX and reaching destination in milliseconds. |
| ACRingingDuration | int | YES | | | Autocall ringing duration in milliseconds. |
| ScriptProcessingDuration | int | YES | | | Script execution duration unrelated to queues in milliseconds. |
| WaitingDuration | int | YES | | | Time waiting in queue in milliseconds. NULL = lost before queue. |
| AlertingDuration | int | YES | | | Time in alerting state in milliseconds. WaitingDuration!=NULL and AlertingDuration=NULL means lost in queue. |
| SpeakingDuration | int | YES | | | Time in speaking state in milliseconds. Not null = connected call. |
| HoldDuration | int | YES | | | Hold time on initiator side only in milliseconds. |
| TotalHoldDuration | int | YES | | | Total hold duration across all queues in milliseconds. |
| AWTDuration | int | YES | | | Agent After Work Time duration for this call in milliseconds. |
| TotalAWTDuration | int | YES | | | Total AWT across all queues in milliseconds. |
| UserLogin | varchar(64) | YES | | | Logged user on channel, or line owner if no user logged in, or null. |
| DTMFTones | varchar(64) | YES | | | DTMF tones entered by caller. |
| IsPrivateCall | int | | | 0 | Possible values:<br/>1 — Private call<br/>0 — Normal call |
| DisconnectSourceType | int | | | | Possible values:<br/>0 — Unknown<br/>1 — PBX<br/>2 — CTI client<br/>3 — SIP |
| DisconnectSource | int | | | | Who initiated disconnect. Possible values:<br/>0 — Unknown<br/>1 — Calling party<br/>2 — Called party<br/>3 — PBX |
| DisconnectReason | int | | | | Disconnect reason code (see T_DISCONNECT_REASONS). |
| ProcessingQueueNumber | varchar(64) | YES | | | First connected queue or last queue before disconnect. NULL when ProcessingResult = 0. |
| ProcessingQueueSLTime | int | YES | | | Service level time. Valid only when ProcessingResult = 4. |
| ProcessingDuration | int | YES | | | Time before connection in milliseconds. Valid only when ProcessingResult = 4. |
| ProcessingResult | int | | | | Queue processing result. Possible values:<br/>0 — Lost without queue<br/>1 — Lost before queue<br/>2 — Lost in queue<br/>3 — Lost in alert<br/>4 — Connected |
| TargetCallId | nvarchar(128) | YES | | | Call created after transfer. NULL if initiator unchanged. |
| ConsultationCallId | nvarchar(128) | YES | | | Consultation call ID. NULL for blind transfer. |
| ConsultativeForCallId | nvarchar(128) | YES | | | Main call ID during consultation. |
| ConsultativeForCallQueueSeqNumber | int | YES | | | Queue sequence reference of main call. |
| OwnPhysicalId | nvarchar(128) | | | | Calling party physical call identifier. |
| PartnerPhysicalId | nvarchar(128) | | | | Called party physical call identifier. |
| DialerCampaignId | int | YES | | | Predictive dialer campaign ID. |
| OrderId | int | YES | | | DB order identifier for async DB write. |
| CustomInfo1 | nvarchar(64) | YES | | | Custom info field 1 (set via CallCustomInfo1 script variable). |
| CustomInfo2 | nvarchar(64) | YES | | | Custom info field 2 (set via CallCustomInfo2 script variable). |
| CustomInfo3 | nvarchar(64) | YES | | | Custom info field 3 (set via CallCustomInfo3 script variable). |
| MediaType | int | | | 0 | Possible values:<br/>0 — Call<br/>1 — Live chat<br/>2 — Task |
| TraineeNumber | varchar(64) | YES | | | Training call participant number. |
| TrainingMode | int | | | 0 | Possible values:<br/>0 — None<br/>1 — Passive<br/>2 — Active<br/>3 — Conference |
| ContactId | nvarchar(64) | YES | | | Resolved contact identifier. |
| CampaignId | int | YES | | | PBX campaign identifier. |
| CallResultId | int | YES | | | Call result set by client application (e.g. Desktop Client disposition). |
| TrainingOptInAnswer | int | YES | | | Training opt-in answer. Possible values:<br/>1 — Yes<br/>2 — No<br/>3 — Timeout<br/>4 — Wrong DTMF |
