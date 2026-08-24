#!/usr/bin/env bash
# Runs every off-game harness. Any failure fails the whole run.
#
# These are plain `dotnet run` console programs rather than a test framework, because what they
# need is not assertions but a way to compile the mod's own source files without Unity or
# Valheim present. Each harness pulls the real .cs files in via <Compile Include="../../..."/>
# and supplies stubs for the game surface they touch.
set -euo pipefail

DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
cd "$(dirname "$0")"

status=0
for harness in ConfigMigrationTests FlightLogTests; do
    echo "==================== $harness ===================="
    if ! "$DOTNET" run --project "$harness/$harness.csproj" -c Release -v quiet --property:WarningLevel=0; then
        status=1
    fi
    echo
done

if [ $status -eq 0 ]; then echo "ALL HARNESSES PASSED"; else echo "SOME HARNESSES FAILED"; fi
exit $status
