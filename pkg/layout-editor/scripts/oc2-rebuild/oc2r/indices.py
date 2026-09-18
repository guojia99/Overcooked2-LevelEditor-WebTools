"""四张权威索引。

1. PrefabIndex  —— dump_bundle/manifest.json 的 9486 个 GameObject 容器路径
                  给出 prefab 名 -> (bundle, bundle 内精确 assetPath)。
                  这是生成 PseudoPrefabSO 三元组的唯一权威来源，
                  取代上一版手写的 NAME_ALIASES 降级表。
2. ShapeIndex   —— AssetRipper 导出的 .prefab 的结构签名（根名 / 子节点名 / mesh guid），
                  用于同名多候选的消歧（实测 204 个歧义键）。
3. ScriptIndex  —— Scripts/**/*.cs.meta 的 guid -> 类名，用于按组件权威判定物件类型。
4. LibIndex     —— 本仓库 Assets/common* 素材库里已有的 PseudoPrefabSO 与 wrapper 预制体。
"""

import json
import os
import re

from . import paths
from . import yamlscene as Y

INDEX_DIR = os.path.join(paths.OUT, "index")

_GUID_RE = re.compile(r"guid: ([0-9a-f]{32})")


def _ensure_dir():
    os.makedirs(INDEX_DIR, exist_ok=True)


def _load(name):
    p = os.path.join(INDEX_DIR, name)
    if os.path.exists(p):
        with open(p, encoding="utf-8") as f:
            return json.load(f)
    return None


def _save(name, data):
    _ensure_dir()
    with open(os.path.join(INDEX_DIR, name), "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False)


def meta_guid(asset_path):
    mp = asset_path + ".meta"
    if not os.path.exists(mp):
        return ""
    try:
        with open(mp, encoding="utf-8", errors="replace") as f:
            head = f.read(400)
    except OSError:
        return ""
    m = _GUID_RE.search(head)
    return m.group(1) if m else ""


# --------------------------------------------------------------------------
# 1. manifest -> prefab 索引
# --------------------------------------------------------------------------


def build_prefab_index(force=False):
    cached = None if force else _load("prefabs.json")
    if cached:
        return cached

    with open(paths.DUMP_MANIFEST, encoding="utf-8") as f:
        man = json.load(f)

    # AssetRipper 导出里所有 .prefab 的相对路径（小写 -> 真实路径）
    ar_by_lower = {}
    ar_root = paths.AR_ASSETS
    for root, _dirs, files in os.walk(ar_root):
        for fn in files:
            if fn.endswith(".prefab"):
                full = os.path.join(root, fn)
                rel = os.path.relpath(full, ar_root).replace("\\", "/")
                ar_by_lower.setdefault(("assets/" + rel).lower(), rel)

    by_name = {}
    by_model = {}      # "目录/主名"(小写) -> [容器]，模型(fbx/obj)里的 GameObject
    by_material = {}   # "目录/主名"(小写) -> [容器]
    by_mesh = {}       # "目录/主名"(小写) -> [容器]
    mesh_count = {}    # 容器 -> 该容器内 Mesh 对象个数
    model_go_count = {}  # 容器 -> 该容器内 GameObject 个数
    for obj in man["objects"]:
        cont = obj.get("container") or ""
        if not cont:
            continue
        typ = obj.get("type")
        stem = os.path.splitext(os.path.basename(cont))[0].lower()
        dirn = os.path.dirname(cont).lower()
        key = dirn + "/" + stem
        if typ == "GameObject":
            if cont.endswith(".prefab"):
                base = os.path.basename(cont)[: -len(".prefab")]
                entry = {
                    "bundle": obj["bundle"],
                    "container": cont,
                    "ar": ar_by_lower.get(cont.lower(), ""),
                }
                lst = by_name.setdefault(base.lower(), [])
                if not any(e["container"] == cont and e["bundle"] == entry["bundle"] for e in lst):
                    lst.append(entry)
            else:
                lst = by_model.setdefault(key, [])
                e = {"bundle": obj["bundle"], "container": cont}
                if e not in lst:
                    lst.append(e)
                model_go_count[cont] = model_go_count.get(cont, 0) + 1
        elif typ == "Material":
            lst = by_material.setdefault(key, [])
            e = {"bundle": obj["bundle"], "container": cont}
            if e not in lst:
                lst.append(e)
        elif typ == "Mesh":
            lst = by_mesh.setdefault(key, [])
            e = {"bundle": obj["bundle"], "container": cont}
            if e not in lst:
                lst.append(e)
            mesh_count[cont] = mesh_count.get(cont, 0) + 1

    data = {
        "count": sum(len(v) for v in by_name.values()),
        "names": len(by_name),
        "byLowerName": by_name,
        "byModelKey": by_model,
        "byMaterialKey": by_material,
        "byMeshKey": by_mesh,
        "meshCount": mesh_count,
        "modelGoCount": model_go_count,
    }
    _save("prefabs.json", data)
    return data


def build_ar_guid_index(force=False):
    """AssetRipper 导出内 guid -> 相对路径（相对 ExportedProject/Assets）。

    场景里的 mesh / material 引用都是 AssetRipper 生成的 guid，
    必须先落到导出文件，再按 目录+主名 反查 bundle 容器。
    """
    cached = None if force else _load("ar_guids.json")
    if cached:
        return cached
    idx = {}
    ar_root = paths.AR_ASSETS
    for root, _dirs, files in os.walk(ar_root):
        for fn in files:
            if not fn.endswith(".meta"):
                continue
            p = os.path.join(root, fn)
            try:
                head = open(p, encoding="utf-8", errors="replace").read(300)
            except OSError:
                continue
            m = _GUID_RE.search(head)
            if not m:
                continue
            rel = os.path.relpath(p[: -len(".meta")], ar_root).replace("\\", "/")
            idx[m.group(1)] = rel
    data = {"count": len(idx), "byGuid": idx}
    _save("ar_guids.json", data)
    return data


# --------------------------------------------------------------------------
# 2. prefab 结构签名
# --------------------------------------------------------------------------


def prefab_shape(ar_rel):
    """读一个 AssetRipper .prefab，返回结构签名。"""
    full = os.path.join(paths.AR_ASSETS, ar_rel)
    if not os.path.exists(full):
        return None
    try:
        txt = open(full, encoding="utf-8", errors="replace").read()
    except OSError:
        return None
    docs = Y.split_docs(txt)
    names = []
    meshes = []
    mats = []
    scripts = []
    go_count = 0
    root_name = ""
    # 预制体里 m_Father == 0 的 Transform 即根
    tr_by_go = {}
    go_names = {}
    fathers = {}
    for d in docs:
        if d.cls == 1:
            go_count += 1
            nm = d.data.get("m_Name")
            go_names[d.anchor] = nm if isinstance(nm, str) else ""
        elif d.cls in (4, 224):
            dd = d.data
            g = Y.ref_fid(dd.get("m_GameObject"))
            tr_by_go[d.anchor] = g
            fathers[d.anchor] = Y.ref_fid(dd.get("m_Father"))
        elif d.cls == 33:
            meshes.append(Y.ref_guid(d.data.get("m_Mesh", {})))
        elif d.cls == 137:
            meshes.append(Y.ref_guid(d.data.get("m_Mesh", {})))
            mats.extend(_material_guids(d))
        elif d.cls == 23:
            mats.extend(_material_guids(d))
        elif d.cls == 114:
            g = Y.ref_guid(d.data.get("m_Script", {}))
            if g:
                scripts.append(g)
    for tr, fa in fathers.items():
        if fa == 0:
            root_name = go_names.get(tr_by_go.get(tr, 0), "")
            break
    names = sorted(v for v in go_names.values() if v)
    return {
        "root": root_name,
        "goCount": go_count,
        "names": names,
        "meshes": sorted(m for m in meshes if m),
        "mats": sorted(mats),
        "scripts": sorted(set(scripts)),
    }


def _material_guids(doc):
    out = []
    mats = doc.data.get("m_Materials")
    if isinstance(mats, list):
        for m in mats:
            g = Y.ref_guid(m) if isinstance(m, dict) else ""
            if g:
                out.append(g)
    return out


def build_shape_index(prefab_index=None, force=False):
    cached = None if force else _load("shapes.json")
    if cached:
        return cached
    prefab_index = prefab_index or build_prefab_index()
    want = set()
    for lst in prefab_index["byLowerName"].values():
        for e in lst:
            if e["ar"]:
                want.add(e["ar"])
    shapes = {}
    for rel in sorted(want):
        s = prefab_shape(rel)
        if s:
            shapes[rel] = s
    data = {"count": len(shapes), "shapes": shapes}
    _save("shapes.json", data)
    return data


# --------------------------------------------------------------------------
# 3. 脚本 guid -> 类名
# --------------------------------------------------------------------------


def build_script_index(force=False):
    cached = None if force else _load("scripts.json")
    if cached:
        return cached
    idx = {}
    sroot = paths.ar("Scripts")
    for root, _dirs, files in os.walk(sroot):
        for fn in files:
            if not fn.endswith(".cs.meta"):
                continue
            p = os.path.join(root, fn)
            try:
                head = open(p, encoding="utf-8", errors="replace").read(400)
            except OSError:
                continue
            m = _GUID_RE.search(head)
            if not m:
                continue
            rel = os.path.relpath(p, sroot).replace("\\", "/")
            idx[m.group(1)] = {
                "cls": fn[: -len(".cs.meta")],
                "ns": os.path.dirname(rel),
            }
    data = {"count": len(idx), "byGuid": idx}
    _save("scripts.json", data)
    return data


# --------------------------------------------------------------------------
# 4. 本仓库素材库
# --------------------------------------------------------------------------

_SO_FIELD = re.compile(r"^  (prefabName|bundleName|assetPath): (.*)$", re.M)
_PSEUDO_SO_REF = re.compile(r"pseudoPrefabSO: \{fileID: \d+, guid: ([0-9a-f]{32})")
_PSEUDO_SO_ARRAY = re.compile(r"pseudoPrefabSOs:\n((?:  - \{fileID: \d+, guid: [0-9a-f]{32}[^\n]*\n)+)")
_SO_SCRIPT_GUID = "0cff7c13895ab9e47a5e02d4619cc3b9"
RECIPE_SO_SCRIPT_GUID = "753d9e70603f6a140b05f30f176ec2dd"
PSEUDO_PREFAB_STUB_GUID = "0f66cc8b36034eb4c8eec31e1994e471"
PSEUDO_PREFAB_GUID = "d58b99f9c4313714e9c4b11f1534ae6f"


def parse_wrapper_prefab(path):
    """解析编辑器素材库里的 wrapper 预制体，取出实例化时要用的内部 fileID。

    Unity 2017 预制体：`!u!1001 Prefab` 里 m_RootGameObject 指向根 GameObject，
    场景实例通过 m_Modifications 对这些内部 fileID 打 override。
    """
    try:
        txt = open(path, encoding="utf-8", errors="replace").read()
    except OSError:
        return None
    docs = Y.split_docs(txt)
    by_anchor = {d.anchor: d for d in docs}
    root_go = 0
    for d in docs:
        if d.cls == 1001:
            root_go = Y.ref_fid(d.data.get("m_RootGameObject"))
            break
    if not root_go:
        # 没有 Prefab 文档（极少数），退回第一个无父 Transform
        for d in docs:
            if d.cls == 4 and Y.ref_fid(d.data.get("m_Father")) == 0:
                root_go = Y.ref_fid(d.data.get("m_GameObject"))
                break
    go_doc = by_anchor.get(root_go)
    if go_doc is None:
        return None
    info = {
        "rootGo": root_go,
        "rootTr": 0,
        "name": go_doc.data.get("m_Name") or "",
        "comps": {},        # 脚本 guid -> 组件 fileID
        "so": "",
        "soArray": [],
        "soArrayFid": 0,
    }
    comps = go_doc.data.get("m_Component")
    if isinstance(comps, list):
        for c in comps:
            fid = Y.ref_fid(c.get("component")) if isinstance(c, dict) else 0
            d = by_anchor.get(fid)
            if d is None:
                continue
            if d.cls == 4:
                info["rootTr"] = fid
            elif d.cls == 114:
                sg = Y.ref_guid(d.data.get("m_Script", {}))
                if sg:
                    info["comps"][sg] = fid
                    if sg == PSEUDO_PREFAB_STUB_GUID or "pseudoPrefabSO" in d.data:
                        ref = d.data.get("pseudoPrefabSO")
                        g = Y.ref_guid(ref) if isinstance(ref, dict) else ""
                        if g and not info["so"]:
                            info["so"] = g
                            info["soFid"] = fid
                    arr = d.data.get("pseudoPrefabSOs")
                    if isinstance(arr, list) and arr:
                        info["soArray"] = [Y.ref_guid(x) for x in arr if isinstance(x, dict)]
                        info["soArrayFid"] = fid
    if not info.get("soFid"):
        # stub 组件可能挂在子物体上（少见），全局再找一次
        for d in docs:
            if d.cls != 114:
                continue
            ref = d.data.get("pseudoPrefabSO")
            if isinstance(ref, dict) and Y.ref_guid(ref):
                info["so"] = Y.ref_guid(ref)
                info["soFid"] = d.anchor
                break
    return info


def build_lib_index(force=False):
    cached = None if force else _load("lib.json")
    if cached:
        return cached

    so = {}
    recipe_so = {}
    by_prefab_name = {}
    lib_roots = []
    for d in sorted(os.listdir(paths.ASSETS)):
        if d.startswith("common"):
            lib_roots.append(os.path.join(paths.ASSETS, d))

    for lib in lib_roots:
        for root, _dirs, files in os.walk(lib):
            for fn in files:
                if not fn.endswith(".asset"):
                    continue
                p = os.path.join(root, fn)
                try:
                    txt = open(p, encoding="utf-8", errors="replace").read()
                except OSError:
                    continue
                is_so = _SO_SCRIPT_GUID in txt
                is_recipe = RECIPE_SO_SCRIPT_GUID in txt
                if not is_so and not is_recipe:
                    continue
                fields = dict(_SO_FIELD.findall(txt))
                g = meta_guid(p)
                if not g:
                    continue
                rel = os.path.relpath(p, paths.REPO).replace("\\", "/")
                pn = (fields.get("prefabName") or "").strip()
                entry = {
                    "prefabName": pn,
                    "bundleName": (fields.get("bundleName") or "").strip(),
                    "assetPath": (fields.get("assetPath") or "").strip().replace("\\", "/"),
                    "path": rel,
                }
                if is_recipe:
                    recipe_so[g] = entry
                    continue
                so[g] = entry
                if pn:
                    by_prefab_name.setdefault(pn.lower(), []).append(g)

    # wrapper 预制体：哪些 prefab 的 PseudoPrefabStub 指向了某个 SO，
    # 以及实例化时需要用到的内部 fileID（根 GameObject / 根 Transform / 各 stub 组件）
    wrappers = {}
    wrapper_by_so = {}
    for lib in lib_roots:
        for root, _dirs, files in os.walk(lib):
            for fn in files:
                if not fn.endswith(".prefab"):
                    continue
                p = os.path.join(root, fn)
                rel = os.path.relpath(p, paths.REPO).replace("\\", "/")
                info = parse_wrapper_prefab(p)
                if info is None:
                    continue
                info["guid"] = meta_guid(p)
                info["id"] = fn[: -len(".prefab")]
                wrappers[rel] = info
                if info.get("so"):
                    wrapper_by_so.setdefault(info["so"], []).append(rel)

    catalog = []
    cat_dir = paths.repo("layout-editor/web/public/catalog")
    if os.path.isdir(cat_dir):
        for fn in sorted(os.listdir(cat_dir)):
            if fn.startswith("items.") and fn.endswith(".json"):
                with open(os.path.join(cat_dir, fn), encoding="utf-8") as f:
                    catalog.extend(json.load(f).get("items", []))

    appearances = {}
    ap = paths.repo("layout-editor/web/public/counter-appearances.json")
    if os.path.exists(ap):
        with open(ap, encoding="utf-8") as f:
            appearances = json.load(f)

    data = {
        "soCount": len(so),
        "so": so,
        "recipeSo": recipe_so,
        "byPrefabName": by_prefab_name,
        "wrappers": wrappers,
        "wrapperBySo": wrapper_by_so,
        "catalog": catalog,
        "appearances": appearances,
    }
    _save("lib.json", data)
    return data


def build_all(force=False):
    pi = build_prefab_index(force)
    si = build_shape_index(pi, force)
    sc = build_script_index(force)
    li = build_lib_index(force)
    ag = build_ar_guid_index(force)
    return pi, si, sc, li, ag
