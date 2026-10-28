package internal

import (
	"os"
	"path/filepath"
	"testing"
)

func writeTree(t *testing.T, base string, names []string) {
	t.Helper()
	for _, n := range names {
		p := filepath.Join(base, n)
		if filepath.Ext(p) == ".meta" {
			if err := os.WriteFile(p, nil, 0o644); err != nil {
				t.Fatal(err)
			}
			continue
		}
		if err := os.MkdirAll(p, 0o755); err != nil {
			t.Fatal(err)
		}
	}
}

func TestFindCommonW(t *testing.T) {
	t.Run("discovers all and sorts naturally", func(t *testing.T) {
		base := t.TempDir()
		writeTree(t, base, []string{
			"commonW1", "commonW1.meta",
			"commonW2", "commonW2.meta",
			"commonW3", "commonW3.meta",
			"commonW4", "commonW4.meta",
			"commonW10", "commonW10.meta",
			"WebCustomStubRuntime", // non-commonW entries ignored
		})
		// A commonW* dir without .meta must fail.
		if err := os.MkdirAll(filepath.Join(base, "commonW9"), 0o755); err != nil {
			t.Fatal(err)
		}
		if _, err := FindCommonW(base); err == nil {
			t.Fatal("expected error for missing commonW9.meta")
		}
		if err := os.WriteFile(filepath.Join(base, "commonW9.meta"), nil, 0o644); err != nil {
			t.Fatal(err)
		}

		got, err := FindCommonW(base)
		if err != nil {
			t.Fatal(err)
		}
		want := []string{"commonW1", "commonW2", "commonW3", "commonW4", "commonW9", "commonW10"}
		if len(got) != len(want) {
			t.Fatalf("got %v, want %v", got, want)
		}
		for i := range want {
			if got[i] != want[i] {
				t.Fatalf("got %v, want %v", got, want)
			}
		}
	})

	t.Run("no commonW dirs", func(t *testing.T) {
		base := t.TempDir()
		if _, err := FindCommonW(base); err == nil {
			t.Fatal("expected error when no commonW* present")
		}
	})

	t.Run("real pkg layout", func(t *testing.T) {
		base := "../.."
		if !IsDir(filepath.Join(base, "pkg")) {
			t.Skip("pkg not available")
		}
		got, err := FindCommonW(filepath.Join(base, "pkg"))
		if err != nil {
			t.Fatal(err)
		}
		if len(got) < 4 || got[3] != "commonW4" {
			t.Fatalf("expected commonW1..4 to be discovered, got %v", got)
		}
	})
}
