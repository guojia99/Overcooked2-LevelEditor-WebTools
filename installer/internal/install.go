package internal

import (
	"fmt"
	"os"
	"path/filepath"
)

// Logger receives human-readable progress lines.
type Logger func(msg string)

// pluginFiles are the exact files copied into Assets/Plugins (README 第7步).
// The Plugins directory is NOT wiped; only these files are overwritten so that
// other upstream (GUA) plugins remain untouched.
var pluginFiles = []string{
	"0Harmony.dll",
	"0Harmony.dll.meta",
	"Mono.Cecil.dll",
	"Mono.Cecil.dll.meta",
	"MonoMod.RuntimeDetour.dll",
	"MonoMod.RuntimeDetour.dll.meta",
	"MonoMod.Utils.dll",
	"MonoMod.Utils.dll.meta",
}

// LocatePkg returns the pkg directory that ships next to the executable.
func LocatePkg() (string, error) {
	return locateSibling("pkg")
}

// LocateAssemblyCSharp returns the pre-decompiled Assembly-CSharp directory that
// ships next to the executable (project root, not under pkg).
func LocateAssemblyCSharp() (string, error) {
	return locateSibling("Assembly-CSharp")
}

// locateSibling finds a directory named `name` next to the executable, falling
// back to the current working directory (useful during dev runs).
func locateSibling(name string) (string, error) {
	exe, err := os.Executable()
	if err != nil {
		return "", fmt.Errorf("无法定位程序路径: %w", err)
	}
	if p := filepath.Join(filepath.Dir(exe), name); IsDir(p) {
		return p, nil
	}
	if cwd, err := os.Getwd(); err == nil {
		if p := filepath.Join(cwd, name); IsDir(p) {
			return p, nil
		}
	}
	return "", fmt.Errorf("未找到 %s 目录，请确保 installer.exe 与 %s 目录在同一位置", name, name)
}

// VerifyPkg ensures the source pkg has all required items before touching target.
func VerifyPkg(pkg string) error {
	required := []string{
		"LayoutEditor",
		"layout-editor",
		"commonW1", "commonW1.meta",
		"commonW2", "commonW2.meta",
		"WebCustomStubRuntime", "WebCustomStubRuntime.meta",
		"Plugins",
	}
	for _, r := range required {
		if !PathExists(filepath.Join(pkg, r)) {
			return fmt.Errorf("pkg 缺少必要项: %s", r)
		}
	}
	for _, f := range pluginFiles {
		if !PathExists(filepath.Join(pkg, "Plugins", f)) {
			return fmt.Errorf("pkg/Plugins 缺少必要文件: %s", f)
		}
	}
	return nil
}

// Install performs the replace steps described in README 准备工作/安装方法.
// It also copies the pre-decompiled Assembly-CSharp into the target so the user
// does not have to run AssetRipper. Only the specific items listed are touched;
// nothing else is modified.
//
// asmSrc is the pre-decompiled Assembly-CSharp directory (may be "" to skip).
func Install(target, pkg, asmSrc string, log Logger) error {
	if log == nil {
		log = func(string) {}
	}

	assets := filepath.Join(target, "Assets")

	// Step 0: pre-decompiled Assembly-CSharp -> Assets/Scripts/Assembly-CSharp
	if asmSrc != "" {
		log("[1/7] 拷贝反编译代码 Assets/Scripts/Assembly-CSharp ...")
		asmDst := filepath.Join(assets, "Scripts", "Assembly-CSharp")
		if err := ReplaceDir(asmSrc, asmDst); err != nil {
			return err
		}
		// README 准备工作 3.6: overlay Assembly-CSharp-Patch/* onto Assembly-CSharp
		// to fix decompiler artifacts (e.g. CS0165 unassigned-local errors).
		patchSrc := filepath.Join(target, "Assembly-CSharp-Patch")
		if IsDir(patchSrc) {
			log("      应用 Assembly-CSharp-Patch 补丁 ...")
			n, err := OverlayDir(patchSrc, asmDst)
			if err != nil {
				return err
			}
			log(fmt.Sprintf("      已覆盖 %d 个补丁文件", n))
		} else {
			log("      警告：未找到 Assembly-CSharp-Patch，跳过补丁（可能导致编译报错）")
		}
	} else {
		log("[1/7] 跳过 Assembly-CSharp（未提供源目录）")
	}

	// Step: LayoutEditor -> Assets/Editor/LayoutEditor
	log("[2/7] 替换 Assets/Editor/LayoutEditor ...")
	if err := ReplaceDir(
		filepath.Join(pkg, "LayoutEditor"),
		filepath.Join(assets, "Editor", "LayoutEditor"),
	); err != nil {
		return err
	}

	// Step: layout-editor -> <project root>/layout-editor
	log("[3/7] 替换 layout-editor (工程根目录) ...")
	if err := ReplaceDir(
		filepath.Join(pkg, "layout-editor"),
		filepath.Join(target, "layout-editor"),
	); err != nil {
		return err
	}

	// Step: commonW1 (+ meta) -> Assets/
	log("[4/7] 替换 Assets/commonW1 ...")
	if err := ReplaceDir(filepath.Join(pkg, "commonW1"), filepath.Join(assets, "commonW1")); err != nil {
		return err
	}
	if err := ReplaceFile(filepath.Join(pkg, "commonW1.meta"), filepath.Join(assets, "commonW1.meta")); err != nil {
		return err
	}

	// Step: commonW2 (+ meta) -> Assets/
	log("[5/7] 替换 Assets/commonW2 ...")
	if err := ReplaceDir(filepath.Join(pkg, "commonW2"), filepath.Join(assets, "commonW2")); err != nil {
		return err
	}
	if err := ReplaceFile(filepath.Join(pkg, "commonW2.meta"), filepath.Join(assets, "commonW2.meta")); err != nil {
		return err
	}

	// Step: WebCustomStubRuntime (+ meta) -> Assets/
	log("[6/7] 替换 Assets/WebCustomStubRuntime ...")
	if err := ReplaceDir(
		filepath.Join(pkg, "WebCustomStubRuntime"),
		filepath.Join(assets, "WebCustomStubRuntime"),
	); err != nil {
		return err
	}
	if err := ReplaceFile(
		filepath.Join(pkg, "WebCustomStubRuntime.meta"),
		filepath.Join(assets, "WebCustomStubRuntime.meta"),
	); err != nil {
		return err
	}

	// Step: Plugins files -> Assets/Plugins (overwrite only listed files)
	log("[7/7] 覆盖 Assets/Plugins 中的组件 ...")
	pluginsDst := filepath.Join(assets, "Plugins")
	if err := os.MkdirAll(pluginsDst, 0o755); err != nil {
		return fmt.Errorf("创建 %s 失败: %w", pluginsDst, err)
	}
	for _, f := range pluginFiles {
		if err := ReplaceFile(filepath.Join(pkg, "Plugins", f), filepath.Join(pluginsDst, f)); err != nil {
			return err
		}
		log("    覆盖 " + f)
	}

	log("安装完成。")
	return nil
}
