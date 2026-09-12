#!/usr/bin/env python3
"""Prepare exactly the approved mod files; never copies a live game installation."""
from pathlib import Path
import hashlib
import json
import shutil
import zipfile

root = Path(__file__).resolve().parents[1]
lock = json.loads((root / 'dependencies.lock.json').read_text())
out = root / 'artifacts/patcher/staging'
if out.exists():
    shutil.rmtree(out)  # only generated staging, never the game directory
for short, package in [('bep', 'BepInExPack_Valheim'), ('jotunn', 'Jotunn')]:
    archive = root / f'.tools/downloads/{short}.zip'
    assert hashlib.sha256(archive.read_bytes()).hexdigest() == lock['packages'][package]['sha256'], package

def write(platform, name, data):
    target = out / platform / name
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)

for platform in ['windows', 'linux']:
    with zipfile.ZipFile(root / '.tools/downloads/bep.zip') as z:
        for info in z.infolist():
            prefix = 'BepInExPack_Valheim/'
            if not info.filename.startswith(prefix) or info.is_dir():
                continue
            name = info.filename[len(prefix):]
            include = name.startswith('BepInEx/core/') or name.startswith('BepInEx/config/') or name == '.doorstop_version'
            include |= platform == 'windows' and name in ['winhttp.dll', 'doorstop_config.ini']
            include |= platform == 'linux' and name in ['doorstop_libs/libdoorstop_x64.so', 'start_game_bepinex.sh']
            if include:
                write(platform, name, z.read(info))
    with zipfile.ZipFile(root / '.tools/downloads/jotunn.zip') as z:
        write(platform, 'BepInEx/plugins/Jotunn/Jotunn.dll', z.read('plugins/Jotunn.dll'))
    for name in ['BlueDepot.dll', 'BlueDepot.Core.dll']:
        write(platform, 'BepInEx/plugins/BlueDepot/' + name, (root / 'src/BlueDepot.Plugin/bin/Release/net472' / name).read_bytes())
    write(platform, 'BepInEx/plugins/MultiUserChest/MultiUserChest.dll', (root / 'vendor/MultiUserChest/bin/Release/net472/MultiUserChest.dll').read_bytes())
    write(platform, 'BepInEx/plugins/ScrubclubLauncher/ScrubclubLauncherBridge.dll', (root / 'patcher/Bridge/bin/Release/net472/ScrubclubLauncherBridge.dll').read_bytes())
    for license in sorted((root / 'licenses').glob('*.txt')):
        write(platform, 'BepInEx/plugins/BlueDepot/licenses/' + license.name, license.read_bytes())
    write(platform, 'BepInEx/plugins/BlueDepot/licenses/BlueDepot-MIT.txt', (root / 'LICENSE').read_bytes())
    write(platform, 'BepInEx/plugins/BlueDepot/licenses/THIRD-PARTY.txt', (root / 'patcher/THIRD-PARTY.txt').read_bytes())
    # Additional mods use ordinary game-relative paths. Publisher validates every path.
    for layer in ['common', platform]:
        directory = root / 'patcher/mod-files' / layer
        if not directory.exists():
            continue
        for source in sorted(directory.rglob('*')):
            if source.is_symlink():
                raise SystemExit('Symlinks are not allowed in mod-files: ' + str(source))
            if source.is_file():
                name = source.relative_to(directory).as_posix()
                if not name.startswith(('BepInEx/', 'doorstop_libs/')):
                    raise SystemExit('Additional mods must stay within BepInEx or doorstop_libs: ' + name)
                write(platform, name, source.read_bytes())
    print(platform + ': ' + str(sum(1 for p in (out / platform).rglob('*') if p.is_file())) + ' staged files')
