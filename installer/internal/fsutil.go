package internal

import (
	"fmt"
	"io"
	"os"
	"path/filepath"
)

// PathExists reports whether a path exists.
func PathExists(p string) bool {
	_, err := os.Stat(p)
	return err == nil
}

// IsDir reports whether the path exists and is a directory.
func IsDir(p string) bool {
	fi, err := os.Stat(p)
	return err == nil && fi.IsDir()
}

// DirNonEmpty reports whether the path is a directory containing at least one entry.
func DirNonEmpty(p string) bool {
	entries, err := os.ReadDir(p)
	if err != nil {
		return false
	}
	return len(entries) > 0
}

// RemoveIfExists removes a file or directory (recursively) if it exists.
func RemoveIfExists(p string) error {
	if !PathExists(p) {
		return nil
	}
	if err := os.RemoveAll(p); err != nil {
		return fmt.Errorf("删除 %s 失败: %w", p, err)
	}
	return nil
}

// CopyFile copies a single file, preserving mode.
func CopyFile(src, dst string) error {
	in, err := os.Open(src)
	if err != nil {
		return fmt.Errorf("打开源文件 %s 失败: %w", src, err)
	}
	defer in.Close()

	if err := os.MkdirAll(filepath.Dir(dst), 0o755); err != nil {
		return fmt.Errorf("创建目录 %s 失败: %w", filepath.Dir(dst), err)
	}

	out, err := os.Create(dst)
	if err != nil {
		return fmt.Errorf("创建目标文件 %s 失败: %w", dst, err)
	}
	defer out.Close()

	if _, err := io.Copy(out, in); err != nil {
		return fmt.Errorf("复制到 %s 失败: %w", dst, err)
	}

	if fi, err := os.Stat(src); err == nil {
		_ = os.Chmod(dst, fi.Mode())
	}
	return nil
}

// CopyDir recursively copies a directory tree from src to dst.
func CopyDir(src, dst string) error {
	return filepath.WalkDir(src, func(path string, d os.DirEntry, err error) error {
		if err != nil {
			return err
		}
		rel, err := filepath.Rel(src, path)
		if err != nil {
			return err
		}
		target := filepath.Join(dst, rel)
		if d.IsDir() {
			return os.MkdirAll(target, 0o755)
		}
		return CopyFile(path, target)
	})
}

// ReplaceDir removes the destination directory (if present) and copies src into it.
func ReplaceDir(src, dst string) error {
	if !IsDir(src) {
		return fmt.Errorf("源目录不存在: %s", src)
	}
	if err := RemoveIfExists(dst); err != nil {
		return err
	}
	return CopyDir(src, dst)
}

// OverlayDir copies every file from src into dst, overwriting existing files but
// keeping all other files in dst untouched (does NOT delete dst first). Returns
// the number of files copied.
func OverlayDir(src, dst string) (int, error) {
	if !IsDir(src) {
		return 0, fmt.Errorf("源目录不存在: %s", src)
	}
	count := 0
	err := filepath.WalkDir(src, func(path string, d os.DirEntry, err error) error {
		if err != nil {
			return err
		}
		if d.IsDir() {
			return nil
		}
		rel, err := filepath.Rel(src, path)
		if err != nil {
			return err
		}
		if err := CopyFile(path, filepath.Join(dst, rel)); err != nil {
			return err
		}
		count++
		return nil
	})
	return count, err
}

// ReplaceFile removes the destination file (if present) and copies src to it.
func ReplaceFile(src, dst string) error {
	if !PathExists(src) {
		return fmt.Errorf("源文件不存在: %s", src)
	}
	if err := RemoveIfExists(dst); err != nil {
		return err
	}
	return CopyFile(src, dst)
}
