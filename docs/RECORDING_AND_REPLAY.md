# Recording and Replay

Stage 09 implements raw UDP telemetry recording and deterministic replay.

## Scope

- Record raw incoming UDP payload bytes exactly as received.
- Record packet order, original sequence number, and relative receive timing.
- Replay packets as `UdpTelemetryPacket` values without opening UDP sockets.
- Keep recording independent of F1 25 parser success so unknown or malformed packets can still be captured.
- Keep replay reusable by the existing `F125PacketParser` and `F125VehicleStateAdapter` path.

Stage 17 feeds replayed packets through the same parser, `VehicleState`, effect, mixer, safety, and output-owned render path as live UDP packets. Replay still does not implement a polished recordings library UI, file picker, profile snapshots inside recordings, real WASAPI output, or physical hardware validation.

## File Format

Recording files use a little-endian binary `.hdrec` format.

Header:

| Field | Type | Notes |
| --- | --- | --- |
| Magic | 8 ASCII bytes | `HDREC001`. |
| Version | `int32` | Current version is `1`. |
| Created UTC ticks | `int64` | `DateTimeOffset` UTC ticks. |
| Source game | UTF-8 string | Length-prefixed with `int32`; default `F1 25`. |
| Source profile | UTF-8 string | Length-prefixed with `int32`; default `Default`. |
| App version | UTF-8 string | Length-prefixed with `int32`. |
| Packet count | `int64` | Finalized on stop. |

Packet record, repeated `packet count` times:

| Field | Type | Notes |
| --- | --- | --- |
| Sequence number | `int64` | Original `UdpTelemetryPacket.SequenceNumber`. |
| Relative ticks | `int64` | Non-negative `TimeSpan` ticks from recording start. |
| Payload length | `int32` | Must be non-negative and at most 65,535 bytes. |
| Payload | bytes | Raw UDP payload, copied byte-for-byte. |

The reader rejects invalid magic, unsupported versions, negative counts, negative relative timestamps, unreasonable payload lengths, truncated records, invalid string lengths, and trailing bytes.

## Runtime Path

Live capture path:

```text
UdpTelemetryReceiver
-> TelemetryRecordingService.RecordPacket
-> background recording writer queue
-> .hdrec file
```

The recorder is fed before parser validation, so parser failures never prevent raw packet capture.

Replay path:

```text
.hdrec file
-> TelemetryReplayService
-> UdpTelemetryPacket event
-> F125PacketParser.Parse(packet.Payload)
-> F125VehicleStateAdapter.Apply(parseResult)
-> existing haptic effects
-> mixer
-> safety processor
-> NullAudioOutputDevice
```

Fast replay emits packets immediately for deterministic automated tests. Time-preserving replay can delay between packets according to recorded relative timing and an optional speed multiplier.

Stage 15 exposes replay active/inactive state, source file path, packets replayed, and status message through the runtime pipeline snapshot. Replay packet ordering and raw byte preservation remain unchanged.
