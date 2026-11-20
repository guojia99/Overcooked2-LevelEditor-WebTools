#!/usr/bin/env python3
"""Check only existing upstream patch blocks; never overwrite host-specific code."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
missing = []
for name, required_import in (("GameUtils.cs", "using LevelEditor;"),
                              ("ClientTray.cs", "using System.Linq;"),
                              ("ServerTray.cs", "using System.Linq;")):
    patch = (ROOT / "Assembly-CSharp-Patch" / name).read_text(encoding="utf-8-sig")
    host = (ROOT / "Assets/Scripts/Assembly-CSharp" / name).read_text(encoding="utf-8-sig")
    # Ignore indentation and CRLF/LF only, preserving every token in each patch.
    normalize = lambda text: "\n".join(line.strip() for line in text.splitlines() if line.strip())
    blocks = re.findall(r"(?m)^\s*// patch\s*\n([\s\S]*?)^\s*// patch\s*$", patch)
    assert blocks, "No upstream patch blocks found: " + name
    expected = [required_import] + blocks
    absent = sum(normalize(block) not in normalize(host) for block in expected)
    if absent:
        missing.append(name)
        print("FAIL " + name + ": " + str(absent) + " existing upstream import/patch blocks missing")
    else:
        print("PASS " + name + ": existing upstream blocks present; unrelated host code not compared")
if missing:
    print("LIMIT: source check only; no files changed. Missing sync: " + ", ".join(missing))
    sys.exit(1)
