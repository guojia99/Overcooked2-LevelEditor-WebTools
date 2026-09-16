package internal

import (
	"os"
	"path/filepath"
	"regexp"
)

// webVersionRelPath is the version.ts location relative to a layout-editor dir.
var webVersionRelPath = filepath.Join("layout-editor", "web", "src", "version.ts")

// appVersionRe extracts APP_VERSION = "vX.Y.Z" from version.ts.
var appVersionRe = regexp.MustCompile(`APP_VERSION\s*=\s*"([^"]+)"`)

// readVersionFile parses APP_VERSION from a version.ts file. Returns "" if the
// file is missing or the constant cannot be found.
func readVersionFile(path string) string {
	data, err := os.ReadFile(path)
	if err != nil {
		return ""
	}
	m := appVersionRe.FindSubmatch(data)
	if m == nil {
		return ""
	}
	return string(m[1])
}

// ReadPackageWebVersion returns APP_VERSION from the bundled pkg/layout-editor.
func ReadPackageWebVersion(pkgRoot string) string {
	return readVersionFile(filepath.Join(pkgRoot, webVersionRelPath))
}

// ReadInstalledWebVersion returns APP_VERSION from the target project's
// installed layout-editor. Returns "" if not installed / not found.
func ReadInstalledWebVersion(target string) string {
	return readVersionFile(filepath.Join(target, webVersionRelPath))
}
