# 1.8.3 request ordering validation

Windows x64 / D3D11, Valheim 1.0.12 (Steam build 25253764), Unity 6000.0.75f1,
BepInEx 5.4.23.3. The normal game and ignored isolated copy have matching
`assembly_valheim.dll` SHA-256:
`27a766a8d23a7bd8b6a54fb9ad0452a96c305fb3629b39c40527c09a1c393a84`.

The production server runs with real ZRpc serialization and controlled player
ZDOs. The probe sends requests 2 then 3 and reverses the queued network packets.
Every receiver keeps request 3. It also reverses a 30-request burst and verifies
the greatest request sequence wins. Duplicate, stale and late legacy-format
packets do not change the server's state or revision. An invalid packet carrying
`long.MaxValue`, a truncated extension and a zero sequence do not advance its
watermark or block a subsequent valid request.

Each connection has an independent watermark. Another player's smaller sequence
is accepted normally. Death/respawn keeps the watermark; opt-out is ordered, so
late selections cannot undo it and a late opt-out cannot undo a newer selection.
A new RPC connection for the same peer starts at one, while callbacks from the
old RPC are ignored. The listen-host bridge uses the same ordering rule.

The production client is separately exercised over ZRpc. Legacy server discovery
emits the exact unextended packet. Capability discovery upgrades it to positive
64-bit sequences 1, 2, 3, 4. Repeated discovery, refreshing and opt-out do not reset
the counter; delayed legacy discovery cannot downgrade it. Replacing the server
RPC resets its sequence and rejects old-RPC discovery. Overflow fails closed
without transmitting a wrapped counter. No-server local mode sends no requests.
Equal-revision and older snapshots cannot replace a newer accepted snapshot.

The full engine suite retains mixed legacy/new clients, an unmodded client with
60 ordinary RPC exchanges, three unanswered discovery rounds (one capability
and one legacy message per round), zero unmodded avatar snapshots, independent
avatar instances, respawn, late join, opt-out, missing/different file fallback and
recovery without a model settings TXT. The pure protocol/order tests also pass.

Upstream ordering requires the sending client and server addon to be 1.8.3 or
newer. Protocol-1 snapshots remain compatible with 1.8.0–1.8.2. Legacy upstream
requests retain arrival-order behavior; compatibility does not add sequence
protection to software that does not send or validate sequence numbers.

These are controlled engine tests with in-memory sockets and player fixtures.
Real Steam/PlayFab dedicated-server login, WAN play, Linux and complete world
playthroughs have not been validated. Test plugins belong only in the ignored
isolated game installation and are never distributed.
