# Scrubclub Achievements 0.1.0

Restore normal achievement progression while using mods in Valheim 1.0.15. Requires BepInEx; no Jötunn dependency. Locally built and staged only, not installed or published.

The plugin replaces exactly one `Game.isModded` read with `false` inside `Achievements.IsCheatedAtAll`. It does not change `Game.isModded` itself: the game remains correctly labeled modded. Original method control flow and caching stay intact, with the eligibility cache invalidated when the plugin loads/unloads.

Character cheat flags, cheated inventory items, world-modifier restrictions, difficulty requirements, stat thresholds and normal platform submission remain unchanged. Players who have used spawn/devcommands or carry cheated items may still be ineligible. This mod does not clear saved flags, modify item provenance, grant achievements, replay old progress, or copy raw statistics into eligible statistics. New normal progress can count once the only blocker is running mods. Existing eligible progress may still be recognized by the game's own checks.

## Installation scope

Put `ScrubclubAchievements.dll` in `BepInEx/plugins/ScrubclubAchievements` on each participating client. The managed DLL supports Windows and Linux using the same BepInEx setup. The server can load the same plugin, but server installation is not a replacement for client installation; platform achievements belong to each player's signed-in account. No server endpoint, credentials or additional network messages are introduced.

Do not combine with another achievement-enabler patch without testing compatibility. If a future game version no longer has exactly the expected single modded check, initialization fails closed and logs the mismatch rather than bypassing a different restriction. Remove the plugin to return to normal vanilla gating; saved profiles/worlds are not migrated.

## Validation

Build succeeded against installed Valheim 1.0.15. Five automated tests pass:

- Execute the transformed eligibility expression for all eight combinations of profile/world/item restrictions; only modded-only eligibility changes.
- Keep the global modded flag true and leave input instructions unmodified.
- Reject missing/ambiguous modded checks.
- Preserve branch labels and exception metadata.
- Verify the installed game still contains the expected gate and native restrictions.

Tests do not call Steam achievement APIs or change real account progress. Full BepInEx startup and an in-world earned achievement remain to be verified before release.

Build/test: `bash mods/ScrubclubAchievements/scripts/build-test.sh`.

Official background on normal cheat/item/world restrictions: https://www.valheimgame.com/support/valheim-1-0-faq/ . The implementation was checked against the installed game, not inferred solely from documentation.
