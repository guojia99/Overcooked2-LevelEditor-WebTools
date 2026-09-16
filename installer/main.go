package main

import (
	"bytes"
	_ "embed"
	"fmt"
	"image"
	_ "image/png"
	"path/filepath"
	"strings"

	"oc2-installer/internal"

	"github.com/lxn/walk"
	. "github.com/lxn/walk/declarative"
	"github.com/lxn/win"
)

//go:embed banner.png
var bannerPNG []byte

type appState struct {
	mw            *walk.MainWindow
	pathEdit      *walk.LineEdit
	logBox        *walk.TextEdit
	downloadBtn   *walk.PushButton
	copyResBtn    *walk.PushButton
	installBtn    *walk.PushButton
	progressBar   *walk.ProgressBar
	progressLabel *walk.Label
	resStatus     *walk.Label
	banner        *walk.ImageView

	target string
	busy   bool
}

func main() {
	st := &appState{}

	if err := (MainWindow{
		AssignTo: &st.mw,
		Title:    "OC2 WebTools 安装器",
		MinSize:  Size{Width: 640, Height: 480},
		Size:     Size{Width: 860, Height: 860},
		Layout:   VBox{},
		Children: []Widget{
			ImageView{
				AssignTo: &st.banner,
				MinSize:  Size{Width: 0, Height: 110},
				MaxSize:  Size{Width: 0, Height: 130},
				Mode:     ImageViewModeZoom,
			},
			Label{
				Text:      "操作前请务必先关闭 Unity，以免数据丢失！",
				TextColor: walk.RGB(200, 0, 0),
			},

			// Four steps arranged in a 2x2 grid.
			Composite{
				Layout: Grid{Columns: 2, Spacing: 8},
				Children: []Widget{
					// Step 1: download upstream project
					GroupBox{
						Title:  "第 1 步 · 获取上游项目 (gua248/Overcooked2-LevelEditor)",
						Layout: VBox{},
						Children: []Widget{
							Label{Text: "若尚未下载上游编辑器，点击下方按钮自动下载并解压（会自动填入项目目录）。"},
							PushButton{
								AssignTo:  &st.downloadBtn,
								Text:      "下载并解压上游项目…",
								MinSize:   Size{Height: 34},
								OnClicked: st.onDownload,
							},
							ProgressBar{
								AssignTo: &st.progressBar,
								MaxValue: 100,
								Value:    0,
							},
							Label{
								AssignTo: &st.progressLabel,
								Text:     "",
							},
							VSpacer{},
						},
					},

					// Step 2: select project directory
					GroupBox{
						Title:  "第 2 步 · 选择项目目录",
						Layout: VBox{},
						Children: []Widget{
							Label{Text: "选择（或由第 1 步自动填入）Overcooked2-LevelEditor 项目目录："},
							LineEdit{
								AssignTo: &st.pathEdit,
								ReadOnly: true,
							},
							PushButton{
								Text:      "浏览…",
								MinSize:   Size{Height: 34},
								OnClicked: st.onBrowse,
							},
							VSpacer{},
						},
					},

					// Step 3: copy game resource base
					GroupBox{
						Title:  "第 3 步 · 拷贝游戏资源底包 (StreamingAssets/Windows)",
						Layout: VBox{},
						Children: []Widget{
							Label{
								Text:      "当前项目资源底包状态：请先选择项目目录",
								AssignTo:  &st.resStatus,
								TextColor: walk.RGB(120, 120, 120),
							},
							Label{Text: "————————————————————————"},
							Label{
								Text:      "【参考位置，仅供参考，请勿直接填写此路径】游戏 StreamingAssets 目录通常在：",
								TextColor: walk.RGB(120, 120, 120),
							},
							Label{
								Text:      `C:\...\Overcooked! 2\Overcooked2_Data\StreamingAssets`,
								TextColor: walk.RGB(120, 120, 120),
							},
							Label{Text: "————————————————————————"},
							Label{Text: "点击下方按钮，在弹窗中【自行选择】你电脑上的游戏 StreamingAssets 目录："},
							PushButton{
								AssignTo:  &st.copyResBtn,
								Text:      "选择游戏目录并拷贝资源底包…",
								MinSize:   Size{Height: 34},
								OnClicked: st.onCopyResources,
							},
							VSpacer{},
						},
					},

					// Step 4: install
					GroupBox{
						Title:  "第 4 步 · 安装（自动检测 → 覆盖安装，含反编译代码）",
						Layout: VBox{},
						Children: []Widget{
							Label{Text: "确认前 3 步就绪后，点击下方按钮开始安装。"},
							PushButton{
								AssignTo:  &st.installBtn,
								Text:      "开 始 安 装",
								MinSize:   Size{Height: 42},
								OnClicked: st.onInstall,
							},
							VSpacer{},
						},
					},
				},
			},

			// Log (large, fills remaining space — at least ~50% of the window).
			GroupBox{
				Title:         "日志",
				Layout:        VBox{},
				StretchFactor: 3,
				Children: []Widget{
					TextEdit{
						AssignTo: &st.logBox,
						ReadOnly: true,
						VScroll:  true,
						MinSize:  Size{Height: 180},
					},
				},
			},
		},
	}).Create(); err != nil {
		panic(err)
	}

	// Force a standard resizable, maximizable, minimizable window with a proper
	// title bar (fixes: cannot resize height; maximize hiding the caption buttons).
	if hwnd := st.mw.Handle(); hwnd != 0 {
		style := win.GetWindowLong(hwnd, win.GWL_STYLE)
		style |= win.WS_OVERLAPPEDWINDOW // CAPTION|SYSMENU|THICKFRAME|MIN/MAXIMIZEBOX
		win.SetWindowLong(hwnd, win.GWL_STYLE, style)
		win.SetWindowPos(hwnd, 0, 0, 0, 0, 0,
			win.SWP_NOMOVE|win.SWP_NOSIZE|win.SWP_NOZORDER|win.SWP_FRAMECHANGED)
	}

	st.loadBanner()
	st.setProgress(0, 0, false)
	st.log("欢迎使用 OC2 WebTools 安装器。")
	st.log("流程：① 下载上游项目 → ② 选择项目目录 → ③ 拷贝游戏资源底包 → ④ 开始安装。")
	st.log("提示：反编译代码由安装器自动提供，无需手动反编译；安装为覆盖式，可重复安装。")
	st.mw.Run()
}

func (st *appState) loadBanner() {
	if len(bannerPNG) == 0 {
		return
	}
	img, _, err := image.Decode(bytes.NewReader(bannerPNG))
	if err != nil {
		return
	}
	bmp, err := walk.NewBitmapFromImageForDPI(img, 96)
	if err != nil {
		return
	}
	st.banner.SetImage(bmp)
}

// log appends a line to the log box. Safe to call from any goroutine.
func (st *appState) log(msg string) {
	st.mw.Synchronize(func() {
		st.logBox.AppendText(msg + "\r\n")
	})
}

// setProgress updates the progress bar/label. Must be called on the UI thread.
func (st *appState) setProgress(downloaded, total int64, active bool) {
	if !active {
		st.progressBar.SetValue(0)
		st.progressLabel.SetText("")
		return
	}
	if total > 0 {
		pct := int(float64(downloaded) / float64(total) * 100)
		if pct > 100 {
			pct = 100
		}
		st.progressBar.SetValue(pct)
		st.progressLabel.SetText(fmt.Sprintf("已下载 %.1f / %.1f MB (%d%%)",
			float64(downloaded)/1024/1024, float64(total)/1024/1024, pct))
	} else {
		// Unknown total: show downloaded MB, keep bar moving as marquee-ish.
		st.progressBar.SetValue(0)
		st.progressLabel.SetText(fmt.Sprintf("已下载 %.1f MB ...", float64(downloaded)/1024/1024))
	}
}

func (st *appState) setBusy(b bool) {
	st.busy = b
	enabled := !b
	st.downloadBtn.SetEnabled(enabled)
	st.copyResBtn.SetEnabled(enabled)
	st.installBtn.SetEnabled(enabled)
}

// refreshResStatus updates the step-3 status label based on the current target.
// Must be called on the UI thread.
func (st *appState) refreshResStatus() {
	if st.target == "" {
		st.resStatus.SetText("当前项目资源底包状态：请先选择项目目录")
		st.resStatus.SetTextColor(walk.RGB(120, 120, 120))
		return
	}
	ok, n := internal.StreamingAssetsConfigured(st.target)
	if ok {
		st.resStatus.SetText(fmt.Sprintf("当前项目资源底包状态：已配置 ✓（StreamingAssets/Windows 含 %d 项，无需重复拷贝）", n))
		st.resStatus.SetTextColor(walk.RGB(0, 140, 0))
	} else {
		st.resStatus.SetText("当前项目资源底包状态：未配置 ✗（需从游戏目录拷贝 StreamingAssets/Windows）")
		st.resStatus.SetTextColor(walk.RGB(200, 0, 0))
	}
}

func (st *appState) onDownload() {
	if st.busy {
		return
	}
	dlg := new(walk.FileDialog)
	dlg.Title = "选择上游项目的解压位置（父目录）"
	ok, err := dlg.ShowBrowseFolder(st.mw)
	if err != nil || !ok {
		return
	}
	parent := dlg.FilePath

	final := filepath.Join(parent, internal.FinalDirName)
	if internal.PathExists(final) {
		walk.MsgBox(st.mw, "目录已存在",
			"目标位置已存在 "+internal.FinalDirName+"，继续将无法解压。\n请选择其他位置或先移除该目录。",
			walk.MsgBoxIconWarning)
		return
	}

	st.setBusy(true)
	st.setProgress(0, 1, true)
	st.log("开始从 codeload 下载上游项目 ...")

	onProgress := func(downloaded, total int64) {
		st.mw.Synchronize(func() {
			st.setProgress(downloaded, total, true)
		})
	}

	go func() {
		finalDir, err := internal.DownloadAndExtract(parent, st.log, onProgress)
		st.mw.Synchronize(func() {
			st.setBusy(false)
			st.setProgress(0, 0, false)
			if err != nil {
				st.log("下载/解压失败：" + err.Error())
				walk.MsgBox(st.mw, "失败", err.Error(), walk.MsgBoxIconError)
				return
			}
			st.target = finalDir
			st.pathEdit.SetText(finalDir)
			st.refreshResStatus()
			st.log("上游项目已就绪：" + finalDir)
			walk.MsgBox(st.mw, "完成",
				"上游项目已下载并解压到:\n"+finalDir+"\n\n请继续第 3 步拷贝资源底包。",
				walk.MsgBoxIconInformation)
		})
	}()
}

func (st *appState) onBrowse() {
	if st.busy {
		return
	}
	dlg := new(walk.FileDialog)
	dlg.Title = "选择 Overcooked2-LevelEditor 项目目录"
	ok, err := dlg.ShowBrowseFolder(st.mw)
	if err != nil || !ok {
		return
	}
	st.target = dlg.FilePath
	st.pathEdit.SetText(st.target)
	st.refreshResStatus()
	st.log("已选择项目目录：" + st.target)
}

func (st *appState) onCopyResources() {
	if st.busy {
		return
	}
	if st.target == "" {
		walk.MsgBox(st.mw, "提示", "请先在第 2 步选择/下载项目目录，再拷贝资源底包。", walk.MsgBoxIconInformation)
		return
	}

	// If already configured, let the user decide whether to overwrite.
	if ok, n := internal.StreamingAssetsConfigured(st.target); ok {
		res := walk.MsgBox(st.mw, "资源底包已存在",
			fmt.Sprintf("检测到当前项目已配置资源底包（StreamingAssets/Windows 含 %d 项），通常无需重复拷贝。\n\n是否仍要重新选择并覆盖拷贝？", n),
			walk.MsgBoxIconQuestion|walk.MsgBoxYesNo)
		if res != walk.DlgCmdYes {
			st.log("已跳过资源底包拷贝（项目已配置）。")
			return
		}
	}

	dlg := new(walk.FileDialog)
	dlg.Title = "选择游戏的 StreamingAssets 目录（或 Overcooked2_Data / 游戏根目录）"
	ok, err := dlg.ShowBrowseFolder(st.mw)
	if err != nil || !ok {
		return
	}
	gamePath := dlg.FilePath

	target := st.target
	st.setBusy(true)
	st.log("开始拷贝游戏资源底包 ...")
	go func() {
		err := internal.CopyStreamingAssets(target, gamePath, st.log)
		st.mw.Synchronize(func() {
			st.setBusy(false)
			st.refreshResStatus()
			if err != nil {
				st.log("资源底包拷贝失败：" + err.Error())
				walk.MsgBox(st.mw, "失败", err.Error(), walk.MsgBoxIconError)
				return
			}
			st.log("资源底包已就绪，可点击第 4 步「开始安装」。")
			walk.MsgBox(st.mw, "完成",
				"资源底包 StreamingAssets/Windows 拷贝完成。\n请点击第 4 步「开始安装」。",
				walk.MsgBoxIconInformation)
		})
	}()
}

func renderChecks(title string, items []internal.CheckResult) string {
	var b strings.Builder
	b.WriteString("── " + title + " ──\r\n")
	for _, c := range items {
		mark := "✓"
		if !c.OK {
			mark = "✗"
		}
		b.WriteString("  " + mark + " " + c.Name)
		if !c.OK && c.Hint != "" {
			b.WriteString("  →  " + c.Hint)
		}
		b.WriteString("\r\n")
	}
	return strings.TrimRight(b.String(), "\r\n")
}

// onInstall runs detection then installation (merged single button).
func (st *appState) onInstall() {
	if st.busy {
		return
	}
	if st.target == "" {
		walk.MsgBox(st.mw, "提示", "请先在第 2 步选择/下载项目目录。", walk.MsgBoxIconInformation)
		return
	}

	// --- Detection (results go to the log) ---
	rep := internal.ValidateTarget(st.target)
	st.log(renderChecks("目录结构校验", rep.Structure))
	st.log(renderChecks("环境依赖检测（资源底包）", rep.Env))
	st.log(renderChecks("反编译代码（安装器自动提供）", rep.Info))

	if !rep.StructureOK() {
		st.log("目录结构校验未通过，请确认已选择正确的 Overcooked2-LevelEditor 项目目录。")
		walk.MsgBox(st.mw, "无法安装", "目录结构校验未通过，请选择正确的项目目录。", walk.MsgBoxIconError)
		return
	}
	if !rep.EnvOK() {
		st.log("环境依赖检测未通过：请先用第 3 步拷贝游戏资源底包 StreamingAssets/Windows。")
		walk.MsgBox(st.mw, "无法安装",
			"缺少资源底包 StreamingAssets/Windows。\n请先用第 3 步从游戏目录拷贝。",
			walk.MsgBoxIconError)
		return
	}

	pkg, err := internal.LocatePkg()
	if err != nil {
		st.log("错误：" + err.Error())
		walk.MsgBox(st.mw, "错误", err.Error(), walk.MsgBoxIconError)
		return
	}
	if err := internal.VerifyPkg(pkg); err != nil {
		st.log("错误：" + err.Error())
		walk.MsgBox(st.mw, "错误", err.Error(), walk.MsgBoxIconError)
		return
	}

	asmSrc, err := internal.LocateAssemblyCSharp()
	if err != nil {
		asmSrc = ""
		st.log("警告：" + err.Error() + "（将跳过反编译代码拷贝）")
	}

	// --- Version check ---
	pkgVer := internal.ReadPackageWebVersion(pkg)
	installedVer := internal.ReadInstalledWebVersion(st.target)
	if pkgVer != "" {
		st.log("安装包 Web 版本：" + pkgVer)
	}
	if installedVer != "" {
		st.log("当前已装 Web 版本：" + installedVer)
	} else {
		st.log("当前项目尚未安装 Web 工具。")
	}

	// --- Confirmation dialog (version-aware) ---
	var confirmMsg string
	switch {
	case installedVer != "" && pkgVer != "" && installedVer == pkgVer:
		confirmMsg = fmt.Sprintf(
			"检测到已安装相同版本 %s，通常无需重复安装。\n\n请确认已关闭 Unity。是否仍要覆盖安装？",
			installedVer)
	case installedVer != "" && pkgVer != "" && installedVer != pkgVer:
		confirmMsg = fmt.Sprintf(
			"当前已装 %s，将安装 %s。\n\n请确认已关闭 Unity。是否继续（覆盖安装）？",
			installedVer, pkgVer)
	default:
		confirmMsg = "请确认已关闭 Unity。\n\n将替换目标工程中的对应目录/文件（含反编译代码），其他内容不受影响。\n是否继续？"
	}
	if walk.MsgBox(st.mw, "确认安装", confirmMsg,
		walk.MsgBoxIconWarning|walk.MsgBoxYesNo) != walk.DlgCmdYes {
		st.log("已取消安装。")
		return
	}

	// --- Install (background) ---
	target := st.target
	st.setBusy(true)
	go func() {
		err := internal.Install(target, pkg, asmSrc, st.log)
		st.mw.Synchronize(func() {
			st.setBusy(false)
			if err != nil {
				st.log("安装失败：" + err.Error())
				walk.MsgBox(st.mw, "安装失败", err.Error(), walk.MsgBoxIconError)
				return
			}
			walk.MsgBox(st.mw, "完成", "安装完成！", walk.MsgBoxIconInformation)
		})
	}()
}
