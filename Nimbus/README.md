# Nimbus 0.1.0 — local prototype

A small **white** cloud for one rider, built with the hammer near a forge:

- 20 iron
- 10 ancient bark
- 10 feathers

Four cargo slots use the native container inventory and saving system. Shift+E (alternate placement + use) opens cargo; E rides. Normal player inventory still travels with the rider. Uses the existing BepInEx + Jötunn setup; every participating client/server must have the same version. This candidate is staged locally only.

Nimbus has a new persistent prefab ID, `Nimbus_Cloud`; vanilla Karves are untouched. Its model uses nine softly shaded white cloud lobes and a custom cloud inventory/build icon. Native network synchronization, single-controller arbitration, rider attachment, durability and Ashlands/world-edge handling are retained. Releasing controls keeps the rider attached at the center; E resumes control, while movement releases the attachment onto a rider-only deck. You can walk around until you step off its edge or jump away, without a forced sideways teleport. Mount by interacting within 2.5 m; the owner verifies the requesting player and a maximum 3 m boarding distance, independently of boat trigger membership. Occupied seats reject a second rider, and overencumbered players get a message. One player attaches; scenery collision excludes player bodies so a second person cannot stand on it as cargo.

## Movement

The motor uses a spring-damped hover target 0.7 m above the supporting surface (visual clearance varies with cloud thickness and movement). Five footprint samples plus three bounded look-ahead samples follow ground, stationary building decks, rocks with walkable tops and water. Steeper surfaces are rejected; terrain probes look only 0.65 m above the cloud, preventing climbing vertical walls. Solid hull collisions remain active against scenery. Water height remains valid when partially submerged, with downward-speed damping near the surface to recover after a drop.

- Hold WASD for camera-relative movement on both land and water; release to brake. A/D strafe, S moves backward relative to the camera, and the cloud smoothly faces its movement direction.
- Speed cap: 5 m/s in every direction, including diagonals; analog input supports slower movement. Hold the normal sprint control for 7.5 m/s while stamina is available. Cost follows vanilla running skill, equipment, status effects and world modifiers. Release sprint after exhaustion to resume it; idle/unsupported movement does not consume sprint stamina. No wind, sail stages or cruise throttle.
- Acceleration and braking are limited to 8 m/s², retaining a little floating drift. The original 0.7 m spring hover remains.
- Only the current rider’s authenticated input drives the network owner’s physics; missing input expires after 0.5 seconds. Dismount and ownership changes clear retained input.
- On leaving support, gravity takes over; no airborne propulsion or steering.
- The rider stands in the center. The cloud and rider smoothly follow slopes; the root body stays upright while the physical hull aligns to the slope; visual tilt increases gradually to 40 degrees on extreme grades.
- Look-ahead begins lifting over low rocks and fallen logs, with a 0.65 m step bound. Saplings, small bushes, growing plants and loose stone/flint pickups are ignored by the hull and ground probes; standing trees, walls and mineable rocks remain solid. Ground support uses the player’s ground-contact cutoff (`normal.y > 0.1`, about 84.3°). Uphill movement is projected onto the support plane so speed is measured along the slope. This matches the sprinting player’s surface eligibility, not a guarantee of identical capsule/hover-body traversal; geometry, step height and hover reach still apply.
- Inherited boat collision self-damage is disabled. Enemy and environmental damage remain.
- At sea, five water samples estimate wave height and slope. Smoothed pitch/roll and wave-rise compensation keep the cloud hovering above the moving water; movement remains wind-independent.
- Holding movement into a steep, player-traversable surface engages input-driven climb assistance even from a standstill and without sprint. It lifts at up to 4 m/s and reduces horizontal push until height catches up. Nearby slope contacts are required; vertical walls, trees and distant surfaces cannot supply climb support.
- The hull uses low-friction contact and slope-dependent normal clearance to reduce catches while climbing or descending. The rider deck has separate collision filtering so it cannot snag scenery.
- Tilt, clearance and lift changes are rate-limited; hover/climb transitions blend, with gentler visual banking and turning. The top deck forwards both mount and cargo interaction to the normal controller.
- Terrain collision impulses no longer cancel forward momentum while driving; solid obstacles still block movement, and normal braking is preserved.
- Unoccupied clouds brake and stop receiving propulsion.

No altitude controls, teleportation, terrain changes or free flight are added. Nimbus is not Ashlands-ready. The iron-stage recipe deliberately keeps it in the Swamp progression tier.

## Validation and remaining testing

Automated movement tests cover direct movement, diagonal speed limits, invalid/stale input, rider changes, terrain slope/reach limits, water recovery eligibility and spring force bounds. Game-contract tests check the control hook against the installed game and guard against wind-dependent propulsion. An isolated prefab probe checks resource resolution, station gate, model, collider/attachment prerequisites, four-slot native cargo, gravity and unchanged vanilla Karve. It does not open a world or test actual riding.

**Before live release:** test mounting/dismounting and the initial collider/seat fit; camera-relative WASD, release-to-stop and cargo-loaded movement on both surfaces; shoreline transitions and waves; inclines, walls, rocks, bridges, docks and cliffs; idle parking; collision damage and destruction; save/reload; rider death/disconnect; controller ownership handoff and two-player contention. Visual appearance also needs in-world review. Physics stability and single-rider behavior have not yet been verified in a networked world.

Test in a disposable backed-up modded world. Build with the hammer, or use `devcommands` then `spawn Nimbus_Cloud 1` for an initial inspection. Keep Nimbus installed when loading worlds containing its custom cloud prefab; vanilla cannot retain unknown prefabs.

Build/test: `bash mods/Nimbus/scripts/build-test.sh`. Uses the existing toolchain/dependencies under BlueDepot. The runtime probe under `tests/Nimbus.RuntimeProbe` is diagnostic only and must not be distributed with the mod.
