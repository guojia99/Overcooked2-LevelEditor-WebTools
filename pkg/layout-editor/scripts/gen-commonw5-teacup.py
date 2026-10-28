#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""commonW5 茶杯洗涤链路 生成器（脏茶杯 / 脏茶杯堆 / 洗茶杯水槽 / 茶杯回收台）。

素材来源：backup_rebuild DLC100（5 周年）+ DLC02（污渍层）AssetRipper 导出。
贴图 1:1 拷贝（保留原 guid）；网格 sv10 packed → OBJ（mesh_to_obj.py）；
新增资产用确定性 guid md5("commonw5-teacup:<kind>:<id>")。

 prefab 结构依据：
  - web_teacup_dirty        ← Ripped GameObject/DLC100_DirtyMug.prefab（茶杯 + 污渍层）
  - web_teacup_stack_dirty  ← common04 custom_utensil_dirty_plate_stack 克隆
                              （字段对齐 Ripped DLC100_DirtyMugStack：heightOffset 0.32、
                              plateType=本地 Mug 装盘步、washed=web_teacup_stack_clean）
  - web_sink_teacup         ← common04 custom_counter_sink 克隆 + s_birthday_1_3 场景
                              workstation_sink_Mug_DLC100 的网格/材质/布局
  - web_teacup_return       ← s_birthday_1_3 场景 workstation_mug_return 结构重建

用法：
  python3 gen-commonw5-teacup.py [--apply]   # 默认 dry-run；--apply 落盘（已存在跳过）
"""
import importlib.util
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

# 复用 gen-commonw5-kettle.py 的全部 helper（det_guid / folder_guid / write_file /
# write_meta / meta_text_for / mesh_to_obj 等）。该模块 main 有 __main__ 守卫。
_spec = importlib.util.spec_from_file_location(
    "gen_commonw5_kettle", os.path.join(HERE, "gen-commonw5-kettle.py"))
K = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(K)

REPO = K.REPO
RIPPED = K.RIPPED
W5 = K.W5

APPLY = "--apply" in sys.argv
K.APPLY = APPLY
report = K.report


def out(msg):
    K.out(msg)


def det_guid(kind, ident):
    import hashlib
    return hashlib.md5(("commonw5-teacup:%s:%s" % (kind, ident)).encode()).hexdigest()


# ---- 既有资产 guid --------------------------------------------------------
G_MESH_CUP = "c85e88f4994b00c32274cb6cc2dba16f"        # kettle/meshes/m_dlc100_teacup_cup.obj
G_MAT_TEACUP = "8710468eee4c4a07674c0edd41e4da4a"      # kettle/materials/mat_web_teacup.mat
G_MAT_COUNTERTOP = "66e64e98"                          # 占位，启动时从 meta 实测
G_SHADER_LIQUID = "33647354e37e8c49afc95fc407974737"   # kettle/shaders/WebLiquid.shader
G_PREFAB_TEACUP = "d1909c74491f43393585dbc25f18c2a6"   # prefabs/teacups/web_teacup.prefab
FID_TEACUP_ROOT = "1003597553421588"
G_PREFAB_STACK_CLEAN = "f3421c20e5e1eb1c4e90d5483bb631ba"  # web_teacup_stack_clean.prefab
FID_STACK_CLEAN_ROOT = "1760872858961958"
G_MUG_PLATING_LOCAL = "eb494a42c3217e146b9259e5f46e5b82"   # common04 Mug PlatingStepData

# common04/_supplies SO（SetupCustomUtensilStub 字段，与 web_teacup 一致）
SO_CONTENT_UI = "43bf068adba247a499675d6d023357fe"
SO_SLUDGE = "a66eb85e308e2b94093d95ba63736c7e"
SO_SPAWN_FX = "6a6c8e08f195b6047a1ec71e8c145a2a"
SO_PROGRESS_UI = "7dff3ebe681a47a4d8503f9ba52b29f4"

# 脚本 guid（Assets/Scripts 宿主 + LevelEditorStub）
SC = {
    "SetupCustomUtensil": "51362215738f92c4da1359d0ba9dce25",
    "PhysicalAttachment": "844e59d8b300f2d5f70fb0ad901e7e4c",
    "CarryableItem": "3a772ee3a0472f1f81c3c934c69940b2",
    "HandlePickupReferral": "5aa99bc248e9dbafc1914a11429b1218",
    "HandlePlacementReferral": "e50807449ce73271325a626b40d5a114",
    "PlacementLayerSwapper": "b03c429c855ed816dd2149d92e1cc057",
    "PlacementCollisionSwapper": "ab91a6d841ff5b19ec46d1bdff85d804",
    "TeleportableItem": "936c4354040669c8f43c18d0f845c235",
    "SpecificPseudoPrefabTag": "2b19728b6e0dfe44baf2f80df9bfc1e9",
    "SetupCustomUtensilStub": "dbcf6aa44a7b711418836b87c6cae192",
    "DummySynchroniser": "df34f7694f2277e854a6a6c09579bbfb",
    "RendererSceneInfo": "29e0d27afe333a64e59860dd9c026821",
    "PlateReturnStation": "f0e58dbd6229013dc940caea019dee51",
    "AttachStation": "0f5cdfb20cbb935c3622d838613aea07",
    "AnticipateInteractionHighlight": "bd19f5e37ff22621b2d51869be523c3e",
    "StaticGridLocation": "0bd487eae7bb6aa54e539e1dbab88f36",
}

# ---- 网格转换（Ripped YAML Mesh → OBJ）------------------------------------
# 水槽为 2 子网格（原木 + 周年台面）：OBJ 导入器只认 usemtl 分子网格，g 段会被
# 合并成 1 个子网格 → 拆成两个 obj、两个 MeshRenderer（同 kettle M_Kettle+M_Cooker
# 双网格先例）。子网格 0=原木(mat_sk_sink_01_wood)、1=台面(mat_Countertop_Anniversary)。
MESH_CONV = [
    ("Models/Shared Kitchen/Meshes/m_sk_plate_return_01.asset",
     "teacup/meshes/m_sk_plate_return_01.obj"),
    ("DownloadableContent/DLC02/DLC_Assets/Models/Shared Kitchen/m_dlc2_glassdirt_01.asset",
     "teacup/meshes/m_dlc2_glassdirt_01.obj"),
]
SINK_MESH_SRC = "Mesh/m_sk_sink_DLC100_0.asset"
SINK_OBJ_WOOD = "teacup/meshes/m_sk_sink_dlc100_wood.obj"
SINK_OBJ_TOP = "teacup/meshes/m_sk_sink_dlc100_top.obj"

# ---- 贴图 1:1 拷贝（保留原 guid；meta 重写为 2017 格式）--------------------
TEX_CONV = [
    ("Models/Shared Kitchen/Meshes/Textures/512/t_sk_sink_01_d.png",
     "teacup/textures/t_sk_sink_01_d.png", "87585dc824d9b084796ee8d6841fc93d"),
    ("Models/Shared Kitchen/Meshes/Textures/512/t_sk_plate_return_01_d.png",
     "teacup/textures/t_sk_plate_return_01_d.png", "63560a5d2bfbbea4d9a6059fddd44401"),
    ("DownloadableContent/DLC02/DLC_Assets/Models/Shared Kitchen/Textures/t_dlc2_glassdirt_01_d.png",
     "teacup/textures/t_dlc2_glassdirt_01_d.png", "d99c7b08e1ba94449bd3a1b5aaced045"),
]

G_MESH_SINK_WOOD = det_guid("mesh", "m_sk_sink_dlc100_wood")
G_MESH_SINK_TOP = det_guid("mesh", "m_sk_sink_dlc100_top")
G_MESH_RETURN = det_guid("mesh", "m_sk_plate_return_01")
G_MESH_DIRT = det_guid("mesh", "m_dlc2_glassdirt_01")

G_MAT_SINK_WOOD = det_guid("mat", "mat_web_sink_wood")
G_MAT_RETURN = det_guid("mat", "mat_web_plate_return")
G_MAT_DIRT = det_guid("mat", "mat_web_glassdirt")
G_MAT_SINK_WATER = det_guid("mat", "mat_web_sink_water")

G_PREFAB_DIRTY = det_guid("prefab", "web_teacup_dirty")
G_PREFAB_STACK_DIRTY = det_guid("prefab", "web_teacup_stack_dirty")
G_PREFAB_SINK = det_guid("prefab", "web_sink_teacup")
G_PREFAB_RETURN = det_guid("prefab", "web_teacup_return")
G_SO_STACK_CLEAN = det_guid("so", "TeacupStackCleanSO")

# web_teacup_dirty 内部 fileID（固定，供 stack 的 m_platePrefab 引用）
FID_DIRTY_ROOT = "1500000001"

# web_teacup_stack_dirty 根节点 + 开局补脏杯 seeder
FID_STACK_DIRTY_ROOT = "1405583879889164"
FID_STACK_SEEDER = "114970000000000001"
G_SCRIPT_STACK_SEEDER = "067a36fc5a0828627001f951d01153b8"  # CustomStub.CleanPlateStackSeeder


def read_meta_guid(asset_abspath):
    with open(asset_abspath + ".meta", encoding="utf-8") as f:
        for line in f:
            if line.startswith("guid: "):
                return line.split(":", 1)[1].strip()
    raise RuntimeError("no guid in " + asset_abspath)


def ensure_folders():
    for rel in ["teacup", "teacup/meshes", "teacup/materials", "teacup/textures",
                "prefabs/counters", "pseudo_prefab_so/utensils"]:
        K.write_file(rel + ".meta",
                     "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\n"
                     "DefaultImporter:\n  externalObjects: {}\n  userData: \n"
                     "  assetBundleName: \n  assetBundleVariant: \n"
                     % K.folder_guid(rel))


def gen_meshes():
    for src, dst in MESH_CONV:
        dst_abs = os.path.join(W5, dst)
        if os.path.exists(dst_abs):
            out("  已存在跳过: %s" % dst)
            continue
        mesh = K.parse_mesh_asset(os.path.join(RIPPED, src))
        text = K.mesh_convert(mesh)
        if APPLY:
            os.makedirs(os.path.dirname(dst_abs), exist_ok=True)
            with open(dst_abs, "w", encoding="utf-8") as f:
                f.write(text)
        out("  OBJ: %s (verts=%s)" % (dst, mesh.get("vertex_count")))
    for _src, dst in MESH_CONV:
        name = os.path.splitext(os.path.basename(dst))[0]
        K.write_meta(dst, det_guid("mesh", name))
    # 水槽：转 obj 后按 g 段拆成 wood/top 两个文件（面索引全局，两份保留全部 v/vt/vn）
    import re as _re
    sink_src = os.path.join(RIPPED, SINK_MESH_SRC)
    wood_abs = os.path.join(W5, SINK_OBJ_WOOD)
    if not os.path.exists(wood_abs):
        text = K.mesh_convert(K.parse_mesh_asset(sink_src))
        parts = _re.split(r"(?m)^(?=g )", text)
        header = parts[0]
        groups = [p for p in parts[1:] if p.startswith("g ")]
        if len(groups) != 2:
            raise SystemExit("水槽网格子网格数 != 2: %d" % len(groups))
        for dst, g in ((SINK_OBJ_WOOD, groups[0]), (SINK_OBJ_TOP, groups[1])):
            if APPLY:
                with open(os.path.join(W5, dst), "w", encoding="utf-8") as f:
                    f.write(header + g)
            out("  OBJ: %s" % dst)
    for dst in (SINK_OBJ_WOOD, SINK_OBJ_TOP):
        name = os.path.splitext(os.path.basename(dst))[0]
        K.write_meta(dst, det_guid("mesh", name))


def gen_textures():
    import shutil
    for src, dst, guid in TEX_CONV:
        dst_abs = os.path.join(W5, dst)
        if not os.path.exists(dst_abs):
            if APPLY:
                os.makedirs(os.path.dirname(dst_abs), exist_ok=True)
                shutil.copyfile(os.path.join(RIPPED, src), dst_abs)
            out("  PNG: %s" % dst)
        # 2017 TextureImporter meta：以 kettle 既有贴图为模板换 guid
        tpl = os.path.join(W5, "kettle/textures/t_Birthday_TeaCup_Colour_2.png.meta")
        text = open(tpl, encoding="utf-8").read()
        import re as _re
        text = _re.sub(r"guid: [0-9a-f]{32}", "guid: " + guid, text, count=1)
        K.write_file(dst + ".meta", text)


MAT_STD_TEMPLATE = None  # 运行时从 mat_anniversary.mat 读


def gen_materials():
    global G_MAT_COUNTERTOP
    G_MAT_COUNTERTOP = read_meta_guid(
        os.path.join(W5, "counters/anniversary/mat_anniversary.mat"))
    tpl = open(os.path.join(W5, "counters/anniversary/mat_anniversary.mat"),
               encoding="utf-8").read()

    def std_mat(name, tex_guid):
        t = tpl.replace("m_Name: mat_anniversary", "m_Name: " + name)
        t = t.replace("guid: 028e2744ce3cb55c9650e0d8b60f71b9", "guid: " + tex_guid)
        return t

    mats = [
        ("teacup/materials/mat_web_sink_wood.mat", G_MAT_SINK_WOOD,
         std_mat("mat_web_sink_wood", "87585dc824d9b084796ee8d6841fc93d")),
        ("teacup/materials/mat_web_plate_return.mat", G_MAT_RETURN,
         std_mat("mat_web_plate_return", "63560a5d2bfbbea4d9a6059fddd44401")),
        ("teacup/materials/mat_web_glassdirt.mat", G_MAT_DIRT,
         std_mat("mat_web_glassdirt", "d99c7b08e1ba94449bd3a1b5aaced045")),
    ]
    # 水槽水面：WebLiquid（同奶茶液体 shader），浅蓝半透明
    water = open(os.path.join(W5, "kettle/materials/mat_web_liquid_water.mat"),
                 encoding="utf-8").read()
    water = water.replace("m_Name: mat_web_liquid_water", "m_Name: mat_web_sink_water")
    mats.append(("teacup/materials/mat_web_sink_water.mat", G_MAT_SINK_WATER, water))

    for rel, guid, text in mats:
        K.write_file(rel, text)
        K.write_meta(rel, guid)
        out("  MAT: %s" % rel)


# ---------------------------------------------------------------------------
# web_teacup_dirty.prefab（脏茶杯：茶杯网格 + 污渍层，tag DirtyPlate）
# 结构对齐 Ripped DLC100_DirtyMug.prefab。
# ---------------------------------------------------------------------------
def gen_teacup_dirty():
    root = FID_DIRTY_ROOT
    tr_root, go_mesh, tr_mesh = "1500000002", "1500000003", "1500000004"
    mf_mesh, mr_mesh = "1500000005", "1500000006"
    go_dirt, tr_dirt, mf_dirt, mr_dirt = "1500000007", "1500000008", "1500000009", "1500000010"
    mb_setup, mb_stub, mb_dummy, mb_rinfo = ("1500000011", "1500000012",
                                             "1500000013", "1500000014")
    box_solid, box_trigger = "1500000020", "1500000021"
    mb_phys, mb_carry, mb_pickup, mb_place = ("1500000022", "1500000023",
                                              "1500000024", "1500000025")
    mb_layer, mb_collswap, mb_tele = "1500000026", "1500000027", "1500000028"
    mb_highlight, mb_tag, mb_watcher = "1500000029", "1500000030", "1500000031"
    comps = [tr_root, box_solid, box_trigger,
             mb_setup, mb_stub, mb_dummy, mb_rinfo,
             mb_phys, mb_carry, mb_pickup, mb_place,
             mb_layer, mb_collswap, mb_tele, mb_highlight, mb_tag, mb_watcher]
    parts = ["%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n", K.prefab_header(root)]
    parts.append(K.go_doc(root, "web_teacup_dirty", comps, 19, tag="DirtyPlate"))
    parts.append(K.go_doc(go_mesh, "Mesh", [tr_mesh, mf_mesh, mr_mesh], 19))
    parts.append(K.go_doc(go_dirt, "m_dlc2_glassdirt_01", [tr_dirt, mf_dirt, mr_dirt], 19))
    parts.append(K.tr_doc(tr_root, root, ("0", "0", "0"), ("0", "0", "0", "1"),
                          ("1", "1", "1"), [tr_mesh]))
    # 茶杯原版变换（同 web_teacup 的 Cup 子物体）
    parts.append(K.tr_doc(tr_mesh, go_mesh, ("0", "0", "0"),
                          ("-0", "0.97075945", "-0", "0.24005422"),
                          ("2.5", "2.5", "2.5"), [tr_dirt], father=tr_root))
    # 污渍层（DLC100_DirtyMug 原版相对变换）
    parts.append(K.tr_doc(tr_dirt, go_dirt, ("0.004", "0.041", "0"),
                          ("-0", "-0.096767694", "-0", "0.9953071"),
                          ("0.50354165", "0.35617626", "0.50354165"), [], father=tr_mesh))
    parts.append(K.meshfilter_doc(mf_mesh, go_mesh, G_MESH_CUP))
    parts.append(K.meshrenderer_doc(mr_mesh, go_mesh, G_MAT_TEACUP))
    parts.append(K.meshfilter_doc(mf_dirt, go_dirt, G_MESH_DIRT))
    parts.append(K.meshrenderer_doc(mr_dirt, go_dirt, G_MAT_DIRT))
    # 碰撞体（茶杯尺寸，同 web_teacup）
    parts.append(K.collider_doc(box_solid, root, ("0.5", "0.73", "0.5"), ("0", "0.36", "0"), 0))
    parts.append(K.collider_doc(box_trigger, root, ("0.7", "0.73", "0.7"), ("0", "0.36", "0"), 1))
    parts.append(K.mb_stub(mb_setup, root, SC["SetupCustomUtensil"], ""))
    parts.append(K.mb_stub(mb_dummy, root, SC["DummySynchroniser"], ""))
    parts.append(K.mb_stub(mb_rinfo, root, SC["RendererSceneInfo"],
                           "  m_rendererClass: 2\n"))
    # 可拿取/可放置（与 web_teacup 同族；单个脏杯可拿、可入堆、可丢垃圾桶）
    parts.append(K.mb_stub(mb_phys, root, SC["PhysicalAttachment"],
        "  m_rigidBodyParams:\n"
        "    m_mass: 1\n"
        "    m_drag: 2\n"
        "    m_angularDrag: 1\n"
        "    m_useGravity: 1\n"
        "    m_interpolation: 0\n"
        "    m_collisionDetectionMode: 0\n"
        "    m_constraints: 80\n"
        "  m_groundCastParams:\n"
        "    m_coliderOverride: 0\n"
        "    m_collider: {fileID: 0}\n"
        "    m_radiusOverride: 0\n"
        "    m_radius: 0\n"
        "    m_rayOffsetOverride: 0\n"
        "    m_rayOffset: {x: 0, y: 0, z: 0}\n"
        "    m_maskOverride: 0\n"
        "    m_mask:\n"
        "      serializedVersion: 2\n"
        "      m_Bits: 0\n"))
    parts.append(K.mb_stub(mb_carry, root, SC["CarryableItem"], ""))
    parts.append(K.mb_stub(mb_pickup, root, SC["HandlePickupReferral"],
                           "  m_pickupReferralObject: {fileID: 0}\n"))
    parts.append(K.mb_stub(mb_place, root, SC["HandlePlacementReferral"],
                           "  m_placementReferralObject: {fileID: 0}\n"))
    parts.append(K.mb_stub(mb_layer, root, SC["PlacementLayerSwapper"],
                           "  m_layerWhenPlaced: HeldPlates\n"))
    parts.append(K.mb_stub(mb_collswap, root, SC["PlacementCollisionSwapper"],
        "  m_materialWhenPlaced: {fileID: 13400000, guid: 23a132d5eedabbe469c9c9a7b2b2fda6, type: 2}\n"))
    parts.append(K.mb_stub(mb_tele, root, SC["TeleportableItem"], ""))
    parts.append(K.mb_stub(mb_highlight, root, SC["AnticipateInteractionHighlight"],
                           "  m_brightnessModifier: 1\n"
                           "  m_highlightObjectOverride: {fileID: 0}\n"))
    parts.append(K.mb_stub(mb_tag, root, SC["SpecificPseudoPrefabTag"],
                           "  prefabTag: DirtyTeacup|\n"))
    parts.append(K.mb_stub(mb_stub, root, SC["SetupCustomUtensilStub"],
        "  ingredientContentUIPrefabSO: {fileID: 11400000, guid: %s, type: 2}\n"
        "  sludgeSO: {fileID: 11400000, guid: %s, type: 2}\n"
        "  spawnEffectSO: {fileID: 11400000, guid: %s, type: 2}\n"
        "  progressUIPrefabSO: {fileID: 11400000, guid: %s, type: 2}\n"
        % (SO_CONTENT_UI, SO_SLUDGE, SO_SPAWN_FX, SO_PROGRESS_UI)))
    # 合并 watcher（单杯+单杯 → 脏杯堆；CustomStub 运行时组件，真机经 tag 自愈补挂）
    parts.append(K.mb_stub(mb_watcher, root, G_SCRIPT_MERGE_WATCHER,
        "  m_stackPrefab: {fileID: 1405583879889164, guid: %s, type: 2}\n"
        % G_PREFAB_STACK_DIRTY))
    rel = "prefabs/teacups/web_teacup_dirty.prefab"
    K.write_file(rel, "".join(parts))
    K.write_meta(rel, G_PREFAB_DIRTY)
    out("  prefab: %s" % rel)


# ---------------------------------------------------------------------------
# web_teacup_stack_dirty.prefab（脏茶杯堆：克隆 common04 脏盘堆模板）
# ---------------------------------------------------------------------------
def gen_teacup_stack_dirty():
    tpl = open(os.path.join(REPO,
                            "Assets/common04/custom_utensil/custom_utensil_dirty_plate_stack.prefab"),
               encoding="utf-8").read()
    s = tpl
    s = s.replace("m_Name: custom_utensil_dirty_plate_stack",
                  "m_Name: web_teacup_stack_dirty")
    # 茶杯堆叠高度（原版 0.32）
    s = s.replace("m_itemTag: DirtyPlate\n  m_heightOffset: 0.1",
                  "m_itemTag: DirtyPlate\n  m_heightOffset: 0.32")
    # 单个脏茶杯
    s = s.replace(
        "m_platePrefab: {fileID: 1024281919400308, guid: 7fbee510d0bc50446bdac6760885dd93,",
        "m_platePrefab: {fileID: %s, guid: %s," % (FID_DIRTY_ROOT, G_PREFAB_DIRTY))
    # 装盘类型：茶杯 Mug（与 web_teacup 的 Plate.m_platingStep 一致；
    # WashingStation patch 据此判定可洗）
    s = s.replace(
        "m_plateType: {fileID: 11400000, guid: 33cdd9a977e652844ba3e2f5a8b23ce7, type: 2}",
        "m_plateType: {fileID: 11400000, guid: %s, type: 2}" % G_MUG_PLATING_LOCAL)
    # 洗净产物 → 干净茶杯堆
    s = s.replace(
        "m_washedPrefab: {fileID: 1760872858961958, guid: 4720bf1b02e845e428d2fd7d98f290f5,",
        "m_washedPrefab: {fileID: %s, guid: %s," % (FID_STACK_CLEAN_ROOT, G_PREFAB_STACK_CLEAN))
    # 干净茶杯
    s = s.replace(
        "m_cleanPlatePrefab: {fileID: 1003597553421588, guid: 7330344a9d4cb9c4da2f2bc0358820b0,",
        "m_cleanPlatePrefab: {fileID: %s, guid: %s," % (FID_TEACUP_ROOT, G_PREFAB_TEACUP))
    # 去掉模板内嵌 PseudoPrefabSO 子资产
    import re as _re
    s = _re.sub(r"^--- !u!114 &114128203264190416\n(?:(?!--- ).*\n)*", "", s, flags=_re.M)
    # 开局补脏茶杯：脏堆 prefab 没有初始数量字段，关卡里放下的脏堆在 Play 中恒为空。
    # 挂 CustomStub.CleanPlateStackSeeder（同时兼容 ServerDirtyPlateStack），按
    # plateCount 在服务端/编辑器 Play 补出 N 只脏杯，供洗杯台演示。
    s = s.replace(
        "  - component: {fileID: 114567524870353100}\n  m_Layer: 19",
        "  - component: {fileID: 114567524870353100}\n"
        "  - component: {fileID: %s}\n  m_Layer: 19" % FID_STACK_SEEDER, 1)
    s += (
        "--- !u!114 &%s\nMonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_PrefabParentObject: {fileID: 0}\n"
        "  m_PrefabInternal: {fileID: 100100000}\n"
        "  m_GameObject: {fileID: %s}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"
        "  m_Name: \n"
        "  m_EditorClassIdentifier: \n"
        "  plateCount: 5\n"
        "  platePseudoPrefabSO: {fileID: 0}\n"
        % (FID_STACK_SEEDER, FID_STACK_DIRTY_ROOT, G_SCRIPT_STACK_SEEDER))
    rel = "prefabs/teacups/web_teacup_stack_dirty.prefab"
    K.write_file(rel, s)
    K.write_meta(rel, G_PREFAB_STACK_DIRTY)
    out("  prefab: %s" % rel)


# ---------------------------------------------------------------------------
# MugPlatingLocalSO（PseudoPrefabSO → common04 本地 Mug PlatingStepData 实体）
# 奶茶菜谱 platingStepSO 用它，与 web_teacup 序列化引用同实例；
# 用 bundle 版 Mug SO 会导致装盘步双实例断链（杯中内容物显示走兜底）。
# ---------------------------------------------------------------------------
G_SO_MUG_PLATING_LOCAL = det_guid("so", "MugPlatingLocalSO")
G_SCRIPT_MERGE_WATCHER = det_guid("script", "DirtyTeacupMergeWatcher")

# common04 本地 Mug PlatingStepData 实体（与 web_teacup 序列化引用同实例）
G_MUG_PLATING_LOCAL = "eb494a42c3217e146b9259e5f46e5b82"


def gen_mug_plating_so():
    rel = "pseudo_prefab_so/utensils/MugPlatingLocalSO.asset"
    text = (
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!114 &11400000\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_PrefabParentObject: {fileID: 0}\n"
        "  m_PrefabInternal: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        "  m_Script: {fileID: 11500000, guid: 0cff7c13895ab9e47a5e02d4619cc3b9, type: 3}\n"
        "  m_Name: MugPlatingLocalSO\n"
        "  m_EditorClassIdentifier: \n"
        "  prefabName: MugPlatingLocalSO\n"
        "  bundleName: \n"
        "  assetPath: \n"
        "  asset: {fileID: 11400000, guid: " + G_MUG_PLATING_LOCAL + ", type: 2}\n")
    K.write_file(rel, text)
    K.write_meta(rel, G_SO_MUG_PLATING_LOCAL)
    out("  SO: %s" % rel)


# ---------------------------------------------------------------------------
# TeacupStackCleanSO（PseudoPrefabSO：web_teacup_stack_clean 本地直引；
# 水槽 SetupCustomSinkStub.cleanPlateStackSO 用）
# ---------------------------------------------------------------------------
def gen_stack_clean_so():
    rel = "pseudo_prefab_so/utensils/TeacupStackCleanSO.asset"
    text = (
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!114 &11400000\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_PrefabParentObject: {fileID: 0}\n"
        "  m_PrefabInternal: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        "  m_Script: {fileID: 11500000, guid: 0cff7c13895ab9e47a5e02d4619cc3b9, type: 3}\n"
        "  m_Name: TeacupStackCleanSO\n"
        "  m_EditorClassIdentifier: \n"
        "  prefabName: TeacupStackCleanSO\n"
        "  bundleName: \n"
        "  assetPath: \n"
        "  asset: {fileID: " + FID_STACK_CLEAN_ROOT + ", guid: " + G_PREFAB_STACK_CLEAN
        + ", type: 2}\n")
    K.write_file(rel, text)
    K.write_meta(rel, G_SO_STACK_CLEAN)
    out("  SO: %s" % rel)


# ---------------------------------------------------------------------------
# web_sink_teacup.prefab（洗茶杯水槽：克隆 common04 水槽模板 + DLC100 网格）
# ---------------------------------------------------------------------------
def _sink_mesh_child(go, tr, mf, mr, name, mesh_guid, mat_guid):
    """水槽基座下的网格子节点（2017 格式，父=容器 transform 4598283894434240）。"""
    return (
        "--- !u!1 &%d\nGameObject:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_PrefabParentObject: {fileID: 0}\n"
        "  m_PrefabInternal: {fileID: 100100000}\n"
        "  serializedVersion: 5\n"
        "  m_Component:\n"
        "  - component: {fileID: %d}\n"
        "  - component: {fileID: %d}\n"
        "  - component: {fileID: %d}\n"
        "  m_Layer: 11\n"
        "  m_Name: %s\n"
        "  m_TagString: Untagged\n"
        "  m_Icon: {fileID: 0}\n"
        "  m_NavMeshLayer: 0\n"
        "  m_StaticEditorFlags: 0\n"
        "  m_IsActive: 1\n"
        "--- !u!4 &%d\nTransform:\n"
        "  m_ObjectHideFlags: 1\n"
        "  m_PrefabParentObject: {fileID: 0}\n"
        "  m_PrefabInternal: {fileID: 100100000}\n"
        "  m_GameObject: {fileID: %d}\n"
        "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n"
        "  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
        "  m_LocalScale: {x: 1, y: 1, z: 1}\n"
        "  m_Children: []\n"
        "  m_Father: {fileID: 4598283894434240}\n"
        "  m_RootOrder: 0\n"
        "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n"
        "--- !u!33 &%d\nMeshFilter:\n"
        "  m_ObjectHideFlags: 1\n"
        "  m_PrefabParentObject: {fileID: 0}\n"
        "  m_PrefabInternal: {fileID: 100100000}\n"
        "  m_GameObject: {fileID: %d}\n"
        "  m_Mesh: {fileID: 4300000, guid: %s, type: 3}\n"
        "--- !u!23 &%d\nMeshRenderer:\n"
        "  m_ObjectHideFlags: 1\n"
        "  m_PrefabParentObject: {fileID: 0}\n"
        "  m_PrefabInternal: {fileID: 100100000}\n"
        "  m_GameObject: {fileID: %d}\n"
        "  m_Enabled: 1\n"
        "  m_CastShadows: 1\n"
        "  m_ReceiveShadows: 1\n"
        "  m_DynamicOccludee: 1\n"
        "  m_MotionVectors: 1\n"
        "  m_LightProbeUsage: 0\n"
        "  m_ReflectionProbeUsage: 0\n"
        "  m_Materials:\n"
        "  - {fileID: 2100000, guid: %s, type: 2}\n"
        "  m_StaticBatchInfo:\n"
        "    firstSubMesh: 0\n"
        "    subMeshCount: 0\n"
        "  m_StaticBatchRoot: {fileID: 0}\n"
        "  m_ProbeAnchor: {fileID: 0}\n"
        "  m_LightProbeVolumeOverride: {fileID: 0}\n"
        "  m_ScaleInLightmap: 1\n"
        "  m_PreserveUVs: 0\n"
        "  m_IgnoreNormalsForChartDetection: 0\n"
        "  m_ImportantGI: 0\n"
        "  m_StitchLightmapSeams: 0\n"
        "  m_SelectedEditorRenderState: 3\n"
        "  m_MinimumChartSize: 4\n"
        "  m_AutoUVMaxDistance: 0.5\n"
        "  m_AutoUVMaxAngle: 89\n"
        "  m_LightmapParameters: {fileID: 0}\n"
        "  m_SortingLayerID: 0\n"
        "  m_SortingLayer: 0\n"
        "  m_SortingOrder: 0\n"
        % (go, tr, mf, mr, name, tr, go, mf, go, mesh_guid, mr, go, mat_guid))


def gen_sink_teacup():
    tpl = open(os.path.join(REPO,
                            "Assets/common04/custom_counter/custom_counter_sink.prefab"),
               encoding="utf-8").read()
    s = tpl
    s = s.replace("m_Name: custom_counter_sink", "m_Name: web_sink_teacup")
    s = s.replace("m_Name: m_sk_sink_01", "m_Name: m_sk_sink_DLC100")
    # 基座变换：模板内置 cube 的缩放 → 原版。网格容器 rot y90（父节点 WashingStation
    # 视觉节点 rot y90，净值 y180：网格长轴 X 与碰撞体一致；AYCE 场景为根 y90+网格
    # y180，净值同为 180，见 s_birthday_1_3）
    s = s.replace(
        "m_LocalRotation: {x: 0, y: 0.7071068, z: 0, w: 0.7071068}\n"
        "  m_LocalPosition: {x: 0, y: 0.25, z: 0}\n"
        "  m_LocalScale: {x: 2.4, y: 0.5, z: 1.2}",
        "m_LocalRotation: {x: 0, y: 0.7071068, z: 0, w: 0.7071068}\n"
        "  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
        "  m_LocalScale: {x: 1, y: 1, z: 1}")
    # 水槽基座网格：内置 cube → DLC100 水槽网格拆双节点（wood/top）
    # 1) 容器 GO 去掉模板 MF/MR 组件引用
    s = s.replace(
        "  m_Component:\n"
        "  - component: {fileID: 4598283894434240}\n"
        "  - component: {fileID: 33900234749050704}\n"
        "  - component: {fileID: 23174870342286728}",
        "  m_Component:\n"
        "  - component: {fileID: 4598283894434240}")
    # 2) 删除模板 MF/MR 文档
    import re as _re
    for fid in ["33900234749050704", "23174870342286728"]:
        s = _re.sub(r"^--- !u!(?:33|23) &%s\n(?:(?!^--- ).*\n)*" % fid, "", s,
                    flags=_re.M)
    # 3) 容器注册两个子网格节点（锚定步骤 1 之后的新变换文本）
    s = s.replace(
        "m_LocalRotation: {x: 0, y: 0.7071068, z: 0, w: 0.7071068}\n"
        "  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
        "  m_LocalScale: {x: 1, y: 1, z: 1}\n"
        "  m_Children: []",
        "m_LocalRotation: {x: 0, y: 0.7071068, z: 0, w: 0.7071068}\n"
        "  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
        "  m_LocalScale: {x: 1, y: 1, z: 1}\n"
        "  m_Children:\n"
        "  - {fileID: 1700000011}\n"
        "  - {fileID: 1700000021}", 1)
    s += _sink_mesh_child(1700000010, 1700000011, 1700000012, 1700000013,
                          "m_sk_sink_DLC100_wood", G_MESH_SINK_WOOD, G_MAT_SINK_WOOD)
    s += _sink_mesh_child(1700000020, 1700000021, 1700000022, 1700000023,
                          "m_sk_sink_DLC100_top", G_MESH_SINK_TOP, G_MAT_COUNTERTOP)
    # 3 个展示用脏茶杯：内置圆柱 → 茶杯网格 + 白瓷材质；挂到根节点并按 AYCE 原版
    # 变换躺在洗杯区（DirtyMug/1/2，scale≈1.073，s_birthday_1_3 场景实测）
    s = s.replace("m_Mesh: {fileID: 10206, guid: 0000000000000000e000000000000000, type: 0}",
                  "m_Mesh: {fileID: 4300000, guid: %s, type: 3}" % G_MESH_CUP)
    s = s.replace("guid: 495f1cf668adc184c89fdedb74c7ae5a", "guid: " + G_MAT_TEACUP)
    ayce_mugs = {
        "4085373707351316": ((0.7300005, 0.206, -0.2670002),
                             (-0.07423493, -0.8209907, -0.20130488, -0.5290933),
                             (1.0732255, 1.073225, 1.0732255)),
        "4291828207603726": ((0.7300005, 0.257, 0.3369999),
                             (0.29639965, -0.747024, 0.395606, -0.44452026),
                             (1.0732249, 1.0732251, 1.0732249)),
        "4638472652858390": ((0.27900028, 0.257, 0.10800004),
                             (0.40727973, 0.5813628, -0.03277448, -0.7036095),
                             (1.073225, 1.073225, 1.0732251)),
    }
    for tr, (pos, rot, scl) in ayce_mugs.items():
        pat = _re.compile(
            r"(--- !u!4 &%s\nTransform:\n(?:.*?\n)*?)" % tr +
            r"  m_LocalRotation: \{[^}]*\}\n" +
            r"  m_LocalPosition: \{[^}]*\}\n" +
            r"  m_LocalScale: \{[^}]*\}\n" +
            r"  m_Children: \[\]\n" +
            r"  m_Father: \{fileID: \d+\}")
        m = pat.search(s)
        assert m, "DirtyPlate transform " + tr
        s = s[:m.start()] + (
            m.group(1) +
            "  m_LocalRotation: {x: %s, y: %s, z: %s, w: %s}\n" % rot +
            "  m_LocalPosition: {x: %s, y: %s, z: %s}\n" % pos +
            "  m_LocalScale: {x: %s, y: %s, z: %s}\n" % scl +
            "  m_Children: []\n" +
            "  m_Father: {fileID: 4588427489122164}") + s[m.end():]
    # WashingStation 视觉节点去掉 3 个脏杯；根节点接回（对齐 AYCE：挂根）
    s = s.replace(
        "  m_Children:\n"
        "  - {fileID: 4598283894434240}\n"
        "  - {fileID: 4291828207603726}\n"
        "  - {fileID: 4638472652858390}\n"
        "  - {fileID: 4085373707351316}\n"
        "  - {fileID: 4818979253597084}",
        "  m_Children:\n"
        "  - {fileID: 4598283894434240}\n"
        "  - {fileID: 4818979253597084}")
    s = s.replace(
        "  m_Children:\n"
        "  - {fileID: 4359435498264842}\n"
        "  - {fileID: 4605026302621150}\n"
        "  - {fileID: 4813882626360302}\n"
        "  - {fileID: 4936393657959448}",
        "  m_Children:\n"
        "  - {fileID: 4359435498264842}\n"
        "  - {fileID: 4605026302621150}\n"
        "  - {fileID: 4813882626360302}\n"
        "  - {fileID: 4936393657959448}\n"
        "  - {fileID: 4291828207603726}\n"
        "  - {fileID: 4638472652858390}\n"
        "  - {fileID: 4085373707351316}")
    # 水面：占位蓝 → WebLiquid 水面材质
    s = s.replace("guid: 740f040d4bbf652409d608861b301515", "guid: " + G_MAT_SINK_WATER)
    # 烘干台干净杯堆：模板 SO → 茶杯堆 SO
    s = s.replace(
        "cleanPlateStackSO: {fileID: 114149305632628198, guid: 4720bf1b02e845e428d2fd7d98f290f5,",
        "cleanPlateStackSO: {fileID: 11400000, guid: %s," % G_SO_STACK_CLEAN)
    # 烘干台 PlateReturnStation：模板里 m_stackPrefab 是 {fileID: 0}，靠 SetupCustomSink
    # 运行时从 cleanPlateStackSO 注入；自定义关卡里 stub 参数缺失时水槽就出不了干净杯
    # （脏杯洗完不回到台面）。生成期直接绑死干净茶杯堆，与 cleanPlateStackSO 同源。
    s = s.replace(
        "  m_Script: {fileID: 11500000, guid: f0e58dbd6229013dc940caea019dee51, type: 3}\n"
        "  m_Name: \n"
        "  m_EditorClassIdentifier: \n"
        "  m_stackPrefab: {fileID: 0}",
        "  m_Script: {fileID: 11500000, guid: f0e58dbd6229013dc940caea019dee51, type: 3}\n"
        "  m_Name: \n"
        "  m_EditorClassIdentifier: \n"
        "  m_stackPrefab: {fileID: %s, guid: %s, type: 2}"
        % (FID_STACK_CLEAN_ROOT, G_PREFAB_STACK_CLEAN))
    rel = "prefabs/counters/web_sink_teacup.prefab"
    K.write_file(rel, s)
    K.write_meta(rel, G_PREFAB_SINK)
    out("  prefab: %s" % rel)


# ---------------------------------------------------------------------------
# web_teacup_return.prefab（茶杯回收台：PlateReturnStation → 干净茶杯堆，开局出 5 只）
# 关卡数据里没有“上菜台 → 回收台”的脏盘回流绑定，回收台按干净杯补给点使用。
# 结构对齐 s_birthday_1_3 场景 workstation_mug_return。
# ---------------------------------------------------------------------------
def gen_teacup_return():
    root = "1600000001"
    tr_root = "1600000002"
    box_root = "1600000003"
    mb_attach, mb_return, mb_highlight, mb_grid = ("1600000004", "1600000005",
                                                   "1600000006", "1600000007")
    go_ap, tr_ap = "1600000010", "1600000011"
    go_top, tr_top, box_top = "1600000020", "1600000021", "1600000022"
    go_mesh, tr_mesh, mf_mesh, mr_mesh = "1600000030", "1600000031", "1600000032", "1600000033"
    go_block, tr_block, box_block = "1600000040", "1600000041", "1600000042"

    parts = ["%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n", K.prefab_header(root)]
    parts.append(K.go_doc(root, "web_teacup_return",
                          [tr_root, box_root, mb_attach, mb_return, mb_highlight, mb_grid],
                          11))
    parts.append(K.go_doc(go_ap, "AttachPoint", [tr_ap], 11))
    parts.append(K.go_doc(go_top, "Worktop", [tr_top, box_top], 11))
    parts.append(K.go_doc(go_mesh, "Mesh", [tr_mesh, mf_mesh, mr_mesh], 11))
    parts.append(K.go_doc(go_block, "Block", [tr_block, box_block], 26))
    parts.append(K.tr_doc(tr_root, root, ("0", "0", "0"), ("0", "0", "0", "1"),
                          ("1", "1", "1"), [tr_ap, tr_top, tr_block]))
    parts.append(K.tr_doc(tr_ap, go_ap, ("0", "0.45", "0"), ("0", "0", "0", "1"),
                          ("1", "1", "1"), [], father=tr_root))
    parts.append(K.tr_doc(tr_top, go_top, ("0", "0", "0"), ("0", "0", "0", "1"),
                          ("1", "1", "1"), [tr_mesh], father=tr_root))
    parts.append(K.tr_doc(tr_mesh, go_mesh, ("0", "0", "0"), ("-0", "-0", "-0", "1"),
                          ("1", "1", "1"), [], father=tr_top))
    parts.append(K.tr_doc(tr_block, go_block, ("0", "0", "0"), ("0", "0", "0", "1"),
                          ("1", "1", "1"), [], father=tr_root))
    parts.append(K.collider_doc(box_root, root, ("1.2", "1", "1.2"), ("0", "1", "0"), 1))
    parts.append(K.collider_doc(box_top, go_top, ("1.2", "0.5", "1.2"), ("0", "0.25", "0"), 0))
    parts.append(K.collider_doc(box_block, go_block, ("1.2", "2", "1.2"), ("0", "1", "0"), 0))
    parts.append(K.meshfilter_doc(mf_mesh, go_mesh, G_MESH_RETURN))
    parts.append(K.meshrenderer_doc(mr_mesh, go_mesh, G_MAT_RETURN))
    parts.append(K.mb_stub(mb_attach, root, SC["AttachStation"],
        "  m_attachPoint: {fileID: %s}\n"
        "  m_pickupPriority: 0\n"
        "  m_placementPriority: 0\n"
        "  m_catchingPriority: 0\n"
        "  m_canCatch: 1\n" % tr_ap))
    parts.append(K.mb_stub(mb_return, root, SC["PlateReturnStation"],
        "  m_stackPrefab: {fileID: %s, guid: %s, type: 2}\n"
        "  m_startingPlateNumber: 5\n" % (FID_STACK_CLEAN_ROOT, G_PREFAB_STACK_CLEAN)))
    parts.append(K.mb_stub(mb_highlight, root, SC["AnticipateInteractionHighlight"],
        "  m_brightnessModifier: 1.2\n"
        "  m_highlightObjectOverride: {fileID: 0}\n"))
    parts.append(K.mb_stub(mb_grid, root, SC["StaticGridLocation"], ""))
    rel = "prefabs/counters/web_teacup_return.prefab"
    K.write_file(rel, "".join(parts))
    K.write_meta(rel, G_PREFAB_RETURN)
    out("  prefab: %s" % rel)


def preflight_guid_collision():
    """与 kettle gen 同规约：新 guid 不得撞上工程已有资产（自身产物除外，幂等）。"""
    import subprocess
    own = set()
    for rel in [SINK_OBJ_WOOD, SINK_OBJ_TOP] + [d for _s, d in MESH_CONV]:
        own.add(os.path.join(REPO, "Assets/commonW5", rel + ".meta"))
    own.update(os.path.join(REPO, "Assets/commonW5", r) for r in [
        "prefabs/teacups/web_teacup_dirty.prefab.meta",
        "prefabs/teacups/web_teacup_stack_dirty.prefab.meta",
        "prefabs/counters/web_sink_teacup.prefab.meta",
        "prefabs/counters/web_teacup_return.prefab.meta",
        "pseudo_prefab_so/utensils/TeacupStackCleanSO.asset.meta",
        "teacup/materials/mat_web_sink_wood.mat.meta",
        "teacup/materials/mat_web_plate_return.mat.meta",
        "teacup/materials/mat_web_glassdirt.mat.meta",
        "teacup/materials/mat_web_sink_water.mat.meta",
    ])
    new_guids = [G_MESH_SINK_WOOD, G_MESH_SINK_TOP, G_MESH_RETURN, G_MESH_DIRT,
                 G_MAT_SINK_WOOD, G_MAT_RETURN, G_MAT_DIRT, G_MAT_SINK_WATER,
                 G_PREFAB_DIRTY, G_PREFAB_STACK_DIRTY, G_PREFAB_SINK,
                 G_PREFAB_RETURN, G_SO_STACK_CLEAN]
    hits = []
    for g in new_guids:
        r = subprocess.run(["grep", "-rl", "--include=*.meta", "guid: " + g,
                            os.path.join(REPO, "Assets")],
                           capture_output=True, text=True)
        for line in r.stdout.strip().split("\n"):
            if line and line not in own:
                hits.append("%s -> %s" % (g, line))
    if hits:
        raise SystemExit("guid 冲突：\n" + "\n".join(hits))
    out("[preflight] guid 冲突检查通过（%d 个新 guid）" % len(new_guids))


def main():
    print("=== gen-commonw5-teacup（%s）===" % ("APPLY" if APPLY else "DRY-RUN"))
    preflight_guid_collision()
    ensure_folders()
    gen_meshes()
    gen_textures()
    gen_materials()
    gen_teacup_dirty()
    gen_teacup_stack_dirty()
    gen_mug_plating_so()
    gen_stack_clean_so()
    gen_sink_teacup()
    gen_teacup_return()
    print("\n=== 汇总 ===")
    print("脏茶杯:        %s" % G_PREFAB_DIRTY)
    print("脏茶杯堆:      %s" % G_PREFAB_STACK_DIRTY)
    print("洗茶杯水槽:    %s" % G_PREFAB_SINK)
    print("茶杯回收台:    %s" % G_PREFAB_RETURN)
    print("干净茶杯堆 SO: %s" % G_SO_STACK_CLEAN)
    print("事件数: %d" % len(report))
    if not APPLY:
        print("\n（dry-run：加 --apply 落盘）")


if __name__ == "__main__":
    main()
