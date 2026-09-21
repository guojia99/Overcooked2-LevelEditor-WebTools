#!/bin/sh
set -e
cd "$(dirname "$0")"
DOTNET="${DOTNET:-dotnet}"
command -v "$DOTNET" >/dev/null 2>&1 || DOTNET="$HOME/.dotnet/dotnet"
"$DOTNET" build debugLog.csproj -c Release "$@"
VERSION_FILE="bin/Release/version.txt"
printf '%s\n' 'Loader=3.2.0' 'debugLog=2.0.0' > "$VERSION_FILE"
echo "-> artifact: $(pwd)/bin/Release/debugLog.dll"
echo "-> version: $(pwd)/$VERSION_FILE"
cat "$VERSION_FILE"
