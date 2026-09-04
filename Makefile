all: run

ZIP_NAME := OC2-Web-v0.7.1.zip

build:
	rm -f $(ZIP_NAME)
	zip -r $(ZIP_NAME) . -x ".*" -x "*/.*" -x "$(ZIP_NAME)"


run:
	rm -rf Plugins/*

	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/Mono.Cecil.dll Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/Mono.Cecil.dll.meta Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/MonoMod.RuntimeDetour.dll Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/MonoMod.RuntimeDetour.dll.meta Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/MonoMod.Utils.dll Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/MonoMod.Utils.dll.meta Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/0Harmony.dll Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/0Harmony.dll.meta Plugins/

	rm -rf ./layout-editor/
	rm -rf ./LayoutEditor/
	rm -rf ./common03/
	rm -rf ./commonW1
	rm -rf ./Assembly-CSharp-Patch/
	rm -rf ./BepInExPlugins/
	rm -rf ./OC2LevelRuntimeLoader.dll

	cp -r ../Overcooked2-LevelEditor/BepInExPlugins/OC2LevelRuntimeLoader/bin/Debug/OC2LevelRuntimeLoader.dll .
	cp -r ../Overcooked2-LevelEditor/BepInExPlugins .
	cp -r ../Overcooked2-LevelEditor/layout-editor .
	cp -r ../Overcooked2-LevelEditor/Assets/commonW1 .
	cp -r ../Overcooked2-LevelEditor/Assets/commonW1.meta .
	cp -r ../Overcooked2-LevelEditor/Assets/Editor/LayoutEditor .

	rm -rf layout-editor/web/node_modules/
	rm -rf layout-editor/scripts/.venv-audio/