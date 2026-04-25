# AgentServices

This table contains information about the services that were assigned and unassigned to the user during the login session.

## Columns

| Name | Type | Null | Key | Default | Description |
|------|------|------|-----|---------|-------------|
| SessionId | uniqueidentifier |  | PK |  | The unique identifier of the login session. |
| SeqNumber | int |  | PK |  | The sequence number of the event within the current login session. |
| EventTime | datetime |  |  |  | The time when the event occurred. |
| EventType | int |  |  |  | The event type. Possible values:<br/>0 — SignIn<br/>1 — SignOut |
| ServiceNumber | varchar(64) |  |  |  | The number of the service the agent is signed in/out. |

## Description

Fixed services are always assigned on login. Hotline assigning depends on the type of login and the tool that is used to log in a user.
Every SignIn event must have a corresponding SignOut event within the single logon session.

## See also

AgentStates, UserBindingDevice, AgentCalls, AgentLogins
