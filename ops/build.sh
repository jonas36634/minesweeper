#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet restore Tests/Tests.csproj --locked-mode
dotnet restore Client/Client.csproj --locked-mode
dotnet run --project Tests -c Release --no-restore
mkdir -p artifacts
release=$(mktemp -d "$PWD/artifacts/release.XXXXXXXX")
dotnet publish Client -c Release --no-restore -o "$release"
tar -C "$release/wwwroot" -czf artifacts/minesweeper-wwwroot.tar.gz .
printf 'Statischer Build: %s/wwwroot\nArchiv: %s/artifacts/minesweeper-wwwroot.tar.gz\n' "$release" "$PWD"
