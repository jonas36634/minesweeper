#!/usr/bin/env bash
set -euo pipefail
# Install the pinned Microsoft SDK in the cloud workspace. Keep build caches outside the repository.
cd /workspace/minesweeper
sdk=10.0.404
source_url=https://builds.dotnet.microsoft.com/dotnet/Sdk/$sdk/dotnet-sdk-$sdk-linux-x64.tar.gz
sdk_hash=577d0eb17fec33d029104c5c63a1ab0accc3e28776274abef2218dbac5af18d2109b5406d26b31d76f5c245215e3ba0926a94c20246c672c41847c9e1eb1dc50
export DOTNET_ROOT=/workspace/.dotnet
mkdir -p "$DOTNET_ROOT" /workspace/.dotnet-home /workspace/scratch
if ! "$DOTNET_ROOT/dotnet" --list-sdks 2>/dev/null | grep -Fq "$sdk ["; then
    pkg=$(mktemp)
    trap 'rm -f "$pkg"' EXIT
    curl -fsSL --retry 3 "$source_url" -o "$pkg"
    printf '%s  %s\n' "$sdk_hash" "$pkg" | sha512sum -c -
    tar -xzf "$pkg" -C "$DOTNET_ROOT"
fi
source ops/cloud-env.sh
dotnet restore Client/Client.csproj --locked-mode
dotnet restore Tests/Tests.csproj --locked-mode
