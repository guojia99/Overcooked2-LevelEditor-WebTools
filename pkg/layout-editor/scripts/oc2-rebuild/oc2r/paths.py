"""路径解析：仓库根、AssetRipper 反编译导出根、dump_bundle 根、输出目录。

所有脚本一律通过本模块取路径，禁止在别处硬编码绝对路径。
AssetRipper 导出目录会在多个候选位置探测（历史上被搬到过 backup_ 目录）。
"""

import os

_HERE = os.path.dirname(os.path.abspath(__file__))

# layout-editor/scripts/oc2-rebuild/oc2r -> 仓库根
REPO = os.path.abspath(os.path.join(_HERE, "..", "..", "..", ".."))

SCRIPT_DIR = os.path.abspath(os.path.join(_HERE, ".."))
OUT = os.path.join(SCRIPT_DIR, "out")
CACHE = os.path.join(SCRIPT_DIR, ".cache")

DUMP = os.path.join(REPO, "dump_bundle")
DUMP_MANIFEST = os.path.join(DUMP, "manifest.json")
ASSETS = os.path.join(REPO, "Assets")
LEVELSETS = os.path.join(ASSETS, "LevelSets")
STREAMING = os.path.join(ASSETS, "StreamingAssets", "Windows")

_AR_CANDIDATES = [
    # 仓库同级
    os.path.join(os.path.dirname(REPO), "AssetRipper_export_20260728_091744", "ExportedProject"),
    # 2026-08-26 备份目录
    os.path.join(
        os.path.dirname(REPO),
        "backup_20260826",
        "AssetRipper_export_20260728_091744",
        "ExportedProject",
    ),
]


def _find_ar():
    env = os.environ.get("OC2_AR_EXPORT")
    if env and os.path.isdir(env):
        return os.path.abspath(env)
    for c in _AR_CANDIDATES:
        if os.path.isdir(c):
            return os.path.abspath(c)
    raise RuntimeError(
        "找不到 AssetRipper 导出目录，请设置环境变量 OC2_AR_EXPORT 指向 ExportedProject"
    )


AR = _find_ar()
AR_ASSETS = os.path.join(AR, "Assets")


def ar(*parts):
    """拼 AssetRipper 导出 Assets 下的路径。"""
    return os.path.join(AR_ASSETS, *parts)


def repo(*parts):
    return os.path.join(REPO, *parts)


def out(*parts):
    os.makedirs(OUT, exist_ok=True)
    return os.path.join(OUT, *parts)


def cache(*parts):
    os.makedirs(CACHE, exist_ok=True)
    return os.path.join(CACHE, *parts)


def normalize_source_scene(p):
    """把 levels.json 里记录的 sourceScene（相对/历史路径）解析成当前可用的绝对路径。"""
    if os.path.isabs(p) and os.path.exists(p):
        return p
    marker = "ExportedProject"
    if marker in p:
        tail = p.split(marker, 1)[1].lstrip("/\\")
        cand = os.path.join(AR, tail)
        if os.path.exists(cand):
            return cand
    cand = os.path.join(REPO, p)
    if os.path.exists(cand):
        return os.path.abspath(cand)
    return None
