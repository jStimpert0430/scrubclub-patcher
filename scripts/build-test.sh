#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$project_dir"
export DOTNET_CLI_HOME="$project_dir/.tools/dotnet-home"
export NUGET_PACKAGES="$project_dir/.tools/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export GameManaged="${GameManaged:-$HOME/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed}"
dotnet_bin="$project_dir/.tools/dotnet/dotnet"
"$dotnet_bin" run --project scripts/Publicize/Publicize.csproj -- "$GameManaged" "$project_dir/.deps/publicized"
"$dotnet_bin" build vendor/MultiUserChest/MultiUserChest.csproj -c Release --nologo
cp vendor/MultiUserChest/bin/Release/net472/MultiUserChest.dll .deps/MultiUserChest.dll
"$dotnet_bin" build src/BlueDepot.Plugin/BlueDepot.Plugin.csproj -c Release --nologo
"$dotnet_bin" test tests/BlueDepot.Tests/BlueDepot.Tests.csproj -c Release --nologo --logger 'trx;LogFileName=regressions.trx' --results-directory artifacts/test-results
