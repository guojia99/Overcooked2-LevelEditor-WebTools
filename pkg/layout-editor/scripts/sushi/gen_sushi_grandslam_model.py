#!/usr/bin/env python3
"""生成 Web 大满贯寿司的本地 5 件套模型素材。

输入：游戏 bundle（只读）
  - bundle38: m_recipe_sushi_04 网格 + t_plated_fish_fillet_02_d 贴图
  - bundle32: t_ingredients_cucumber_01_d 贴图
  - bundle33: t_ingredients_rice_01_d / t_ingredients_seaweed_02_d /
              t_ingredients_prawn_01_d 贴图
  - bundle32/33/38: 原材质参数（颜色/浮点/贴图名）

输出（全部位于 Assets/commonW5/custom_recipes/sushi/models/）：
  meshes/m_recipe_sushi_05.obj       5 件寿司（原 4 件外扩 + 中央虾寿司）
  meshes/*.png                       5 张用到的贴图（与 OBJ 同目录，便于网页预览）
  ../../../../../layout-editor/scripts/data/sushi-grandslam-model.json
                                     材质参数（供 gen-commonw5-sushi.mjs 生成 .mat）

坐标处理与 pasta/kettle 导入管线一致：OBJ 写出前镜像 X 并反转绕序，
抵消 Unity ModelImporter 的 X 轴镜像。
"""
import collections
import json
import os
import sys

try:
    import UnityPy
except Exception as exc:  # pragma: no cover
    raise SystemExit("需要 UnityPy: python3 -m pip install UnityPy\n" + str(exc))

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
BUNDLE_DIR = os.path.join(ROOT, "Assets/StreamingAssets/Windows")
OUT_MODEL_DIR = os.path.join(ROOT, "Assets/commonW5/custom_recipes/sushi/models")
OUT_MESH = os.path.join(OUT_MODEL_DIR, "meshes/m_recipe_sushi_05.obj")
OUT_TEX_DIR = os.path.join(OUT_MODEL_DIR, "meshes")
OUT_JSON = os.path.join(ROOT, "layout-editor/scripts/data/sushi-grandslam-model.json")

SUSHI_MESH_NAME = "m_recipe_sushi_04"
SUSHI_MESH_BUNDLE = "bundle38"
PRAWN_MESH_NAME = "m_ingredients_prawn_01"
PRAWN_MESH_BUNDLE = "bundle32"

# 原材质 → (源 bundle, material path id, 本地贴图名, 本地材质 id)
MATERIALS = [
    {
        "id": "Web_Sushi_Mat_Cucumber",
        "bundle": "bundle32",
        "pathId": -3759134075159175969,
        "texture": "t_ingredients_cucumber_01_d",
    },
    {
        "id": "Web_Sushi_Mat_Rice",
        "bundle": "bundle32",
        "pathId": 2438439272237536624,
        "texture": "t_ingredients_rice_01_d",
    },
    {
        "id": "Web_Sushi_Mat_Seaweed",
        "bundle": "bundle32",
        "pathId": 4346201789577252377,
        "texture": "t_ingredients_seaweed_02_d",
    },
    {
        "id": "Web_Sushi_Mat_Fish",
        "bundle": "bundle38",
        "pathId": -295124967908237508,
        "texture": "t_plated_fish_fillet_02_d",
    },
    {
        "id": "Web_Sushi_Mat_Prawn",
        "bundle": "bundle32",
        "pathId": 142310072479264988,
        "texture": "t_ingredients_prawn_01_d",
    },
]

# 贴图 path id（用于从对应 bundle 直接导出，避免 PPtr 跨 bundle 解析）
TEXTURES = {
    "t_ingredients_cucumber_01_d": ("bundle32", -4846796749092667836),
    "t_ingredients_rice_01_d": ("bundle33", -1809861196636814609),
    "t_ingredients_seaweed_02_d": ("bundle33", -4042413866281730073),
    "t_plated_fish_fillet_02_d": ("bundle38", 8553451943116446159),
    "t_ingredients_prawn_01_d": ("bundle33", 8814625018857302013),
}

# 原 4 件寿司的四个方位中心（XZ，取自网格实测；用于按连通块归类）。
PIECE_CENTERS = {
    "left": (-0.208, 0.0),
    "right": (0.198, 0.0),
    "top": (0.0, 0.269),
    "bottom": (0.0, -0.269),
}
PIECE_PUSH = {
    "left": (-0.10, 0.0),
    "right": (0.10, 0.0),
    "top": (0.0, 0.10),
    "bottom": (0.0, -0.10),
}
GLOBAL_SCALE = 0.85
PRAWN_SCALE = 0.22
PRAWN_BOTTOM_Y = 0.24
CENTER_BODY_GROUPS = (1, 2)  # 1=rice, 2=seaweed

_cache = {}


def load_bundle(name):
    if name not in _cache:
        _cache[name] = UnityPy.load(os.path.join(BUNDLE_DIR, name))
    return _cache[name]


def find_object(bundle, path_id):
    env = load_bundle(bundle)
    for obj in env.objects:
        if obj.path_id == path_id:
            return obj.read()
    raise RuntimeError("找不到对象 %s:%s" % (bundle, path_id))


def parse_obj(path):
    verts, uvs, normals = [], [], []
    groups = collections.OrderedDict()
    current = None
    for line in open(path, "r", encoding="utf-8"):
        if line.startswith("v "):
            verts.append(tuple(float(x) for x in line.split()[1:4]))
        elif line.startswith("vt "):
            uvs.append(tuple(float(x) for x in line.split()[1:3]))
        elif line.startswith("vn "):
            normals.append(tuple(float(x) for x in line.split()[1:4]))
        elif line.startswith("g "):
            current = line.split(None, 1)[1].strip()
            groups.setdefault(current, [])
        elif line.startswith("f "):
            if current is None:
                current = "default"
                groups.setdefault(current, [])
            face = []
            for token in line.split()[1:]:
                parts = token.split("/")
                vi = int(parts[0]) - 1
                ti = int(parts[1]) - 1 if len(parts) > 1 and parts[1] else -1
                ni = int(parts[2]) - 1 if len(parts) > 2 and parts[2] else -1
                face.append((vi, ti, ni))
            groups[current].append(face)
    return verts, uvs, normals, groups


def components(faces):
    """按共享顶点把面分成连通块，返回 [(face_indices, vertex_indices)]。"""
    parent = {}

    def find(x):
        parent.setdefault(x, x)
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb

    for face in faces:
        ids = [c[0] for c in face]
        for a, b in zip(ids, ids[1:]):
            union(a, b)
        union(ids[-1], ids[0])

    comp_faces = collections.defaultdict(list)
    comp_verts = collections.defaultdict(set)
    for idx, face in enumerate(faces):
        root = find(face[0][0])
        comp_faces[root].append(idx)
        comp_verts[root].update(c[0] for c in face)
    return [(comp_faces[r], sorted(comp_verts[r])) for r in comp_faces]


def nearest_piece(x, z):
    best = None
    best_d = 1e9
    for name, (cx, cz) in PIECE_CENTERS.items():
        d = (x - cx) ** 2 + (z - cz) ** 2
        if d < best_d:
            best_d = d
            best = name
    return best


def transform_vertex(v, piece):
    x, y, z = v
    dx, dz = PIECE_PUSH[piece]
    return (x * GLOBAL_SCALE + dx, y, z * GLOBAL_SCALE + dz)


def export_mesh(obj, path):
    mesh = obj
    open(path, "w", encoding="utf-8").write(mesh.export())


def main():
    # ---- 1) 取原始寿司网格 ----
    sushi_env = load_bundle(SUSHI_MESH_BUNDLE)
    sushi_mesh_obj = None
    for o in sushi_env.objects:
        if o.type.name == "Mesh":
            d = o.read()
            if getattr(d, "m_Name", "") == SUSHI_MESH_NAME:
                sushi_mesh_obj = d
                break
    if sushi_mesh_obj is None:
        raise RuntimeError("找不到网格 " + SUSHI_MESH_NAME)
    raw_sushi = os.path.join("/tmp", SUSHI_MESH_NAME + ".obj")
    open(raw_sushi, "w", encoding="utf-8").write(sushi_mesh_obj.export())
    verts, uvs, normals, groups = parse_obj(raw_sushi)

    # ---- 2) 按连通块归类到 4 个方位，并做外扩 ----
    vertex_piece = {}
    group_faces_out = {i: [] for i in range(4)}  # 原 4 个子网格
    left_body_faces = {1: [], 2: []}
    for group_name, faces in groups.items():
        # 组名形如 m_recipe_sushi_04_0
        try:
            gi = int(group_name.rsplit("_", 1)[1])
        except Exception:
            continue
        if gi not in (0, 1, 2, 3):
            continue
        for comp_face_ids, comp_vert_ids in components(faces):
            cx = sum(verts[i][0] for i in comp_vert_ids) / len(comp_vert_ids)
            cz = sum(verts[i][2] for i in comp_vert_ids) / len(comp_vert_ids)
            piece = nearest_piece(cx, cz)
            for vi in comp_vert_ids:
                vertex_piece.setdefault(vi, piece)
            for fi in comp_face_ids:
                group_faces_out[gi].append(faces[fi])
                if piece == "left" and gi in CENTER_BODY_GROUPS:
                    left_body_faces[gi].append(faces[fi])

    # 原始顶点只变换一次
    base_v = list(verts)
    for vi, piece in vertex_piece.items():
        base_v[vi] = transform_vertex(verts[vi], piece)

    out_v = list(base_v)
    out_vt = list(uvs)
    out_vn = list(normals)
    out_faces = {i: [] for i in range(4)}
    for gi, faces in group_faces_out.items():
        out_faces[gi] = [list(f) for f in faces]

    # ---- 3) 复制左件身体（rice + seaweed）到中心，作为虾寿司饭底 ----
    # 计算复制后的中心（变换后的左件身体 bbox 中心）
    body_vs = []
    for gi in CENTER_BODY_GROUPS:
        for face in left_body_faces[gi]:
            body_vs.extend(c[0] for c in face)
    if not body_vs:
        raise RuntimeError("没有找到左件身体子网格")
    min_x = min(base_v[i][0] for i in body_vs)
    max_x = max(base_v[i][0] for i in body_vs)
    min_z = min(base_v[i][2] for i in body_vs)
    max_z = max(base_v[i][2] for i in body_vs)
    shift_x = -(min_x + max_x) / 2.0
    shift_z = -(min_z + max_z) / 2.0

    dup_index = {}
    for gi in CENTER_BODY_GROUPS:
        for face in left_body_faces[gi]:
            new_face = []
            for vi, ti, ni in face:
                key = (vi, ti, ni)
                if key not in dup_index:
                    x, y, z = base_v[vi]
                    out_v.append((x + shift_x, y, z + shift_z))
                    out_vt.append(uvs[ti] if ti >= 0 else (0.0, 0.0))
                    out_vn.append(normals[ni] if ni >= 0 else (0.0, 1.0, 0.0))
                    dup_index[key] = (
                        len(out_v) - 1,
                        len(out_vt) - 1 if ti >= 0 else -1,
                        len(out_vn) - 1 if ni >= 0 else -1,
                    )
                new_face.append(dup_index[key])
            out_faces[gi].append(new_face)

    # ---- 4) 取整只虾网格作为中央配料（第 5 个子网格） ----
    prawn_env = load_bundle(PRAWN_MESH_BUNDLE)
    prawn_mesh_obj = None
    for o in prawn_env.objects:
        if o.type.name == "Mesh":
            d = o.read()
            if getattr(d, "m_Name", "") == PRAWN_MESH_NAME:
                prawn_mesh_obj = d
                break
    if prawn_mesh_obj is None:
        raise RuntimeError("找不到网格 " + PRAWN_MESH_NAME)
    raw_prawn = os.path.join("/tmp", PRAWN_MESH_NAME + ".obj")
    open(raw_prawn, "w", encoding="utf-8").write(prawn_mesh_obj.export())
    pv, pt, pn, pg = parse_obj(raw_prawn)

    pmin_x = min(v[0] for v in pv)
    pmax_x = max(v[0] for v in pv)
    pmin_y = min(v[1] for v in pv)
    pmin_z = min(v[2] for v in pv)
    pmax_z = max(v[2] for v in pv)
    pcx = (pmin_x + pmax_x) / 2.0
    pcz = (pmin_z + pmax_z) / 2.0
    prawn_index = {}
    out_faces[4] = []
    for faces in pg.values():
        for face in faces:
            new_face = []
            for vi, ti, ni in face:
                key = (vi, ti, ni)
                if key not in prawn_index:
                    x, y, z = pv[vi]
                    out_v.append(
                        (
                            (x - pcx) * PRAWN_SCALE,
                            (y - pmin_y) * PRAWN_SCALE + PRAWN_BOTTOM_Y,
                            (z - pcz) * PRAWN_SCALE,
                        )
                    )
                    out_vt.append(pt[ti] if ti >= 0 else (0.0, 0.0))
                    out_vn.append(pn[ni] if ni >= 0 else (0.0, 1.0, 0.0))
                    prawn_index[key] = (
                        len(out_v) - 1,
                        len(out_vt) - 1 if ti >= 0 else -1,
                        len(out_vn) - 1 if ni >= 0 else -1,
                    )
                new_face.append(prawn_index[key])
            out_faces[4].append(new_face)

    # ---- 5) 写 OBJ：镜像 X、反转绕序（抵消 Unity ModelImporter 的 X 镜像） ----
    os.makedirs(os.path.dirname(OUT_MESH), exist_ok=True)
    with open(OUT_MESH, "w", encoding="utf-8") as f:
        f.write("# generated by gen_sushi_grandslam_model.py (5-piece Grand Slam sushi)\n")
        f.write("# mirror X, reverse winding\n")
        for x, y, z in out_v:
            f.write("v %.6f %.6f %.6f\n" % (-x, y, z))
        for u, v in out_vt:
            f.write("vt %.6f %.6f\n" % (u, v))
        for x, y, z in out_vn:
            f.write("vn %.6f %.6f %.6f\n" % (-x, y, z))
        for gi in range(5):
            f.write("\ng %s_%d\n" % ("m_recipe_sushi_05", gi))
            f.write("usemtl mat_%d\n" % gi)
            for face in out_faces[gi]:
                # 镜像后反转绕序
                tokens = []
                for vi, ti, ni in reversed(face):
                    a = str(vi + 1)
                    b = str(ti + 1) if ti >= 0 else ""
                    c = str(ni + 1) if ni >= 0 else ""
                    tokens.append(a + "/" + b + "/" + c)
                f.write("f " + " ".join(tokens) + "\n")

    # ---- 6) 导出贴图 ----
    os.makedirs(OUT_TEX_DIR, exist_ok=True)
    for name, (bundle, path_id) in TEXTURES.items():
        tex = find_object(bundle, path_id)
        img = tex.image
        img.save(os.path.join(OUT_TEX_DIR, name + ".png"))
        print("贴图", name, img.size)

    # ---- 7) 材质参数 JSON ----
    materials = []
    for info in MATERIALS:
        mat = find_object(info["bundle"], info["pathId"])
        sp = getattr(mat, "m_SavedProperties", None)
        colors = {}
        floats = {}
        texenvs = {}
        if sp is not None:
            for k, v in getattr(sp, "m_Colors", []) or []:
                colors[k] = [float(v.r), float(v.g), float(v.b), float(v.a)]
            for k, v in getattr(sp, "m_Floats", []) or []:
                floats[k] = float(v)
            for k, v in getattr(sp, "m_TexEnvs", []) or []:
                texenvs[k] = {
                    "scale": [float(v.m_Scale.x), float(v.m_Scale.y)],
                    "offset": [float(v.m_Offset.x), float(v.m_Offset.y)],
                }
        materials.append(
            {
                "id": info["id"],
                "sourceName": getattr(mat, "m_Name", ""),
                "texture": info["texture"],
                "colors": colors,
                "floats": floats,
                "texEnvs": texenvs,
            }
        )
    os.makedirs(os.path.dirname(OUT_JSON), exist_ok=True)
    with open(OUT_JSON, "w", encoding="utf-8") as f:
        json.dump({"mesh": "m_recipe_sushi_05", "materials": materials}, f, ensure_ascii=False, indent=2)
        f.write("\n")

    print("完成：", OUT_MESH)
    print("材质参数：", OUT_JSON)
    print("顶点数：", len(out_v), "面数：", sum(len(v) for v in out_faces.values()))
    for gi in range(5):
        print("  submesh", gi, "faces", len(out_faces[gi]))


if __name__ == "__main__":
    main()
