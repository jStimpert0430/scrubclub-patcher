#!/usr/bin/env python3
"""Export an explicit source allowlist into a clean publishing checkout."""
from pathlib import Path
import shutil

root = Path(__file__).resolve().parents[1]
out = root / 'artifacts/patcher/repository'
out.mkdir(parents=True, exist_ok=True)
ignored = {'bin', 'obj', '.git', '__pycache__'}
for folder in ['src', 'tests', 'vendor', 'licenses', 'patcher', 'scripts']:
    for source in (root / folder).rglob('*'):
        relative = source.relative_to(root)
        if relative.parts[:2] == ('patcher', 'mod-files') and len(relative.parts) > 3:
            continue
        if not source.is_file() or any(part in ignored for part in relative.parts):
            continue
        if source.suffix not in {'.cs', '.csproj', '.props', '.json', '.md', '.txt', '.py', '.sh', '.pem', '.config', '.ttf'} and source.name != 'LICENSE':
            continue
        if source.suffix == '.pem' and relative.as_posix() != 'patcher/App/release-public.pem':
            raise SystemExit('Unexpected key file')
        target = out / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
for name in ['LICENSE', 'Directory.Build.props', 'dependencies.lock.json', '.gitignore']:
    shutil.copy2(root / name, out / name)
shutil.copy2(root / 'README.md', out / 'BLUE-DEPOT.md')
# Export only explicitly selected standalone mods.
for mod in ['RoadLights', 'SleepVote', 'MonkStyle', 'Nimbus', 'ScrubclubAchievements', 'Torchlight', 'TofuTools', 'TofuFog']:
    road = root.parent / mod
    if not road.exists():
        road = root / mod
    for source in road.rglob('*'):
        relative = source.relative_to(road)
        if relative.parts[0] not in {'src', 'tests', 'scripts', 'README.md', 'AUDIT.md', 'Directory.Build.props'}:
            continue
        if not source.is_file() or any(part in ignored for part in relative.parts):
            continue
        if source.suffix not in {'.cs', '.csproj', '.sh', '.md', '.props'}:
            continue
        target = out / mod / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        data = source.read_text().replace('../../../BlueDepot/.deps', '../../../.deps')
        data = data.replace('../../BlueDepot/.deps', '../../.deps')
        data = data.replace('$project_dir/../BlueDepot/', '$project_dir/../')
        target.write_text(data)
    shutil.copy2(root / 'LICENSE', out / mod / 'LICENSE')
(out / 'README.md').write_text('''# Scrubclub Patcher & Blue Depot

[Download the patcher](https://github.com/jStimpert0430/scrubclub-patcher/releases/latest)

Windows players: download **ScrubclubPatcher-windows-x64.zip**, extract everything, close Valheim, then double-click **ScrubclubPatcher.exe**. Click **Update**, choose a character and click **Launch game** with Steam running. The server address is prefilled; enter its password in Valheim. Run the same launcher for future mod updates.

Linux players: use **ScrubclubPatcher-linux-x64.tar.gz**, then run `./ScrubclubPatcher`. The ImGui interface and Launch game button work on both platforms. Native Linux x64 only.

The patcher installs Blue Depot, Road Lights, Sleep Vote, MonkStyle, Nimbus, Scrubclub Achievements, Torchlight, TofuTools, TofuFog, the compatible MultiUserChest fork, Jötunn and BepInEx. Road Lights removes decorative-light fuel upkeep and station requirements, and adds optional free freestanding lights when using the hoe's Pathen tool. TofuFog reduces fog to quarter strength, with Mistlands relief unlocked per character after obtaining a Wisplight; rain and snow particles remain unchanged. TofuTools prevents death from reducing earned skill levels while retaining partial-XP loss. Sleep Vote asks awake players to approve passing the night, with a 15-second grace period to get into bed after unanimous approval. Live vote status stays visible outside combat; combat pauses the countdown and suppresses panels and dialogs for affected players. Downloads come from GitHub over HTTPS and are signature-checked before installation. Existing configuration is preserved; replaced files are backed up. No home-network hosting service or server credentials are involved.

This is an initial test release. Automated tests cover installation and failures; the Windows executable has been exercised under Wine, not yet on a real Windows PC. The patcher does not deploy mods to a game server. Test the mod in a local world before server rollout.

[Patcher instructions, safeguards and build documentation](patcher/README.md) · [Blue Depot mod documentation](BLUE-DEPOT.md)

Only source and public release assets belong in this repository. Signing keys, local game assemblies, saves, logs and credentials are excluded.
''')
for path in out.rglob('*'):
    if not path.is_file() or '.git' in path.parts:
        continue
    data = path.read_bytes()
    if (b'-' * 5 + b'BEGIN PRIVATE KEY' + b'-' * 5) in data or path.suffix.lower() in {'.dll', '.exe'}:
        raise SystemExit('Unexpected private key or binary in source export: ' + str(path))
print('Source export ready: ' + str(out))
