# Scrubclub Patcher & Blue Depot

[Download the patcher](https://github.com/jStimpert0430/scrubclub-patcher/releases/latest)

Windows players: download **ScrubclubPatcher-windows-x64.zip**, extract everything, close Valheim, then double-click **ScrubclubPatcher.exe**. Click **Update**, choose a character and click **Launch game** with Steam running. The server address is prefilled; enter its password in Valheim. Run the same launcher for future mod updates.

Linux players: use **ScrubclubPatcher-linux-x64.tar.gz**, then run `./ScrubclubPatcher`. The ImGui interface and Launch game button work on both platforms. Native Linux x64 only.

The patcher installs Blue Depot, Road Lights, the compatible MultiUserChest fork, Jötunn and BepInEx. Road Lights removes decorative-light fuel upkeep and station requirements, and adds optional free freestanding lights when using the hoe's Pathen tool. Downloads come from GitHub over HTTPS and are signature-checked before installation. Existing configuration is preserved; replaced files are backed up. No home-network hosting service or server credentials are involved.

This is an initial test release. Automated tests cover installation and failures; the Windows executable has been exercised under Wine, not yet on a real Windows PC. The patcher does not deploy mods to a game server. Test the mod in a local world before server rollout.

[Patcher instructions, safeguards and build documentation](patcher/README.md) · [Blue Depot mod documentation](BLUE-DEPOT.md)

Only source and public release assets belong in this repository. Signing keys, local game assemblies, saves, logs and credentials are excluded.
