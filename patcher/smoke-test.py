#!/usr/bin/env python3
"""Exercise the actual standalone binary using signed payloads, never a real game."""
from pathlib import Path
import hashlib
import json
import os
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parents[1]
platform = 'windows' if '--windows' in sys.argv else 'linux'
version = next((a.split('=', 1)[1] for a in sys.argv if a.startswith('--version=')), '0.1.2')
release = root / 'artifacts/patcher/release' / version
env = dict(os.environ)
env.update(WINEPREFIX=str(root / 'artifacts/patcher/wine-prefix'), WINEDEBUG='-all', DOTNET_EnableWriteXorExecute='0')
binary = root / 'artifacts/patcher' / platform / ('ScrubclubPatcher.exe' if platform == 'windows' else 'ScrubclubPatcher')

def argument(path):
    return 'Z:' + str(path).replace('/', '\\') if platform == 'windows' else str(path)

with tempfile.TemporaryDirectory(prefix='smoke-', dir=root / 'artifacts/patcher') as temp:
    game = Path(temp) / 'Valheim'; game.mkdir()
    (game / ('valheim.exe' if platform == 'windows' else 'valheim.x86_64')).write_text('disposable marker, not game code')
    def run(manifest, payload, success=True):
        command = (['wine', str(binary)] if platform == 'windows' else [str(binary)]) + [
            '--game-dir', argument(game), '--manifest-file', argument(manifest), '--payload-file', argument(payload), '--yes']
        result = subprocess.run(command, env=env, capture_output=True, text=True, errors='replace', timeout=90)
        print(result.stdout.strip())
        if result.stderr:
            print(result.stderr.strip())
        assert (result.returncode == 0) == success, (result.returncode, result.stdout, result.stderr)
        return result.stdout + result.stderr
    manifest = release / f'release-{platform}.json'
    payload = release / f'scrubclub-{version}-{platform}.zip'
    run(manifest, payload)
    assert (game / 'BepInEx/plugins/BlueDepot/BlueDepot.dll').is_file()
    assert '0 files changed' in run(manifest, payload)
    def snapshot():
        return {str(p.relative_to(game)): hashlib.sha256(p.read_bytes()).hexdigest() for p in game.rglob('*') if p.is_file()}
    before = snapshot()
    corrupt = Path(temp) / 'corrupt.zip'
    data = bytearray(payload.read_bytes()); data[0] ^= 1; corrupt.write_bytes(data)
    assert 'checksum mismatch' in run(manifest, corrupt, False)
    assert snapshot() == before
    bad_manifest = Path(temp) / 'bad.json'
    signed = json.loads(manifest.read_text()); signature = signed['Signature']; signed['Signature'] = ('A' if signature[0] != 'A' else 'B') + signature[1:]
    bad_manifest.write_text(json.dumps(signed))
    assert 'signature is invalid' in run(bad_manifest, payload, False)
    assert snapshot() == before
    other = 'linux' if platform == 'windows' else 'windows'
    assert 'wrong platform' in run(release / f'release-{other}.json', payload, False)
    assert snapshot() == before
    print(f'PASS: {platform} standalone install, repeat update, corrupted payload, invalid signature, wrong platform; failed updates left all files unchanged.')
