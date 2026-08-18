all: run


run:
	rm -rf ./layout-editor/
	rm -rf ./LayoutEditor/
	rm -rf ./common_w/
	cp -r ../Overcooked2-LevelEditor/layout-editor .
	cp -r ../Overcooked2-LevelEditor/Assets/common_w .
	cp -r ../Overcooked2-LevelEditor/Assets/Editor/LayoutEditor .

	rm -rf layout-editor/web/node_modules/
	rm -rf layout-editor/scripts/.venv-audio/