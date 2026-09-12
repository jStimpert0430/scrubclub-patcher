# Blue Depot — local-test alpha 0.1.2

A blue wooden chest with 100 native slots and a categorized view of accessible storage within 20 metres. Items remain in their real containers; the UI is a view, not a second inventory. Build from Hammer → Furniture with 20 Wood and 5 Blueberries at a workbench.

## Implemented

- Vanilla wooden chest clone, independently tinted blue; 10 × 10 native inventory.
- Capacity = native slots plus the actual slot counts of eligible nearby chests.
- All items, Materials, Weapons, Armor, Food, Potions, Ammunition, Tools, Trophies, Miscellaneous tabs. Empty categories show an empty state, not an item catalogue.
- Search, pagination, icons, stack amounts, quality labels, click-to-withdraw.
- Drop box tab lists unequipped player items for single-stack deposits. Bulk **Drop materials** skips equipped items and the hotbar.
- Deposits first enter the blue chest. Once per second its owning nearby client routes a stack to compatible partial stacks, then chests holding the same item/category, then available slots. Overflow stays in the blue chest.
- Range/ward/container access checks. Ships, carts, the Obliterator and other blue depots are excluded. Ranges do not chain through other depots.
- MUC owner-mediated transfers with explicit completion responses, a pending-request gate, and extra item-identity guards for our requests. A timed-out or exceptional transfer pauses further requests rather than automatically retrying.

## Test locally

The local Steam installation has BepInEx, Jötunn, the rebuilt MultiUserChest and Blue Depot installed. **No live-server changes were made.** Normal Steam launch remains vanilla on this native Linux setup unless you add a BepInEx launch option. Use the wrapper to run modded:

```bash
/home/bunta/famitracker/mods/BlueDepot/scripts/play-local.sh
```

The wrapper sets a separate local save directory at `artifacts/test-saves`. Create a **new character and new local world**, such as `BlueDepotTest`; do not select a cloud copy of a valued world. Custom chests require this mod whenever that world is loaded. Empty and remove blue chests before removing the mod.

1. Build Blue Depot and two ordinary chests within 20 m. The header should show **120 total slots** (100 + 10 + 10).
2. Put wood in one ordinary chest and stone in the other. Open Blue Depot: Materials should show both, and weapons should be empty.
3. Open Drop box and deposit wood/stone. Wait for the transfer acknowledgements and sorting ticks; inspect the actual destination chests.
4. Take a stack through its category row; verify the source chest and player counts both change correctly.
5. Fill a destination, test partial stacks, and verify excess stays in the depot.
6. Build a chest outside 20 m: its items and capacity must not appear. Remove a nearby chest and verify the view updates.
7. Save, exit, reload; check all contents and the 100 native slots.

**Validation so far:** plugin builds against the installed game; unit/regression and game-ABI checks pass; a real native Linux client startup loaded all three plugins and registered the chest. Actual UI interaction, save/reload and two-client behavior still need the above playtest. Automated tests are not proof of multiplayer durability.

## Build and regression tests

```bash
./scripts/build-test.sh
```

Requires workspace .NET 8 SDK, dependency DLLs and a local game installation. `GameManaged` overrides the game assembly directory. The build generates publicized **reference copies only** under `.deps/publicized` for the vendored MUC source; game assemblies are neither modified nor distributed.

The xUnit suite covers aggregation, filtering, range boundaries, duplicate discovery, overflow, exact variant keys, deterministic routing, stale slots, response gates, and 1,000 seeded planner cases. Metadata tests verify Harmony targets and game type references, including the dependency's types to catch the published MUC `InventoryGrid.Element` startup failure.

## Dependencies and source

BepInExPack 5.4.2350; Jötunn 2.30.0; **our local MultiUserChest source build 0.6.2**. The released MUC 0.6.1 binary failed on this Valheim installation; the checked-in upstream source uses `InventoryElement` and builds successfully against the current game. The local version is a fork identifier, not a claim that upstream released 0.6.2. Do not substitute the stock DLL when testing.

ChestButler is a design/source reference; its DLL is not a dependency. Registration, source-owner routing and explicit completion tracking are adapted from its MIT-licensed implementation. MultiUserChest's MIT source is included in `vendor/MultiUserChest`. Exact upstream revisions and package hashes are recorded in `dependencies.lock.json`; notices are in `licenses/`.

## Limits before server deployment

- Only loaded, accessible, stationary chests count. Sorting runs while an eligible owning player is nearby, not offline across unloaded world areas.
- The UI currently uses mouse controls with Escape to close. Controller navigation, draggable stacks and a placement-radius marker remain follow-up work.
- A hung MUC request pauses this client's automation pending its response; it is not automatically refunded or retried. Test disconnects/ownership changes with a disposable world before any server release.
- All clients and the server will need matching Blue Depot and the compatible MUC build. Current server remains vanilla.
- Distribution is local-only. No Thunderstore release or automatic updater has been published. A future modpack/profile or launcher can distribute tested versions.

Installer backups and installed-file manifests are under `artifacts/local-install-backups/`. The install script backs up overwritten files. To play vanilla again on native Linux, launch normally in Steam without the BepInEx wrapper, and use an unmodded world.

## UI regression fix — 0.1.1

Replaced the standalone TMP-default-font overlay with Jötunn's Valheim font, wood-panel and button helpers. The depot panel is a child of the normal animated player inventory panel, opened with InventoryGui.Show; inventory/character/crafting panels stay available. Closing follows the native inventory animation. Search focus prevents inventory hotkeys from interrupting typing. Item rows are rebuilt only when their displayed contents change. 31 automated tests pass, including a regression contract forbidding the old default TMP font/global input-block path. Visual interaction remains a local playtest.

## Button states — 0.1.2

Category buttons now have distinct colors (materials amber, weapons red, armor blue, food green, potions purple, ammunition gold, tools cyan, trophies orange, miscellaneous mauve), with a brighter selected state. Drop materials is green when eligible materials are present and grey/disabled otherwise; it also disables during transfers. Eligibility is shared with the bulk operation: material items outside the hotbar, unequipped, with a positive count and no pending slot transfer. This preserves the previous hotbar protection. Colors refresh as inventory contents change.
