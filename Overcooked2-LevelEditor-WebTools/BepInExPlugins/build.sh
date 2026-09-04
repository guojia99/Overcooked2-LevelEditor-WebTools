#!/bin/sh
# 一键打包 BepInEx 插件（mac 本机可用；Windows 用 dotnet build + -p:GameDir）
# 产物：BepInExPlugins/OC2LevelRuntimeLoader/bin/Release/OC2LevelRuntimeLoader.dll
# 安装：复制到游戏 BepInEx/plugins/ 目录
set -e
cd "$(dirname "$0")"
DOTNET="${DOTNET:-dotnet}"
command -v "$DOTNET" >/dev/null 2>&1 || DOTNET="$HOME/.dotnet/dotnet"
"$DOTNET" build OC2LevelRuntimeLoader/OC2LevelRuntimeLoader.csproj -c Release "$@"
DLL="OC2LevelRuntimeLoader/bin/Release/OC2LevelRuntimeLoader.dll"
echo ""
echo "→ 产物: BepInExPlugins/$DLL"
ls -la "$DLL"
echo "→ 安装: 复制到游戏 BepInEx/plugins/OC2DIYLevel/（与 OC2DIYLevel.dll 同层，随模组包分发）"
