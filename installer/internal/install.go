package internal

import (
	"fmt"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"strings"
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

// FindCommonW returns the names of all commonW* directories inside pkg, sorted
// naturally (commonW1, commonW2, ..., commonW10). Every directory must also
// ship a matching .meta file. This lets new commonW* bundles ship in pkg
// without any installer changes.
func FindCommonW(pkg string) ([]string, error) {
	matches, err := filepath.Glob(filepath.Join(pkg, "commonW*"))
	if err != nil {
		return nil, fmt.Errorf("扫描 commonW* 失败: %w", err)
	}
	var names []string
	for _, m := range matches {
		if IsDir(m) {
			names = append(names, filepath.Base(m))
		}
	}
	if len(names) == 0 {
		return nil, fmt.Errorf("pkg 中未找到任何 commonW* 目录")
	}
	sort.Slice(names, func(i, j int) bool {
		ni, oki := commonWNum(names[i])
		nj, okj := commonWNum(names[j])
		switch {
		case oki && okj:
			return ni < nj
		case oki:
			return false
		case okj:
			return true
		default:
			return names[i] < names[j]
		}
	})
	for _, n := range names {
		if !PathExists(filepath.Join(pkg, n+".meta")) {
			return nil, fmt.Errorf("pkg 缺少必要项: %s.meta", n)
		}
	}
	return names, nil
}

// commonWNum extracts the numeric suffix of a commonW* name ("commonW10" -> 10).
func commonWNum(name string) (int, bool) {
	s := strings.TrimPrefix(name, "commonW")
	if s == "" {
		return 0, false
	}
	n, err := strconv.Atoi(s)
	if err != nil {
		return 0, false
	}
	return n, true
}

// VerifyPkg ensures the source pkg has all required items before touching target.
func VerifyPkg(pkg string) error {
	required := []string{
		"LayoutEditor",
		"layout-editor",
		"WebCustomStubRuntime", "WebCustomStubRuntime.meta",
		"Plugins",
	}
	for _, r := range required {
		if !PathExists(filepath.Join(pkg, r)) {
			return fmt.Errorf("pkg 缺少必要项: %s", r)
		}
	}
	if _, err := FindCommonW(pkg); err != nil {
		return err
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

	// Auto-discover all commonW* bundles (commonW1, commonW2, ... commonW10+).
	commonDirs, err := FindCommonW(pkg)
	if err != nil {
		return err
	}
	totalSteps := 5 + len(commonDirs)
	step := 0
	next := func() string {
		step++
		return fmt.Sprintf("[%d/%d] ", step, totalSteps)
	}

	// Step 0: pre-decompiled Assembly-CSharp -> Assets/Scripts/Assembly-CSharp
	if asmSrc != "" {
		log(next() + "拷贝反编译代码 Assets/Scripts/Assembly-CSharp ...")
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
		step++
		log(fmt.Sprintf("[%d/%d] 跳过 Assembly-CSharp（未提供源目录）", step, totalSteps))
	}

	// Step: LayoutEditor -> Assets/Editor/LayoutEditor
	log(next() + "替换 Assets/Editor/LayoutEditor ...")
	if err := ReplaceDir(
		filepath.Join(pkg, "LayoutEditor"),
		filepath.Join(assets, "Editor", "LayoutEditor"),
	); err != nil {
		return err
	}

	// Step: layout-editor -> <project root>/layout-editor
	log(next() + "替换 layout-editor (工程根目录) ...")
	if err := ReplaceDir(
		filepath.Join(pkg, "layout-editor"),
		filepath.Join(target, "layout-editor"),
	); err != nil {
		return err
	}

	// Step: every commonW* (+ meta) -> Assets/
	for _, name := range commonDirs {
		log(next() + fmt.Sprintf("替换 Assets/%s ...", name))
		if err := ReplaceDir(filepath.Join(pkg, name), filepath.Join(assets, name)); err != nil {
			return err
		}
		if err := ReplaceFile(filepath.Join(pkg, name+".meta"), filepath.Join(assets, name+".meta")); err != nil {
			return err
		}
	}

	// Step: WebCustomStubRuntime (+ meta) -> Assets/
	log(next() + "替换 Assets/WebCustomStubRuntime ...")
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
	log(next() + "覆盖 Assets/Plugins 中的组件 ...")
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
