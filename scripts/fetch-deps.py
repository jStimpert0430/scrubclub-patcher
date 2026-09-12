#!/usr/bin/env python3
"""Fetch only the pinned public mod packages; never downloads game assemblies."""
from pathlib import Path
import hashlib,json,urllib.request,zipfile
root=Path(__file__).resolve().parents[1]
lock=json.loads((root/'dependencies.lock.json').read_text())
cache=root/'.tools/downloads';cache.mkdir(parents=True,exist_ok=True)
deps=root/'.deps';deps.mkdir(exist_ok=True)
for name,owner,short in [('BepInExPack_Valheim','denikson','bep'),('Jotunn','ValheimModding','jotunn'),('MultiUserChest','MSchmoecker','muc')]:
    pin=lock['packages'][name];archive=cache/(short+'.zip')
    if not archive.exists():
        url=f"https://thunderstore.io/package/download/{owner}/{name}/{pin['version']}/"
        with urllib.request.urlopen(url,timeout=60) as response: archive.write_bytes(response.read())
    if hashlib.sha256(archive.read_bytes()).hexdigest()!=pin['sha256']:raise SystemExit('Checksum mismatch: '+name)
    with zipfile.ZipFile(archive) as z:
        for entry in z.namelist():
            if Path(entry).name in ('BepInEx.dll','0Harmony.dll','Jotunn.dll','Mono.Cecil.dll'):
                (deps/Path(entry).name).write_bytes(z.read(entry))
print('Pinned dependencies verified. MultiUserChest must be built from vendor source.')
