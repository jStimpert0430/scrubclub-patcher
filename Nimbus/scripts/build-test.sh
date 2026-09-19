#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_CLI_HOME=/tmp/blue-depot-dotnet-home
export NUGET_PACKAGES="$project_dir/../.tools/nuget"
dotnet_tool="$project_dir/../.tools/dotnet/dotnet"
"$dotnet_tool" build "$project_dir/src/Nimbus.Plugin/Nimbus.Plugin.csproj" -c Release --nologo
"$dotnet_tool" test "$project_dir/tests/Nimbus.Tests/Nimbus.Tests.csproj" -c Release --nologo --logger 'trx;LogFileName=nimbus.trx' --results-directory "$project_dir/artifacts/test-results"
