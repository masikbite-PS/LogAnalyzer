# UserBindingDevice

Device binding records for agent login sessions.

## Columns

| Name | Type | Null | Key | Default | Description |
|------|------|------|-----|---------|-------------|
| SessionId | uniqueidentifier |  | PK |  | The unique identifier of the login session. |
| SeqNumber | int |  |  |  | The sequence number of the event within the current login session. |
| ActionTime | datetime |  |  |  | The time when the bind/unbind action occurred. |
| ActionType | int |  |  |  | The type of binding action. Possible values:<br/>0 — Unbind the device<br/>1 — Bind internal phone<br/>2 — Bind device using DialIn<br/>3 — Bind external phone<br/>4 — Bind Microsoft Teams |
| ChannelNumber | int | YES |  | null | The channel number assigned to the agent if the agent is bind channel (logged in as "external phone" or "BISP", null otherwise). |
| RemotePhone | varchar(64) | YES |  | null | The remote phone number if the agent is bind phone not from this PBX (logged in as out of office, null if as dial-in, "external phone", or "BISP"). |
| LineId | int | YES |  | null | The line identifier. |

## Description

There are the following types of ActionType:
- 0 — Unbind the device
- 1 — Bind internal phone
- 2 — Bind device using DialIn
- 3 — Bind external phone
- 4 — Bind Microsoft Teams

## See also

AgentStates, AgentServices, AgentCalls, AgentLogins
