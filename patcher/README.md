# Scrubclub Launcher

A Dear ImGui launcher and signed updater for the Steam edition of Valheim on Windows x64 and native Linux x64. Both player packages include their .NET runtime, graphics libraries and font. Players do not need Python, .NET or a mod manager. Keep the extracted files together; do not move just the executable out of its folder.

## Players

1. Download `ScrubclubPatcher-windows-x64.zip` from [Releases](https://github.com/jStimpert0430/scrubclub-patcher/releases/latest), then extract it.
2. Save and close Valheim. Double-click `ScrubclubPatcher.exe`.
3. Click **Update** at the bottom left. The adjacent progress bar reports downloading and installation. If needed, set the game folder under **Installation & connection**; Steam → Valheim → Manage → Browse local files opens the correct folder.
4. Choose a character from the dropdown, or leave **Choose in game** selected. Click **Launch game**, with Steam running. **Join scrubclub** connects to the configured server; untick it to use the normal game menu for a local world.

Run the same patcher again before playing whenever the server owner announces a mod update. It checks the current signed release and installs changed files. It does not run in the background, update Steam itself, or download mods during a server connection.

Bundle 0.2.0 contains Blue Depot 0.1.2, our compatible MultiUserChest 0.6.2 fork, Jötunn 2.30.0, BepInExPack 5.4.2350, and the client-only Scrubclub character helper 0.2.0. Blue Depot and its dependencies must also be installed on the server before using the chest there. This launcher release does not deploy anything to the server. Test in a local world first.

On Linux, extract `ScrubclubPatcher-linux-x64.tar.gz` and run `./ScrubclubPatcher`. The Launch game button uses the BepInEx wrapper automatically. This payload targets native Linux Valheim, not the Windows game running in Proton. OpenGL 3.3-capable drivers and a graphical desktop are required for the ImGui window.

The character list reads local saves and the active Steam account's cached Cloud characters; backup files are excluded. It does not move or edit saves. Let Steam finish syncing before launch. If a selected character is unavailable in-game, the helper falls back to Valheim's character screen. The optional custom save directory is also passed to the game. Selection is applied once, through Valheim's existing menu/join flow, retaining EULA, privilege and password prompts.

The server address is prefilled at the owner's request and publicly visible in source. It can be changed in **Installation & connection** if the home IP changes. No password is embedded. Launcher preferences are stored in the user's local application-data folder under `ScrubclubLauncher/settings.json`.

## Future mods

The launcher is not tied to Blue Depot's filenames. Its mod list and installer use the signed release manifest. Add future client plugins, dependencies, patchers, configuration and assets through [mod-files](mod-files/README.md), sign a higher bundle version and publish it. Players use the same Update button; a launcher rebuild is not needed for ordinary mod changes. Compatibility testing still matters, and mods requiring external installers or writes outside the approved mod folders need explicit integration.

The Windows executable has no commercial Authenticode certificate. Windows may show an unknown-publisher/SmartScreen prompt. Obtain it from the release link above; the patcher's own release signature checks apply to its downloaded mod files, not to the initial executable.

## Update safety and hosting

Downloads use GitHub Releases over HTTPS. There is no home-network download service, additional inbound port, Proxmox connection, server password, or GitHub token in the launcher. Release assets and the prefilled game-server address are public. Only repository maintainers can publish releases. Knowing the game address does not bypass its password or provide Proxmox credentials.

The patcher verifies RSA-PSS/SHA-256 signatures using its embedded public key, then checks the archive checksum and every file's length and checksum before changing the installation. ZIP contents must match the signed file list exactly. Paths are confined to BepInEx, Doorstop libraries and the required root loader files; path traversal and symlink/junction targets are refused. It blocks downgrades below its recorded installed version. This is not a sandbox for the installed mods: only sign code you trust.

Existing BepInEx configuration is preserved. Files outside the release remain untouched; an existing mod manager profile or extra mods may still cause compatibility conflicts. Backups and the installed file list live in `<Valheim>/.scrubclub-patcher/`. Only previously managed, unchanged files can be removed by a later release. An edited retired file blocks the update and names the file to move aside.

Exceptions during installation trigger rollback. A journal allows the next run to restore an interrupted transaction before updating. Backups remain in `backup-*` directories, with `journal.json` mapping each numbered `.bak` file to its original path. `Existed: false` means the update introduced that file. Do not delete this directory while an update is running. Hardware/storage failure can still require manual recovery.

## Build and test

Use the .NET 8 SDK. From this directory:

```sh
dotnet test Tests/Tests.csproj -c Release
dotnet publish App/App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ../artifacts/patcher/windows
dotnet publish App/App.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ../artifacts/patcher/linux
```

Tests cover installation, repeat runs, rollback, interrupted-update recovery, backups, configuration preservation, retired files, signatures, hashes, path validation, symlinks, locks, platform checks, downgrade prevention and Linux launcher permissions.

`smoke-test.py` exercises the actual standalone executables against disposable game folders: signed offline install, repeat install, rejected tampering, and wrong-platform rejection. `--windows` runs the Windows executable with Wine from Linux. This tests the Windows runtime/file operations; a real Windows PC remains necessary to validate Steam detection, Windows security prompts and game launch.

```sh
python3 smoke-test.py --windows
python3 smoke-test.py --linux
```

The executable opens the GUI by default. The CLI remains available with `--game-dir PATH --yes`, optional `--manifest-url HTTPS_URL`, and signed offline testing with `--manifest-file FILE --payload-file FILE`. There is no switch to disable signature checks. A `patcher-settings.json` beside the executable can override the CLI's `windowsManifestUrl` / `linuxManifestUrl`, but cannot change the trusted signing key. The GUI uses the built-in release URL. `--gui --smoke` renders and closes a preview without updates or game launch. `--gui --game-dir PATH` uses an isolated folder selection without saving launcher preferences, for UI integration testing.

## Maintainers: prepare a release

Build and test Blue Depot using the parent project's scripts first. `stage-payloads.py` reads only pinned dependency ZIPs and specific built mod DLLs; it never packages the local Steam directory, game assemblies, saves, logs or credentials. Run `prepare-release.py VERSION` from this folder after the standalone builds. It stages both platforms, signs each manifest, creates player download archives, copies the Doorstop source archive and writes a SHA256SUMS file. Review the generated `artifacts/patcher/release/VERSION` directory before upload.

The private key is at `../.signing/release-private.pem`, excluded from version control and all packaging. Keep a separate secure backup; losing it prevents signing updates for existing patchers. It stays local and is never uploaded to GitHub. The public key is embedded in `App/release-public.pem`. The initial key already exists locally: do not regenerate it for ordinary releases.

For a new deployment with a new key, generate an RSA 3072-bit private key with OpenSSL, export its public key to `App/release-public.pem`, and rebuild the patcher. Changing trust keys requires redistributing the patcher itself.

Publish each release's versioned ZIPs and `release-windows.json` / `release-linux.json` together. The executable reads `/releases/latest/download/release-PLATFORM.json`; that signed manifest points to the ZIP under the specific version tag. Test a release before making it the latest stable release. For a bad release, issue a higher version containing the previous good mod files rather than downgrading the release number.

The mod payload contains the applicable licenses. The runtime notices travel with the patcher executable; UnityDoorstop's corresponding source archive travels as a release asset. No proprietary game files are redistributed.
