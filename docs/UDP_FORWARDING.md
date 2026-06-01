# UDP Forwarding

Stage 05 adds byte-preserving UDP forwarding on top of the raw Stage 04 listener.

Current behavior:

- The app listens on UDP port `20778` by default.
- Received datagrams are preserved as raw byte arrays.
- Each packet event includes a sequence number, remote endpoint, and receive timestamp.
- The forwarder accepts raw packet events and sends exact packet payload bytes to enabled destinations.
- The dashboard shows listener state, packet count, packet rate, no-packet warning, forwarding destination state, forwarded datagram count, and forwarded byte count.

The shell currently starts with zero forwarding destinations configured. Destination editing should be added later through settings, profiles, or the Telemetry / UDP Router page.

Forwarding rules:

- Forward the exact received byte payload.
- Allow one or more destination endpoints.
- Count forwarded packets and forwarding errors separately from receive errors.
- Keep forwarding independent of haptic output device state.
- Do not crash the listener if one forwarding destination fails.

Planned later work:

- Add UI controls for destination host, port, enabled state, and friendly name.
- Persist forwarding destinations in the profile or settings model.
- Show per-destination forwarded packet counts and errors.
- Warn when a configured destination points back to the listener port.
