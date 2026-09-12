# Scrubclub Patcher & Blue Depot

[Download the patcher](https://github.com/jStimpert0430/scrubclub-patcher/releases/latest)

Windows players: download **ScrubclubPatcher-windows-x64.zip**, extract it, close Valheim, then double-click **ScrubclubPatcher.exe**. Confirm your Steam Valheim folder and install. Launch Valheim from Steam afterward. Run the patcher again when a mod update is announced.

Linux players: use **ScrubclubPatcher-linux-x64.tar.gz**, then launch the modded game using `start_game_bepinex.sh` in the Valheim folder. Native Linux x64 only.

The patcher installs Blue Depot, its compatible MultiUserChest fork, Jötunn and BepInEx. Downloads come from GitHub over HTTPS and are signature-checked before installation. Existing configuration is preserved; replaced files are backed up. No home-network hosting service or server credentials are involved.

This is an initial test release. Automated tests cover installation and failures; the Windows executable has been exercised under Wine, not yet on a real Windows PC. The patcher does not deploy mods to a game server. Test the mod in a local world before server rollout.

[Patcher instructions, safeguards and build documentation](patcher/README.md) · [Blue Depot mod documentation](BLUE-DEPOT.md)

Only source and public release assets belong in this repository. Signing keys, local game assemblies, saves, logs and credentials are excluded.
