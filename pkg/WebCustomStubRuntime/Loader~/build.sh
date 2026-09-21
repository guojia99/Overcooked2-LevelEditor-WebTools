#!/bin/sh
# 一键编译 OC2DIYLevelRuntimeWLoader（统一运行时加载器，产物 Loader.dll）。
# 位置：Assets/WebCustomStubRuntime/Loader~/（`~` 目录 Unity 忽略，dotnet 单独编）。
# mac 本机可用；Windows 用 dotnet build + -p:GameDir。
# 产物：bin/Release/Loader.dll + bin/Release/version.txt
# 分发维护：编译后手动拷贝覆盖 layout-editor/web/public/Loader.dll（导出 zip 从此处读取）。
set -e
cd "$(dirname "$0")"
DOTNET="${DOTNET:-dotnet}"
command -v "$DOTNET" >/dev/null 2>&1 || DOTNET="$HOME/.dotnet/dotnet"
"$DOTNET" build Loader.csproj -c Release "$@"
DLL="bin/Release/Loader.dll"
VERSION_FILE="bin/Release/version.txt"
printf '%s\n' 'Loader=3.2.1' 'debugLog=2.0.0' > "$VERSION_FILE"
echo ""
echo "→ 产物: Assets/WebCustomStubRuntime/Loader~/$DLL"
ls -la "$DLL"
echo "→ 版本: Assets/WebCustomStubRuntime/Loader~/$VERSION_FILE"
cat "$VERSION_FILE"
echo "→ 分发: 拷贝覆盖 layout-editor/web/public/Loader.dll（导出依赖包 zip 时打进 OC2DIYLevelRuntimeWLoader/）"
