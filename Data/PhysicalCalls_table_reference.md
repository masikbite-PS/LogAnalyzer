# PhysicalCalls

The physical call represents the SIP call that is used to serve zero, one or more calls. Each record in this table represents one physical call with its initial data and voice quality information.

## Columns

| Name | Type | Null | Key | Default | Description |
|------|------|------|-----|---------|-------------|
| Id | nvarchar(128) |  | PK |  | The physical call identifier. |
| StartTime | datetime |  |  |  | The physical call start time. |
| Duration | int |  |  |  | The physical call duration. |
| CallingNumber | varchar(64) | YES |  | null | The initial calling number. |
| CalledNumber | varchar(64) | YES |  | null | The initial called number. |
| ChannelNumber | int |  |  |  | The number of the channel that is assigned to this physical call. |
| LineId | int |  |  | 0 | The identifier of line. |
| LocalPort | int | YES |  | null | The UDP port number used for RTP. |
| InitialCallId | nvarchar(128) | YES |  | null | The identifier of the call (from the table Calls) which initiated the outgoing physical call. If null, the physical call is incoming. |
| ReceivedPackets | int | YES |  | null | The number of received RTP packets. |
| MaxJitter | int | YES |  | null | The maximal jitter in milliseconds of incoming RTP packets. |
| MeanJitter | int | YES |  | null | The mean jitter in milliseconds of incoming RTP packets. |
| MaxDelta | int | YES |  | null | The maximal delta in milliseconds of incoming RTP packets. |
| LostPackets | int | YES |  | null | The number of incoming lost RTP packets. |
| ReorderedPackets | int | YES |  | null | The number of incoming reordered RTP packets (a packet is reordered if it is received after the next packet arrival). |
| SentPackets | int | YES |  | null | The number of sent RTP packets. |
| RRMaxJitter | int | YES |  | null | The maximal jitter in milliseconds of outgoing RTP packets (from the received RTCP). |
| RRMeanJitter | int | YES |  | null | The mean jitter in milliseconds of outgoing RTP packets (from the received RTCP). |
| RRLostPackets | int | YES |  | null | The number of lost outgoing RTP packets (from the received RTCP). |
| SipDisconnectReason | int | YES |  | null | SIP disconnect reason. Can be taken from response code to INVITE request or from "Reason" header in CANCEL or BYE requests. |

## See also

Calls, T_DISCONNECT_SIP_REASONS
