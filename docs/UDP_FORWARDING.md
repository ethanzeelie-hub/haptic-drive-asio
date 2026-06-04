# UDP Forwarding

Stage 05 added byte-preserving UDP forwarding on top of the raw Stage 04 listener. Stage 18 adds app-level destination editing and persistence.

Current behavior:

- The app listens on UDP port `20778` by default.
- Received datagrams are preserved as raw byte arrays.
- Each packet event includes a sequence number, remote endpoint, and receive timestamp.
- The forwarder accepts raw packet events and sends exact packet payload bytes to enabled destinations.
- The dashboard shows listener state, packet count, packet rate, no-packet warning, forwarding destination state, forwarded datagram count, and forwarded byte count.
- The Telemetry / UDP Router page can add, edit, remove, enable, disable, and persist forwarding destinations.
- Destinations support IP addresses, `localhost`, DNS hostnames, ports, enabled state, and friendly names.
- Obvious enabled loopback to the local listener port `20778` is blocked in the UI to avoid forwarding loops.

The shell still starts safely when no destinations are configured. Destination settings are app preferences, not haptic profiles.

Forwarding rules:

- Forward the exact received byte payload.
- Allow one or more destination endpoints.
- Count forwarded packets and forwarding errors separately from receive errors.
- Keep forwarding independent of haptic output device state.
- Keep forwarding independent of F1 25 header parser success.
- Do not crash the listener if one forwarding destination fails.

Planned later work:

- Add per-destination forwarded packet and error counters if the router grows beyond the current global diagnostics.
- Add import/export for router presets if external telemetry tool workflows need it.
