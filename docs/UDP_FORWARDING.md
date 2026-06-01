# UDP Forwarding

Stage 04 adds the raw UDP listener that forwarding will build on.

Current behavior:

- The app listens on UDP port `20778` by default.
- Received datagrams are preserved as raw byte arrays.
- Each packet event includes a sequence number, remote endpoint, and receive timestamp.
- The dashboard shows listener state, packet count, packet rate, and no-packet warning.

Forwarding is not implemented yet. Stage 05 should add byte-preserving relay destinations that do not depend on parser success.

Planned forwarding rules:

- Forward the exact received byte payload.
- Allow one or more destination endpoints.
- Count forwarded packets and forwarding errors separately from receive errors.
- Keep forwarding independent of haptic output device state.
- Do not crash the listener if one forwarding destination fails.
