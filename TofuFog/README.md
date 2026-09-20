# TofuFog 0.1.0

Client-side visibility relief for Valheim. Defaults to one-quarter atmospheric fog density and one-quarter particle opacity for drifting ground/distant fog, fog within weather effects, and Mistlands mist. It retains weather, time of day, colors, and night lighting. Reducing fog does not brighten genuinely unlit areas.

## Mistlands progression

Mistlands mist relief unlocks separately for each character after they obtain a **Wisplight** (native item prefab `Demister`). The mod reads the character's existing known-item history using the prefab's shared name. Already-acquired Wisplights count, and discovery remains unlocked after depositing or unequipping the item. A server-wide boss kill or another player's Wisplight does not unlock it for you. There are no new save flags. Missing character/item data keeps the gate locked.

Before discovery, Mistlands particles retain vanilla opacity everywhere, including when viewed from outside the biome. Atmospheric and ground-fog reductions are also suspended while the current biome/environment is Mistlands. The console setting cannot bypass this gate. Native wisp clearing remains unchanged. Outside Mistlands, ordinary fog relief still applies before obtaining the item. Fog and particle transitions keep native timing; existing particles fade naturally.

## Controls

`BepInEx/config/scrubclub.tofufog.cfg` has three independent multipliers under `[Visibility]`: `AtmosphericDensity`, `GroundFogOpacity`, and `MistlandsOpacity`. Each defaults to `0.25`. `0.25` means one-quarter strength, `1` means vanilla, and `0` makes that layer invisible. Config-file edits can be applied on restart.

With the game console enabled, use `tofufog` to inspect settings, `tofufog 0.5` for half strength, `tofufog 0.25` for the recommended default, or `tofufog 1` to restore vanilla strength. The command sets all three values and saves them. Atmospheric changes apply on the next environment update; existing particles retain their birth opacity until they fade. Newly emitted particles use the new setting.

These are density/opacity multipliers, not a guarantee of exactly double view distance. Dense overlapping Mistlands particles may still need a lower multiplier; visual tuning remains to be tested in-world.

## Scope

Requires BepInEx; copy `TofuFog.dll` and `TofuFog.Core.dll` into `BepInEx/plugins/TofuFog`. No Jötunn dependency. Windows and Linux use the same managed DLLs. This is a client-only rendering change; dedicated servers skip patching, and no server installation or network messages are required.

Distance fog scales after the game's fresh `EnvMan.SetEnv` calculation. Particle discovery is limited to fog emitter components, Mistlands `ParticleMist`, and named fog/mist systems under the environment manager's particle roots. It does not globally alter all particle systems. Rain, snowflakes, blizzard particles, Nimbus mist/trails, campfire smoke, spell VFX, lights, and wisp effects are not targets. Mist-area detection, wisplight clearing, cold/wetness, wind, enemy behavior, and resource requirements are unchanged.

Each particle system stores one unmodified start-color baseline. Repeated environment activation cannot compound the opacity multiplier. Gradient modes and RGB colors are preserved; alpha gradients are cloned rather than modifying shared assets. Existing mist births and clearing behavior stay native. Removing the mod restores ordinary rendering on the next launch; no character or world data is stored.

## Validation

Build/test: `bash mods/TofuFog/scripts/build-test.sh`.

37 tests cover per-character progression gates, native persisted item history, multiplier bounds and invalid values, environment name classification, Mistlands priority, native fog reset/emission hooks, and absence of game-field, particle-count, lighting, or particle-clearing mutations. Build and contract checks target installed Valheim 1.0.15. Tests do not establish the visual result in every biome or particle shader.

Before publishing, verify Mistlands remains vanilla on a character that has never acquired a Wisplight, then acquire one and verify relief unlocks. Switch to a fresh character and check that the gate relocks. Compare vanilla (`tofufog 1`), half (`tofufog 0.5`) and quarter (`tofufog 0.25`) in morning fog, rainy weather, a snowstorm and Mistlands. Let old particles fade between comparisons. Revisit environments to check transitions, and confirm wisplight clears mist normally and Nimbus trails/fire smoke remain unchanged.

Included in client modpack 0.2.12; dedicated servers do not need this mod.
