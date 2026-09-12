#!/usr/bin/env python3
"""
extract-oc2dlc-screenshots.py — 为 oc2_dlc_story 关卡集补齐 LevelInfo 截图。

数据链路（每关）：
  levels.json(id/dlc/sceneName 原始名)
    → 各 DLC 的 coopgamescenedirectory 资产（UnityPy typetree 实读，保留 PPtr 的
      m_FileID/m_PathID —— dump JSON 已把外部引用拍平成 guid=0，信息不足）
    → Screenshot PPtr 跨 bundle 解析（externals → cab → bundle，CAB 映射全量扫
      一次后缓存 .cache/cabmap.json）
    → Sprite → PIL → Assets/LevelSets/oc2_dlc_story/data/<level>/screenshot.png
    → .meta（复制 oc1_story 模板：TextureImporter spriteMode 1；guid 确定性
      md5("oc2dlc-story-screenshot:<levelId>")）
    → 回填 LevelInfo: screenshot: {fileID: 21300000, guid: …, type: 3}

兜底：PPtr 解析失败时按 Sprite 名 DLC_<nn>_<suffix> 在目录 bundle 的依赖里扫描。
用法：python3 layout-editor/scripts/oc2-import/extract-oc2dlc-screenshots.py
"""
import hashlib
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
WIN = os.path.join(ROOT, "Assets", "StreamingAssets", "Windows")
SET_DIR = os.path.join(ROOT, "Assets", "LevelSets", "oc2_dlc_story")
LEVELS_JSON = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "levels.json")
CACHE_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".cache")
CABMAP = os.path.join(CACHE_DIR, "cabmap.json")
OC1_META_TEMPLATE = os.path.join(
    ROOT, "Assets", "LevelSets", "oc1_story", "data", "OC1_Story_1_1", "screenshot.png.meta")

# DLC -> directory 资产所在 bundle（镜像 scan-levels.py 的 DIRECTORIES）
DIRECTORIES = {
    "dlc02": "bundle161", "dlc03": "bundle208", "dlc04": "bundle224",
    "dlc05": "bundle247", "dlc07": "bundle293", "dlc08": "bundle354",
    "dlc09": "bundle404", "dlc10": "bundle222", "dlc11": "bundle222",
    "dlc13": "bundle222",
}

try:
    import UnityPy
except ImportError:
    sys.exit("ERROR: UnityPy not installed. Run: pip install UnityPy Pillow")


def det_guid(level_id):
    return hashlib.md5(("oc2dlc-story-screenshot:" + level_id).encode()).hexdigest()


def build_cabmap():
    """全量扫 bundle 文件建 CAB→bundle 映射（一次，缓存）。"""
    if os.path.exists(CABMAP):
        return json.load(open(CABMAP))
    cab_re = re.compile(r"(cab-[0-9a-f]{32})", re.I)
    m = {}
    files = sorted((f for f in os.listdir(WIN) if re.fullmatch(r"bundle\d+", f)),
                   key=lambda s: int(s[6:]))
    for bf in files:
        try:
            env = UnityPy.load(os.path.join(WIN, bf))
            cabs = set()
            for f in env.files.values():
                name = getattr(f, "name", None)
                if name:
                    cabs.add(name)
                for inner in (getattr(f, "files", None) or {}):
                    cabs.add(inner)
            for c in cabs:
                mm = cab_re.search(c)
                if mm:
                    m.setdefault(mm.group(1).lower(), bf)
        except Exception as e:
            print("  ! scan fail", bf, e)
    os.makedirs(CACHE_DIR, exist_ok=True)
    json.dump(m, open(CABMAP, "w"))
    return m


def load_env(bundle):
    return UnityPy.load(os.path.join(WIN, bundle))


def read_directory_scenes(bundle):
    """读 directory 资产 typetree：返回 [{Label, scene_names:set, ptr:(fileID,pathID)}]。"""
    env = load_env(bundle)
    entries = []
    for o in env.objects:
        if str(o.type) not in ("114",):  # MonoBehaviour
            continue
        try:
            tt = o.read_typetree()
        except Exception:
            continue
        scenes = tt.get("Scenes") if isinstance(tt, dict) else None
        if not scenes:
            continue
        for sc in scenes:
            names, ptr = set(), (0, 0)
            for v in sc.get("SceneVarients", []):
                sn = v.get("SceneName")
                if sn:
                    names.add(sn)
                sp = v.get("Screenshot") or {}
                fid, pid = sp.get("m_FileID", 0), sp.get("m_PathID", 0)
                if pid and fid is not None and ptr == (0, 0):
                    ptr = (fid, pid)
            if ptr == (0, 0):
                lso = sc.get("LoadScreenOverride") or {}
                if lso.get("m_PathID", 0):
                    ptr = (lso.get("m_FileID", 0), lso.get("m_PathID", 0))
            entries.append({"label": sc.get("Label"), "names": names, "ptr": ptr})
        break
    return env, entries


def externals_of(env, obj):
    try:
        return [e.path for e in obj.assets_file.externals]
    except Exception:
        return []


def resolve_sprite_image(ptr, env, obj, cabmap, cache={}):
    """PPtr -> PIL Image。cache[bundle] = {path_id: object reader}。"""
    fid, pid = ptr
    if fid == 0:
        # bundle 内部引用：按 pathID 在 directory bundle 里找对象
        # （directory bundle 很小，直接建索引；键必须按 env 隔离，避免跨 DLC 污染）
        target_obj = {o.path_id: o for o in env.objects}.get(pid)
        if target_obj is None:
            return None
        try:
            return target_obj.read().image
        except Exception:
            return None
    else:
        exts = externals_of(env, obj)
        if fid - 1 >= len(exts):
            return None
        m = re.search(r"(cab-[0-9a-f]{32})", exts[fid - 1], re.I)
        if not m:
            return None
        bundle = cabmap.get(m.group(1).lower())
        if not bundle:
            return None
        if bundle not in cache:
            e2 = load_env(bundle)
            cache[bundle] = (e2, {o.path_id: o for o in e2.objects})
        target_env, idx = cache[bundle]
        if pid not in idx:
            return None
        target_obj = idx[pid]
    try:
        d = target_obj.read()
        return d.image
    except Exception:
        return None


def main():
    levels = json.load(open(LEVELS_JSON))["levels"]
    print("levels:", len(levels))
    print("building CAB map (cached) …")
    cabmap = build_cabmap()
    print("  cabs:", len(cabmap))

    meta_template = open(OC1_META_TEMPLATE).read()

    # 每个 DLC 的 directory 只读一次
    dir_cache = {}
    obj_cache = {}
    ok, failed = [], []
    for lv in levels:
        lid, dlc, scene = lv["id"], lv["dlc"], lv["sceneName"]
        bundle = DIRECTORIES.get(dlc)
        if not bundle:
            failed.append((lid, "no dlc bundle")); continue
        if dlc not in dir_cache:
            try:
                dir_cache[dlc] = read_directory_scenes(bundle)
            except Exception as e:
                dir_cache[dlc] = None
                print("  ! directory read fail", dlc, e)
        if not dir_cache[dlc]:
            failed.append((lid, "directory unreadable")); continue
        env, entries = dir_cache[dlc]
        # 定位 directory MonoBehaviour 本体（供 externals 解析）
        dir_obj = None
        for o in env.objects:
            if str(o.type) == "114":
                try:
                    if isinstance(o.read_typetree().get("Scenes"), list):
                        dir_obj = o; break
                except Exception:
                    continue
        ptr = None
        for e in entries:
            if scene in e["names"] and e["ptr"] != (0, 0):
                ptr = e["ptr"]; break
        img = None
        if ptr and dir_obj:
            img = resolve_sprite_image(ptr, env, dir_obj, cabmap, obj_cache)
        if img is None:
            # 兜底：按 Sprite 名扫描本 DLC 依赖 bundle —— 名形如 DLC_02_1_1 / DLC_02_H9
            m = re.match(r"OC2_DLC(\d+)_(.+)", lid)
            if m:
                want = "DLC_%s_%s" % (m.group(1), m.group(2))
                for cand in sorted(set(list(DIRECTORIES.values()))):
                    if cand not in obj_cache:
                        e2 = load_env(cand)
                        obj_cache[cand] = (e2, {o.path_id: o for o in e2.objects})
                    e2, idx = obj_cache[cand]
                    for o in e2.objects:
                        if str(o.type) in ("213", "28"):
                            try:
                                d = o.read()
                                if getattr(d, "m_Name", "") == want:
                                    img = d.image; break
                            except Exception:
                                continue
                    if img is not None:
                        break
        if img is None:
            failed.append((lid, "sprite not resolved")); continue

        level_dir = os.path.join(SET_DIR, "data", lid)
        if not os.path.isdir(level_dir):
            failed.append((lid, "level dir missing")); continue
        png = os.path.join(level_dir, "screenshot.png")
        img.save(png)
        g = det_guid(lid)
        with open(png + ".meta", "w") as fh:
            fh.write(re.sub(r"^guid: [0-9a-f]{32}", "guid: " + g,
                            meta_template, count=1, flags=re.M))
        info_path = os.path.join(level_dir, "LevelInfo_%s.asset" % lid)
        txt = open(info_path).read()
        new = txt.replace(
            "screenshot: {fileID: 0}",
            "screenshot: {fileID: 21300000, guid: %s, type: 3}" % g)
        if new != txt:
            open(info_path, "w").write(new)
        ok.append((lid, img.size))

    print("extracted:", len(ok), "failed:", len(failed))
    for lid, size in ok[:5]:
        print("  ", lid, size)
    for lid, why in failed:
        print("  FAIL", lid, why)
    json.dump({"ok": [[l, list(s)] for l, s in ok], "failed": failed},
              open(os.path.join(os.path.dirname(LEVELS_JSON), "screenshots-report.json"), "w"),
              ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
