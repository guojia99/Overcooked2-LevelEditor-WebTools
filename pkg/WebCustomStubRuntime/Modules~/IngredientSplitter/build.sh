#!/bin/sh
# 编译分料机真机运行时模块（产物 IngredientSplitterRuntime.dll）。
# 依赖 Unity 已编译 Library/ScriptAssemblies/{Assembly-CSharp,LevelEditorStub,IngredientSplitterStub}.dll。
set -e
cd "$(dirname "$0")"
DOTNET="${DOTNET:-dotnet}"
command -v "$DOTNET" >/dev/null 2>&1 || DOTNET="$HOME/.dotnet/dotnet"
"$DOTNET" build IngredientSplitterRuntime.csproj -c Release "$@"
echo "→ 产物: $(pwd)/bin/Release/IngredientSplitterRuntime.dll"
echo "→ 导出/Staging 时由 LayoutStubDllBuilder 自动复制进 webcustomstub_runtime bundle。"
