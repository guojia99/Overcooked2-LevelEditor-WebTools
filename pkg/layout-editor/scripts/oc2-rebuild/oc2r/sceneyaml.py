"""Unity 场景 YAML 片段生成器（写出端）。

fileID 布局沿用 Assets/Template/s_template.unity 的固定骨架，
与上一版 gen-scenes.py 保持一致，保证生成的场景能被编辑器正常打开。
"""

import math

# ---- 模板骨架固定 fileID ----
FID = {
    "design_go": 1601241121, "design_tr": 1601241122,
    "collision_go": 426971518, "collision_tr": 426971519,
    "utensils_go": 300944383, "utensils_tr": 300944384,
    "counters_go": 492637707, "counters_tr": 492637708,
    "floor_col_go": 1772028758, "floor_col_tr": 1772028759, "floor_col_box": 1772028760,
    "chefs_go": 274136423, "chefs_tr": 274136424,
    "art_go": 137629142, "art_tr": 137629143,
    "lights_go": 750247770, "lights_tr": 750247771,
    "day_go": 846550584, "day_tr": 846550585, "day_light": 846550586,
    "ground_go": 1443152664, "ground_tr": 1443152665,
    "floor_go": 55104702, "floor_tr": 55104703, "floor_mr": 55104704, "floor_mf": 55104705,
    "cam_root_go": 420415744, "cam_root_tr": 420415746,
    "debug_go": 409915802,
    "mgr_go": 975597275, "mgr_stub": 975597277,
}

ENV_GUID = "0c70fdc0bdf8b644e82e3ebc3f7a8140"
ENV_ROOT_TR = 4331603006656700
ENV_GRID_TR = 4591096154185836
ENV_GRID_COMP = 114964503590968644
ENV_KP_TR = 4387514011412392
ENV_KP_RESPAWN = 114694390702290380
ENV_STRIPPED_GO = (
    (1056758580, 1695328107371074), (1056758581, 1263439063448506),
    (1056758582, 1538596508456208), (1056758583, 1083572226556830),
    (1056758584, 1272937010316338), (1056758585, 1910194520664404),
)
ENV_INSTANCE_ID = 1056758579
ENV_STRIPPED_MB = (1056758586, 114101035504861502, "b3b3020cd743f2a49408c8f20306f0c2")

PLAYER_GUID = "78d1be00b5b01df4ca974d31ced391b8"
PLAYER_TR = 4319532165554580
PLAYER_GO = 1382526047942262
PLAYER_PID = 114800773322134856

RESPAWN_SCRIPT_GUID = "41abd1af01e045ca82b6eace136ae95a"
BUILTIN_MESH_GUID = "0000000000000000e000000000000000"

SCENE_META_TMPL = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: {bundle}
  assetBundleVariant: 
"""


class FidGen(object):
    def __init__(self, start=3000000000):
        self.n = start

    def next(self):
        self.n += 1
        return self.n


def fnum(v):
    """浮点数写成 Unity 风格的紧凑十进制。"""
    if isinstance(v, str):
        return v
    if isinstance(v, int):
        return str(v)
    if v != v or v in (float("inf"), float("-inf")):
        return "0"
    if abs(v) < 1e-7:
        return "0"
    if float(v).is_integer() and abs(v) < 1e15:
        return str(int(v))
    s = repr(float(v))
    if s.endswith(".0"):
        s = s[:-2]
    return s


def v3(v):
    return (fnum(v[0]), fnum(v[1]), fnum(v[2]))


def q4(q):
    return (fnum(q[0]), fnum(q[1]), fnum(q[2]), fnum(q[3]))


# ---------------------------------------------------------------------------


def go_yaml(fid, name, comps, layer=0, active=1, tag="Untagged"):
    lines = ["--- !u!1 &%d" % fid, "GameObject:",
             "  m_ObjectHideFlags: 0", "  m_PrefabParentObject: {fileID: 0}",
             "  m_PrefabInternal: {fileID: 0}", "  serializedVersion: 5",
             "  m_Component:"]
    lines += ["  - component: {fileID: %d}" % c for c in comps]
    lines += ["  m_Layer: %d" % layer, "  m_Name: %s" % name,
              "  m_TagString: %s" % tag, "  m_Icon: {fileID: 0}",
              "  m_NavMeshLayer: 0", "  m_StaticEditorFlags: 0",
              "  m_IsActive: %d" % active]
    return "\n".join(lines)


def tr_yaml(fid, go, parent, root_order, pos, rot, scale, children, hint=None):
    ch = "\n".join("  - {fileID: %d}" % c for c in children)
    h = hint or (0, 0, 0)
    return "\n".join([
        "--- !u!4 &%d" % fid, "Transform:",
        "  m_ObjectHideFlags: 0", "  m_PrefabParentObject: {fileID: 0}",
        "  m_PrefabInternal: {fileID: 0}", "  m_GameObject: {fileID: %d}" % go,
        "  m_LocalRotation: {x: %s, y: %s, z: %s, w: %s}" % tuple(q4(rot)),
        "  m_LocalPosition: {x: %s, y: %s, z: %s}" % tuple(v3(pos)),
        "  m_LocalScale: {x: %s, y: %s, z: %s}" % tuple(v3(scale)),
        "  m_Children:" + ("" if children else " []"),
        *([ch] if children else []),
        "  m_Father: {fileID: %d}" % parent,
        "  m_RootOrder: %d" % root_order,
        "  m_LocalEulerAnglesHint: {x: %s, y: %s, z: %s}" % tuple(v3(h))])


def box_yaml(fid, go, size, center, trigger=0):
    return "\n".join([
        "--- !u!65 &%d" % fid, "BoxCollider:",
        "  m_ObjectHideFlags: 0", "  m_PrefabParentObject: {fileID: 0}",
        "  m_PrefabInternal: {fileID: 0}", "  m_GameObject: {fileID: %d}" % go,
        "  m_Material: {fileID: 0}", "  m_IsTrigger: %d" % trigger, "  m_Enabled: 1",
        "  serializedVersion: 2",
        "  m_Size: {x: %s, y: %s, z: %s}" % tuple(v3(size)),
        "  m_Center: {x: %s, y: %s, z: %s}" % tuple(v3(center))])


def respawn_yaml(fid, go, rtype):
    return "\n".join([
        "--- !u!114 &%d" % fid, "MonoBehaviour:",
        "  m_ObjectHideFlags: 0", "  m_PrefabParentObject: {fileID: 0}",
        "  m_PrefabInternal: {fileID: 0}", "  m_GameObject: {fileID: %d}" % go,
        "  m_Enabled: 1", "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % RESPAWN_SCRIPT_GUID,
        "  m_Name: ", "  m_EditorClassIdentifier: ",
        "  m_respawnType: %s" % rtype,
        "  m_respawnFilter:", "    serializedVersion: 2", "    m_Bits: 4294967295",
        "  m_onlyRespawnables: 0", "  m_onRespawnTrigger: ",
        "  m_onDeathEffect: {fileID: 0}"])


def mod_line(target, pguid, path, value, objref=None):
    objref = objref or "{fileID: 0}"
    return ("    - target: {fileID: %s, guid: %s, type: 2}\n"
            "      propertyPath: %s\n"
            "      value: %s\n"
            "      objectReference: %s" % (target, pguid, path, value, objref))


def prefab_instance(iid, tid, pguid, ptr, parent, mods):
    return "\n".join([
        "--- !u!1001 &%d" % iid, "Prefab:",
        "  m_ObjectHideFlags: 0", "  serializedVersion: 2",
        "  m_Modification:", "    m_TransformParent: {fileID: %d}" % parent,
        "    m_Modifications:", *mods, "    m_RemovedComponents: []",
        "  m_ParentPrefab: {fileID: 100100000, guid: %s, type: 2}" % pguid,
        "  m_IsPrefabParent: 0",
        "--- !u!4 &%d stripped" % tid, "Transform:",
        "  m_PrefabParentObject: {fileID: %s, guid: %s, type: 2}" % (ptr, pguid),
        "  m_PrefabInternal: {fileID: %d}" % iid])


def mesh_renderer_yaml(fid, go, material_refs):
    """material_refs：已经成形的引用串列表，如 `{fileID: 2100000, guid: …, type: 2}`。"""
    mats = "\n".join("  - %s" % r for r in material_refs)
    return "\n".join([
        "--- !u!23 &%d" % fid, "MeshRenderer:",
        "  m_ObjectHideFlags: 0", "  m_PrefabParentObject: {fileID: 0}",
        "  m_PrefabInternal: {fileID: 0}", "  m_GameObject: {fileID: %d}" % go,
        "  m_Enabled: 1", "  m_CastShadows: 1", "  m_ReceiveShadows: 1",
        "  m_DynamicOccludee: 1", "  m_MotionVectors: 1", "  m_LightProbeUsage: 0",
        "  m_ReflectionProbeUsage: 1", "  m_Materials:",
        mats if material_refs else "  []",
        "  m_StaticBatchInfo:", "    firstSubMesh: 0", "    subMeshCount: 0",
        "  m_StaticBatchRoot: {fileID: 0}", "  m_ProbeAnchor: {fileID: 0}",
        "  m_LightProbeVolumeOverride: {fileID: 0}", "  m_ScaleInLightmap: 1",
        "  m_PreserveUVs: 1", "  m_IgnoreNormalsForChartDetection: 0",
        "  m_ImportantGI: 0", "  m_StitchLightmapSeams: 0",
        "  m_SelectedEditorRenderState: 3", "  m_MinimumChartSize: 4",
        "  m_AutoUVMaxDistance: 0.5", "  m_AutoUVMaxAngle: 89",
        "  m_LightmapParameters: {fileID: 0}", "  m_SortingLayerID: 0",
        "  m_SortingLayer: 0", "  m_SortingOrder: 0"])


def mesh_filter_yaml(fid, go, mesh_fid, mesh_guid=BUILTIN_MESH_GUID):
    return "\n".join([
        "--- !u!33 &%d" % fid, "MeshFilter:",
        "  m_ObjectHideFlags: 0", "  m_PrefabParentObject: {fileID: 0}",
        "  m_PrefabInternal: {fileID: 0}", "  m_GameObject: {fileID: %d}" % go,
        "  m_Mesh: {fileID: %s, guid: %s, type: 0}" % (mesh_fid, mesh_guid)])


def light_yaml(go_fid, tr_fid, light_fid, parent_tr, rot, color, intensity,
               shadow_strength, name="day", ltype=1, rng=10, spot=30):
    return "\n".join([
        go_yaml(go_fid, name, [tr_fid, light_fid]),
        tr_yaml(tr_fid, go_fid, parent_tr, 0, (0, 0, 0), rot, (1, 1, 1), []),
        "--- !u!108 &%d" % light_fid, "Light:",
        "  m_ObjectHideFlags: 0", "  m_PrefabParentObject: {fileID: 0}",
        "  m_PrefabInternal: {fileID: 0}", "  m_GameObject: {fileID: %d}" % go_fid,
        "  m_Enabled: 1", "  serializedVersion: 8", "  m_Type: %s" % ltype,
        "  m_Color: %s" % color,
        "  m_Intensity: %s" % intensity,
        "  m_Range: %s" % rng, "  m_SpotAngle: %s" % spot, "  m_CookieSize: 10",
        "  m_Shadows:", "    m_Type: 1", "    m_Resolution: -1",
        "    m_CustomResolution: -1",
        "    m_Strength: %s" % shadow_strength,
        "    m_Bias: 0.01", "    m_NormalBias: 0", "    m_NearPlane: 0.1",
        "  m_Cookie: {fileID: 0}", "  m_DrawHalo: 0", "  m_Flare: {fileID: 0}",
        "  m_RenderMode: 0", "  m_CullingMask:", "    serializedVersion: 2",
        "    m_Bits: 4294967263", "  m_Lightmapping: 4", "  m_AreaSize: {x: 1, y: 1}",
        "  m_BounceIntensity: 1", "  m_ColorTemperature: 6570",
        "  m_UseColorTemperature: 0", "  m_ShadowRadius: 0", "  m_ShadowAngle: 0"])


def env_instance(grid_pos, grid_half, kp_pos, kp_scale, kp_respawn):
    mods = [
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_LocalPosition.x", "0"),
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_LocalPosition.y", "0"),
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_LocalPosition.z", "0"),
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_LocalRotation.x", "-0"),
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_LocalRotation.y", "-0"),
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_LocalRotation.z", "-0"),
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_LocalRotation.w", "1"),
        mod_line(ENV_ROOT_TR, ENV_GUID, "m_RootOrder", "0"),
        mod_line(ENV_GRID_TR, ENV_GUID, "m_LocalPosition.x", fnum(grid_pos[0])),
        mod_line(ENV_GRID_TR, ENV_GUID, "m_LocalPosition.y", fnum(grid_pos[1])),
        mod_line(ENV_GRID_TR, ENV_GUID, "m_LocalPosition.z", fnum(grid_pos[2])),
        mod_line(ENV_GRID_COMP, ENV_GUID, "m_gridHalfSize.X", str(grid_half[0])),
        mod_line(ENV_GRID_COMP, ENV_GUID, "m_gridHalfSize.Z", str(grid_half[1])),
        mod_line(ENV_KP_TR, ENV_GUID, "m_LocalPosition.x", fnum(kp_pos[0])),
        mod_line(ENV_KP_TR, ENV_GUID, "m_LocalPosition.y", fnum(kp_pos[1])),
        mod_line(ENV_KP_TR, ENV_GUID, "m_LocalPosition.z", fnum(kp_pos[2])),
        mod_line(ENV_KP_TR, ENV_GUID, "m_LocalScale.x", fnum(kp_scale[0])),
        mod_line(ENV_KP_TR, ENV_GUID, "m_LocalScale.y", fnum(kp_scale[1])),
        mod_line(ENV_KP_TR, ENV_GUID, "m_LocalScale.z", fnum(kp_scale[2])),
        mod_line(ENV_KP_RESPAWN, ENV_GUID, "m_respawnType", str(kp_respawn)),
    ]
    block = prefab_instance(ENV_INSTANCE_ID, 0, ENV_GUID, ENV_ROOT_TR, 0, mods)
    block = block[:block.find("--- !u!4 &0 stripped")].rstrip("\n")
    out = [block]
    for sfid, pfid in ENV_STRIPPED_GO:
        out.append("\n".join([
            "--- !u!1 &%d stripped" % sfid, "GameObject:",
            "  m_PrefabParentObject: {fileID: %d, guid: %s, type: 2}" % (pfid, ENV_GUID),
            "  m_PrefabInternal: {fileID: %d}" % ENV_INSTANCE_ID]))
    mb_fid, mb_parent, mb_script = ENV_STRIPPED_MB
    out.append("\n".join([
        "--- !u!114 &%d stripped" % mb_fid, "MonoBehaviour:",
        "  m_PrefabParentObject: {fileID: %d, guid: %s, type: 2}" % (mb_parent, ENV_GUID),
        "  m_PrefabInternal: {fileID: %d}" % ENV_INSTANCE_ID,
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % mb_script]))
    return out


def player_instance(fidgen, n, pos, rot, hint_y, parent_tr):
    iid, tid = fidgen.next(), fidgen.next()
    mods = [
        mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalPosition.x", fnum(pos[0])),
        mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalPosition.y", fnum(pos[1])),
        mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalPosition.z", fnum(pos[2])),
        mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalRotation.x", fnum(rot[0])),
        mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalRotation.y", fnum(rot[1])),
        mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalRotation.z", fnum(rot[2])),
        mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalRotation.w", fnum(rot[3])),
        mod_line(PLAYER_TR, PLAYER_GUID, "m_RootOrder", str(n - 1)),
        mod_line(PLAYER_GO, PLAYER_GUID, "m_Name", "Player %d" % n),
        mod_line(PLAYER_PID, PLAYER_GUID, "playerID", str(n - 1)),
        mod_line(PLAYER_TR, PLAYER_GUID, "m_LocalEulerAnglesHint.y", fnum(hint_y)),
    ]
    return prefab_instance(iid, tid, PLAYER_GUID, PLAYER_TR, parent_tr, mods), tid
