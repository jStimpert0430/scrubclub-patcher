# Torchlight 0.1.0

A separate client-side BepInEx mod that keeps placed torch and campfire lights visible farther from the player. Default visibility distance is 125 metres for both torches and campfires, including the campfire's high- and low-fuel light variants. Native fade-in/out is retained. This is visibility distance, not the light's illumination radius.

Covers wooden, iron, blue, green, wisp and wall torches plus campfires, including already-built pieces when loaded. Does not change light brightness/colour, radius, shadows, fuel, recipes, mist-clearing range, world data or graphics light-count budgets. Independent of and compatible with Road Lights; requires BepInEx only. Does not require a dedicated-server install.

`BepInEx/config/scrubclub.torchlight.cfg`: `Visibility.MinimumDistance` defaults to 125, bounded 80–160 metres. Restart the client after changing. Larger values can render more lights and increase GPU load. Vanilla light-count budgets and world-object streaming can still turn distant lights off. Remove the mod and restart to restore vanilla behavior; no save migration needed.

Installed Valheim 1.0.15 source inspection: `LightLod.UpdateLoop` fades illumination range to zero outside `m_lightDistance`. The reference point is the local player except free-fly/no-player mode, which uses the camera. Asset inspection: campfire high/low visibility 100/50 m; sampled wooden, iron, blue and wall torches 80 m. The player's saved point-light setting was unlimited; shadows had a separate limit of three.

Built with 21 passing regression tests, loaded successfully and locally playtested before release. Distributed through Scrubclub modpack 0.2.9.
