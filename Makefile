all: run


run:
	rm -rf ./layout-editor/
	rm -rf ./LayoutEditor/
	rm -rf ./common03/
	rm -rf ./Assembly-CSharp-Patch/
	cp -r ../Overcooked2-LevelEditor/layout-editor .
	cp -r ../Overcooked2-LevelEditor/Assets/common03 .
	cp -r ../Overcooked2-LevelEditor/Assets/Editor/LayoutEditor .
	cp -r ../Overcooked2-LevelEditor/Assembly-CSharp-Patch .
	rm -rf layout-editor/web/node_modules/
	rm -rf layout-editor/scripts/.venv-audio/