#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
game_dir="$HOME/.local/share/Steam/steamapps/common/Valheim"
mkdir -p "$project_dir/artifacts/test-saves"
cd "$game_dir"
exec ./start_game_bepinex.sh "$@" -savedir "$project_dir/artifacts/test-saves"
