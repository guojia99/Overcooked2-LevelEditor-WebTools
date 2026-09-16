#!/usr/bin/env bash
# Cross-compile the OC2 WebTools installer for Windows (amd64) from macOS.
# Pure Go, CGO-free (lxn/walk uses syscall bindings), so no mingw needed.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
INST="$ROOT/installer"
SRC_PNG="$ROOT/pkg/layout-editor/web/public/base_bg.png"

cd "$INST"

# 1) Regenerate icon + banner from base_bg.png if ImageMagick is available.
if command -v magick >/dev/null 2>&1; then
  echo "[build] regenerating banner.png and icon.ico from base_bg.png"
  magick "$SRC_PNG" -resize 680x160 banner.png
  magick "$SRC_PNG" -resize 256x256 -background none -gravity center -extent 256x256 \
    \( -clone 0 -resize 48x48 \) \( -clone 0 -resize 32x32 \) \( -clone 0 -resize 16x16 \) \
    icon.ico
else
  echo "[build] ImageMagick not found; using existing banner.png / icon.ico"
fi

# 2) Resolve dependencies.
echo "[build] go mod tidy"
GOOS=windows GOARCH=amd64 go mod tidy

# 3) Embed manifest (DPI/theme) + icon into a .syso via rsrc.
echo "[build] generating rsrc.syso (manifest + icon)"
go run github.com/akavel/rsrc@latest -manifest app.manifest -ico icon.ico -o rsrc.syso

# 4) Cross-compile to a single windowsgui exe placed at the project root.
echo "[build] compiling installer.exe"
GOOS=windows GOARCH=amd64 CGO_ENABLED=0 \
  go build -trimpath -ldflags="-H windowsgui -s -w" -o "$ROOT/installer.exe" .

echo "[build] done -> $ROOT/installer.exe"
