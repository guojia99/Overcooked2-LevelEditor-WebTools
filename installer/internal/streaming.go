package internal

import (
	"fmt"
	"os"
	"path/filepath"
)

// StreamingAssetsConfigured reports whether the target project already has a
// non-empty Assets/StreamingAssets/Windows folder, and how many top-level
// entries it contains.
func StreamingAssetsConfigured(target string) (bool, int) {
	winDir := filepath.Join(target, "Assets", "StreamingAssets", "Windows")
	entries, err := os.ReadDir(winDir)
	if err != nil {
		return false, 0
	}
	return len(entries) > 0, len(entries)
}

// resolveWindowsSource takes a user-selected path and locates the game's
// StreamingAssets/Windows folder. The selected path may itself be:
//   - the Windows folder
//   - the StreamingAssets folder (contains Windows)
//   - the Overcooked2_Data folder (contains StreamingAssets/Windows)
//   - the game root "Overcooked! 2" (contains Overcooked2_Data/StreamingAssets/Windows)
func resolveWindowsSource(selected string) (string, error) {
	candidates := []string{
		selected,
		filepath.Join(selected, "Windows"),
		filepath.Join(selected, "StreamingAssets", "Windows"),
		filepath.Join(selected, "Overcooked2_Data", "StreamingAssets", "Windows"),
	}
	for _, c := range candidates {
		// A valid Windows folder is a non-empty directory.
		if IsDir(c) && DirNonEmpty(c) {
			// If the selected path *is* the Windows folder, its basename is "Windows".
			if filepath.Base(c) == "Windows" {
				return c, nil
			}
		}
	}
	return "", fmt.Errorf("未在所选目录中找到 StreamingAssets/Windows，请选择游戏的 StreamingAssets 目录（例如 .../Overcooked2_Data/StreamingAssets）")
}

// CopyStreamingAssets copies the game's StreamingAssets/Windows folder into the
// target project's Assets/StreamingAssets/Windows (delete-then-replace). Only the
// Windows folder is touched; other contents of Assets/StreamingAssets are kept.
func CopyStreamingAssets(target, selectedGamePath string, log Logger) error {
	if log == nil {
		log = func(string) {}
	}

	src, err := resolveWindowsSource(selectedGamePath)
	if err != nil {
		return err
	}

	dst := filepath.Join(target, "Assets", "StreamingAssets", "Windows")
	log("正在拷贝资源底包 StreamingAssets/Windows ...")
	log("  源: " + src)
	log("  目标: " + dst)

	if err := ReplaceDir(src, dst); err != nil {
		return err
	}

	log("资源底包拷贝完成。")
	return nil
}
