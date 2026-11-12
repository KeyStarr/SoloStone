#!/bin/sh
# Every installer check, with no release scan skipped. Run from the repository root. Stops at the first failure.
# DOTNET overrides the SDK. Output stays in installer/out, which is ignored.
set -e
# An explicit DOTNET wins; otherwise the `dotnet` on the PATH.
if [ -z "${DOTNET:-}" ]; then DOTNET="$(command -v dotnet)"; fi
"$DOTNET" build installer/SoloStone.Launcher/SoloStone.Launcher.csproj -c Release -p:NuGetAudit=false --output installer/out/app -nologo -v q
"$DOTNET" build plugin/OfflineProbe.csproj -c Debug -p:EngineSource=true -p:Installer=true -p:NuGetAudit=false --artifacts-path installer/out/plugin-release -nologo -v q
"$DOTNET" build installer/SoloStone.Launcher.Tests/SoloStone.Launcher.Tests.csproj -c Release --artifacts-path installer/out/gate -p:NuGetAudit=false -nologo -v q
SOLOSTONE_REQUIRE_ALL=1 "$DOTNET" installer/out/gate/bin/SoloStone.Launcher.Tests/release/SoloStone.Launcher.Tests.dll
python3 -I installer/test_loader_inputs.py
python3 -I installer/test_stage.py
