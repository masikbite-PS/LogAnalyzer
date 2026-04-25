# AgentStates

Users can be in various states during the working process. The information about these states is written to this table.

## Columns

| Name | Type | Null | Key | Default | Description |
|------|------|------|-----|---------|-------------|
| SessionId | uniqueidentifier |  | PK |  | The unique identifier of the login session. References AgentLogins.SessionId. |
| SeqNumber | int |  | PK |  | The sequence number of the event within the current login session. |
| StartTime | datetime |  |  |  | The time when the state started. |
| ServerStartDateTime | datetime |  |  |  | The time when the call started to be related to the specified queue. In local time of server. |
| StateType | int |  |  |  | The state type. Possible values:<br/>1 — Ready<br/>2 — Busy<br/>3 — AWT<br/>4 — Break<br/>5 — LoggedOff |
| ActionReason | int | YES |  | null | Contains the break reason number for the break state or the logout reason for the logout action. Possible values:<br/>0 — The manual logout by the user<br/>1 — The ringing timeout exceeded<br/>2 — Another user is logged in on the same channel or the same user is logged in from the other client<br/>3 — CTI connection is lost<br/>4 — The PBX server is shut down<br/>5 — Another user is logged in on the same channel via the 9#00 command<br/>6 — Unsuccessful binding on login<br/>7 — The group logout is activated according to the schedule<br/>8 — The AWT timeout fired<br/>9 — The user was removed |

## Description

The state duration is not written and can be calculated as the difference between the current and the next state transitions. The entry is written on the state started.

## See also

AgentServices, UserBindingDevice, AgentCalls, AgentLogins
