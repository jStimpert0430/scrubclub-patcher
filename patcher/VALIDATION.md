# Validation — 2026-09-12

- .NET 8 patcher regression suite: 25 passed, 0 failed.
- Windows x64 self-contained publish: successful, zero compiler warnings/errors.
- Linux x64 self-contained publish: successful, zero compiler warnings/errors.
- Actual Windows executable under Wine 11.17 on Linux: signed offline install, idempotent rerun, corrupted-payload rejection, invalid-signature rejection, wrong-platform rejection all passed. Rejected updates left installed files unchanged.
- Actual Linux executable: the same five smoke scenarios passed.
- Tests used disposable folders with game-name marker files. The patcher did not modify the developer's Steam installation or any server.
- Published Windows player archive downloaded from GitHub: checksum matched the local build; the extracted executable under Wine successfully used its built-in latest-release URL to download, verify and install all 37 files.
- Published Linux player archive checksum matched the local build. Its executable fetched and verified the public manifest, then correctly refused installation because the user's Valheim session was running. That game was left untouched; Linux installation itself was already covered by the signed offline smoke test above.

Still requires a real Windows PC playtest for Steam auto-detection, Windows publisher prompts and Valheim startup. Blue Depot 0.1.2 itself was previously tested interactively in a local Linux world; this patcher work does not substitute for multiplayer/disconnection testing before server rollout.
