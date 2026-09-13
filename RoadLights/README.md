# Road Lights 0.2.0

Road Lights for scrubclub. Version 0.2.0 includes the locally tested dedicated
hoe-tab row and is included in modpack 0.2.4.
Requires BepInEx and Jotunn on clients and server. No Blue Depot dependency.

Supported decorative lights use no fuel and have no crafting-station requirement.
Normal material costs, material discovery, placement distance, supports, collisions,
ward restrictions, weather/water behavior and native on/off capabilities remain.
Manual building still costs materials. Blue Depot can still supply
materials under its normal rules when installed alongside this mod.

Included: standing wood/iron/blue/green torches, sconces, wisp torches, Dvergr wall
and pole lanterns, hooded lanterns, lava lanterns, resin candles, jack-o-turnips,
and hanging/standing/blue standing braziers (explicitly requested).

Excluded: campfires/fire pits, hearths, bonfires, ovens, cooking stations, smelters,
kilns, blast furnaces, station upgrades, handheld torches/lanterns, NPC props and
unknown modded lights. Selection uses 15 exact prefab IDs rather than any object
that happens to emit light. Prefab IDs were checked against the Jotunn-generated
[Valheim prefab catalog](https://valheim-modding.github.io/Jotunn/data/prefabs/prefab-list.html).
Actual prefab availability is logged at startup and needs a local gameplay check.

Existing and newly placed supported lights get native `m_infiniteFuel = true` and
`m_canRefill = false`. Saved fuel amounts and on/off state are not overwritten;
previously empty lights can burn using the native infinite-fuel path. Native smoke,
heat, fire damage and environmental extinction remain as the game implements them,
including for braziers. Lights that support toggling can still be switched on/off
at zero saved fuel, using the existing owner RPC and a ward access check.

The mod removes only the Piece crafting-station reference for included prefabs and
loaded instances, leaving resource requirements intact. This also removes that
station requirement for vanilla removal/repair checks where they use the same
reference. No new prefabs, replacement objects, or inventory writes are introduced.
Automatically generated lights have a persistent boolean ZDO tag that suppresses
material refunds when destroyed; manually built lights retain normal refunds. Removing the mod and restarting restores vanilla
rules; existing pieces still exist, and their underlying saved fuel remains.

## Local test

Build succeeded with zero warnings/errors; 49 tests pass. Tests cover the exact
inclusion/exclusion list, toggle restrictions, installed-game hook contracts, and
an IL check that only the station reference and two fuel flags are modified. They
are not a substitute for runtime/client-server validation.

Save and exit before installing the two staged DLLs into
`BepInEx/plugins/RoadLights/`, then launch the modded game with `-console`.

In a disposable local test world:

1. Place several torch/lantern variants and braziers away from all workbenches with
   their normal material costs available. Check crafting costs are consumed and
   missing materials still prevent placement (do not enable no-cost building).
2. Place a campfire beside them. Fuel it normally, then use `setfuel 0` to empty
   all loaded Fireplace lights/fires in that test world. Decorative lights should
   remain lit while the campfire requires wood. On/off-capable lights must toggle.
3. Try resin/coal/wood hotbar use and normal interaction on permanent lights: no
   fuel should be consumed. Check campfires and Blue Depot station supplies still
   accept wood normally.
4. Save/reload and check existing empty decorative lights stay lit. Test wards,
   supports, rain/water, hammer removal, and repair.
5. Before live release, check a second player and owner handoff, including braziers.

Release build: `artifacts/staged/0.2.0-header-row/`.

## Build without GitHub Actions

`scripts/build-test.sh` uses the local SDK/dependency cache from the BlueDepot
workspace. Tests run locally, then signed release packaging can include these two
DLLs using the patcher's existing additional-mod payload folder. Nothing in this
project enables a GitHub Actions workflow or a self-hosted runner.

## Automatic roadside lights (0.2.0)

Equip the hoe, open its build menu, and select the **Road lighting** tab.
Select an unlocked freestanding recipe, or **Off** (default). The selected choice
is highlighted and saved in the local configuration; recipes are checked again
against the current character before every placement. Wall and hanging lights
are excluded from this selector. Use **Hoe** to return to the normal tools.

After a successful `path_v2` placement by the local player's hoe, the mod checks
for existing supported decorative lights within 12.192 metres (40 feet). If there
are none, it tries a position three metres left or right of the player-to-path
direction, chosen randomly, then the opposite side if the first is unsuitable.
Both the path center and proposed light position are checked for nearby lights.
Level Ground, Raise Ground and Paved Road do not trigger lights.

Placement requires reasonably level, unblocked, dry ground, permitted biome and
ward access, and excludes no-build locations. If neither side is suitable, the
path is still made without a light. Generated lights cost no items, use the native
placement/creator/effects routine, and yield no resource refunds on destruction.
This refund rule requires the mod; uninstalling restores vanilla refund behavior.

Local gameplay confirmed automatic road lighting and the hoe-tab layout.
Additional validation: locked recipe filtering, Off,
Pathen-only trigger, both sides, spacing, blocked/underwater sites, material counts,
save/reload and free-versus-manual refunds. Before live release test simultaneous
two-player roadwork: placement currently runs on each player's client, so racing
clicks can create two lights before network replication arrives. No server-wide
reservation protocol is included in this local candidate.
