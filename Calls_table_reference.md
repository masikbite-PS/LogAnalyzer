# Calls Table — Column Reference (Clarity PBX)

## Primary Identifiers

### SeqNumber

Type: int identity(1,1)
Nullable: No
Description:
Sequential numeric identifier of the call record.

### Id

Type: nvarchar(128)
Nullable: No
Key: Primary Key
Description:
Unique identifier of the call inside PBX.

### SessionId

Type: nvarchar(128)
Nullable: No
Description:
Groups related calls belonging to the same session.

Rules:

Calls created during transfer may share SessionId
If transfer changes call initiator:
new call SessionId = original call Id
new call receives a new Id
Parallel consultation calls share SessionId with main call
## Time Fields

### StartTime

Type: datetime
Nullable: No
Description:
Call start timestamp (UTC).

### ServerStartDateTime

Type: datetime
Nullable: No
Description:
Call start timestamp (server local time).

### Duration

Type: int
Nullable: No
Description:
Total call lifetime inside PBX in milliseconds.

Formula:

Duration =
ACScriptProcessingDuration
+ ACProceeedingDuration
+ ACRingingDuration
+ ScriptProcessingDuration
+ WaitingDuration
+ AlertingDuration
+ SpeakingDuration
+ WaitingOnHoldDuration
+ HoldDuration

Special rules:

Transfer with initiator change splits duration between records
Each segment only stores its own lifetime portion

## Call Direction

### CallType

Type: int
Nullable: No

Values:

1 = Inbound
2 = Outbound
3 = Internal
0 = Other (Predictive dialer / PBX generated)

## Call Participants

### CallingNumber

Type: varchar(64)
Nullable: Yes

Description:

Call initiator identity.

Possible formats:

international number
PBX trunk + destination format (during transfers)

Always callable back.

### CalledNumber

Type: varchar(64)
Nullable: Yes

Description:

Original target number.

Important:

May differ from actual connected destination if forwarding occurred.

After transfer:

Stores transfer target.

## Channel Information

### ChannelNumber

Type: int
Nullable: No

Description:

Channel used to start the call.

### IsTrunk

Type: int
Nullable: No

Values:

1 = trunk channel
0 = internal line

### LineId

Type: int
Nullable: Yes

Description:

Unique PBX line identifier.

## Autocall Fields

Used only for predictive dialer / callback scenarios.

### ACSource

Type: int
Nullable: Yes

Values:

0 = Client or PBX default
1 = Predictive dialer
2 = Callback call
>=1000 = custom sources

### ACQueueNumber

Type: varchar(64)
Nullable: Yes

Description:

Effective queue number for autocall.

For trunk calls:

Stores outgoing service number only.

### ACCalledNumber

Type: varchar(64)
Nullable: Yes

Description:

Autocall destination number.

Outgoing service prefix removed.

### ACCallingNumber

Type: varchar(64)
Nullable: Yes

Description:

Caller ID displayed to destination.

Example:

Predictive dialer visible number.

## Script Processing Durations

### ACScriptProcessingDuration

Type: int
Nullable: Yes

Description:

Autocall script execution duration in milliseconds.

Affected by:

UserGotPartner.scr

### ACProceeedingDuration

Type: int
Nullable: Yes

Description:

Time between leaving PBX and reaching destination in milliseconds.

### ACRingingDuration

Type: int
Nullable: Yes

Description:

Autocall ringing duration in milliseconds.

## Queue Processing Durations
all values in milliseconds
### ScriptProcessingDuration

Type: int
Nullable: Yes

Formula:

Calls.ScriptProcessingDuration =
Σ CallsQueues.ScriptProcessingDuration(i)
+ PureScriptProcessingDuration

PureScriptProcessingDuration:

Script processing unrelated to queues.

Example:

non-existing queue routing.

### WaitingDuration

Type: int
Nullable: Yes

Formula:

Calls.WaitingDuration =
Σ CallsQueues.WaitingDuration(i)

Special logic:

NULL = lost before queue

Meaning:

Call never entered queue.

### AlertingDuration

Type: int
Nullable: Yes

Formula:

Calls.AlertingDuration =
Σ CallsQueues.AlertingDuration(i)

Special logic:

WaitingDuration != NULL
AND AlertingDuration = NULL
→ lost in queue

### SpeakingDuration

Type: int
Nullable: Yes

Formula:

Calls.SpeakingDuration =
Σ CallsQueues.SpeakingDuration(i)

Interpretation:

AlertingDuration != NULL
AND SpeakingDuration = NULL
→ lost in alert
SpeakingDuration != NULL
→ connected call

## Hold Durations
in milliseconds

### HoldDuration

Type: int
Nullable: Yes

Description:

Hold time on initiator side only.

### TotalHoldDuration

Type: int
Nullable: Yes

Formula:

TotalHoldDuration =
Calls.HoldDuration
+ Σ CallsQueues.HoldDuration(i)

## After Work Time (AWT)
in milliseconds

### AWTDuration

Type: int
Nullable: Yes

Description:

Agent AWT duration for this call.

NULL if:

AWT not started
consultation call during attended transfer

### TotalAWTDuration

Type: int
Nullable: Yes

Formula:

TotalAWTDuration =
Calls.AWTDuration
+ Σ CallsQueues.AWTDuration(i)

## Queue Processing Result

### ProcessingQueueNumber

Type: varchar(64)
Nullable: Yes

Rules:

If connected:

first connected queue

If not connected:

last queue before disconnect

NULL when:

ProcessingResult = 0

### ProcessingQueueSLTime

Type: int
Nullable: Yes

Used only when:

ProcessingResult = 4

### ProcessingDuration

Type: int
Nullable: Yes

Meaning:

Time before connection.

Valid only when:

ProcessingResult = 4

### ProcessingResult

Type: int
Nullable: No

Values:

0 = lost without queue
1 = lost before queue
2 = lost in queue
3 = lost in alert
4 = connected

## Agent Information

### UserLogin

Type: varchar(64)
Nullable: Yes

Resolution order:

logged user on channel
→ line owner
→ NULL

## Disconnect Information

### DisconnectSourceType

Type: int

Values:

0 = Unknown
1 = PBX
2 = CTI client
3 = SIP

### DisconnectSource

Type: int

Values:

0 = Unknown
1 = Calling party
2 = Called party
3 = PBX

### DisconnectReason

Type: int

Reference:

T_DISCONNECT_REASONS

## Transfer Relations

### TargetCallId

Type: nvarchar(128)
Nullable: Yes

Meaning:

Call created after transfer.

NULL if initiator unchanged.

### ConsultationCallId

Type: nvarchar(128)
Nullable: Yes

Meaning:

Consultation call ID.

NULL for blind transfer.

### ConsultativeForCallId

Type: nvarchar(128)
Nullable: Yes

Meaning:

Main call ID during consultation.

### ConsultativeForCallQueueSeqNumber

Type: int
Nullable: Yes

Meaning:

Queue sequence reference of main call.

## Physical Call Identifiers

### OwnPhysicalId

Type: nvarchar(128)

Calling party physical call identifier.

### PartnerPhysicalId

Type: nvarchar(128)

Called party physical call identifier.

## Campaign / Dialer Fields

### DialerCampaignId

Type: int
Nullable: Yes

Predictive dialer campaign ID.

### CampaignId

Type: int
Nullable: Yes

PBX campaign identifier.

### CallResultId

Type: int
Nullable: Yes

Set by client application.

Example:

Desktop Client disposition.

## Custom Fields

### CustomInfo1
### CustomInfo2
### CustomInfo3

Type: nvarchar(64)
Nullable: Yes

Filled via:

CallCustomInfoX script variables

Outbound calls:

Auto-filled from user or line.

## Media Type

### MediaType

Type: int
Default: 0

Values:

0 = Call
1 = Live chat
2 = Task
## Training Fields

### TraineeNumber

Type: varchar(64)
Nullable: Yes

Training call participant.

### TraingMode

Type: int
Default: 0

Values:

0 = None
1 = Passive
2 = Active
3 = Conference
## Contact Resolution

### ContactId

Type: nvarchar(64)
Nullable: Yes

Resolved contact identifier.

## Training Opt-In Result

### TrainingOptInAnswer

Type: int
Nullable: Yes

Values:

1 = Yes
2 = No
3 = Timeout
4 = Wrong DTMF

NULL if menu not played.