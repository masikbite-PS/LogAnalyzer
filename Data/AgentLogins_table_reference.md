# AgentLogins

This table contains information about agent login sessions.

## Columns

| Name | Type | Null | Key | Default | Description |
|------|------|------|-----|---------|-------------|
| SessionId | uniqueidentifier |  | PK |  | The unique identifier of the login session. |
| UserLogin | varchar(64) |  |  |  | The user login (agent number). |
| LoginTime | datetime |  |  |  | The time when the login state started. |
| ServerStartDateTime | datetime |  |  |  | The time when the call started to be related to the specified queue. In local time of server. |
| ConnectionId | varchar(64) |  |  |  | A connection string of the program that initiated the login. |

## See also

AgentStates, AgentServices, AgentCalls, UserBindingDevice
