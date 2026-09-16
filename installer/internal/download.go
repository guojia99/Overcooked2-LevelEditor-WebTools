package internal

import (
	"archive/zip"
	"fmt"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"time"
)

// UpstreamURL is the codeload archive of gua248/Overcooked2-LevelEditor (main).
const UpstreamURL = "https://codeload.github.com/gua248/Overcooked2-LevelEditor/zip/refs/heads/main"

// FinalDirName is the directory name expected by the rest of the tool / README.
const FinalDirName = "Overcooked2-LevelEditor"

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

// DownloadAndExtract downloads the upstream zip and extracts it into destParent,
// renaming the archive's root folder (Overcooked2-LevelEditor-main) to
// Overcooked2-LevelEditor. It returns the final project directory path.
// onProgress (may be nil) receives download progress updates.
func DownloadAndExtract(destParent string, log Logger, onProgress ProgressFunc) (string, error) {
	if log == nil {
		log = func(string) {}
	}

	if !IsDir(destParent) {
		return "", fmt.Errorf("目标目录不存在: %s", destParent)
	}

	// 1) Download to a temp zip.
	tmpZip, err := os.CreateTemp("", "oc2-upstream-*.zip")
	if err != nil {
		return "", fmt.Errorf("创建临时文件失败: %w", err)
	}
	tmpPath := tmpZip.Name()
	defer os.Remove(tmpPath)

	log("正在下载上游项目包 ...")
	client := &http.Client{Timeout: 10 * time.Minute}
	resp, err := client.Get(UpstreamURL)
	if err != nil {
		tmpZip.Close()
		return "", fmt.Errorf("下载失败: %w", err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		tmpZip.Close()
		return "", fmt.Errorf("下载失败，HTTP 状态: %s", resp.Status)
	}

	pw := &progressWriter{total: resp.ContentLength, onProgress: onProgress}
	n, err := io.Copy(io.MultiWriter(tmpZip, pw), resp.Body)
	tmpZip.Close()
	if err != nil {
		return "", fmt.Errorf("写入下载内容失败: %w", err)
	}
	// Final progress tick (ensures 100%).
	if onProgress != nil {
		onProgress(n, pw.total)
	}
	log(fmt.Sprintf("下载完成 (%.1f MB)，正在解压 ...", float64(n)/1024/1024))

	// 2) Extract into destParent.
	rootName, err := extractZip(tmpPath, destParent, log)
	if err != nil {
		return "", err
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
