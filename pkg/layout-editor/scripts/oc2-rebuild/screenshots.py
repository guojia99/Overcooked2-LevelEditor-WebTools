"""关卡截图提取（阶段 3）。

与上一版的差别：
  * **优先 LoadScreenOverride**，它才是一关一张的加载画面；
    上一版优先 SceneVarients[].Screenshot，而 DLC09 五关的该指针指向同一张通用图，
    导致 87 张截图只有 83 张唯一。
  * 目录 entry 按 DLC 归属选取，避免 combineddlc 这类混合目录里同名场景串关。
  * 落盘后做 md5 唯一性校验，重复即报错。

用法：
    python3 screenshots.py --set dlc
    python3 screenshots.py --set base --apply     # 同时回填 LevelInfo.screenshot
"""

import argparse
import hashlib
import io
import json
import os
import re
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from oc2r import levels as LV
from oc2r import paths

try:
    import UnityPy
except ImportError:
    sys.exit("需要 UnityPy：pip install UnityPy Pillow")

# DLC -> 场景目录资产所在 bundle
DIRECTORY_BUNDLES = {
    "dlc02": "bundle161", "dlc03": "bundle208", "dlc04": "bundle224",
    "dlc05": "bundle247", "dlc07": "bundle293", "dlc08": "bundle354",
    "dlc09": "bundle404", "dlc10": "bundle222", "dlc11": "bundle222",
    "dlc13": "bundle222", "base": "bundle18",
}

CAB_RE = re.compile(r"(cab-[0-9a-f]{32})", re.I)


def det_guid(set_name, level_id):
    return hashlib.md5(("oc2rebuild-screenshot:%s:%s" % (set_name, level_id)).encode()).hexdigest()


def build_cabmap():
    p = paths.cache("cabmap.json")
    if os.path.exists(p):
        with open(p, encoding="utf-8") as f:
            return json.load(f)
    m = {}
    files = sorted((f for f in os.listdir(paths.STREAMING) if re.fullmatch(r"bundle\d+", f)),
                   key=lambda s: int(s[6:]))
    for bf in files:
        try:
            env = UnityPy.load(os.path.join(paths.STREAMING, bf))
            cabs = set()
            for f in env.files.values():
                name = getattr(f, "name", None)
                if name:
                    cabs.add(name)
                for inner in (getattr(f, "files", None) or {}):
                    cabs.add(inner)
            for c in cabs:
                mm = CAB_RE.search(c)
                if mm:
                    m.setdefault(mm.group(1).lower(), bf)
        except Exception as e:  # noqa: BLE001 - bundle 解析失败只影响兜底
            print("  ! 扫描失败", bf, e)
    with open(p, "w", encoding="utf-8") as f:
        json.dump(m, f)
    return m


_env_cache = {}
_obj_cache = {}


def load_bundle(name):
    if name not in _env_cache:
        _env_cache[name] = UnityPy.load(os.path.join(paths.STREAMING, name))
    return _env_cache[name]


def bundle_objects(name):
    """bundle 内 pathID -> object。

    env.files 顶层是 BundleFile，真正带 objects 的是它内部的 SerializedFile，
    直接用 env.objects 遍历最稳。
    """
    if name not in _obj_cache:
        env = load_bundle(name)
        idx = {}
        for o in env.objects:
            pid = getattr(o, "path_id", None)
            if pid is not None:
                idx[pid] = o
        _obj_cache[name] = idx
    return _obj_cache[name]


def read_directories(bundle_name):
    """返回 [(资产名, entries, 所属对象)]。

    一个 bundle 里可能有多份场景目录（本体 bundle18 同时装着本体目录和
    overcooked_legacy 的 DLC 目录），必须全部读出来再按场景名挑，
    不能取"第一个带 Scenes 的"。
    """
    env = load_bundle(bundle_name)
    out = []
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            tt = obj.read_typetree()
        except Exception:  # noqa: BLE001
            continue
        scenes = tt.get("Scenes")
        if not isinstance(scenes, list) or not scenes:
            continue
        entries = []
        for e in scenes:
            names = set()
            shot = None
            for v in (e.get("SceneVarients") or []):
                sn = v.get("SceneName")
                if sn:
                    names.add(sn)
                if shot is None:
                    p = v.get("Screenshot") or {}
                    if p.get("m_PathID"):
                        shot = (p.get("m_FileID", 0), p["m_PathID"])
            lso = None
            p = e.get("LoadScreenOverride") or {}
            if p.get("m_PathID"):
                lso = (p.get("m_FileID", 0), p["m_PathID"])
            entries.append({"names": names, "shot": shot, "lso": lso,
                            "label": e.get("Label")})
        out.append((tt.get("m_Name") or "", entries, obj))
    return out


def resolve_pptr(src_obj, bundle_name, ptr, cabmap):
    """把 (m_FileID, m_PathID) 解析成具体对象。"""
    fid, pid = ptr
    assetsfile = src_obj.assets_file
    if fid == 0:
        o = assetsfile.objects.get(pid)
        return o
    externals = getattr(assetsfile, "externals", None) or []
    if fid - 1 >= len(externals):
        return None
    ext = externals[fid - 1]
    ref = getattr(ext, "path", "") or getattr(ext, "name", "")
    m = CAB_RE.search(ref or "")
    if not m:
        return None
    target_bundle = cabmap.get(m.group(1).lower())
    if not target_bundle:
        return None
    return bundle_objects(target_bundle).get(pid)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--set", default="dlc", choices=["dlc", "base", "all"])
    ap.add_argument("--apply", action="store_true", help="回填 LevelInfo.screenshot")
    ap.add_argument("--out", default="screenshots.json")
    args = ap.parse_args()

    cabmap = build_cabmap()
    lvs = []
    if args.set in ("dlc", "all"):
        lvs += LV.dlc_levels()
    if args.set in ("base", "all"):
        lvs += LV.base_levels()

    dirs = {}
    recs = []
    for lv in lvs:
        dlc = (lv.get("dlc") or "base").lower()
        bundle = DIRECTORY_BUNDLES.get(dlc)
        if not bundle:
            recs.append({"id": lv["id"], "ok": False, "error": "未知 DLC:" + dlc})
            continue
        if bundle not in dirs:
            dirs[bundle] = read_directories(bundle)
        scene = lv.get("sceneName")
        hit = None
        src_obj = None
        # 优先名字里带 coop 的目录，其次任何包含该场景的目录
        cands = sorted(dirs[bundle],
                       key=lambda d: (0 if "coop" in (d[0] or "").lower() else 1, d[0]))
        for name, entries, obj in cands:
            for e in entries:
                if scene in e["names"]:
                    hit, src_obj = e, obj
                    break
            if hit:
                break
        if hit is None:
            recs.append({"id": lv["id"], "ok": False, "error": "目录里找不到场景 " + str(scene)})
            continue
        ptr = hit["lso"] or hit["shot"]
        src = "lso" if hit["lso"] else "shot"
        if ptr is None:
            recs.append({"id": lv["id"], "ok": False, "error": "两个指针都空"})
            continue
        obj = resolve_pptr(src_obj, bundle, ptr, cabmap)
        if obj is None:
            recs.append({"id": lv["id"], "ok": False, "error": "指针无法解析 %s" % (ptr,)})
            continue
        try:
            data = obj.read()
            img = data.image
        except Exception as e:  # noqa: BLE001
            recs.append({"id": lv["id"], "ok": False, "error": "读图失败 %s" % e})
            continue
        if img is None:
            recs.append({"id": lv["id"], "ok": False, "error": "对象不是图片"})
            continue

        buf = io.BytesIO()
        img.save(buf, format="PNG")
        raw = buf.getvalue()
        rec = {"id": lv["id"], "set": lv["set"], "ok": True, "source": src,
               "ptr": list(ptr), "size": list(img.size),
               "md5": hashlib.md5(raw).hexdigest(),
               "label": hit.get("label")}
        if args.apply:
            rec["written"] = write_screenshot(lv, raw)
        recs.append(rec)
        print("%-22s %-4s %sx%s %s" % (lv["id"], src, img.size[0], img.size[1], rec["md5"][:8]))

    ok = [r for r in recs if r.get("ok")]
    md5s = Counter(r["md5"] for r in ok)
    dup = {k: v for k, v in md5s.items() if v > 1}
    print("\n成功 %d / 失败 %d；唯一图 %d" % (len(ok), len(recs) - len(ok), len(md5s)))
    if dup:
        print("!! 仍有重复图 %d 组：" % len(dup))
        for k, v in dup.items():
            print("   ", [r["id"] for r in ok if r["md5"] == k])
    else:
        print("所有截图两两不同 ✓")
    for r in recs:
        if not r.get("ok"):
            print("   失败", r["id"], r.get("error"))

    p = paths.out(args.out)
    with open(p, "w", encoding="utf-8") as f:
        json.dump(recs, f, ensure_ascii=False, indent=1)
    print("报告:", p)
    return 0 if not dup and len(ok) == len(recs) else 1


SCREENSHOT_META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  fileIDToRecycleName:
    21300000: {name}
  serializedVersion: 4
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: -1
    aniso: -1
    mipBias: -1
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  platformSettings:
  - buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 
    vertices: []
    indices: 
    edges: []
    weights: []
  spritePackingTag: 
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def write_screenshot(lv, raw):
    """写图。已有 .meta 的一律沿用原 guid（guid 纪律：生成过的 meta 不重造）。"""
    d = os.path.join(paths.LEVELSETS, lv["set"], "data", lv["id"])
    if not os.path.isdir(d):
        return ""
    png = os.path.join(d, "screenshot.png")
    meta = png + ".meta"
    g = ""
    if os.path.exists(meta):
        m = re.search(r"guid: ([0-9a-f]{32})",
                      open(meta, encoding="utf-8", errors="replace").read(400))
        g = m.group(1) if m else ""
    fresh = not g
    if fresh:
        g = det_guid(lv["set"], lv["id"])
    with open(png, "wb") as f:
        f.write(raw)
    if fresh:
        with open(meta, "w", encoding="utf-8") as f:
            f.write(SCREENSHOT_META.format(guid=g, name="screenshot"))
    info = os.path.join(d, "LevelInfo_%s.asset" % lv["id"])
    if os.path.exists(info):
        txt = open(info, encoding="utf-8").read()
        new = re.sub(r"screenshot: \{[^}]*\}",
                     "screenshot: {fileID: 21300000, guid: %s, type: 3}" % g, txt, count=1)
        if new != txt:
            with open(info, "w", encoding="utf-8") as f:
                f.write(new)
    return os.path.relpath(png, paths.REPO)


if __name__ == "__main__":
    sys.exit(main())
