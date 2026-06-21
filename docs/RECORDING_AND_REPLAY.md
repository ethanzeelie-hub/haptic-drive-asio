# Recording and Replay

Stage 26I hardens raw UDP telemetry recording and replay with the resilient `.hdrec` v2 file format, CRC-checked recovery, richer metadata, and absolute-deadline replay timing.

## Scope

- Record raw incoming UDP payload bytes exactly as received.
- Record packet order, original sequence number, and original receive timestamps.
- Replay packets as `UdpTelemetryPacket` values without opening UDP sockets.
- Keep recording independent of F1 25 parser success so unknown or malformed packets can still be captured.
- Keep replay reusable by the existing `F125PacketParser` and `F125VehicleStateAdapter` path.
- Mark incomplete captures when packets are dropped or a recording is not cleanly finalized.
- Recover valid packets from truncated or partially written v2 files without reconstructing packet bytes.

Stage 17 feeds replayed packets through the same parser, `VehicleState`, effect, mixer, safety, and output-owned render path as live UDP packets. Stage 18 adds a recordings library UI that lists local `.hdrec` files, reads metadata summaries without loading full payloads, and can replay the selected recording. Stage 18p-B makes normal UI replay time-preserving by default and keeps fast replay as an explicit debug/parser mode.

Replay still does not implement recording trimming, route snapshots, profile snapshots inside recordings, real WASAPI output, or physical hardware validation.

## File Format

New recording files use the v2 little-endian binary `.hdrec` format. The v1 reader remains supported for older captures.

Header:

| Field | Type | Notes |
| --- | --- | --- |
| Magic | 8 ASCII bytes | `HDRVREC2`. |
| Header length | `uint32` | Little-endian UTF-8 JSON byte length. |
| Header JSON | bytes | Includes schema version, app version, game integration metadata, selected profile metadata, source/bind metadata, packet count, dropped-packet count, and completion flag. |

Packet record, repeated until footer or first invalid/truncated record:

| Field | Type | Notes |
| --- | --- | --- |
| Record magic | 4 ASCII bytes | `PKT2`. |
| Sequence number | `int64` | Original `UdpTelemetryPacket.SequenceNumber`. |
| Received-at Unix nanoseconds | `int64` | Original packet receive timestamp in UTC. |
| Payload length | `int32` | Must be non-negative and at most 65,535 bytes. |
| Payload CRC32 | `uint32` | CRC of the raw UDP payload bytes only. |
| Payload | bytes | Raw UDP payload, copied byte-for-byte. |

Footer:

| Field | Type | Notes |
| --- | --- | --- |
| Footer magic | 4 ASCII bytes | `END2`. |
| Packet count | `int64` | Number of packet records written before stop. |
| Ended-at Unix nanoseconds | `int64` | Clean-stop completion timestamp. |
| Recording CRC32 | `uint32` | CRC of the concatenated packet records, used to validate the completed recording. |

The reader:

- rejects invalid magic and unsupported versions,
- validates every packet payload length and CRC,
- stops recovery before the first invalid or truncated packet,
- marks the loaded metadata incomplete when the footer is missing or invalid,
- preserves recovered raw packet bytes exactly,
- keeps v1 compatibility for older `HDREC001` files.

## Runtime Path

Live capture path:

```text
UdpTelemetryReceiver
-> TelemetryRecordingService.RecordPacket
-> bounded background recording writer queue
-> .hdrec file
```

The recorder is fed before parser validation, so parser failures never prevent raw packet capture.

Live recording keeps the telemetry path non-blocking. If the bounded recording queue is full, the service drops the newest packet, increments dropped-packet diagnostics, marks the recording incomplete, and reports the overload through recording status rather than stalling telemetry receive or the haptics path.

Replay path:

```text
.hdrec file
-> TelemetryRecordingFile open reader
-> TelemetryReplayService
-> UdpTelemetryPacket event
-> F125PacketParser.Parse(packet.Payload)
-> F125VehicleStateAdapter.Apply(parseResult)
-> existing haptic effects
-> mixer
-> safety processor
-> NullAudioOutputDevice
```

Time-preserving replay now schedules by absolute deadline from the first recorded packet timestamp and an optional speed multiplier. That keeps scheduler overhead from accumulating into later packets. Replayed packets are re-emitted with fresh receive timestamps so downstream freshness logic sees them as live replay input rather than stale historical wall-clock values. The Telemetry / UDP UI uses time-preserving replay by default for Replay Latest and Replay Selected. Fast replay emits packets immediately for deterministic automated tests and explicit parser/debug work; it is not suitable for physical haptic feel or latency testing. Replay from file now streams packets from the `.hdrec` reader directly instead of fully loading the entire recording before playback starts.

The runtime snapshot now also exposes total replay drift, max late packet, and skipped-sleep count. Replay packet ordering and raw byte preservation remain unchanged.

## Stage 18 Library UI

The Recordings page can:

- Start and stop raw UDP recording.
- Persist selected game/profile hash metadata in new v2 captures.
- Replay the latest local recording in Real-time mode by default.
- Refresh the local recordings library.
- Show file name, packet count, file size, source game, source profile, completion state, dropped-packet count, app version, created time, and modified time for readable `.hdrec` files.
- Replay the selected recording through the same output-owned haptic pipeline in Real-time mode by default.
- Explicitly switch replay mode to Fast debug for parser diagnostics.
- Rename the selected recording with guarded filename sanitization, preserved `.hdrec` extension, no overwrite, and directory-bound checks.
- Delete the selected recording after guarding that the selected path is an `.hdrec` file inside the recordings folder and is not the active recording output.

The library reads from:

```text
%LOCALAPPDATA%\HapticDrive.Asio\Recordings
```
