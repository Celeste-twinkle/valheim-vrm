# ValheimVRM 2.0.3 validation

This client-only release changes imported avatar template residency without changing synchronization packets, server state, configuration files or model formats. The existing 2.0.2 server addon remains compatible and does not require an update.

## Required behavior

- A synchronized player outside the local area of interest retains the selected imported template and cache hash while the authoritative server snapshot still lists that character.
- Removing the last player/model reference releases the template after its final rendered instance is destroyed.
- Without an authoritative snapshot, Valheim's connected-character list keeps temporarily unloaded online players resident and makes real disconnects collectible.
- Local model changes preserve the old template until the replacement attaches successfully; selecting the native model releases it after its visual is gone.
- Same-name generations and multiple players sharing one model retain independent resource ownership.
- Local world shutdown clears every imported template, cache hash and residency claim.

## Completed validation

- From a clean generated-source state, the 2.0.3 client, residency test, synchronization test and focused lifecycle test compiled with zero warnings and zero errors.
- The standalone avatar catalog and synchronization protocol suites passed model discovery/persistence, Unicode names, manual-refresh caching, all calibration bounds, request ordering, malformed input, reconnect, respawn, late join, opt-out and path/hash rejection.
- The real Valheim/Mono residency test passed shared-template ownership, same-name generations, authoritative online selection retention outside the local area of interest, last-reference release, pending attachment pinning, reimport and local-world-exit cleanup.
- The focused lifecycle test destroyed the player or visual at ten asynchronous attachment boundaries without exceptions or leaked pending leases.
- The complete synchronization engine test passed against the existing server addon, including old/new protocol negotiation, player disconnect/opt-out, same-model isolation, respawn, hash mismatch containment and unmodded peers.
- Only the client package is updated. No server package, model file or configuration migration is required.
