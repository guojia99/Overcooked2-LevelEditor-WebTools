all: run

ZIP_NAME := Overcooked2-LevelEditor-WebTools.zip

build:
	rm -f $(ZIP_NAME)
	zip -r $(ZIP_NAME) . -x ".*" -x "*/.*" -x "$(ZIP_NAME)"


run:
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