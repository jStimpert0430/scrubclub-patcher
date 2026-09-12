# Validation — 2026-09-12

## Launcher 0.2.0

- Patcher/launcher regression suite: 39 passed. Covers the updater plus character enumeration, active save-source distinction, safe argument handling, launch prerequisites, and adding new mod DLLs/assets/configuration/patchers in a later release.
- Blue Depot/game contract suite: 39 passed, including the character helper's hook methods and field types against the locally installed game.
- Linux self-contained ImGui window: rendered and closed cleanly.
- Windows self-contained ImGui window under Wine 11.17: rendered and closed cleanly. Native GLFW/cimgui libraries must be distributed beside the executable; the packaging includes them.
- Actual Windows ImGui buttons: Update installed the current signed public release into a disposable folder; Launch game started a harmless recording stub with the expected server address. No real game or server was changed by this test.
- Published 0.2.0 Windows player archive: downloaded from GitHub, checksum matched, extracted with its native libraries and font, then tested under Wine. Update installed all 43 signed payload files including the character helper. Selecting a disposable character name in the dropdown and clicking Launch game passed its full name, Local source and the configured server to the recording stub. The fixture was removed afterward.
- Signed 0.2.0 CLI regression smoke: installation, idempotent rerun, corrupt payload, invalid signature and wrong-platform rejection passed under Wine.
- A real Windows Steam playtest and an in-game local/Cloud character handoff remain outstanding. Game-contract checks do not substitute for a playtest.

## Previous console patcher 0.1.2

- .NET 8 patcher regression suite: 25 passed, 0 failed.
- Windows x64 self-contained publish: successful, zero compiler warnings/errors.
- Linux x64 self-contained publish: successful, zero compiler warnings/errors.
- Actual Windows executable under Wine 11.17 on Linux: signed offline install, idempotent rerun, corrupted-payload rejection, invalid-signature rejection, wrong-platform rejection all passed. Rejected updates left installed files unchanged.
- Actual Linux executable: the same five smoke scenarios passed.
- Tests used disposable folders with game-name marker files. The patcher did not modify the developer's Steam installation or any server.
- Published Windows player archive downloaded from GitHub: checksum matched the local build; the extracted executable under Wine successfully used its built-in latest-release URL to download, verify and install all 37 files.
- Published Linux player archive checksum matched the local build. Its executable fetched and verified the public manifest, then correctly refused installation because the user's Valheim session was running. That game was left untouched; Linux installation itself was already covered by the signed offline smoke test above.

Still requires a real Windows PC playtest for Steam auto-detection, Windows publisher prompts and Valheim startup. Blue Depot 0.1.2 itself was previously tested interactively in a local Linux world; this patcher work does not substitute for multiplayer/disconnection testing before server rollout.
