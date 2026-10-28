#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""commonW5 热水器 + 奶茶体系 生成器（水壶视觉件 / 茶杯 / 液体内容物 /
HotWater 节点 / 4 菜谱 + 1 中间产物 + 奶茶分类）。

素材来源：backup_rebuild DLC100（5 周年）AssetRipper 导出。原版素材 1:1 拷贝
（保留原 guid，依赖闭包零改写）；新增资产用确定性 guid md5("commonw5-kettle:<kind>:<id>")；
菜谱沿用 gen-commonw5.mjs 的 md5("commonw5-recipe:<id>") 命名空间（uID 续 58323 段）。

格式适配（项目 Unity 2017.4.8f1，Ripped 为 2019 序列化）：
  - Mesh（sv10 packed）→ OBJ（mesh_to_obj.py，与 C# dumper 同约定：X 取反、面序倒排）
  - .controller / .anim / .mat：文档头 2019→2017 重写
    （m_CorrespondingSourceObject/m_PrefabInstance/m_PrefabAsset → m_PrefabParentObject/m_PrefabInternal）
  - 场景子树（ParticleSystem 等）逐字拷贝 + 头部重写 + 引用重映射；GO/Transform/
    碰撞体按 2017 格式重建
  - Shader Forge 着色器为 DummyShaderTextExporter 占位（不可用）→ 自建 5 个
    Custom/Web* 着色器；水壶主体材质保留原 .mat（_EmissiveStrength 动画绑定名
    一致）仅换 shader 引用

用法：
  python3 gen-commonw5-kettle.py [--apply]   # 默认 dry-run；--apply 落盘（已存在文件跳过）
  python3 gen-commonw5-kettle.py --repair    # 幂等修补已生成资产：
                                             #   %TAG 末尾冒号 / OBJ m_Mesh type 3 /
                                             #   OBJ+prefab+mat+asset+shader importer meta
                                             # （不重写 prefab 粒子兼容段）
"""
import glob
import hashlib
import json
import os
import re
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from mesh_to_obj import parse_mesh_asset, convert as mesh_convert  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
RIPPED = ("/Users/guojia/Desktop/oc2_editor/backup_rebuild_20260917_132624/"
          "Ripped/UnityProject_AssetsOnly/ExportedProject/Assets")
W5 = os.path.join(REPO, "Assets", "commonW5")

APPLY = "--apply" in sys.argv
REPAIR = "--repair" in sys.argv
report = []


def out(msg):
    report.append(msg)
    print(msg)


def det_guid(kind, ident):
    return hashlib.md5(("commonw5-kettle:%s:%s" % (kind, ident)).encode()).hexdigest()


def folder_guid(rel):
    return hashlib.md5(("commonw5-folder:Assets/commonW5/%s" % rel).encode()).hexdigest()


def write_file(rel_from_w5, content, binary=False):
    """写 Assets/commonW5 下文件（rel_from_w5 相对 commonW5）。返回绝对路径。"""
    path = os.path.join(W5, rel_from_w5)
    if os.path.exists(path):
        out("  已存在跳过: %s" % rel_from_w5)
        return path
    if APPLY:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb" if binary else "w") as f:
            f.write(content)
    out("  写入: %s" % rel_from_w5)
    return path


def obj_mesh_name(rel_from_w5):
    """OBJ 首个 g 段名（= Unity 导入后的 Mesh 子资产名）。"""
    path = os.path.join(W5, rel_from_w5)
    if os.path.exists(path):
        try:
            with open(path, encoding="utf-8") as f:
                for line in f:
                    if line.startswith("g "):
                        return line[2:].strip()
        except Exception:
            pass
    return os.path.splitext(os.path.basename(rel_from_w5))[0]


def meta_text_for(rel_from_w5, guid):
    """按扩展名给出完整 importer meta（极简 meta 在 2017 下会让 prefab/OBJ
    子资产引用不稳定；与 commonW5/counters 的既有可工作资产保持一致）。"""
    lower = rel_from_w5.lower()
    if lower.endswith(".prefab"):
        return META_PREFAB % guid
    if lower.endswith(".obj"):
        # 必须给全 fileIDToRecycleName：空映射会让 Unity 对旧 Library 里的
        # <mesh>_0Mat / <file>Avatar 断言 "GetObject failed to find Object"。
        # importMaterials=0 + 完整映射 = 与 m_space_rail_01.obj 同款（无材质/Avatar 子资产）。
        return META_MODEL % {"guid": guid, "mesh": obj_mesh_name(rel_from_w5)}
    if lower.endswith(".shader"):
        return META_SHADER % guid
    if lower.endswith(".mat"):
        return META_NATIVE % (guid, 2100000)
    if lower.endswith(".anim"):
        return META_NATIVE % (guid, 7400000)
    if lower.endswith(".controller"):
        return META_NATIVE % (guid, 9100000)
    if lower.endswith(".asset"):
        return META_NATIVE % (guid, 11400000)
    return "fileFormatVersion: 2\nguid: %s\n" % guid


def write_meta(rel_from_w5, guid):
    return write_file(rel_from_w5 + ".meta", meta_text_for(rel_from_w5, guid))


def copy_asset(src_rel, dst_rel):
    """Ripped 资产 1:1 拷贝（文件 + .meta，保 guid）。"""
    src = os.path.join(RIPPED, src_rel)
    dst = os.path.join(W5, dst_rel)
    if not os.path.exists(src):
        raise SystemExit("缺源文件: %s" % src_rel)
    if os.path.exists(dst):
        out("  已存在跳过: %s" % dst_rel)
        return
    if APPLY:
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copy2(src, dst)
        shutil.copy2(src + ".meta", dst + ".meta")
    out("  拷贝(保guid): %s" % dst_rel)


HDR_2019 = ("  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n")
HDR_2017 = "  m_PrefabParentObject: {fileID: 0}\n  m_PrefabInternal: {fileID: 0}\n"

# Unity 的 %TAG 指令必须带末尾冒号（tag:unity3d.com,2011:）。漏掉冒号时
# Unity 2017 的 YAML 解析器会一直向后找 ':'，报 "could not find expected ':'"，
# 多文档资产随后报 "Unexpected node type."（热水器 prefab / 奶茶内容物 prefab 全中招）。
YAML_HEADER = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"

META_PREFAB = ("fileFormatVersion: 2\nguid: %s\nPrefabImporter:\n"
               "  externalObjects: {}\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n")
META_NATIVE = ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n"
               "  externalObjects: {}\n  mainObjectFileID: %d\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n")
META_MODEL = ("fileFormatVersion: 2\nguid: %(guid)s\nModelImporter:\n"
              "  serializedVersion: 22\n  fileIDToRecycleName:\n"
              "    100000: //RootNode\n"
              "    100002: %(mesh)s\n"
              "    400000: //RootNode\n"
              "    400002: %(mesh)s\n"
              "    2100000: %(mesh)sMat\n"
              "    2300000: %(mesh)s\n"
              "    3300000: %(mesh)s\n"
              "    4300000: %(mesh)s\n"
              "  externalObjects: {}\n  materials:\n"
              "    importMaterials: 0\n    materialName: 0\n"
              "    materialSearch: 1\n    materialLocation: 1\n"
              "  meshes:\n    globalScale: 1\n    meshCompression: 0\n"
              "    addColliders: 0\n  isReadable: 1\n  importAnimation: 0\n"
              "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
META_SHADER = ("fileFormatVersion: 2\nguid: %s\nShaderImporter:\n"
               "  externalObjects: {}\n  defaultTextures: []\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n")


def rewrite_header(text):
    """2019 文档头 → 2017（全文件统一替换）。"""
    return text.replace(HDR_2019, HDR_2017)


def rewrite_controller_2017(text):
    """AnimatorController 2019 → 2017：给 m_ChildStates/m_ChildStateMachines
    条目补 serializedVersion: 1（2017 原始 Camera.controller 同款）。

    2019 控制器省略了这个字段；Unity 2017 在解析状态机子状态时会报
    "Unexpected node type."（Kettle 控制器正好 3 个 child state → 3 条）。
    """
    lines = text.splitlines()
    out = []
    for line in lines:
        stripped = line.strip()
        if (line.startswith("  - m_State:") or line.startswith("  - m_StateMachine:")):
            if not out or out[-1].strip() != "- serializedVersion: 1":
                out.append("  - serializedVersion: 1")
                out.append("    " + stripped[1:].lstrip())
                continue
        out.append(line)
    text = "\n".join(out)
    if text and not text.endswith("\n"):
        text += "\n"
    return text


def rewrite_anim_2017(text):
    """AnimationClip 2019 keyframe(sv3/weighted) → 2017 keyframe(sv2)。

    Unity 2017 读到 2019 的 `- serializedVersion: 3` + weightedMode/inWeight/
    outWeight 会报 "Unexpected node type."；这些权重字段在 weightedMode=0 时
    不影响曲线形状，直接降为 sv2 并丢弃即可。
    """
    lines = text.splitlines()
    out = []
    for line in lines:
        stripped = line.strip()
        if stripped == "- serializedVersion: 3" and line.startswith("      - "):
            out.append(line.replace("serializedVersion: 3", "serializedVersion: 2"))
            continue
        if stripped.startswith("weightedMode:") or stripped.startswith("inWeight:") \
                or stripped.startswith("outWeight:"):
            continue
        if "outSlope: Infinity" in line:
            out.append(line.replace("outSlope: Infinity", "outSlope: 0"))
            continue
        out.append(line)
    text = "\n".join(out)
    if text and not text.endswith("\n"):
        text += "\n"
    return text


def copy_asset_header2017(src_rel, dst_rel):
    src = os.path.join(RIPPED, src_rel)
    if not os.path.exists(src):
        raise SystemExit("缺源文件: %s" % src_rel)
    text = rewrite_header(open(src, encoding="utf-8").read())
    if dst_rel.endswith(".anim"):
        text = rewrite_anim_2017(text)
    elif dst_rel.endswith(".controller"):
        text = rewrite_controller_2017(text)
    write_file(dst_rel, text)
    # meta 保 guid
    meta = open(src + ".meta", encoding="utf-8").read()
    write_file(dst_rel + ".meta", meta)


# ============ 常量表（来源：Ripped guid_map / 场景提取 / 项目 meta） ============

# --- Ripped 原 guid（1:1 拷贝保留） ---
G_CTRL_KETTLE = "9829eb598a1a43740a14bc3609465f0a"      # DLC0100_Kettle.controller
G_CLIP_COLD = "1a8e5f5612d72c14e99644a004311c76"
G_CLIP_HEATING = "6ef05611c487dd34491ac27dc877141e"
G_CLIP_HOT = "353a32f20a445c4438614ea0291a6e7c"
G_MAT_KETTLE = "41eb5cb32b12b4545969504e30a14a1c"        # 保 guid + 换 shader
G_MAT_SPOON = "9623807b4f7192540a51634e060853ba"         # builtin Standard(Specular)
G_TEX_KETTLE_D = "5206b892f076c9b42870ae06903a1090"
G_TEX_KETTLE_RMEAO = "5fda814a232ebb9429ebbee119c7b13a"
G_TEX_COOKER_D = "415ef143c7fe1e749a0f3bf5a89f340d"
G_TEX_CUP = "fb0c7a4c0f2f00b4a8d3a58b2484438b"
G_TEX_LIQ_WATER = "962382c3d2f4760478d6a20fa73f3216"
G_TEX_LIQ_MILK = "469a2705713faba44811527071a1c4a3"
G_TEX_LIQ_MILKTEA = "62e1104e98d3f0e4597c3747961594fc"
G_TEX_LIQ_HONEYTEA = "490a3f2b7a02c3f47a75c085067de472"
G_TEX_FLAME = "ce40cab9f9f8a3749bbe8c92b4263786"
G_TEX_DRIP = "cceedcc0ba3f58a4e80504b353fa718c"
G_TEX_NOISE = "d1504e17233c09942b6e8d10a63a031d"
G_AUDIO_BOIL = "d1df4a67ba8e9c148902301bca049c30"
G_AUDIO_POUR = "62c09748da8354743bb0108e82701aa5"
G_ICON_HOTWATER = "d6989d06fdaadbb45963da481b38da35"     # 纹理拷贝后保此 guid
G_ICON_TEA_SIMPLE = "8f93297ac9804e7498be3b5abb539560"
G_ICON_TEA_MILK = "1f21965338cd5e04298be0f3912524bf"
G_ICON_TEA_HONEY = "c1e0c3f14bccc5b45bb90eeec43c0b8e"
G_ICON_TEA_MILK_HONEY = "55d3ad539a7f61444b0550fc1dddd2d4"

# --- 项目既有 guid ---
G_TEA_SO = "550026dedfe271ff327918c9443de8a7"            # commonW5 TeaSO
G_MILK_SO = "7f8657a5292f561670c991f7783a097e"           # common03 DLC03_Milk
G_HONEY_SO = "122895ac50bba48499d9a8ad16e03582"           # common01 HoneycombSO
G_MUG_PLATING_SO = "ecaf3c7db326ed3e46cc60d374d49160"     # commonW5 pseudo_prefab_so/utensils/MugPlatingLocalSO（本地直引 common04 Mug 实体，
                            # 与 web_teacup 的 Plate/AttachedOrderCosmeticDecisions 引用同实例——
                            # 用 bundle 版 Mug 会导致装盘步双实例断链，杯中内容物显示走兜底）
G_MUG_PLATING_LOCAL = "eb494a42c3217e146b9259e5f46e5b82"  # common04 _supplies PlatingStepData/Mug（本地数据资产）
G_COOKINGUI_SO = "14d9cc6b6f1488a4b99909100e2618fd"       # common04 _supplies CookingUI
G_TEMPLATE_PLATE = "7330344a9d4cb9c4da2f2bc0358820b0"     # common04 custom_utensil_plate.prefab

# --- 脚本 guid（宿主 Assets/Scripts，与 Ripped 一致已验证） ---
G_S_INGREDIENT_PROPS = "783f186f6c065949ab472d0813030172"
G_S_PLACEMENT_SPAWNER = "c83e2efdd8f88899b62bbc44666a7aad"
G_S_TRIGGER_TIMER = "feebd72ebe3e61c6fe77baefef90a054"
G_S_STATIC_GRID = "0bd487eae7bb6aa54e539e1dbab88f36"
G_S_ANTICIPATE = "bd19f5e37ff22621b2d51869be523c3e"
G_S_SPECIFIC_TAG = None  # 运行时读取（LevelEditorStub/SpecificPseudoPrefabTag.cs.meta）

# --- 生成资产 guid（确定性） ---
def g_shader(name):
    return det_guid("shader", name)


def g_mat(name):
    return det_guid("mat", name)


def g_mesh(name):
    return det_guid("mesh", name)


def g_prefab(ident):
    return det_guid("prefab", ident)


def g_icon(name):
    return det_guid("icon", name)


G_NODE_HOTWATER = det_guid("node", "Node_HotWater")
G_SO_HOTWATER = det_guid("so", "HotWaterSO")
SHADER_KETTLE = "Custom/WebKettleEmissive"
SHADER_OPAQUE = "Custom/WebOpaqueDiffuse"
SHADER_LIQUID = "Custom/WebLiquid"
SHADER_PFX_ALPHA = "Custom/WebPfxAlpha"
SHADER_PFX_ADD = "Custom/WebPfxAdditive"

# ============ 着色器（Unity 2017.4 / CG） ============

SHADER_KETTLE_SRC = """Shader "Custom/WebKettleEmissive" {
    Properties {
        _ColourAlpha ("Colour/Alpha", 2D) = "white" {}
        _RMEAO ("R/M/E/AO", 2D) = "black" {}
        _Normal ("Normal", 2D) = "bump" {}
        _EmissiveColor ("Emissive Color", Color) = (0.5, 0.5, 0.5, 1)
        _EmissiveStrength ("Emissive Strength", Range(0, 1)) = 0
        _Metallic ("Metallic", Range(0, 1)) = 1
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
    }
    SubShader {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _ColourAlpha;
        sampler2D _RMEAO;
        struct Input { float2 uv_ColourAlpha; };
        half4 _EmissiveColor;
        half _EmissiveStrength;
        half _Metallic;
        half _Glossiness;
        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 c = tex2D(_ColourAlpha, IN.uv_ColourAlpha);
            fixed4 m = tex2D(_RMEAO, IN.uv_ColourAlpha);
            o.Albedo = c.rgb;
            o.Metallic = _Metallic * m.g;
            o.Smoothness = _Glossiness * (1.0 - m.r * 0.5);
            o.Occlusion = m.a;
            o.Emission = _EmissiveColor.rgb * _EmissiveStrength * (0.35 + m.b * 0.65);
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "VertexLit"
}
"""

SHADER_OPAQUE_SRC = """Shader "Custom/WebOpaqueDiffuse" {
    Properties {
        _ColourAlpha ("Colour/Alpha", 2D) = "white" {}
        _Normal ("Normal", 2D) = "bump" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Glossiness ("Smoothness", Range(0, 1)) = 0.35
    }
    SubShader {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _ColourAlpha;
        struct Input { float2 uv_ColourAlpha; };
        half _Metallic;
        half _Glossiness;
        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 c = tex2D(_ColourAlpha, IN.uv_ColourAlpha);
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "VertexLit"
}
"""

SHADER_LIQUID_SRC = """Shader "Custom/WebLiquid" {
    Properties {
        _DiffuseTexture ("Diffuse", 2D) = "white" {}
        _TintColor ("Tint", Color) = (1, 1, 1, 1)
        _ScrollSpeed ("Scroll Speed", Float) = 0.03
    }
    SubShader {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _DiffuseTexture;
            float4 _DiffuseTexture_ST;
            fixed4 _TintColor;
            float _ScrollSpeed;
            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            v2f vert (appdata_base v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _DiffuseTexture);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target {
                float2 uv = i.uv;
                uv.x += _Time.y * _ScrollSpeed;
                fixed4 c = tex2D(_DiffuseTexture, uv) * _TintColor;
                return c;
            }
            ENDCG
        }
    }
}
"""

SHADER_PFX_ALPHA_SRC = """Shader "Custom/WebPfxAlpha" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _TintColor;
            struct appdata_t {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };
            struct v2f {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            v2f vert (appdata_t v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.texcoord.xy;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target {
                return tex2D(_MainTex, i.uv) * i.color * _TintColor;
            }
            ENDCG
        }
    }
}
"""

SHADER_PFX_ADD_SRC = SHADER_PFX_ALPHA_SRC.replace(
    'Shader "Custom/WebPfxAlpha"',
    'Shader "Custom/WebPfxAdditive"').replace(
    "Blend SrcAlpha OneMinusSrcAlpha", "Blend SrcAlpha One")


# ============ YAML 模板 ============

def mb_stub(fid, go, script_guid, fields):
    return ("--- !u!114 &%s\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 1\n"
            "  m_PrefabParentObject: {fileID: 0}\n"
            "  m_PrefabInternal: {fileID: 100100000}\n"
            "  m_GameObject: {fileID: %s}\n"
            "  m_Enabled: 1\n"
            "  m_EditorHideFlags: 0\n"
            "  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"
            "  m_Name: \n"
            "  m_EditorClassIdentifier: \n%s" % (fid, go, script_guid, fields))


_FID = [1910000000]  # 根 GO 固定 1900000001，组件/子物体从 1910000001 起（防撞）


def fid_new():
    _FID[0] += 1
    return str(_FID[0])


def go_doc(fid, name, comps, layer, tag="Untagged", active=1):
    return ("--- !u!1 &%s\nGameObject:\n"
            "  m_ObjectHideFlags: 0\n"
            "  m_PrefabParentObject: {fileID: 0}\n"
            "  m_PrefabInternal: {fileID: 100100000}\n"
            "  serializedVersion: 5\n"
            "  m_Component:\n%s\n"
            "  m_Layer: %d\n"
            "  m_Name: %s\n"
            "  m_TagString: %s\n"
            "  m_Icon: {fileID: 0}\n"
            "  m_NavMeshLayer: 0\n"
            "  m_StaticEditorFlags: 0\n"
            "  m_IsActive: %d\n" %
            (fid, "\n".join("  - component: {fileID: %s}" % c for c in comps),
             layer, name, tag, active))


def tr_doc(fid, go, pos, rot, scale, children, father="0"):
    # 空 children 必须同行 "m_Children: []"；换行写 "  []" 会让 2017 加载失败
    children_yaml = ("  m_Children:\n" +
                     "\n".join("  - {fileID: %s}" % c for c in children)) if children \
        else "  m_Children: []"
    return ("--- !u!4 &%s\nTransform:\n"
            "  m_ObjectHideFlags: 1\n"
            "  m_PrefabParentObject: {fileID: 0}\n"
            "  m_PrefabInternal: {fileID: 100100000}\n"
            "  m_GameObject: {fileID: %s}\n"
            "  m_LocalRotation: %s\n"
            "  m_LocalPosition: %s\n"
            "  m_LocalScale: %s\n"
            "%s\n"
            "  m_Father: {fileID: %s}\n"
            "  m_RootOrder: 0\n"
            "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n" %
            (fid, go,
             "{x: %s, y: %s, z: %s, w: %s}" % rot,
             "{x: %s, y: %s, z: %s}" % pos,
             "{x: %s, y: %s, z: %s}" % scale,
             children_yaml,
             father))


def collider_doc(fid, go, size, center, trigger):
    return ("--- !u!65 &%s\nBoxCollider:\n"
            "  m_ObjectHideFlags: 1\n"
            "  m_PrefabParentObject: {fileID: 0}\n"
            "  m_PrefabInternal: {fileID: 100100000}\n"
            "  m_GameObject: {fileID: %s}\n"
            "  m_Material: {fileID: 0}\n"
            "  m_IsTrigger: %d\n"
            "  m_Enabled: 1\n"
            "  serializedVersion: 2\n"
            "  m_Size: {x: %s, y: %s, z: %s}\n"
            "  m_Center: {x: %s, y: %s, z: %s}\n" %
            (fid, go, trigger, size[0], size[1], size[2], center[0], center[1], center[2]))


def meshfilter_doc(fid, go, mesh_guid):
    return ("--- !u!33 &%s\nMeshFilter:\n"
            "  m_ObjectHideFlags: 1\n"
            "  m_PrefabParentObject: {fileID: 0}\n"
            "  m_PrefabInternal: {fileID: 100100000}\n"
            "  m_GameObject: {fileID: %s}\n"
            "  m_Mesh: {fileID: 4300000, guid: %s, type: 3}\n" % (fid, go, mesh_guid))


def meshrenderer_doc(fid, go, mat_guid):
    return ("--- !u!23 &%s\nMeshRenderer:\n"
            "  m_ObjectHideFlags: 1\n"
            "  m_PrefabParentObject: {fileID: 0}\n"
            "  m_PrefabInternal: {fileID: 100100000}\n"
            "  m_GameObject: {fileID: %s}\n"
            "  m_Enabled: 1\n"
            "  m_CastShadows: 1\n"
            "  m_ReceiveShadows: 1\n"
            "  m_MotionVectors: 1\n"
            "  m_LightProbeUsage: 1\n"
            "  m_ReflectionProbeUsage: 1\n"
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
            "  m_SortingLayerID: 0\n"
            "  m_SortingLayer: 0\n"
            "  m_SortingOrder: 0\n" % (fid, go, mat_guid))


def prefab_header(root_go):
    return ("--- !u!1001 &100100000\nPrefab:\n"
            "  m_ObjectHideFlags: 1\n"
            "  serializedVersion: 2\n"
            "  m_Modification:\n"
            "    m_TransformParent: {fileID: 0}\n"
            "    m_Modifications: []\n"
            "    m_RemovedComponents: []\n"
            "  m_ParentPrefab: {fileID: 0}\n"
            "  m_RootGameObject: {fileID: %s}\n"
            "  m_IsPrefabParent: 1\n" % root_go)


def audio_source_doc(fid, go, clip_guid):
    return ("--- !u!82 &%s\nAudioSource:\n"
            "  m_ObjectHideFlags: 1\n"
            "  m_PrefabParentObject: {fileID: 0}\n"
            "  m_PrefabInternal: {fileID: 100100000}\n"
            "  m_GameObject: {fileID: %s}\n"
            "  m_Enabled: 1\n"
            "  serializedVersion: 4\n"
            "  m_audioClip: {fileID: 8300000, guid: %s, type: 3}\n"
            "  m_PlayOnAwake: 0\n"
            "  m_Volume: 0.8\n"
            "  m_Pitch: 1\n"
            "  Loop: 0\n"
            "  Mute: 0\n"
            "  Spatialize: 0\n"
            "  SpatializePostEffects: 0\n"
            "  Priority: 128\n"
            "  DopplerLevel: 1\n"
            "  MinDistance: 1\n"
            "  MaxDistance: 15\n"
            "  Pan2D: 0\n"
            "  rolloffMode: 1\n"
            "  BypassEffects: 0\n"
            "  BypassListenerEffects: 0\n"
            "  BypassReverbZones: 0\n"
            "  reverbZoneMix: 1\n"
            "  m_3DSound: 1\n"
            "  use3DPosition: 0\n" % (fid, go, clip_guid))


def sprite_meta(guid, max_size=512):
    return """fileFormatVersion: 2
guid: %s
TextureImporter:
  fileIDToRecycleName: {}
  externalObjects: {}
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
    wrapW: -1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
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
    maxTextureSize: %d
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
  spritePackingTag: 
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""" % (guid, max_size)


def pseudo_so_yaml(name, asset_ref):
    """PseudoPrefabSO（LevelEditorStub，直引本地资产）。"""
    return ("""%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 0cff7c13895ab9e47a5e02d4619cc3b9, type: 3}
  m_Name: %s
  m_EditorClassIdentifier: 
  prefabName: %s
  bundleName: 
  assetPath: 
  asset: %s
""" % (name, name, asset_ref))


CUSTOM_RECIPE_SCRIPT = "83fb008bcc8e793429b02c178c430815"
INGREDIENT_NODE_SCRIPT = "bff1936f87185a161a744a226a12f22d"
SPECIFIC_TAG_SCRIPT = "2b19728b6e0dfe44baf2f80df9bfc1e9"  # SpecificPseudoPrefabTag（运行时校验）


def recipe_yaml(ident, uid, score, comp_guids, icon_guid, model_so_guid,
                plating_so_guid=G_MUG_PLATING_SO, rtype=1):
    comps = "\n".join(
        "  - {fileID: 11400000, guid: %s, type: 2}" % g for g in comp_guids)
    icon = ("{fileID: 21300000, guid: %s, type: 3}" % icon_guid
            if icon_guid else "{fileID: 0}")
    return ("""%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name: %s
  m_EditorClassIdentifier: 
  type: %d
  recipeName: %s
  uID: %d
  score: %d
  platingStepSO: {fileID: 11400000, guid: %s, type: 2}
  modelSO: {fileID: 11400000, guid: %s, type: 2}
  model: {fileID: 0}
  iconSO: {fileID: 0}
  icon: %s
  compositionSOs:
%s
  optionalSOs: []
  cookingStepSO: {fileID: 0}
  cookingStepIconSO: {fileID: 0}
  cookingStepIcon: {fileID: 0}
  cookingProgress: 0
  mixingIconSO: {fileID: 0}
  mixingIcon: {fileID: 0}
  mixingProgress: 0
""" % (CUSTOM_RECIPE_SCRIPT, ident, rtype, ident, uid, score,
       plating_so_guid, model_so_guid, icon, comps if comps else "  []"))


def node_yaml(name, uid, icon_guid, colour=(0.8820755, 1, 1, 1)):
    return ("""%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name: %s
  m_EditorClassIdentifier: 
  m_orderGuiDescription: []
  m_platingStep: {fileID: 0}
  m_platingPrefab: {fileID: 0}
  m_uID: %d
  m_iconSprite: {fileID: 21300000, guid: %s, type: 3}
  m_crateLid:
    m_atlasTexture: {fileID: 0}
    m_subRect:
      serializedVersion: 2
      x: 0
      y: 0
      width: 0
      height: 0
  m_colour: {r: %s, g: %s, b: %s, a: %s}
""" % (INGREDIENT_NODE_SCRIPT, name, uid, icon_guid,
       colour[0], colour[1], colour[2], colour[3]))


# ============ 场景提取（ParticleSystem 等） ============

SCENE_REL = "DownloadableContent/DLC100/DLC_Assets/Scenes/s_birthday_1_3.unity"


def load_scene_docs():
    text = open(os.path.join(RIPPED, SCENE_REL), encoding="utf-8").read()
    docs = {}
    for m in re.finditer(r"^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)", text, re.S | re.M):
        docs[(m.group(1), m.group(2))] = m.group(3)
    return docs


def remap_scene_doc(class_id, body, old_go, new_go, mat_map=None, new_fid=None):
    """场景 doc → prefab doc：重建 doc 头（load_scene_docs 捕获时已剥离原头）+
    2019→2017 + GO 反引重映射 + 材质重映射。new_fid 为本 prefab 内的新 doc id。"""
    body = rewrite_header(body)
    body = body.replace("m_GameObject: {fileID: %s}" % old_go,
                        "m_GameObject: {fileID: %s}" % new_go)
    # m_ObjectHideFlags 0 → 1（prefab 组件）
    body = body.replace("m_ObjectHideFlags: 0", "m_ObjectHideFlags: 1", 1)
    # 补 m_PrefabInternal（场景 doc 无该字段：2019 头已换成 2017 两行，其中
    # m_PrefabInternal 值为 {fileID: 0}，prefab 内应指向 100100000）
    body = body.replace(
        "m_PrefabParentObject: {fileID: 0}\n  m_PrefabInternal: {fileID: 0}",
        "m_PrefabParentObject: {fileID: 0}\n  m_PrefabInternal: {fileID: 100100000}")
    if mat_map:
        for old_guid, new_guid in mat_map.items():
            body = body.replace("guid: %s" % old_guid, "guid: %s" % new_guid)
    return "--- !u!%s &%s\n%s" % (class_id, new_fid, body)


# ============ 主流程 ============

def preflight_guid_collision():
    """保留 guid 拷贝前扫全项目 meta，防 guid 冲突。"""
    keep = {
        G_CTRL_KETTLE, G_CLIP_COLD, G_CLIP_HEATING, G_CLIP_HOT, G_MAT_KETTLE,
        G_MAT_SPOON, G_TEX_KETTLE_D, G_TEX_KETTLE_RMEAO, G_TEX_COOKER_D,
        G_TEX_CUP, G_TEX_LIQ_WATER, G_TEX_LIQ_MILK, G_TEX_LIQ_MILKTEA,
        G_TEX_LIQ_HONEYTEA, G_TEX_FLAME, G_TEX_DRIP, G_TEX_NOISE,
        G_AUDIO_BOIL, G_AUDIO_POUR, G_ICON_HOTWATER, G_ICON_TEA_SIMPLE,
        G_ICON_TEA_MILK, G_ICON_TEA_HONEY, G_ICON_TEA_MILK_HONEY,
    }
    hits = []
    for root, _dirs, files in os.walk(os.path.join(REPO, "Assets")):
        if root.startswith(os.path.join(REPO, "Assets", "commonW5") + os.sep):
            continue  # 本生成器自己的输出，不算冲突
        for fn in files:
            if not fn.endswith(".meta"):
                continue
            p = os.path.join(root, fn)
            try:
                head = open(p, encoding="utf-8", errors="ignore").read(200)
            except OSError:
                continue
            m = re.search(r"^guid: ([0-9a-f]{32})$", head, re.M)
            if m and m.group(1) in keep:
                hits.append("%s -> %s" % (m.group(1), os.path.relpath(p, REPO)))
    if hits:
        raise SystemExit("guid 冲突（项目已有同名 guid 资产）：\n" + "\n".join(hits))
    out("[preflight] guid 冲突检查通过（%d 个保留 guid）" % len(keep))


def ensure_folders():
    folders = [
        "kettle", "kettle/meshes", "kettle/animations", "kettle/materials",
        "kettle/shaders", "kettle/textures", "kettle/audio",
        "prefabs", "prefabs/kettle", "prefabs/teacups",
        "custom_recipes/milk_tea", "custom_recipes/milk_tea/icons",
        "custom_recipes/milk_tea/models", "custom_recipes/milk_tea/model_so",
        "ingredients/hotwater",
    ]
    for rel in folders:
        write_file(rel + ".meta",
                   "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\n"
                   "DefaultImporter:\n  externalObjects: {}\n  userData: \n"
                   "  assetBundleName: \n  assetBundleVariant: \n" % folder_guid(rel))
    write_file("custom_recipes/milk_tea/models.meta",
               "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\n"
               "DefaultImporter:\n  externalObjects: {}\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n" % folder_guid("custom_recipes/milk_tea/models"))


MESH_CONV = [
    ("Mesh/m_Kettle_Anniversary_01.asset", "kettle/meshes/m_kettle_anniversary_01.obj"),
    ("Mesh/m_sk_cooker_Anniversary_01.asset", "kettle/meshes/m_sk_cooker_anniversary_01.obj"),
    ("Mesh/Cup_0.asset", "kettle/meshes/m_dlc100_teacup_cup.obj"),
    ("Mesh/m_dlc100_teacup_spoon2_0.asset", "kettle/meshes/m_dlc100_teacup_drink_surface.obj"),
    ("Mesh/m_dlc100_teacup_spoon_0.asset", "kettle/meshes/m_dlc100_teacup_spoon.obj"),
]


def gen_meshes():
    out("[meshes] Mesh(sv10) → OBJ")
    guids = {}
    for src, dst in MESH_CONV:
        ident = os.path.splitext(os.path.basename(dst))[0]
        guid = g_mesh(ident)
        guids[ident] = guid
        final = os.path.join(W5, dst)
        if os.path.exists(final):
            out("  已存在跳过: %s" % dst)
            continue
        mesh = parse_mesh_asset(os.path.join(RIPPED, src))
        obj_text = mesh_convert(mesh)
        if APPLY:
            os.makedirs(os.path.dirname(final), exist_ok=True)
            open(final, "w").write(obj_text)
        out("  转换: %s (%d verts)" % (dst, mesh["vertex_count"]))
        write_meta(dst, guid)
    return guids


def gen_copied_assets():
    out("[copy] 控制器/动画/材质/贴图/音频 1:1 拷贝（2019→2017 头部重写）")
    copy_asset_header2017("AnimatorController/DLC0100_Kettle.controller",
                          "kettle/animations/DLC0100_Kettle.controller")
    for clip in ("KettleCold", "KettleHeating", "KettleHot"):
        copy_asset_header2017("AnimationClip/%s.anim" % clip,
                              "kettle/animations/%s.anim" % clip)
    # 水壶主体材质：保 guid + 换 shader（_ColourAlpha/_RMEAO/_EmissiveStrength 绑定全保留）
    src = os.path.join(RIPPED, "Material/mat_Kettle_Anniversary_01.mat")
    text = rewrite_header(open(src, encoding="utf-8").read())
    text = text.replace(
        "m_Shader: {fileID: 4800000, guid: 964920b5c255e9c4f859c4c5da183fbd, type: 3}",
        "m_Shader: {fileID: 4800000, guid: %s, type: 3}" % g_shader("WebKettleEmissive"))
    write_file("kettle/materials/mat_Kettle_Anniversary_01.mat", text)
    meta = open(src + ".meta", encoding="utf-8").read()
    write_file("kettle/materials/mat_Kettle_Anniversary_01.mat.meta", meta)
    # 勺子白瓷材质（builtin Standard(Specular) 可直接用）
    copy_asset("Material/mat_Birthday_TeaCup_01_6.mat",
               "kettle/materials/mat_Birthday_TeaCup_01_6.mat")
    texs = [
        ("Texture2D/t_dlc100_Kettle_Anniversary_01_d.png", "kettle/textures/t_dlc100_Kettle_Anniversary_01_d.png"),
        ("Texture2D/t_dlc100_Kettle_Anniversary_01_RMEAO.png", "kettle/textures/t_dlc100_Kettle_Anniversary_01_RMEAO.png"),
        ("Texture2D/t_dlc100_Cooker_Anniversary_01_d.png", "kettle/textures/t_dlc100_Cooker_Anniversary_01_d.png"),
        ("Texture2D/t_Birthday_TeaCup_Colour_2.png", "kettle/textures/t_Birthday_TeaCup_Colour_2.png"),
        ("Texture2D/t_Birthday_Tea_Water_0.png", "kettle/textures/t_Birthday_Tea_Water_0.png"),
        ("Texture2D/t_Birthday_Tea_Milk_0.png", "kettle/textures/t_Birthday_Tea_Milk_0.png"),
        ("Texture2D/t_Birthday_Tea_Milktea_0.png", "kettle/textures/t_Birthday_Tea_Milktea_0.png"),
        ("Texture2D/t_Birthday_Tea_Honeytea_0.png", "kettle/textures/t_Birthday_Tea_Honeytea_0.png"),
        ("Particles/Textures/t_pfx_gassFlame_01.png", "kettle/textures/t_pfx_gassFlame_01.png"),
        ("Texture2D/t_pfx_water_drop_01_d.png", "kettle/textures/t_pfx_water_drop_01_d.png"),
        ("Texture2D/noise_round.png", "kettle/textures/noise_round.png"),
    ]
    for src_rel, dst_rel in texs:
        copy_asset(src_rel, dst_rel)
    copy_asset("AudioClip/DLC_100_Kettle_Dispenser_Boil_01.ogg",
               "kettle/audio/DLC_100_Kettle_Dispenser_Boil_01.ogg")
    copy_asset("AudioClip/DLC_100_Kettle_Dispenser_Pour_01.ogg",
               "kettle/audio/DLC_100_Kettle_Dispenser_Pour_01.ogg")


def gen_shaders():
    out("[shaders] 自建着色器（Shader Forge 为占位导出不可用）")
    for name, src in (
            ("WebKettleEmissive", SHADER_KETTLE_SRC),
            ("WebOpaqueDiffuse", SHADER_OPAQUE_SRC),
            ("WebLiquid", SHADER_LIQUID_SRC),
            ("WebPfxAlpha", SHADER_PFX_ALPHA_SRC),
            ("WebPfxAdditive", SHADER_PFX_ADD_SRC)):
        rel = "kettle/shaders/%s.shader" % name
        write_file(rel, src)
        write_meta(rel, g_shader(name))


def _fresh_mat(name, shader_name, props_yaml):
    """新建材质（确定性 guid，属性绑定显式写入）。
    空属性段必须写 []——2017 YAML 解析器对空映射（null 节点）报
    "Unexpected node type."（mat_web_pfx_* 曾因此报错）。"""
    guid = g_mat(name)
    tex = props_yaml[0] if props_yaml[0].strip() else ""
    flt = props_yaml[1] if props_yaml[1].strip() else ""
    col = props_yaml[2] if props_yaml[2].strip() else ""
    text = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 6
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_Name: %s
  m_Shader: {fileID: 4800000, guid: %s, type: 3}
  m_ShaderKeywords: 
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:%s
%s    m_Floats:%s
%s    m_Colors:%s
%s""" % (name, g_shader(shader_name),
            "" if tex else " []", tex,
            "" if flt else " []", flt,
            "" if col else " []", col)
    write_file("kettle/materials/%s.mat" % name, text)
    write_meta("kettle/materials/%s.mat" % name, guid)
    return guid


def _tex_env(prop, tex_guid):
    return ("      %s:\n"
            "        m_Texture: {fileID: 2800000, guid: %s, type: 3}\n"
            "        m_Scale: {x: 1, y: 1}\n"
            "        m_Offset: {x: 0, y: 0}\n" % (prop, tex_guid))


def gen_materials():
    out("[materials] 新建材质（灶座/茶杯/液体×4/粒子×3）")
    mats = {}
    mats["cooker"] = _fresh_mat(
        "mat_web_cooker", "WebOpaqueDiffuse",
        (_tex_env("_ColourAlpha", G_TEX_COOKER_D),
         "      _Metallic: 0\n      _Glossiness: 0.35\n",
         "      - _Color: {r: 1, g: 1, b: 1, a: 1}\n"))
    mats["teacup"] = _fresh_mat(
        "mat_web_teacup", "WebOpaqueDiffuse",
        (_tex_env("_ColourAlpha", G_TEX_CUP),
         "      _Metallic: 0\n      _Glossiness: 0.3\n",
         "      - _Color: {r: 1, g: 1, b: 1, a: 1}\n"))
    for key, tex in (("water", G_TEX_LIQ_WATER), ("milk", G_TEX_LIQ_MILK),
                     ("milktea", G_TEX_LIQ_MILKTEA), ("honeytea", G_TEX_LIQ_HONEYTEA)):
        mats[key] = _fresh_mat(
            "mat_web_liquid_%s" % key, "WebLiquid",
            (_tex_env("_DiffuseTexture", tex),
             "      _ScrollSpeed: 0.03\n",
             "      - _TintColor: {r: 1, g: 1, b: 1, a: 0.92}\n"))
    mats["pfx_steam"] = _fresh_mat(
        "mat_web_pfx_steam", "WebPfxAlpha",
        (_tex_env("_MainTex", G_TEX_NOISE),
         "",
         "      - _TintColor: {r: 0.85, g: 0.9, b: 0.95, a: 0.5}\n"))
    mats["pfx_fire"] = _fresh_mat(
        "mat_web_pfx_fire", "WebPfxAdditive",
        (_tex_env("_MainTex", G_TEX_FLAME),
         "",
         "      - _TintColor: {r: 1, g: 0.8, b: 0.5, a: 1}\n"))
    mats["pfx_drip"] = _fresh_mat(
        "mat_web_pfx_drip", "WebPfxAlpha",
        (_tex_env("_MainTex", G_TEX_DRIP),
         "",
         "      - _TintColor: {r: 0.8, g: 0.92, b: 1, a: 0.85}\n"))
    return mats


def gen_kettle_prefab(mesh_guids, mats):
    out("[prefab] web_kettle_hotwater（场景子树提取重建）")
    docs_scene = load_scene_docs()
    parts = [prefab_header("1900000001")]

    root_comps = []
    root_comps.append(fid_new())   # transform
    c_collider = fid_new()
    root_comps.append(c_collider)
    c_ingprops = fid_new()
    root_comps.append(c_ingprops)
    c_spawner = fid_new()
    root_comps.append(c_spawner)
    c_timer = fid_new()
    root_comps.append(c_timer)
    c_static = fid_new()
    root_comps.append(c_static)
    c_anticip = fid_new()
    root_comps.append(c_anticip)
    c_anim = fid_new()
    root_comps.append(c_anim)
    c_tag = fid_new()
    root_comps.append(c_tag)
    parts.append(go_doc("1900000001", "web_kettle_hotwater", root_comps, 11, "Crate"))

    # 子物体（ItemAttach / Block / MeshParent(M_Kettle,M_Cooker) / Back / PFX×3 / SFX×2）
    def child_go(name, layer, comps_builder, active=1):
        go = fid_new()
        tr = fid_new()
        comps = [tr] + comps_builder
        return go, tr, comps, (name, layer, comps, go, active)

    items = []

    ia_go, ia_tr, _, ia_meta = child_go("ItemAttach", 11, [])
    items.append(ia_meta)
    bl_go, bl_tr, bl_comps, bl_meta = child_go("Block", 26, [fid_new()])
    items.append(bl_meta)
    mp_go, mp_tr, mp_comps, mp_meta = child_go("MeshParent", 11, [fid_new()])
    items.append(mp_meta)
    mk_go, mk_tr, mk_comps, mk_meta = child_go("M_Kettle", 0, [fid_new(), fid_new()])
    items.append(mk_meta)
    mc_go, mc_tr, mc_comps, mc_meta = child_go("M_Cooker", 0, [fid_new(), fid_new()])
    items.append(mc_meta)
    bk_go, bk_tr, bk_comps, bk_meta = child_go("Back", 11, [fid_new()])
    items.append(bk_meta)
    st_go, st_tr, st_comps, st_meta = child_go("PFX_Steam", 14, [fid_new(), fid_new()], active=0)
    items.append(st_meta)
    fl_go, fl_tr, fl_comps, fl_meta = child_go("PFX_Flames", 0,
                                               [fid_new(), fid_new()], active=0)
    items.append(fl_meta)
    dr_go, dr_tr, dr_comps, dr_meta = child_go("PFX_Drip", 14, [fid_new(), fid_new()], active=0)
    items.append(dr_meta)
    bb_go, bb_tr, bb_comps, bb_meta = child_go("SFX_Boil", 0, [fid_new()])
    items.append(bb_meta)
    bp_go, bp_tr, bp_comps, bp_meta = child_go("SFX_Pour", 0, [fid_new()])
    items.append(bp_meta)

    # 根 Transform
    parts.append(tr_doc(root_comps[0], "1900000001", ("0", "0", "0"),
                         ("0", "0", "0", "1"), ("1", "1", "1"),
                         [ia_tr, bl_tr, mp_tr, bk_tr, st_tr, fl_tr, dr_tr, bb_tr, bp_tr]))
    # 根组件
    parts.append(collider_doc(c_collider, "1900000001", (1.2, 0.5, 1.2), (0, 0.75, 0), 1))
    parts.append(mb_stub(c_ingprops, "1900000001", G_S_INGREDIENT_PROPS,
                         "  m_ingredientOrderNode: {fileID: 11400000, guid: %s, type: 2}\n"
                         % G_NODE_HOTWATER))
    parts.append(mb_stub(c_spawner, "1900000001", G_S_PLACEMENT_SPAWNER,
                         "  m_pickupPriority: 1\n"
                         "  m_condimentAchievementFilter: {fileID: 0}\n"))
    parts.append(mb_stub(c_timer, "1900000001", G_S_TRIGGER_TIMER,
                         "  m_startTrigger: Switch\n"
                         "  m_completeTrigger: Active\n"
                         "  m_time: 5\n"  # 默认 5s（与 Kettle.m_heatSeconds 镜像；0/负值运行时兜底 5s）
                         "  m_CookingUIPrefab: {fileID: 0}\n"
                         "  m_progressUIPrefabOffset: {x: 0, y: 2, z: 0}\n"
                         "  m_startTiming: 0\n"
                         "  m_triggerAtStart: 0\n"))
    parts.append(mb_stub(c_static, "1900000001", G_S_STATIC_GRID,
                         "  m_gridManager: {fileID: 0}\n"
                         "  m_method: 0\n"))
    parts.append(mb_stub(c_anticip, "1900000001", G_S_ANTICIPATE,
                         "  m_brightnessModifier: 1.5\n"
                         "  m_highlightObjectOverride: {fileID: 0}\n"))
    parts.append(("--- !u!95 &%s\nAnimator:\n"
                  "  m_ObjectHideFlags: 1\n"
                  "  m_PrefabParentObject: {fileID: 0}\n"
                  "  m_PrefabInternal: {fileID: 100100000}\n"
                  "  m_GameObject: {fileID: 1900000001}\n"
                  "  m_Enabled: 1\n"
                  "  m_Avatar: {fileID: 0}\n"
                  "  m_Controller: {fileID: 9100000, guid: %s, type: 2}\n"
                  "  m_CullingMode: 0\n"
                  "  m_UpdateMode: 0\n"
                  "  m_ApplyRootMotion: 0\n"
                  "  m_LinearVelocityBlending: 0\n"
                  "  m_WarningMessage: \n"
                  "  m_HasTransformHierarchy: 1\n"
                  "  m_AllowConstantClipSamplingOptimization: 1\n"
                  % (c_anim, G_CTRL_KETTLE)))
    parts.append(mb_stub(c_tag, "1900000001", SPECIFIC_TAG_SCRIPT,
                         "  prefabTag: Kettle|\n"))

    # ItemAttach
    parts.append(go_doc(ia_go, "ItemAttach", [ia_tr], 11))
    parts.append(tr_doc(ia_tr, ia_go, ("-0", "0.5", "-0.329"), ("0", "0", "0", "1"),
                         ("1", "1", "1"), []))
    # Block
    parts.append(go_doc(bl_go, "Block", bl_comps, 26))
    parts.append(tr_doc(bl_tr, bl_go, ("0", "0", "0"), ("0", "0", "0", "1"),
                         ("1", "1", "1"), []))
    parts.append(collider_doc(bl_comps[1], bl_go, (1.2, 2, 1.2), (0, 1, 0), 0))
    # MeshParent
    parts.append(go_doc(mp_go, "MeshParent", mp_comps, 11))
    parts.append(tr_doc(mp_tr, mp_go, ("0", "0", "0"), ("0", "0", "0", "1"),
                         ("1", "1", "1"), [mk_tr, mc_tr]))
    parts.append(collider_doc(mp_comps[1], mp_go, (1.2, 0.5, 1.2), (0, 0.25, 0), 0))
    # M_Kettle
    parts.append(go_doc(mk_go, "M_Kettle", mk_comps, 0))
    parts.append(tr_doc(mk_tr, mk_go, ("-0", "0.482", "0"), ("0", "0", "0", "1"),
                         ("1", "1", "1"), []))
    parts.append(meshfilter_doc(mk_comps[1], mk_go,
                                mesh_guids["m_kettle_anniversary_01"]))
    parts.append(meshrenderer_doc(mk_comps[2], mk_go, G_MAT_KETTLE))
    # M_Cooker
    parts.append(go_doc(mc_go, "M_Cooker", mc_comps, 0))
    parts.append(tr_doc(mc_tr, mc_go, ("-0", "0", "0"), ("0", "0", "0", "1"),
                         ("1", "1", "1"), []))
    parts.append(meshfilter_doc(mc_comps[1], mc_go,
                                mesh_guids["m_sk_cooker_anniversary_01"]))
    parts.append(meshrenderer_doc(mc_comps[2], mc_go, mats["cooker"]))
    # Back
    parts.append(go_doc(bk_go, "Back", bk_comps, 11))
    parts.append(tr_doc(bk_tr, bk_go, ("0", "0", "0"),
                         ("-0", "6.7055214e-07", "-0", "1"), ("1", "1", "1"), []))
    parts.append(collider_doc(bk_comps[1], bk_go, (1.2, 1.6, 0.6), (0, 1.3, 0.3), 0))

    # PFX：场景逐字拷贝（PS + PSRenderer），GO 反引与材质重映射
    steam_map = {"4282d750f8a76fd41ab42a8dcab1ab15": mats["pfx_steam"]}
    parts.append(go_doc(st_go, "PFX_Steam", st_comps, 14, active=0))
    parts.append(tr_doc(st_tr, st_go, ("0", "2.009", "-0.411"),
                         ("-0.7071068", "-0", "-0", "0.7071068"), ("1", "1", "1"), []))
    parts.append(remap_scene_doc("198", docs_scene[("198", "2254")], "838", st_go, None, st_comps[1]))
    parts.append(remap_scene_doc("199", docs_scene[("199", "2282")], "838", st_go, steam_map, st_comps[2]))

    parts.append(go_doc(fl_go, "PFX_Flames", fl_comps, 0, active=0))
    parts.append(tr_doc(fl_tr, fl_go, ("0", "0.642", "0"),
                         ("-0", "0.70710576", "-0", "0.70710784"),
                         ("1", "1.47", "1"), []))
    parts.append(meshfilter_builtin_quad(fl_comps[1], fl_go))
    parts.append(meshrenderer_doc(fl_comps[2], fl_go, mats["pfx_fire"]))

    drip_map = {"315a988e5e6b59e49bb6508874e76f57": mats["pfx_drip"]}
    parts.append(go_doc(dr_go, "PFX_Drip", dr_comps, 14, active=0))
    parts.append(tr_doc(dr_tr, dr_go, ("0.001", "1.101", "-0.943"),
                         ("-0.7071068", "-0", "-0", "0.7071068"), ("1", "1", "1"), []))
    parts.append(remap_scene_doc("198", docs_scene[("198", "2250")], "792", dr_go, None, dr_comps[1]))
    parts.append(remap_scene_doc("199", docs_scene[("199", "2286")], "792", dr_go, drip_map, dr_comps[2]))

    # SFX
    parts.append(go_doc(bb_go, "SFX_Boil", bb_comps, 0))
    parts.append(tr_doc(bb_tr, bb_go, ("0", "1.2", "0"), ("0", "0", "0", "1"),
                         ("1", "1", "1"), []))
    parts.append(audio_source_doc(bb_comps[1], bb_go, G_AUDIO_BOIL))
    parts.append(go_doc(bp_go, "SFX_Pour", bp_comps, 0))
    parts.append(tr_doc(bp_tr, bp_go, ("0", "1.1", "-0.6"), ("0", "0", "0", "1"),
                         ("1", "1", "1"), []))
    parts.append(audio_source_doc(bp_comps[1], bp_go, G_AUDIO_POUR))

    text = YAML_HEADER + "".join(parts)
    rel = "prefabs/kettle/web_kettle_hotwater.prefab"
    write_file(rel, text)
    write_meta(rel, g_prefab("web_kettle_hotwater"))
    return g_prefab("web_kettle_hotwater")


def meshfilter_builtin_quad(fid, go):
    return ("--- !u!33 &%s\nMeshFilter:\n"
            "  m_ObjectHideFlags: 1\n"
            "  m_PrefabParentObject: {fileID: 0}\n"
            "  m_PrefabInternal: {fileID: 100100000}\n"
            "  m_GameObject: {fileID: %s}\n"
            "  m_Mesh: {fileID: 10210, guid: 0000000000000000e000000000000000, type: 0}\n"
            % (fid, go))


def gen_mug_and_stack(mesh_guids, mats):
    out("[prefab] web_teacup（茶杯） + web_teacup_stack_clean（干净茶杯堆）（克隆 common04 模板）")
    plate = open(os.path.join(REPO, "Assets/common04/custom_utensil/custom_utensil_plate.prefab"),
                 encoding="utf-8").read()
    mug_guid = g_prefab("web_mug_teacup")  # guid 锚保留旧 id（改名前），勿改：改了会换 guid
    t = plate
    t = t.replace("m_Name: custom_utensil_plate", "m_Name: web_teacup")
    t = t.replace("m_Name: Plate\n", "m_Name: Cup\n")
    # 网格：builtin 圆柱 → DLC100 茶杯网格（茶杯原版变换：pos 0 / rotY≈150° / scale 2.5）
    t = t.replace(
        "m_Mesh: {fileID: 10206, guid: 0000000000000000e000000000000000, type: 0}",
        "m_Mesh: {fileID: 4300000, guid: %s, type: 3}" % mesh_guids["m_dlc100_teacup_cup"])
    t = t.replace("  m_LocalPosition: {x: -0, y: 0.05, z: 0}",
                  "  m_LocalPosition: {x: 0, y: 0, z: 0}")
    t = t.replace(
        "m_LocalRotation: {x: 0, y: -0, z: -0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 0.1, z: 1}",
        "m_LocalRotation: {x: -0, y: 0.97075945, z: -0, w: 0.24005422}\n"
        "  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 2.5, y: 2.5, z: 2.5}")
    # 材质：模板占位材质 → 茶杯白瓷
    t = t.replace("guid: 7d2df6961ae6f3049a823adc8ebbb1ae", "guid: " + mats["teacup"])
    # 装盘数据：Plate → 本地 Mug（AttachedOrderCosmeticDecisions + Plate 两处）
    t = t.replace("guid: 33cdd9a977e652844ba3e2f5a8b23ce7", "guid: " + G_MUG_PLATING_LOCAL)
    # 碰撞体：马克杯尺寸（DLC03 equipment_mug_01 实测）
    t = t.replace("m_Size: {x: 0.7, y: 0.1, z: 0.7}\n  m_Center: {x: 0, y: 0.05, z: 0}",
                  "m_Size: {x: 0.5, y: 0.73, z: 0.5}\n  m_Center: {x: 0, y: 0.36, z: 0}")
    t = t.replace("m_Size: {x: 0.7, y: 0.55, z: 0.7}\n  m_Center: {x: 0, y: 0.375, z: 0}",
                  "m_Size: {x: 0.7, y: 0.73, z: 0.7}\n  m_Center: {x: 0, y: 0.36, z: 0}")
    rel = "prefabs/teacups/web_teacup.prefab"
    write_file(rel, t)
    write_meta(rel, mug_guid)

    stack = open(os.path.join(
        REPO, "Assets/common04/custom_utensil/custom_utensil_clean_plate_stack.prefab"),
        encoding="utf-8").read()
    s = stack
    s = s.replace("m_Name: custom_utensil_clean_plate_stack", "m_Name: web_teacup_stack_clean")
    # 出生物：模板盘子 → 茶杯马克杯（模板根 GO fileID 1003597553421588 不变）
    s = s.replace(
        "m_platePrefab: {fileID: 1003597553421588, guid: %s," % G_TEMPLATE_PLATE,
        "m_platePrefab: {fileID: 1003597553421588, guid: %s," % mug_guid)
    # 马克杯堆叠高度（原版 CleanMugStack 0.32）
    s = s.replace("m_itemTag: Plate\n  m_heightOffset: 0.1",
                  "m_itemTag: Plate\n  m_heightOffset: 0.32")
    # 去掉模板内嵌 PseudoPrefabSO 子资产（custom content 体系专用）
    s = re.sub(r"^--- !u!114 &114149305632628198\n(?:(?!--- ).*\n)*",
               "", s, flags=re.M)
    rel2 = "prefabs/teacups/web_teacup_stack_clean.prefab"
    write_file(rel2, s)
    write_meta(rel2, g_prefab("web_cleanmugstack_teacup"))  # guid 锚保留旧 id，勿改
    return mug_guid


CONTENTS_ROOT_FID = "1000000001"


def gen_contents(mesh_guids, mats):
    out("[prefab] 液体内容物 ×5（Web_TeaContents_*）")
    drink_mesh = mesh_guids["m_dlc100_teacup_drink_surface"]
    spoon_mesh = mesh_guids["m_dlc100_teacup_spoon"]
    variants = [
        # (id, 液体材质, 带勺)
        ("Web_TeaContents_Water", mats["water"], False),
        ("Web_TeaContents_Tea", mats["honeytea"], False),
        ("Web_TeaContents_MilkTea", mats["milktea"], False),
        ("Web_TeaContents_HoneyTea", mats["honeytea"], True),
        ("Web_TeaContents_HoneyMilkTea", mats["milktea"], True),
    ]
    so_guids = {}
    for ident, mat, spoon in variants:
        parts = [prefab_header(CONTENTS_ROOT_FID)]
        root_tr = fid_new()
        # Contents 的 id 必须先分配（保持 fileID 序列不变），root 文档再引用它：
        #  - root.m_Children 必须含 Contents
        #  - Contents.m_Father 必须指回 root
        # 否则 Contents 会变成 prefab 里的第二个根对象，实例化时不是 root 的子节点，
        # 挂到杯子的 AttachPoint 后什么都看不到（“接完热水杯里没有热水模型”的根因）。
        cs_go, cs_tr = fid_new(), fid_new()
        dk_go, dk_tr, dk_mf, dk_mr = fid_new(), fid_new(), fid_new(), fid_new()
        parts.append(go_doc(CONTENTS_ROOT_FID, ident, [root_tr], 0))
        parts.append(tr_doc(root_tr, CONTENTS_ROOT_FID, ("0", "0", "0"),
                             ("0", "0", "0", "1"), ("1", "1", "1"), [cs_tr]))
        # Contents
        if spoon:
            sp_go, sp_tr, sp_mf, sp_mr = fid_new(), fid_new(), fid_new(), fid_new()
            parts.append(go_doc(cs_go, "Contents", [cs_tr], 0))
            parts.append(tr_doc(cs_tr, cs_go, ("0", "0", "0"), ("-0", "-0", "-0", "1"),
                                 ("1", "1", "1"), [dk_tr, sp_tr], father=root_tr))
            parts.append(go_doc(dk_go, "Drink", [dk_tr, dk_mf, dk_mr], 0))
            parts.append(tr_doc(dk_tr, dk_go, ("0", "0", "0"), ("-0", "-0", "-0", "1"),
                                 ("2", "2", "2"), []))
            parts.append(meshfilter_doc(dk_mf, dk_go, drink_mesh))
            parts.append(meshrenderer_doc(dk_mr, dk_go, mat))
            parts.append(go_doc(sp_go, "M_Birthday_TeaCup_Spoon", [sp_tr, sp_mf, sp_mr], 0))
            parts.append(tr_doc(sp_tr, sp_go, ("-0.019", "0.136", "-0.0076213037"),
                                 ("-0", "-0", "-0.17123483", "0.9852303"),
                                 ("2", "2", "2"), []))
            parts.append(meshfilter_doc(sp_mf, sp_go, spoon_mesh))
            parts.append(meshrenderer_doc(sp_mr, sp_go, G_MAT_SPOON))
        else:
            parts.append(go_doc(cs_go, "Contents", [cs_tr], 0))
            parts.append(tr_doc(cs_tr, cs_go, ("0", "0", "0"), ("-0", "-0", "-0", "1"),
                                 ("1", "1", "1"), [dk_tr], father=root_tr))
            parts.append(go_doc(dk_go, "Drink", [dk_tr, dk_mf, dk_mr], 0))
            parts.append(tr_doc(dk_tr, dk_go, ("0", "0", "0"), ("-0", "-0", "-0", "1"),
                                 ("2", "2", "2"), []))
            parts.append(meshfilter_doc(dk_mf, dk_go, drink_mesh))
            parts.append(meshrenderer_doc(dk_mr, dk_go, mat))
        text = YAML_HEADER + "".join(parts)
        rel = "custom_recipes/milk_tea/models/%s.prefab" % ident
        write_file(rel, text)
        pg = g_prefab(ident)
        write_meta(rel, pg)
        # 模型包装 SO（直引本地 prefab）
        so_guid = det_guid("so", "Model_" + ident)
        so_guids[ident] = so_guid
        so_yaml = pseudo_so_yaml(
            "Model_" + ident,
            "{fileID: %s, guid: %s, type: 2}" % (CONTENTS_ROOT_FID, pg))
        rel2 = "custom_recipes/milk_tea/model_so/Model_%s.asset" % ident
        write_file(rel2, so_yaml)
        write_meta(rel2, so_guid)
    return so_guids


def gen_hotwater():
    out("[ingredient] Node_HotWater + HotWaterSO + 图标")
    icon_rel = "ingredients/hotwater/Hot_Water.png"
    copy_asset("Texture2D/DLC100_Hot_Water_0.png", icon_rel)
    # 覆盖 meta 为 sprite 导入（确定性 guid；拷贝的原 meta 是普通纹理）
    icon_guid = g_icon("Hot_Water")
    if APPLY:
        path = os.path.join(W5, icon_rel + ".meta")
        open(path, "w").write(sprite_meta(icon_guid))
    out("  覆盖meta(sprite): %s.meta" % icon_rel)
    node = node_yaml("Node_HotWater", 100000004, icon_guid)
    node = node.replace(
        "  m_platingStep: {fileID: 0}\n  m_platingPrefab: {fileID: 0}",
        "  m_platingStep: {fileID: 11400000, guid: " + G_MUG_PLATING_LOCAL + ", type: 2}\n"
        "  m_platingPrefab: {fileID: 1000000001, guid: 47e66e067f2d110ab6bddc304b3f696f, type: 2}")
    write_file("ingredients/hotwater/Node_HotWater.asset", node)
    write_meta("ingredients/hotwater/Node_HotWater.asset", G_NODE_HOTWATER)
    write_file("pseudo_prefab_so/food/HotWaterSO.asset",
               pseudo_so_yaml("HotWaterSO",
                              "{fileID: 11400000, guid: %s, type: 2}" % G_NODE_HOTWATER))
    write_meta("pseudo_prefab_so/food/HotWaterSO.asset", G_SO_HOTWATER)
    return icon_guid


RECIPES = [
    # (id, uid, score, composition SO guids, icon 源文件名, 图标 guid 常量, model so id, zh, en)
    ("Web_MilkTea_HotWater", 58323007, 0,
     None, None, None, "Web_TeaContents_Water",
     "奶茶·热水（中间产物）", "Milk Tea Hot Water (Intermediate)"),
    ("Web_MilkTea_Tea", 58323008, 40,
     "HOTWATER,TEA", "DLC100_Teapot_Simple_Tea_0.png", "G_ICON_TEA_SIMPLE",
     "Web_TeaContents_Tea", "纯茶", "Plain Tea"),
    ("Web_MilkTea_MilkTea", 58323009, 60,
     "HOTWATER,MILK,TEA", "DLC100_Teapot_Milk_0.png", "G_ICON_TEA_MILK",
     "Web_TeaContents_MilkTea", "奶茶", "Milk Tea"),
    ("Web_MilkTea_HoneyTea", 58323010, 80,
     "HOTWATER,HONEY,TEA", "DLC100_Teapot_Honey_0.png", "G_ICON_TEA_HONEY",
     "Web_TeaContents_HoneyTea", "蜂蜜茶", "Honey Tea"),
    ("Web_MilkTea_HoneyMilkTea", 58323011, 100,
     "HOTWATER,MILK,HONEY,TEA", "DLC100_Teapot_Milk_Honey.png", "G_ICON_TEA_MILK_HONEY",
     "Web_TeaContents_HoneyMilkTea", "蜂蜜奶茶", "Honey Milk Tea"),
]


def gen_recipes(model_so_guids):
    out("[recipes] 4 成品 + 1 中间产物 + 图标 + 分类/名称")
    comp_map = {
        "HOTWATER": G_SO_HOTWATER,
        "TEA": G_TEA_SO,
        "MILK": G_MILK_SO,
        "HONEY": G_HONEY_SO,
    }
    for (ident, uid, score, comps, icon_src, icon_const, model_id, _zh, _en) in RECIPES:
        # 图标
        icon_guid = None
        if icon_src:
            icon_guid = g_icon(ident)
            copy_asset("Texture2D/%s" % icon_src,
                       "custom_recipes/milk_tea/icons/%s.png" % ident)
            # 覆盖为 sprite meta
            if APPLY:
                open(os.path.join(W5, "custom_recipes/milk_tea/icons/%s.png.meta" % ident),
                     "w").write(sprite_meta(icon_guid))
            out("  覆盖meta(sprite): custom_recipes/milk_tea/icons/%s.png.meta" % ident)
        comp_guids = ([comp_map[c] for c in comps.split(",")] if comps else [G_SO_HOTWATER])
        # 中间产物（热水）组合就是 [HotWater]
        text = recipe_yaml(ident, uid, score, comp_guids, icon_guid,
                           model_so_guids[model_id])
        rel = "custom_recipes/milk_tea/%s.asset" % ident
        write_file(rel, text)
        write_meta(rel, hashlib.md5(("commonw5-recipe:%s" % ident).encode()).hexdigest())
    merge_config_and_names()


def merge_config_and_names():
    """CustomRecipeConfig（追加 milk_tea 分类 + nextSequence≥12）与 names.json 合并。"""
    cfg_path = os.path.join(W5, "custom_recipes/CustomRecipeConfig.asset")
    cfg = open(cfg_path, encoding="utf-8").read()
    changed = False
    if "id: milk_tea" not in cfg:
        cfg = cfg.replace(
            "  categories:\n",
            "  categories:\n  - id: milk_tea\n    zh: \"\\u5976\\u8336\"\n    en: Milk Tea\n",
            1)
        changed = True
    m = re.search(r"nextSequence: (\d+)", cfg)
    if m and int(m.group(1)) < 12:
        cfg = cfg.replace("nextSequence: %s" % m.group(1), "nextSequence: 12", 1)
        changed = True
    if changed and APPLY:
        open(cfg_path, "w", encoding="utf-8").write(cfg)
    out("  CustomRecipeConfig: %s" % ("更新" if changed else "无需更新"))

    names_path = os.path.join(W5, "custom_recipes/names.json")
    data = json.load(open(names_path, encoding="utf-8-sig"))
    existing = {n["id"] for n in data["names"]}
    added = False
    for (_ident, _uid, _s, _c, _i, _ic, _m, zh, en) in RECIPES:
        if _ident not in existing:
            data["names"].append({"id": _ident, "zh": zh, "en": en})
            added = True
    if added and APPLY:
        with open(names_path, "w", encoding="utf-8-sig") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
            f.write("\n")
    out("  names.json: %s" % ("追加 %d 条" % (len(data["names"]) - len(existing)) if added else "无需更新"))


def sync_web_icons():
    out("[icons] 同步 web/public")
    jobs = [
        ("custom_recipes/milk_tea/icons/Web_MilkTea_Tea.png",
         "icons/recipes/Web_MilkTea_Tea.png"),
        ("custom_recipes/milk_tea/icons/Web_MilkTea_MilkTea.png",
         "icons/recipes/Web_MilkTea_MilkTea.png"),
        ("custom_recipes/milk_tea/icons/Web_MilkTea_HoneyTea.png",
         "icons/recipes/Web_MilkTea_HoneyTea.png"),
        ("custom_recipes/milk_tea/icons/Web_MilkTea_HoneyMilkTea.png",
         "icons/recipes/Web_MilkTea_HoneyMilkTea.png"),
        ("ingredients/hotwater/Hot_Water.png", "icons/ingredients/HotWaterSO.png"),
    ]
    pub = os.path.join(REPO, "layout-editor", "web", "public")
    for src_rel, dst_rel in jobs:
        src = os.path.join(W5, src_rel)
        dst = os.path.join(pub, dst_rel)
        if not os.path.exists(src):
            out("  源缺失跳过: %s" % src_rel)
            continue
        if os.path.exists(dst):
            out("  已存在跳过: public/%s" % dst_rel)
            continue
        if APPLY:
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            shutil.copy2(src, dst)
        out("  同步: public/%s" % dst_rel)


def verify_specific_tag_guid():
    """SpecificPseudoPrefabTag 脚本 guid 与常量一致性校验。"""
    meta = os.path.join(REPO, "Assets/Scripts/LevelEditorStub/SpecificPseudoPrefabTag.cs.meta")
    g = re.search(r"^guid: ([0-9a-f]{32})$", open(meta).read(), re.M).group(1)
    if g != SPECIFIC_TAG_SCRIPT:
        raise SystemExit("SpecificPseudoPrefabTag guid 不一致: 常量 %s 实际 %s"
                         % (SPECIFIC_TAG_SCRIPT, g))
    out("[verify] SpecificPseudoPrefabTag guid 一致: %s" % g)


def _read_text(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def repair_obj_face_normals(text):
    """给旧 OBJ 面补 vn 引用（vn 与顶点同序）。

    早期 mesh_to_obj.py 只写 vn 行、面仍是 v/vt，Unity 会报
    "Mesh ... has no normals. Recalculating normals."；本函数把 f v/vt
    补成 f v/vt/v，已有 vn 的面保持不变。
    """
    lines = text.splitlines()
    if not any(line.startswith("vn ") for line in lines):
        return text
    changed = False
    out = []
    for line in lines:
        if not line.startswith("f "):
            out.append(line)
            continue
        tokens = line.split()
        new_tokens = [tokens[0]]
        for tok in tokens[1:]:
            parts = tok.split("/")
            if len(parts) == 1:
                new_tokens.append(parts[0] + "//" + parts[0])
                changed = True
            elif len(parts) == 2:
                new_tokens.append(parts[0] + "/" + parts[1] + "/" + parts[0])
                changed = True
            else:
                new_tokens.append(tok)
        out.append(" ".join(new_tokens))
    return "\n".join(out) + ("\n" if text.endswith("\n") else "")


def _write_text(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def _meta_guid(path):
    m = re.search(r"^guid: ([0-9a-f]{32})$", _read_text(path), re.M)
    return m.group(1) if m else None


def _rewrite_meta(rel_from_w5):
    path = os.path.join(W5, rel_from_w5 + ".meta")
    if not os.path.exists(path):
        return False
    guid = _meta_guid(path)
    if not guid:
        return False
    new_text = meta_text_for(rel_from_w5, guid)
    old_text = _read_text(path)
    if old_text == new_text:
        return False
    _write_text(path, new_text)
    return True


def repair_existing():
    """幂等修补已生成的资产，不重写 prefab 的粒子兼容段。

    仅处理本脚本生成的 26 个 YAML、5 个 OBJ 及其 meta、生成的
    prefab/mat/asset/shader meta；拷贝自 Ripped 的 controller/anim/
    material/texture meta 一律不动，GUID 全部保留。
    """
    out("[repair] 修补 commonW5 热水器资产（不重新生成 prefab）")
    header_fixed = 0
    yaml_exts = (".prefab", ".mat", ".asset", ".anim", ".controller")
    for dp, _dns, fns in os.walk(W5):
        for fn in fns:
            if not fn.endswith(yaml_exts):
                continue
            path = os.path.join(dp, fn)
            text = _read_text(path)
            m = re.search(r"^%TAG !u! tag:unity3d\.com,2011$", text, re.M)
            if not m:
                continue
            text = text[:m.start()] + "%TAG !u! tag:unity3d.com,2011:" + text[m.end():]
            _write_text(path, text)
            header_fixed += 1
    out("  YAML %%TAG 修复: %d" % header_fixed)
    anim_fixed = 0
    for path in glob.glob(os.path.join(W5, "kettle/animations/*.anim")):
        old_text = _read_text(path)
        new_text = rewrite_anim_2017(old_text)
        if new_text != old_text:
            _write_text(path, new_text)
            anim_fixed += 1
    out("  AnimationClip 2019→2017 keyframe: %d" % anim_fixed)
    ctrl_fixed = 0
    for path in glob.glob(os.path.join(W5, "kettle/animations/*.controller")):
        old_text = _read_text(path)
        new_text = rewrite_controller_2017(old_text)
        if new_text != old_text:
            _write_text(path, new_text)
            ctrl_fixed += 1
    out("  AnimatorController 子状态 serializedVersion: %d" % ctrl_fixed)

    obj_normals_fixed = 0
    for _src, dst in MESH_CONV:
        path = os.path.join(W5, dst)
        if not os.path.exists(path):
            continue
        old_text = _read_text(path)
        new_text = repair_obj_face_normals(old_text)
        if new_text != old_text:
            _write_text(path, new_text)
            obj_normals_fixed += 1
    out("  OBJ 面补 vn 引用: %d" % obj_normals_fixed)

    mesh_guids = set(
        g_mesh(os.path.splitext(os.path.basename(dst))[0])
        for _src, dst in MESH_CONV)
    prefab_globs = [
        "prefabs/kettle/*.prefab",
        "prefabs/teacups/*.prefab",
        "custom_recipes/milk_tea/models/*.prefab",
    ]
    mesh_fixed = 0
    for pattern in prefab_globs:
        for path in glob.glob(os.path.join(W5, pattern)):
            text = _read_text(path)
            old = text
            for guid in mesh_guids:
                text = text.replace(
                    "m_Mesh: {fileID: 4300000, guid: %s, type: 2}" % guid,
                    "m_Mesh: {fileID: 4300000, guid: %s, type: 3}" % guid)
            if text != old:
                _write_text(path, text)
                mesh_fixed += 1
    out("  OBJ m_Mesh type 2→3: %d 个 prefab" % mesh_fixed)

    meta_targets = []
    for pattern in [
            "prefabs/kettle/*.prefab",
            "prefabs/teacups/*.prefab",
            "custom_recipes/milk_tea/models/*.prefab",
            "custom_recipes/milk_tea/*.asset",
            "custom_recipes/milk_tea/model_so/*.asset",
            "kettle/materials/mat_web_*.mat",
            "kettle/shaders/*.shader"]:
        meta_targets.extend(glob.glob(os.path.join(W5, pattern)))
    meta_targets.extend(os.path.join(W5, dst) for _src, dst in MESH_CONV)
    meta_targets.append(os.path.join(W5, "ingredients/hotwater/Node_HotWater.asset"))
    meta_targets.append(os.path.join(W5, "pseudo_prefab_so/food/HotWaterSO.asset"))
    meta_fixed = 0
    for path in meta_targets:
        rel = os.path.relpath(path, W5).replace(os.sep, "/")
        if _rewrite_meta(rel):
            meta_fixed += 1
    out("  importer meta 补全: %d" % meta_fixed)
    pfx_fixed = restore_kettle_pfx_from_scene()
    out("[repair] 完成；粒子还原 %d 段（不重新生成 prefab）" % pfx_fixed)


KETTLE_PFX_GO = {"2254": "1910000031", "2250": "1910000039"}  # 场景PS锚 -> prefab PFX GO fid


def restore_kettle_pfx_from_scene():
    """检测热水器粒子数值是否为原版（2019 原版 198 文档无法直接反序列化，
    禁止文本回写——由 Unity 菜单 DiagKettlePfxRestore 用 API 设值）。"""
    prefab_path = os.path.join(W5, "prefabs/kettle/web_kettle_hotwater.prefab")
    if not os.path.exists(prefab_path):
        return 0
    text = _read_text(prefab_path)
    if re.search(r"startLifetime:\n      serializedVersion: \d+\n      minMaxState: 3", text):
        out("  [pfx] 热水器粒子已是原版数值")
        return 0
    out("  [pfx] 热水器粒子为默认值：请在 Unity 运行菜单 "
        "Layout Editor/诊断/热水器粒子原版参数还原")
    return 0


def main():
    if REPAIR:
        repair_existing()
        return
    print("=== gen-commonw5-kettle（%s）===" % ("APPLY" if APPLY else "DRY-RUN"))
    verify_specific_tag_guid()
    preflight_guid_collision()
    ensure_folders()
    mesh_guids = gen_meshes()
    gen_copied_assets()
    gen_shaders()
    mats = gen_materials()
    kettle_guid = gen_kettle_prefab(mesh_guids, mats)
    mug_guid = gen_mug_and_stack(mesh_guids, mats)
    model_sos = gen_contents(mesh_guids, mats)
    gen_hotwater()
    gen_recipes(model_sos)
    sync_web_icons()
    print("\n=== 汇总 ===")
    print("热水器 prefab guid: %s" % kettle_guid)
    print("茶杯 prefab guid: %s" % mug_guid)
    print("Node_HotWater guid: %s（uID 100000004）" % G_NODE_HOTWATER)
    print("HotWaterSO guid: %s" % G_SO_HOTWATER)
    print("事件数: %d" % len(report))
    if not APPLY:
        print("\n（dry-run：加 --apply 落盘）")


if __name__ == "__main__":
    main()
