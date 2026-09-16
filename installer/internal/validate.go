package internal

import (
	"path/filepath"
	"strings"
)

// CheckResult is a single validation item outcome.
type CheckResult struct {
	Name string
	OK   bool
	Hint string
}

// ValidateReport aggregates structure and environment checks.
type ValidateReport struct {
	Structure []CheckResult
	Env       []CheckResult
	Info      []CheckResult
}

// StructureOK reports whether all structure checks passed.
func (r ValidateReport) StructureOK() bool {
	for _, c := range r.Structure {
		if !c.OK {
			return false
		}
	}
	return true
}

// EnvOK reports whether all environment checks passed.
func (r ValidateReport) EnvOK() bool {
	for _, c := range r.Env {
		if !c.OK {
			return false
		}
	}
	return true
}

// AllOK reports whether the target is safe to install into.
func (r ValidateReport) AllOK() bool {
	return r.StructureOK() && r.EnvOK()
}

// ValidateTarget runs structure + environment checks against the selected
// Overcooked2-LevelEditor project directory.
func ValidateTarget(target string) ValidateReport {
	var rep ValidateReport

	// ---- Directory structure checks ----
	base := filepath.Base(strings.TrimRight(target, string(filepath.Separator)))
	nameOK := base == "Overcooked2-LevelEditor" || IsDir(filepath.Join(target, "ProjectSettings"))
	rep.Structure = append(rep.Structure, CheckResult{
		Name: "工程根目录 (Overcooked2-LevelEditor / ProjectSettings)",
		OK:   nameOK,
		Hint: "请选择 Overcooked2-LevelEditor 工程根目录",
	})

	assets := filepath.Join(target, "Assets")
	rep.Structure = append(rep.Structure, CheckResult{
		Name: "Assets/ 目录",
		OK:   IsDir(assets),
		Hint: "缺少 Assets 目录，可能不是 Unity 工程",
	})
	rep.Structure = append(rep.Structure, CheckResult{
		Name: "Assets/Editor/ 目录",
		OK:   IsDir(filepath.Join(assets, "Editor")),
		Hint: "缺少 Assets/Editor 目录",
	})
	rep.Structure = append(rep.Structure, CheckResult{
		Name: "Assets/Plugins/ 目录",
		OK:   IsDir(filepath.Join(assets, "Plugins")),
		Hint: "缺少 Assets/Plugins 目录，请先安装 GUA 上游编辑器",
	})

	// ---- Environment prerequisite checks (README 准备工作 2、3) ----
	winDir := filepath.Join(assets, "StreamingAssets", "Windows")
	rep.Env = append(rep.Env, CheckResult{
		Name: "Assets/StreamingAssets/Windows (准备工作第2步，需手动拷贝资源底包)",
		OK:   IsDir(winDir) && DirNonEmpty(winDir),
		Hint: "请将游戏目录 Overcooked2_Data/StreamingAssets/Windows 复制到 Assets/StreamingAssets",
	})

	// Assembly-CSharp (准备工作第3步) is provided by the installer itself, so this
	// is informational only and does NOT block installation.
	asmDir := filepath.Join(assets, "Scripts", "Assembly-CSharp")
	rep.Info = append(rep.Info, CheckResult{
		Name: "Assets/Scripts/Assembly-CSharp (准备工作第3步，安装时将自动拷贝反编译代码)",
		OK:   IsDir(asmDir) && DirNonEmpty(asmDir),
		Hint: "无需手动反编译，安装器会自动拷贝",
	})

	return rep
}
