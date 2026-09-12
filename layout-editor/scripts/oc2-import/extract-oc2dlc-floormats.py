#!/usr/bin/env python3
"""
extract-oc2dlc-floormats.py — 提取 oc2_dlc_story 源场景引用、工程缺失的 DLC 地板
材质到 commonW2/materials + commonW2/textures，并把中文翻译写入 names-dictionary.json。

数据链路（每材质）：
  dump_bundle/manifest.json 定位 (bundle, container)
    → UnityPy 读 Material typetree 的 _DiffuseMap/_MainTex PPtr（含 scale）
    → 纹理（同 bundle 或 externals→cab→bundle，复用 .cache/cabmap.json）解码 PNG
    → Assets/commonW2/textures/<tex>.png（TEX_META，guid=md5("commonw2-floortex:<tex>")）
    → Assets/commonW2/materials/<id>.mat（MAT_TEMPLATE 双绑 _DiffuseMap/_MainTex，
       guid=md5("commonw2-floormat:<id>")，sx/sy 取源材质 tiling）
    → names-dictionary.json 追加 {id, zh, en}（TARGETS 人工校对表）

筛选口径：87 个源场景地板类物件实际引用、且工程（LevelSets/common01/02/W1 materials）
缺失的 66 种中，排除非地板贴图（Default_Material、lambert1_0、城墙/门/井盖/通风口/
楼梯/植物/摊位/robin 本体）后的 ~54 种。幂等可重跑。

用法：python3 layout-editor/scripts/oc2-import/extract-oc2dlc-floormats.py
"""
import hashlib
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
WIN = os.path.join(ROOT, "Assets", "StreamingAssets", "Windows")
W2 = os.path.join(ROOT, "Assets", "commonW2")
OUT_MAT = os.path.join(W2, "materials")
OUT_TEX = os.path.join(W2, "textures")
MANIFEST = os.path.join(ROOT, "dump_bundle", "manifest.json")
NAMES_DICT = os.path.join(ROOT, "layout-editor", "scripts", "data", "names-dictionary.json")
CABMAP = os.path.join(HERE, ".cache", "cabmap.json")
REPORT = os.path.join(HERE, "out", "floormats-report.json")

try:
    import UnityPy
except ImportError:
    sys.exit("ERROR: UnityPy not installed. Run: pip install UnityPy Pillow")

# ---------------------------------------------------------------- 目标清单（人工校对）
# id -> (zh, en)
TARGETS = {
    "mat_old_tiles": ("旧版厨房地砖", "old kitchen tiles"),
    "mat_dlc5_city_assets_01": ("露营·城市地砖 01", "camping city tiles 01"),
    "mat_dlc4_grass_cards_01": ("火锅·草地贴片", "hotpot grass cards"),
    "mat_dlc4_dressingassets_02": ("火锅·苔藓贴片", "hotpot moss patch"),
    "mat_dlc07_city_assets_01": ("部落·城市地砖 01", "horde city tiles 01"),
    "mat_dlc07_city_assets_02": ("部落·城市地砖 02", "horde city tiles 02"),
    "mat_dlc5_grass_cards_02": ("露营·草地贴片", "camping grass cards"),
    "mat_dlc07_grass_01": ("部落·草地 01", "horde grass 01"),
    "mat_dlc2_sand_decal_01": ("海滩·沙地贴花 01", "beach sand decal 01"),
    "mat_dlc2_sand_decal_03": ("海滩·沙地贴花 03", "beach sand decal 03"),
    "dlc11_city_path_side_01": ("夏日·小径侧边 01", "summer path side 01"),
    "mat_dlc4_mud_01": ("火锅·泥地 01", "hotpot mud 01"),
    "mat_dlc4_mud_02": ("火锅·泥地 02", "hotpot mud 02"),
    "mat_dlc4_mud_03": ("火锅·泥地 03", "hotpot mud 03"),
    "mat_dlc4_grass_01": ("火锅·草地 01", "hotpot grass 01"),
    "mat_dlc4_grass_02": ("火锅·草地 02", "hotpot grass 02"),
    "mat_dlc4_grass_03": ("火锅·草地 03", "hotpot grass 03"),
    "mat_dlc5_dressing_assets_02": ("露营·装饰贴片 02", "camping dressing patch 02"),
    "mat_dlc2_purple_carpet_01": ("海滩·紫色地毯", "beach purple carpet"),
    # mat_dlc3_snow_01：纯 shader 颜色（sparkle/goo/波纹），无反照率贴图，不做 Standard 材质
    # mat_dlc9_snow_02：纯 shader 颜色（sparkle/goo/波纹），无反照率贴图，不做 Standard 材质
    "mat_dlc2_shoreline_animatoncontrol_wet_sand": ("海滩·水线湿沙（动画）", "beach shoreline wet sand (animated)"),
    "mat_dlc09_grass_01": ("仙境·草地 01", "wonderland grass 01"),
    "mat_dlc07_keep_mud_01": ("部落·城堡泥地", "horde keep mud"),
    "mat_dlc4_crazypaving_01": ("火锅·疯狂铺路", "hotpot crazy paving"),
    "dlc11_city_path_corner_01": ("夏日·小径转角 01", "summer path corner 01"),
    "mat_dlc3_water_01": ("冬季·水面 01", "winter water 01"),
    "mat_dlc13_mud_01": ("中秋·泥地 01", "moonfestival mud 01"),
    "mat_dlc2_sand_01": ("海滩·沙地 01", "beach sand 01"),
    "mat_dlc2_sand_05": ("海滩·沙地 05", "beach sand 05"),
    "mat_dlc2_sand_07": ("海滩·沙地 07", "beach sand 07"),
    "mat_dlc2_sand_09": ("海滩·沙地 09", "beach sand 09"),
    "mat_dlc2_sand_09b": ("海滩·沙地 09b", "beach sand 09b"),
    "mat_dlc2_sand_10": ("海滩·沙地 10", "beach sand 10"),
    "mat_dlc2_sand_11": ("海滩·沙地 11", "beach sand 11"),
    "mat_dlc2_sand_18": ("海滩·沙地 18", "beach sand 18"),
    "dlc11_mat_city_assets_01": ("夏日·城市地砖 01", "summer city tiles 01"),
    "mat_dlc07_courtyard_water_01": ("部落·庭院水面", "horde courtyard water"),
    "t_dlc07_battlements_grass_01": ("部落·堡垒草地", "horde battlements grass"),
    "mat_dlc2_poolwater_02": ("海滩·池水 02", "beach pool water 02"),
    "mat_dlc2_shoreline_02_wet_sand": ("海滩·水线湿沙 02", "beach shoreline wet sand 02"),
    "mat_dlc2_firepit_04": ("海滩·火坑地砖 04", "beach firepit 04"),
    "mat_dlc4_water_01": ("火锅·水面 01", "hotpot water 01"),
    # mat_dlc07_goo_01：纯 shader 颜色（sparkle/goo/波纹），无反照率贴图，不做 Standard 材质
    # mat_dlc4_ripples_02：纯 shader 颜色（sparkle/goo/波纹），无反照率贴图，不做 Standard 材质
    "mat_dlc2_river_bottom02": ("海滩·河底 02", "beach river bottom 02"),
    "mat_dlc2_river_water_01": ("海滩·河水 01", "beach river water 01"),
    "mat_dlc3_cobble_01": ("冬季·鹅卵石", "winter cobblestone"),
    "mat_dlc5_water_01": ("露营·水面 01", "camping water 01"),
    "mat_dlc5_cliff_01": ("露营·崖壁 01", "camping cliff 01"),
    "t_dlc07_courtyard_grass_01": ("部落·庭院草地", "horde courtyard grass"),
    "mat_dlc13_grass_01": ("中秋·草地 01", "moonfestival grass 01"),
    "dlc11_city_path_side_objectspace": ("夏日·小径侧边（对象空间）", "summer path side (object space)"),
    "dlc11_city_path_corner_objectspace": ("夏日·小径转角（对象空间）", "summer path corner (object space)"),
}

MAT_TEMPLATE = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 6
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {{fileID: 0}}
  m_PrefabInternal: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 7, guid: 0000000000000000f000000000000000, type: 0}}
  m_ShaderKeywords:
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BumpMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _DetailAlbedoMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _DetailMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _DetailMask:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _DetailNormalMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _DiffuseMap:
        m_Texture: {{fileID: 2800000, guid: {tex_guid}, type: 3}}
        m_Scale: {{x: {sx}, y: {sy}}}
        m_Offset: {{x: 0, y: 0}}
    - _EmissionMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _MainTex:
        m_Texture: {{fileID: 2800000, guid: {tex_guid}, type: 3}}
        m_Scale: {{x: {sx}, y: {sy}}}
        m_Offset: {{x: 0, y: 0}}
    - _MetallicGlossMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _OcclusionMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _ParallaxMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _SpecGlossMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Floats:
    - _Brightness: 0
    - _BumpScale: 1
    - _Cutoff: 0.5
    - _DetailNormalMapScale: 1
    - _DetailPreview: 1
    - _DiffusePreview: 1
    - _DstBlend: 0
    - _GlossMapScale: 1
    - _Glossiness: 0.5
    - _GlossyReflections: 1
    - _Metallic: 0.4
    - _Mode: 0
    - _OcclusionStrength: 1
    - _Parallax: 0.02
    - _PreviewAO: 1
    - _PreviewEmission: 0
    - _ReflectionCubemap_Preivew: 0
    - _RimIntensity: 1.3
    - _RimPower: 1
    - _RimPreview: 0
    - _Roughness: 0.3
    - _ScreenspaceDetail: 0
    - _SmoothnessTextureChannel: 0
    - _SpecularHighlights: 1
    - _SrcBlend: 1
    - _UVSec: 0
    - _UseMetallicTexture: 0
    - _UseMetallicTexture_copy: 0
    - _UseRoughnessTexture: 1
    - _WorldUVMultipllier: 0.35
    - _WorldUVRotator: 0.66
    - _ZWrite: 1
    m_Colors:
    - _Color: {{r: 0.8602941, g: 0.8602941, b: 0.8602941, a: 1}}
    - _EmissionColor: {{r: 0, g: 0, b: 0, a: 1}}
    - _EmissionColour: {{r: 1, g: 1, b: 1, a: 1}}
    - _MaskColor: {{r: 0.5019608, g: 0.5019608, b: 0.5019608, a: 1}}
    - _highlight_colour: {{r: 0.9117647, g: 0.8719588, b: 0.7843858, a: 1}}
"""

MAT_META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 2100000
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName: commonW2
  assetBundleVariant:
"""


def det_guid(prefix, key):
    return hashlib.md5(f"{prefix}:{key}".encode("utf-8")).hexdigest()


def env_pairs(tt):
    """m_TexEnvs entries: (name, dict) 元组或 {first, second} 字典两种形态。"""
    for entry in tt or []:
        if isinstance(entry, (list, tuple)):
            yield entry[0], entry[1]
        elif isinstance(entry, dict):
            yield entry.get("first"), entry.get("second")


def cab_of(p):
    m = re.search(r"(cab-[0-9a-f]{32})", p or "", re.I)
    return m.group(1).lower() if m else None


def load_bundle(name):
    return UnityPy.load(os.path.join(WIN, name))


def build_mat_index():
    mf = json.load(open(MANIFEST))
    idx = {}
    for o in mf["objects"]:
        c = o.get("container") or ""
        if o.get("type") == "Material" and c.endswith(".mat"):
            idx.setdefault(os.path.basename(c)[:-4].lower(), (o["bundle"], c))
    return idx


def find_material(env, container):
    """env.container[name] 返回 PPtr -> 按 pathID 换 ObjectReader。"""
    try:
        pptr = env.container[container]
    except Exception:
        return None
    pid = getattr(pptr, "m_PathID", None)
    if not pid:
        return None
    for o in env.objects:
        if o.path_id == pid:
            return o
    return None


def resolve_texture(pptr, env, obj, cabmap, cache):
    fid, pid = pptr.get("m_FileID", 0), pptr.get("m_PathID", 0)
    if not pid:
        return None
    if fid == 0:
        return env.objects and {o.path_id: o for o in env.objects}.get(pid)
    try:
        exts = [e.path for e in obj.assets_file.externals]
    except Exception:
        return None
    if fid - 1 >= len(exts):
        return None
    cab = cab_of(exts[fid - 1])
    bundle = cabmap.get(cab) if cab else None
    if not bundle:
        return None
    if bundle not in cache:
        e2 = load_bundle(bundle)
        cache[bundle] = {o.path_id: o for o in e2.objects}
    return cache[bundle].get(pid)


def main():
    mat_index = build_mat_index()
    cabmap = json.load(open(CABMAP)) if os.path.exists(CABMAP) else {}
    os.makedirs(OUT_MAT, exist_ok=True)
    os.makedirs(OUT_TEX, exist_ok=True)
    for rel in ("materials", "textures"):
        mp = os.path.join(W2, rel + ".meta")
        if not os.path.exists(mp):
            open(mp, "w").write(FOLDER_META_TEMPLATE.format(
                guid=det_guid("commonw2-folder", rel)))

    env_cache = {}
    obj_cache = {}
    done, failed = [], []
    for mid, (zh, en) in TARGETS.items():
        mat_path = os.path.join(OUT_MAT, mid + ".mat")
        loc = mat_index.get(mid.lower())
        if not loc:
            failed.append((mid, "manifest 无 .mat 容器"))
            continue
        bundle, container = loc
        try:
            if bundle not in env_cache:
                env_cache[bundle] = load_bundle(bundle)
            env = env_cache[bundle]
            obj = find_material(env, container)
            if obj is None:
                failed.append((mid, "bundle 内未找到材质对象"))
                continue
            tt = obj.read_typetree()
            props = tt.get("m_SavedProperties", {})
            texenvs = dict(env_pairs(props.get("m_TexEnvs")))
            # 漫反射槽优先级：Standard 系（_DiffuseMap/_MainTex）→ DLC PBR 系
            # （_ColourAlpha=反照率、_Detail=沙地/草地细节色、_sea_texture=海面、
            #  _Diffuse_Map=水面）；_Normal/_RMEAO/_Noise 等非颜色槽不取。
            for slot_name in ("_DiffuseMap", "_MainTex", "_Albedo", "_Diffuse",
                              "_Diffuse_Map", "_ColourAlpha", "_Detail", "_sea_texture"):
                slot = texenvs.get(slot_name)
                if slot and (slot.get("m_Texture") or {}).get("m_PathID"):
                    break
                slot = None
            if not slot:
                failed.append((mid, "无漫反射贴图槽"))
                continue
            pptr = slot.get("m_Texture") or {}
            tobj = resolve_texture(pptr, env, obj, cabmap, obj_cache)
            if tobj is None:
                failed.append((mid, "纹理解析失败"))
                continue
            tdata = tobj.read()
            tex_name = getattr(tdata, "m_Name", None) or (mid + "_d")
            scale = slot.get("m_Scale") or {}
            sx, sy = scale.get("x", 1) or 1, scale.get("y", 1) or 1

            tex_path = os.path.join(OUT_TEX, tex_name + ".png")
            tex_guid = det_guid("commonw2-floortex", tex_name)
            if not os.path.exists(tex_path):
                tdata.image.save(tex_path)
                open(tex_path + ".meta", "w").write(
                    open(os.path.join(HERE, "data", "tex-meta-template.meta")).read()
                    .replace("__GUID__", tex_guid))
            if not os.path.exists(mat_path):
                open(mat_path, "w").write(MAT_TEMPLATE.format(
                    name=mid, tex_guid=tex_guid,
                    sx=repr(float(sx)), sy=repr(float(sy))))
                open(mat_path + ".meta", "w").write(
                    MAT_META_TEMPLATE.format(guid=det_guid("commonw2-floormat", mid)))
            done.append({"id": mid, "zh": zh, "en": en, "bundle": bundle,
                         "texture": tex_name, "scale": [sx, sy]})
        except Exception as ex:
            failed.append((mid, repr(ex)[:120]))

    # 翻译写入 names-dictionary.json（append missing）
    names_doc = json.load(open(NAMES_DICT, encoding="utf-8"))
    have = {n["id"] for n in names_doc["names"]}
    added = 0
    for mid, (zh, en) in TARGETS.items():
        if mid in have:
            continue
        names_doc["names"].append({"id": mid, "zh": zh, "en": en})
        added += 1
    if added:
        names_doc["names"].sort(key=lambda n: n["id"])
        json.dump(names_doc, open(NAMES_DICT, "w", encoding="utf-8"),
                  ensure_ascii=False, indent=2)
        open(NAMES_DICT, "a", encoding="utf-8").write("\n")

    os.makedirs(os.path.dirname(REPORT), exist_ok=True)
    json.dump({"done": done, "failed": failed, "namesAdded": added},
              open(REPORT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print("提取成功:", len(done), "失败:", len(failed), "翻译新增:", added)
    for mid, why in failed:
        print("  FAIL", mid, why)


if __name__ == "__main__":
    main()
