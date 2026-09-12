#!/usr/bin/env python3
"""
restore-oc2dlc-story.py — 恢复 oc2_dlc_story 关卡集的失联资源（重建到 commonW2）。

背景：
  oc2_dlc_story 关卡集于 2026-08-26 生成（gen-scenes/gen-level-assets），随后
  b499bd15e（2026-09-02 common03 对齐上游正式版）删除了旧布局资产，导致场景与
  LevelInfo 引用的部分 guid 失联（wrapper prefab / 菜谱 SO 等）。

策略：
  1. 扫描 Assets/LevelSets/oc2_dlc_story 的全部 guid 引用，与工程现有资产
     （排除 StreamingAssets / AssetBundles）求差集。
  2. git archive 导出 b499bd15e^ 的旧 common03 树到临时目录，建 guid→路径索引。
  3. BFS 传递闭包：逐个取回失联文件（.asset/.prefab + .meta，guid 原样保留，
     场景/LevelInfo 零改动），新取回文件的引用再校验，直到全部解析。
  4. 落位 commonW2（镜像旧 common03 子路径；Recipes/Ingredients/CookingSteps/
     PlatingSteps 归入 food/，对齐现行 common03 布局），文件夹 .meta 用确定性
     guid md5("commonw2-folder:<相对路径>") 并显式打 commonW2 包标
     （镜像 import-dlc-content.mjs 的 ensureCommonW1FolderMeta 约定）。

用法：
  python3 layout-editor/scripts/oc2-import/restore-oc2dlc-story.py
幂等：目标文件已存在则跳过拷贝、仍参与引用解析。
"""
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
ASSETS = os.path.join(ROOT, "Assets")
SET_DIR = os.path.join(ASSETS, "LevelSets", "oc2_dlc_story")
W2_ROOT = os.path.join(ASSETS, "commonW2")
GIT_REF = "b499bd15e^"          # common03 对齐 upstream 前一版
W2_BUNDLE = "commonW2"
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")

BUILTIN = {"0000000000000000f000000000000000", "0000000000000000e000000000000000"}
GUID_RE = re.compile(r"guid: ([0-9a-f]{32})")
SCRIPT_RE = re.compile(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})")
META_GUID_RE = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
EXCLUDE_DIRS = {"StreamingAssets", "AssetBundles", ".git"}

FOLDER_META_TMPL = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName: {bundle}
  assetBundleVariant:
"""


def det_guid(prefix, key):
    return hashlib.md5(f"{prefix}:{key}".encode("utf-8")).hexdigest()


def index_project():
    """当前工程 guid → 路径（排除 StreamingAssets / AssetBundles / 关卡集自身）。"""
    idx = {}
    for dirpath, dirs, files in os.walk(ASSETS):
        dirs[:] = [d for d in dirs if d not in EXCLUDE_DIRS]
        for f in files:
            if not f.endswith(".meta"):
                continue
            p = os.path.join(dirpath, f)
            try:
                m = META_GUID_RE.search(open(p, errors="ignore").read(300))
            except OSError:
                continue
            if m:
                idx[m.group(1)] = p[:-5]
    return idx


def extract_old_tree(tmpdir):
    """git archive 导出 GIT_REF 的旧 common03 到 tmpdir，返回绝对根。"""
    dest = os.path.join(tmpdir, "old")
    os.makedirs(dest, exist_ok=True)
    proc = subprocess.run(
        ["git", "archive", GIT_REF, "Assets/common03"],
        cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if proc.returncode != 0:
        sys.exit("git archive 失败: " + proc.stderr.decode(errors="ignore"))
    subprocess.run(["tar", "-x", "-C", dest], input=proc.stdout, check=True)
    return os.path.join(dest, "Assets", "common03")


def index_tree(base):
    """遍历目录建 guid → 相对(base)路径 索引（取首次出现，路径短者优先）。"""
    idx = {}
    for dirpath, dirs, files in os.walk(base):
        dirs[:] = [d for d in dirs if not d.startswith(".")]
        for f in sorted(files):
            if not f.endswith(".meta"):
                continue
            p = os.path.join(dirpath, f)
            try:
                m = META_GUID_RE.search(open(p, errors="ignore").read(300))
            except OSError:
                continue
            if not m:
                continue
            g = m.group(1)
            rel = os.path.relpath(p[:-5], base)
            if g not in idx or len(rel) < len(idx[g]):
                idx[g] = rel
    return idx


def scan_set_refs():
    """扫关卡集：返回 {guid: {"script": n, "asset": n, "files": [...]}}。"""
    refs = {}
    for dirpath, dirs, files in os.walk(SET_DIR):
        for f in files:
            if f.endswith(".meta"):
                continue
            p = os.path.join(dirpath, f)
            try:
                content = open(p, errors="ignore").read()
            except OSError:
                continue
            scripts = set(SCRIPT_RE.findall(content))
            allg = set(GUID_RE.findall(content))
            rel = os.path.relpath(p, SET_DIR)
            for g in scripts:
                refs.setdefault(g, {"script": 0, "asset": 0, "files": []})
                refs[g]["script"] += 1
                if rel not in refs[g]["files"]:
                    refs[g]["files"].append(rel)
            for g in allg - scripts:
                refs.setdefault(g, {"script": 0, "asset": 0, "files": []})
                refs[g]["asset"] += 1
                if rel not in refs[g]["files"]:
                    refs[g]["files"].append(rel)
    return refs


def map_dest(old_rel):
    """旧 common03 相对路径 → commonW2 目标相对路径。"""
    parts = old_rel.split(os.sep)
    top = parts[0]
    rest = parts[1:]
    if top in ("Recipes", "Ingredients", "CookingSteps", "PlatingSteps"):
        return os.path.join("food", top, *rest)
    return os.path.join(*parts)


def ensure_folder_meta(abs_dir):
    """为 commonW2 下新建目录补 folder .meta（确定性 guid + 包标）。"""
    created = []
    cur = abs_dir
    while cur.startswith(W2_ROOT) and cur != W2_ROOT:
        meta = cur + ".meta"
        if not os.path.exists(meta):
            rel = os.path.relpath(cur, W2_ROOT).replace(os.sep, "/")
            with open(meta, "w", encoding="utf-8") as fh:
                fh.write(FOLDER_META_TMPL.format(
                    guid=det_guid("commonw2-folder", rel), bundle=W2_BUNDLE))
            created.append(os.path.relpath(meta, ASSETS))
        cur = os.path.dirname(cur)
    return created


def file_refs(path):
    try:
        content = open(path, errors="ignore").read()
    except OSError:
        return set()
    if path.endswith((".prefab", ".asset", ".mat", ".unity", ".controller")):
        return set(GUID_RE.findall(content))
    return set()


def classify(rel):
    top = rel.split(os.sep)[0].lower()
    if "prefab" in top:
        return "prefab"
    if "recipe" in rel.lower() or "ingredient" in rel.lower():
        return "recipe_so"
    if "pseudo_prefab_so" in top:
        return "pseudo_so"
    return "other"


def main():
    if not os.path.isdir(SET_DIR):
        sys.exit("关卡集不存在: " + SET_DIR)

    print("[1/5] 索引当前工程 …")
    project = index_project()
    print("      工程资产 guid:", len(project))

    print("[2/5] 扫描关卡集引用 …")
    refs = scan_set_refs()
    set_missing = {}
    for g, info in refs.items():
        if g in BUILTIN or g in project:
            continue
        # 集合内部互相引用（LevelSetInfo→LevelInfo→config 等）会随后拷入工程
        set_missing[g] = info
    print("      引用 guid:", len(refs), "失联:", len(set_missing))

    print("[3/5] 导出旧 common03 树（%s）…" % GIT_REF)
    tmp = tempfile.mkdtemp(prefix="oc2dlc_restore_")
    try:
        old_root = extract_old_tree(tmp)
        old_idx = index_tree(old_root)
        print("      旧树 guid:", len(old_idx))

        print("[4/5] BFS 闭包恢复 …")
        # 集合自身的资产也算"已存在"（已拷入工程）
        existing = set(project.keys())
        pending = sorted(set_missing.keys())
        unresolved = {}          # guid -> refs info
        restored = {}            # guid -> {"src": 旧相对路径, "dst": 工程相对路径}
        folders_created = []

        def locate(g):
            """返回旧树内相对路径；找不到则 git grep 兜底一次。"""
            if g in old_idx:
                return old_idx[g]
            proc = subprocess.run(
                ["git", "grep", "-l", "guid: " + g, GIT_REF, "--", "Assets"],
                cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
            for line in proc.stdout.splitlines():
                # 形如 <ref>:Assets/common03/xxx.meta
                m = re.match(r"^[^:]*:(Assets/.*\.meta)$", line.strip())
                if m:
                    meta_path = m.group(1)
                    rel = os.path.relpath(meta_path[:-5], old_root)
                    # 非 common03 来源：git show 单独取
                    if not meta_path.startswith("Assets/common03/"):
                        fetch = subprocess.run(
                            ["git", "show", GIT_REF + ":" + meta_path],
                            cwd=ROOT, stdout=subprocess.PIPE)
                        if fetch.returncode == 0:
                            dest_dir = os.path.join(old_root, os.path.dirname(rel))
                            os.makedirs(dest_dir, exist_ok=True)
                            with open(os.path.join(old_root, rel), "wb") as fh:
                                fh.write(fetch.stdout)
                        meta_rel = os.path.relpath(meta_path, "Assets/common03")
                        fetch_meta = subprocess.run(
                            ["git", "show", GIT_REF + ":" + meta_path],
                            cwd=ROOT, stdout=subprocess.PIPE)
                        if fetch_meta.returncode == 0:
                            dest_dir = os.path.join(old_root, os.path.dirname(meta_rel))
                            os.makedirs(dest_dir, exist_ok=True)
                            with open(os.path.join(old_root, meta_rel), "wb") as fh:
                                fh.write(fetch_meta.stdout)
                    return rel
            return None

        queue = list(pending)
        while queue:
            g = queue.pop(0)
            if g in existing or g in restored or g in unresolved:
                continue
            rel = locate(g)
            if rel is None:
                unresolved[g] = set_missing.get(g)
                continue
            src_abs = os.path.join(old_root, rel)
            dst_rel = map_dest(rel)
            dst_abs = os.path.join(W2_ROOT, dst_rel)
            os.makedirs(os.path.dirname(dst_abs), exist_ok=True)
            copied = False
            if not os.path.exists(dst_abs):
                shutil.copyfile(src_abs, dst_abs)
                copied = True
            meta_src = src_abs + ".meta"
            if os.path.exists(meta_src) and not os.path.exists(dst_abs + ".meta"):
                shutil.copyfile(meta_src, dst_abs + ".meta")
                copied = True
            restored[g] = {"src": "Assets/common03/" + rel.replace(os.sep, "/"),
                           "dst": "Assets/commonW2/" + dst_rel.replace(os.sep, "/"),
                           "kind": classify(dst_rel)}
            if copied:
                folders_created += ensure_folder_meta(os.path.dirname(dst_abs))
            # 传递闭包：取回文件自身的引用
            for rg in file_refs(dst_abs):
                if rg in BUILTIN or rg in existing or rg in restored:
                    continue
                if rg not in queue and rg not in unresolved:
                    queue.append(rg)

        print("      恢复文件:", len(restored), "未解决:", len(unresolved))

        print("[5/5] 写报告 …")
        os.makedirs(OUT_DIR, exist_ok=True)
        from collections import Counter
        kinds = Counter(v["kind"] for v in restored.values())
        report = {
            "gitRef": GIT_REF,
            "missingInSet": {g: info for g, info in set_missing.items()},
            "restored": restored,
            "restoredKinds": dict(kinds),
            "foldersCreated": sorted(set(folders_created)),
            "unresolved": {g: (info["files"][:5] if info else None)
                           for g, info in unresolved.items()},
        }
        with open(os.path.join(OUT_DIR, "restore-report.json"), "w", encoding="utf-8") as fh:
            json.dump(report, fh, ensure_ascii=False, indent=1)
        print("      分类:", dict(kinds))
        if unresolved:
            print("      ⚠ 未解决 guid:", len(unresolved))
            for g in list(unresolved)[:20]:
                info = unresolved[g] or {}
                print("        ", g, info.get("files", [])[:2])
        print("完成。报告: out/restore-report.json")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    main()
