#!/usr/bin/env python3
"""Install into the user's native Steam Valheim. Preserve every overwritten file."""
from pathlib import Path
from datetime import datetime, timezone
import json, shutil, zipfile, hashlib
root=Path(__file__).resolve().parents[1]
game=Path.home()/'.local/share/Steam/steamapps/common/Valheim'
if not (game/'valheim.x86_64').is_file():raise SystemExit('Native Steam Valheim was not found')
files={}
z=zipfile.ZipFile(root/'.tools/downloads/bep.zip')
for name in z.namelist():
    if not name.startswith('BepInExPack_Valheim/') or name.endswith('/'):continue
    relative=name.removeprefix('BepInExPack_Valheim/')
    if relative=='winhttp.dll' or relative.endswith('.dylib'):continue
    files[relative]=z.read(name)
for archive,folder in [('jotunn','Jotunn'),('muc','MultiUserChest')]:
    z=zipfile.ZipFile(root/'.tools/downloads'/(archive+'.zip'))
    for name in z.namelist():
        if name.endswith(('.dll','.bundle')) or 'assetbundle' in name.lower() and not name.endswith('/'):
            # Jotunn embeds its required assets; preserve any distributed companion bundle.
            files['BepInEx/plugins/'+folder+'/'+Path(name).name]=z.read(name)
files['BepInEx/plugins/MultiUserChest/MultiUserChest.dll']=(root/'vendor/MultiUserChest/bin/Release/net472/MultiUserChest.dll').read_bytes()
for name in ['BlueDepot.dll','BlueDepot.Core.dll']:
    files['BepInEx/plugins/BlueDepot/'+name]=(root/'src/BlueDepot.Plugin/bin/Release/net472'/name).read_bytes()
for name in ['ChestButler-MIT.txt','MultiUserChest-MIT.txt']:
    files['BepInEx/plugins/BlueDepot/licenses/'+name]=(root/'licenses'/name).read_bytes()
stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')
backup=root/'artifacts'/'local-install-backups'/stamp
backup.mkdir(parents=True,exist_ok=False)
manifest={'game':str(game),'files':[]}
for relative,data in files.items():
    dest=game/relative
    existed=dest.exists()
    if existed:
        old=backup/relative;old.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(dest,old)
    dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    if dest.suffix=='.sh':dest.chmod(0o755)
    manifest['files'].append({'path':relative,'existed':existed,'sha256':hashlib.sha256(data).hexdigest()})
(backup/'manifest.json').write_text(json.dumps(manifest,indent=2))
print('Installed local test build into',game)
print('Rollback manifest:',backup/'manifest.json')
