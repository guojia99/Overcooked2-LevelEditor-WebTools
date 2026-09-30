package internal

import (
	"archive/zip"
	"bufio"
	"fmt"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"time"
)

// UpstreamRepoArchiveURL is the canonical GitHub archive URL of the upstream
// project (main branch). Prefix-style mirrors wrap this URL directly.
const UpstreamRepoArchiveURL = "https://github.com/gua248/Overcooked2-LevelEditor/archive/refs/heads/main.zip"

// UpstreamDirectURL is the official codeload endpoint used when downloading
// without a mirror.
const UpstreamDirectURL = "https://codeload.github.com/gua248/Overcooked2-LevelEditor/zip/refs/heads/main"

// FinalDirName is the directory name expected by the rest of the tool / README.
const FinalDirName = "Overcooked2-LevelEditor"

// Mirror describes a prefix-style GitHub download accelerator: the original
// URL is appended to Prefix to form the proxied download URL.
type Mirror struct {
	Name   string // display name
	Prefix string // URL prefix ("" would mean direct, not used here)
	Note   string // short usage note
}

// Mirrors lists the supported prefix-style mirrors in fallback order.
// (toolwa.com/github/ is a web-form tool and cannot be prefixed, so it is
// only shown as a manual link in the UI.)
var Mirrors = []Mirror{
	{Name: "镜像 1 · gh-proxy.com", Prefix: "https://gh-proxy.com/", Note: "支持批量文件加速"},
	{Name: "镜像 2 · ghproxy.net", Prefix: "https://ghproxy.net/", Note: "自动识别文件类型，支持断点续传"},
	{Name: "镜像 3 · ghproxy.homeboyc.cn", Prefix: "https://ghproxy.homeboyc.cn/", Note: "适合下载大体积文件（1GB+ 稳定）"},
	{Name: "镜像 4 · github.akams.cn", Prefix: "https://github.akams.cn/", Note: "支持 API / Clone / Releases 等加速"},
}

// MirrorNames returns the combo entries for the UI: "auto" first, then each
// mirror (with its note), and "official direct" last. The returned indexes map
// directly to the mirrorSel argument of DownloadAndExtract.
func MirrorNames() []string {
	names := make([]string, 0, len(Mirrors)+2)
	names = append(names, "自动（推荐：按序尝试镜像，失败自动切换）")
	for _, m := range Mirrors {
		names = append(names, fmt.Sprintf("%s — %s", m.Name, m.Note))
	}
	names = append(names, "官方直连（不使用镜像）")
	return names
}

// downloadSource is one concrete download attempt (display name + real URL).
type downloadSource struct {
	Name string
	URL  string
}

// resolveSources builds the ordered attempt list for a combo selection:
// sel == 0 (auto) tries every mirror then direct; a specific mirror is tried
// first with the remaining mirrors (and finally direct) as fallback; the
// direct choice only uses the official URL.
func resolveSources(sel int) []downloadSource {
	direct := downloadSource{Name: "官方直连", URL: UpstreamDirectURL}
	ms := make([]downloadSource, 0, len(Mirrors))
	for _, m := range Mirrors {
		ms = append(ms, downloadSource{Name: m.Name, URL: m.Prefix + UpstreamRepoArchiveURL})
	}
	switch {
	case sel >= 1 && sel <= len(ms):
		out := []downloadSource{ms[sel-1]}
		for i, m := range ms {
			if i != sel-1 {
				out = append(out, m)
			}
		}
		return append(out, direct)
	case sel == len(ms)+1:
		return []downloadSource{direct}
	default:
		return append(ms, direct)
	}
}

// ProgressFunc reports download progress. total is 0 when the server does not
// provide a Content-Length (indeterminate progress).
type ProgressFunc func(downloaded, total int64)

// progressWriter counts bytes written and invokes onProgress (throttled).
type progressWriter struct {
	total      int64
	downloaded int64
	lastCall   time.Time
	lastBytes  int64
	onProgress ProgressFunc
}

func (pw *progressWriter) Write(p []byte) (int, error) {
	n := len(p)
	pw.downloaded += int64(n)
	if pw.onProgress == nil {
		return n, nil
	}
	// Throttle: report at most every 100ms or per 256KB, plus always the first.
	now := time.Now()
	if pw.lastCall.IsZero() ||
		now.Sub(pw.lastCall) >= 100*time.Millisecond ||
		pw.downloaded-pw.lastBytes >= 256*1024 {
		pw.onProgress(pw.downloaded, pw.total)
		pw.lastCall = now
		pw.lastBytes = pw.downloaded
	}
	return n, nil
}

// DownloadAndExtract downloads the upstream zip using the selected download
// source (with automatic fallback to the remaining ones on failure) and
// extracts it into destParent, renaming the archive's root folder
// (Overcooked2-LevelEditor-main) to FinalDirName. It returns the final
// project directory path. mirrorSel is the combo index from MirrorNames().
// onProgress (may be nil) receives download progress updates.
func DownloadAndExtract(destParent string, mirrorSel int, log Logger, onProgress ProgressFunc) (string, error) {
	if log == nil {
		log = func(string) {}
	}

	if !IsDir(destParent) {
		return "", fmt.Errorf("目标目录不存在: %s", destParent)
	}

	// 1) Download to a temp zip (reused across source attempts).
	tmpZip, err := os.CreateTemp("", "oc2-upstream-*.zip")
	if err != nil {
		return "", fmt.Errorf("创建临时文件失败: %w", err)
	}
	tmpPath := tmpZip.Name()
	tmpZip.Close()
	defer os.Remove(tmpPath)

	sources := resolveSources(mirrorSel)
	var failures []string
	for i, src := range sources {
		log(fmt.Sprintf("下载源[%s] 开始下载 ...", src.Name))
		if onProgress != nil {
			onProgress(0, 0)
		}
		n, err := downloadFile(src.URL, tmpPath, onProgress)
		if err != nil {
			failures = append(failures, fmt.Sprintf("· %s：%s", src.Name, err.Error()))
			log(fmt.Sprintf("下载源[%s] 失败：%s", src.Name, err.Error()))
			if i < len(sources)-1 {
				log("自动切换到下一个下载源 ...")
			}
			continue
		}
		log(fmt.Sprintf("下载完成 (%.1f MB)，正在解压 ...", float64(n)/1024/1024))

		// 2) Extract into destParent.
		rootName, err := extractZip(tmpPath, destParent, log)
		if err != nil {
			failures = append(failures, fmt.Sprintf("· %s：解压失败：%s", src.Name, err.Error()))
			log(fmt.Sprintf("下载源[%s] 的压缩包解压失败：%s", src.Name, err.Error()))
			if i < len(sources)-1 {
				log("压缩包可能不完整，自动切换到下一个下载源 ...")
			}
			continue
		}

		extracted := filepath.Join(destParent, rootName)
		final := filepath.Join(destParent, FinalDirName)

		// 3) Rename root folder to FinalDirName.
		if rootName != FinalDirName {
			if PathExists(final) {
				return "", fmt.Errorf("目标位置已存在 %s，请先移除后重试", final)
			}
			if err := os.Rename(extracted, final); err != nil {
				return "", fmt.Errorf("重命名目录失败: %w", err)
			}
		}

		log("解压完成: " + final)
		return final, nil
	}

	return "", fmt.Errorf("所有下载源均失败，请检查网络后重试：\n%s", strings.Join(failures, "\n"))
}

// downloadFile fetches url into dstPath and returns the bytes written. It
// validates the HTTP status and sniffs the zip magic ("PK") because some
// proxies answer 200 with an HTML error page.
func downloadFile(url, dstPath string, onProgress ProgressFunc) (int64, error) {
	f, err := os.OpenFile(dstPath, os.O_WRONLY|os.O_CREATE|os.O_TRUNC, 0o644)
	if err != nil {
		return 0, fmt.Errorf("创建临时文件失败: %w", err)
	}
	defer f.Close()

	client := &http.Client{Timeout: 10 * time.Minute}
	resp, err := client.Get(url)
	if err != nil {
		return 0, fmt.Errorf("连接失败: %w", err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		return 0, fmt.Errorf("HTTP 状态: %s", resp.Status)
	}

	body := bufio.NewReader(resp.Body)
	if magic, err := body.Peek(2); err == nil && len(magic) == 2 && (magic[0] != 'P' || magic[1] != 'K') {
		return 0, fmt.Errorf("响应不是有效的 zip 文件（下载源可能返回了错误页）")
	}

	pw := &progressWriter{total: resp.ContentLength, onProgress: onProgress}
	n, err := io.Copy(io.MultiWriter(f, pw), body)
	if err != nil {
		return 0, fmt.Errorf("传输中断: %w", err)
	}
	if n == 0 {
		return 0, fmt.Errorf("下载内容为空")
	}
	// Final progress tick (ensures 100%).
	if onProgress != nil {
		onProgress(n, pw.total)
	}
	return n, nil
}

// extractZip unzips src into destDir and returns the top-level directory name
// contained in the archive.
func extractZip(src, destDir string, log Logger) (string, error) {
	r, err := zip.OpenReader(src)
	if err != nil {
		return "", fmt.Errorf("打开压缩包失败: %w", err)
	}
	defer r.Close()

	rootName := ""
	total := len(r.File)
	for i, f := range r.File {
		// Track the archive root folder name.
		parts := strings.SplitN(filepath.ToSlash(f.Name), "/", 2)
		if rootName == "" && parts[0] != "" {
			rootName = parts[0]
		}

		destPath := filepath.Join(destDir, filepath.FromSlash(f.Name))

		// Zip-slip protection.
		if !strings.HasPrefix(destPath, filepath.Clean(destDir)+string(os.PathSeparator)) {
			return "", fmt.Errorf("非法压缩包路径: %s", f.Name)
		}

		if f.FileInfo().IsDir() {
			if err := os.MkdirAll(destPath, 0o755); err != nil {
				return "", fmt.Errorf("创建目录失败: %w", err)
			}
			continue
		}

		if err := os.MkdirAll(filepath.Dir(destPath), 0o755); err != nil {
			return "", fmt.Errorf("创建目录失败: %w", err)
		}

		rc, err := f.Open()
		if err != nil {
			return "", fmt.Errorf("读取压缩条目失败: %w", err)
		}
		out, err := os.OpenFile(destPath, os.O_WRONLY|os.O_CREATE|os.O_TRUNC, f.Mode())
		if err != nil {
			rc.Close()
			return "", fmt.Errorf("写入文件失败: %w", err)
		}
		if _, err := io.Copy(out, rc); err != nil {
			out.Close()
			rc.Close()
			return "", fmt.Errorf("解压文件失败: %w", err)
		}
		out.Close()
		rc.Close()

		if i%200 == 0 {
			log(fmt.Sprintf("  解压中 %d/%d ...", i+1, total))
		}
	}

	if rootName == "" {
		return "", fmt.Errorf("压缩包内容为空")
	}
	return rootName, nil
}
