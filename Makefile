all: run

ZIP_NAME := OC2-Web-v0.8.4.zip
INSTALLER_ZIP := OC2-Installer.zip

.PHONY: all build run installer installer-zip

# Cross-compile the Windows GUI installer (installer.exe) from macOS.
installer:
	./installer/build.sh

# Package installer.exe together with pkg/ and Assembly-CSharp/ into a distributable zip.
installer-zip: installer
	rm -f $(INSTALLER_ZIP)
	zip -r $(INSTALLER_ZIP) installer.exe pkg Assembly-CSharp -x "*/.DS_Store" -x ".DS_Store"

build:
	rm -f $(ZIP_NAME)
	zip -r $(ZIP_NAME) . -x ".*" -x "*/.*" -x "$(ZIP_NAME)"


run:
	rm -rf ./pkg/*
	mkdir ./pkg/Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/Mono.Cecil.dll ./pkg/Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/Mono.Cecil.dll.meta ./pkg/Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/MonoMod.RuntimeDetour.dll ./pkg/Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/MonoMod.RuntimeDetour.dll.meta ./pkg/Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/MonoMod.Utils.dll ./pkg/Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/MonoMod.Utils.dll.meta ./pkg/Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/0Harmony.dll ./pkg/Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/Plugins/0Harmony.dll.meta ./pkg/Plugins/
	cp -r ../Overcooked2-LevelEditor/Assets/WebCustomStubRuntime ./pkg/
	cp -r ../Overcooked2-LevelEditor/Assets/WebCustomStubRuntime.meta ./pkg/
	cp -r ../Overcooked2-LevelEditor/layout-editor ./pkg/
	cp -r ../Overcooked2-LevelEditor/Assets/commonW1 ./pkg/
	cp -r ../Overcooked2-LevelEditor/Assets/commonW1.meta ./pkg/
	cp -r ../Overcooked2-LevelEditor/Assets/commonW2 ./pkg/
	cp -r ../Overcooked2-LevelEditor/Assets/commonW2.meta ./pkg/
	cp -r ../Overcooked2-LevelEditor/Assets/Editor/LayoutEditor ./pkg/

	rm -rf ./pkg/layout-editor/web/node_modules/
	rm -rf ./pkg/layout-editor/scripts/.venv-audio/