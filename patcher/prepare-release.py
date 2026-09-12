#!/usr/bin/env python3
"""Build signed, allowlisted release assets locally. Does not publish anything."""
from pathlib import Path
import hashlib
import os
import re
import shutil
import subprocess
import sys
import tarfile
import urllib.request
import zipfile

root = Path(__file__).resolve().parents[1]
if len(sys.argv) != 2 or not re.fullmatch(r'\d+\.\d+\.\d+(?:\.\d+)?', sys.argv[1]):
    raise SystemExit('Usage: prepare-release.py VERSION')
version = sys.argv[1]
out = root / 'artifacts/patcher/release' / version
if out.exists():
    raise SystemExit(f'{out} already exists. Move it aside before rebuilding the same release.')
subprocess.run([sys.executable, str(root / 'patcher/stage-payloads.py')], check=True)
dotnet = str(root / '.tools/dotnet/dotnet') if (root / '.tools/dotnet/dotnet').exists() else 'dotnet'
env = dict(os.environ)
env.setdefault('DOTNET_CLI_HOME', '/tmp/blue-depot-dotnet-home')
env.setdefault('NUGET_PACKAGES', str(root / '.tools/nuget'))
subprocess.run([dotnet, 'build', str(root / 'patcher/Publisher/Publisher.csproj'), '-c', 'Release'], env=env, check=True)
out.mkdir(parents=True)
for platform, rid in [('windows', 'win-x64'), ('linux', 'linux-x64')]:
    subprocess.run([dotnet, str(root / 'patcher/Publisher/bin/Release/net8.0/Publisher.dll'),
                    str(root / 'artifacts/patcher/staging' / platform), str(out), version, platform,
                    f'https://github.com/jStimpert0430/scrubclub-patcher/releases/download/v{version}',
                    str(root / '.signing/release-private.pem')], env=env, check=True)
    binary = root / 'artifacts/patcher' / platform / ('ScrubclubPatcher.exe' if platform == 'windows' else 'ScrubclubPatcher')
    # Use the exact resolved runtime version from restore, not an arbitrary cache entry.
    import json
    assets = json.loads((root / 'patcher/App/obj/project.assets.json').read_text())
    runtime = next((k.split('/')[1] for k in assets['libraries'] if k.startswith('Microsoft.NETCore.App.Runtime.' + rid + '/')), None)
    if runtime is None:
        # Each publish can replace assets.json; inspect the executable's generated dependency file.
        deps = json.loads((root / f'patcher/App/bin/Release/net8.0/{rid}/ScrubclubPatcher.deps.json').read_text())
        runtime = next(k.split('/')[1] for k in deps['libraries'] if k.startswith('runtimepack.Microsoft.NETCore.App.Runtime.' + rid + '/'))
    notice_root = Path(env['NUGET_PACKAGES']) / ('microsoft.netcore.app.runtime.' + rid) / runtime
    entries = [(binary, binary.name), (root / 'patcher/README.md', 'README.md'), (root / 'LICENSE', 'LICENSE.txt'),
               (notice_root / 'LICENSE.TXT', 'DOTNET-LICENSE.txt'), (notice_root / 'THIRD-PARTY-NOTICES.TXT', 'DOTNET-THIRD-PARTY-NOTICES.txt')]
    if platform == 'windows':
        with zipfile.ZipFile(out / 'ScrubclubPatcher-windows-x64.zip', 'w', zipfile.ZIP_DEFLATED) as z:
            for path, name in entries:
                z.write(path, name)
    else:
        with tarfile.open(out / 'ScrubclubPatcher-linux-x64.tar.gz', 'w:gz') as tar:
            for path, name in entries:
                tar.add(path, name, recursive=False)
source = root / '.tools/downloads/UnityDoorstop-4.4.0-source.zip'
if not source.exists():
    with urllib.request.urlopen('https://codeload.github.com/NeighTools/UnityDoorstop/zip/refs/tags/v4.4.0', timeout=60) as response:
        source.write_bytes(response.read())
if hashlib.sha256(source.read_bytes()).hexdigest() != 'f982a2dc2c7fa50f35013051b82268b0bc9fcd73786f5f0b4c664a0e5381b1e9':
    raise SystemExit('Doorstop source checksum mismatch')
shutil.copy2(source, out / source.name)
(out / 'SHA256SUMS').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + p.name + '\n' for p in sorted(out.iterdir()) if p.is_file()))
print('Prepared for review (not uploaded): ' + str(out))
