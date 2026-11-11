#!/usr/bin/env python3
"""Build salad sauce assets using Blender; source FBX files remain read-only.

  Blender --background --factory-startup --python <this.py> -- --build
  Blender --background --factory-startup --python <this.py> -- --verify
  python3 <this.py> --plate-reference

Requires Blender's bundled numpy. Only --plate-reference requires system UnityPy.
The build stores no .blend, full-resolution extraction, or duplicate embedded maps.
"""
import argparse
import hashlib
import json
import math
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SOURCE = ROOT.parent / "backup_rebuild_20260917_132624" / "酱料"
OUT = ROOT / "Assets/commonW3/custom_recipes/salad/models"
AUDIT = ROOT / "layout-editor/reports/salad-sauce-models"
SPECS = (
    # Original filenames are reversed relative to their actual embedded colors.
    ("SaladKetchup", "黄芥末.fbx", 88.0, (-0.022, 0.012)),
    ("SaladMustard", "番茄.fbx", 55.0, (0.022, -0.012)),
)


def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def relative(path):
    return str(path.relative_to(ROOT))


def guid(path):
    meta = Path(str(path) + ".meta")
    if meta.exists():
        match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(), re.M)
        if not match:
            raise ValueError("Existing meta has no GUID: " + str(meta))
        return match.group(1)
    return hashlib.md5(("salad-sauce:" + relative(path)).encode()).hexdigest()


def meta(path, body, folder=False):
    destination = Path(str(path) + ".meta")
    if not destination.exists():
        destination.write_text(
            "fileFormatVersion: 2\nguid: " + guid(path) + "\n"
            + ("folderAsset: yes\n" if folder else "") + body, encoding="utf-8")


def plate_reference():
    """Read the original prefab and mesh without exporting or changing bundles."""
    import UnityPy
    root = ROOT / "Assets/StreamingAssets/Windows"
    env = UnityPy.load(str(root / "bundle47"))
    pointer = next(v for k, v in env.container.items()
                   if k.lower().endswith("/equipment_plate_01.prefab"))
    game = pointer.read()
    transform = next(c.component.read() for c in game.m_Component
                     if c.component.type.name == "Transform")
    children = [p.read() for p in transform.m_Children]
    attach = next(t for t in children if t.m_GameObject.read().m_Name == "AttachPoint")
    plate = next(t for t in children if t.m_GameObject.read().m_Name == "Plate")
    mesh_pointer = next(c.component.read().m_Mesh for c in plate.m_GameObject.read().m_Component
                        if c.component.type.name == "MeshFilter")
    mesh_env = UnityPy.load(str(root / "bundle39"))
    mesh = next(o.read() for o in mesh_env.objects
                if o.type.name == "Mesh" and o.path_id == mesh_pointer.m_PathID)
    bounds = mesh.m_LocalAABB
    vec = lambda v: [v.x, v.y, v.z]
    result = {
        "prefab": "bundle47:assets/prefabs/shared_kitchen/equipment_plate_01.prefab",
        "prefab_path_id": pointer.path_id, "mesh_bundle": "bundle39",
        "mesh": mesh.m_Name, "mesh_path_id": mesh_pointer.m_PathID,
        "mesh_center": vec(bounds.m_Center),
        "mesh_size": [2 * v for v in vec(bounds.m_Extent)],
        "mesh_child_position": vec(plate.m_LocalPosition),
        "mesh_child_scale": vec(plate.m_LocalScale),
        "mesh_child_rotation_xyzw": [plate.m_LocalRotation.x, plate.m_LocalRotation.y,
                                      plate.m_LocalRotation.z, plate.m_LocalRotation.w],
        "food_attach_point": vec(attach.m_LocalPosition),
        "preview_plate": "dimension-matched neutral proxy; not the original plate mesh",
    }
    print(json.dumps(result))


def coordinates(obj):
    import numpy as np
    a = np.empty(len(obj.data.vertices) * 3, dtype=np.float64)
    obj.data.vertices.foreach_get("co", a)
    return a.reshape((-1, 3))


def unity_coordinates(obj):
    import numpy as np
    a = coordinates(obj)
    matrix = np.array(obj.matrix_world)
    world = a @ matrix[:3, :3].T + matrix[:3, 3]
    return world[:, [0, 2, 1]] * [1, 1, -1]


def bounds(a):
    return {"min": a.min(axis=0).tolist(), "max": a.max(axis=0).tolist(),
            "size": (a.max(axis=0) - a.min(axis=0)).tolist()}


def quality_metrics(source, reduced, faces):
    import numpy as np
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    tree = BVHTree.FromPolygons(reduced.tolist(), faces, all_triangles=True)
    sample = source[::max(1, len(source) // 15000)]
    distances = np.array([tree.find_nearest(Vector(p))[3] for p in sample])
    # Compare exact convex silhouette support every 2 degrees. This checks the
    # outer footprint, while the sampled surface distance also sees inner strokes.
    angles = np.linspace(0, 2 * np.pi, 180, endpoint=False)
    directions = np.stack((np.cos(angles), np.sin(angles)))
    errors = []
    for start in range(0, 180, 12):
        d = directions[:, start:start + 12]
        errors.extend(abs((source[:, :2] @ d).max(axis=0)
                          - (reduced[:, :2] @ d).max(axis=0)).tolist())
    result = {"source_vertex_samples": len(sample),
              "source_to_reduced_surface_p95_m": float(np.percentile(distances, 95)),
              "source_to_reduced_surface_max_m": float(distances.max()),
              "outer_footprint_support_max_error_m": max(errors),
              "outer_footprint_directions": 180,
              "limitation": "one-way sampled surface error and convex outer footprint; not visual or full bidirectional Hausdorff proof"}
    assert result["source_to_reduced_surface_p95_m"] < 0.005
    assert result["outer_footprint_support_max_error_m"] < 0.015
    return result


def texture_meta(path, normal=False):
    meta(path, """TextureImporter:
  fileIDToRecycleName: {}
  externalObjects: {}
  serializedVersion: 4
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: %d
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
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
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  alphaUsage: %d
  alphaIsTransparency: 0
  textureType: %d
  textureShape: 1
  platformSettings:
  - serializedVersion: 2
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
  userData: salad-sauce external texture
  assetBundleName: 
  assetBundleVariant: 
""" % (1 if path.stem.endswith("Albedo") else 0,
           1 if path.stem.endswith("MetallicSmoothness") else 0, 1 if normal else 0))


def model_meta(path, name):
    meta(path, """ModelImporter:
  serializedVersion: 22
  fileIDToRecycleName:
    100000: //RootNode
    100002: %s
    400000: //RootNode
    400002: %s
    2300000: %s
    3300000: %s
    4300000: %s
  externalObjects: {}
  materials:
    importMaterials: 0
    materialName: 0
    materialSearch: 1
    materialLocation: 1
  animations:
    legacyGenerateAnimations: 4
    bakeSimulation: 0
    optimizeGameObjects: 0
    clipAnimations: []
    isReadable: 0
  meshes:
    lODScreenPercentages: []
    globalScale: 1
    meshCompression: 0
    addColliders: 0
    importVisibility: 0
    importBlendShapes: 0
    importCameras: 0
    importLights: 0
    swapUVChannels: 0
    generateSecondaryUV: 0
    useFileUnits: 1
    optimizeMeshForGPU: 1
    keepQuads: 0
    weldVertices: 1
    preserveHierarchy: 0
    indexFormat: 0
    useFileScale: 1
  tangentSpace:
    normalSmoothAngle: 60
    normalImportMode: 0
    tangentImportMode: 3
    normalCalculationMode: 4
  importAnimation: 0
  animationType: 0
  userData: salad-sauce single mesh; geometry baked; external material assigned by integrator
  assetBundleName: 
  assetBundleVariant: 
""" % ((name,) * 5))


def material_asset(path, textures):
    text = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 6
  m_ObjectHideFlags: 0
  m_PrefabParentObject: {fileID: 0}
  m_PrefabInternal: {fileID: 0}
  m_Name: %s
  m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}
  m_ShaderKeywords: _METALLICGLOSSMAP _NORMALMAP
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
""" % path.stem
    for slot, key in (("_MainTex", "Albedo"), ("_BumpMap", "Normal"),
                      ("_MetallicGlossMap", "MetallicSmoothness")):
        text += ("    - %s:\n        m_Texture: {fileID: 2800000, guid: %s, type: 3}\n"
                 "        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}\n") % (slot, guid(textures[key]))
    text += """    m_Floats:
    - _BumpScale: 0.5
    - _GlossMapScale: 1
    - _Glossiness: 0.4
    - _Metallic: 0
    - _Mode: 0
    - _SrcBlend: 1
    - _DstBlend: 0
    - _ZWrite: 1
    - _SmoothnessTextureChannel: 0
    - _SpecularHighlights: 1
    - _GlossyReflections: 1
    m_Colors:
    - _Color: {r: 1, g: 1, b: 1, a: 1}
    - _EmissionColor: {r: 0, g: 0, b: 0, a: 0}
"""
    path.write_text(text)
    meta(path, "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def prepare_textures(obj, name):
    import bpy
    import numpy as np
    images = [n.image for n in obj.data.materials[0].node_tree.nodes if n.type == "TEX_IMAGE"]
    records = []
    for image in images:
        records.append({"name": image.name, "size": list(image.size),
                        "packed_bytes": len(image.packed_file.data) if image.packed_file else 0,
                        "packed_sha256": hashlib.sha256(image.packed_file.data).hexdigest()
                        if image.packed_file else None})
    albedo = next(i for i in images if not any(k in i.name.lower() for k in ("normal", "roughness", "metallic")))
    color = np.empty(len(albedo.pixels), dtype=np.float32)
    albedo.pixels.foreach_get(color)
    mean_color = color.reshape((-1, 4))[:, :3].mean(axis=0).tolist()
    normal = next(i for i in images if "normal" in i.name.lower())
    rough = next(i for i in images if "roughness" in i.name.lower())
    metallic = next(i for i in images if "metallic" in i.name.lower())
    paths = {k: OUT / (name + "_" + k + ".png") for k in ("Albedo", "Normal", "MetallicSmoothness")}
    for image, key, size in ((albedo, "Albedo", 1024), (normal, "Normal", 512)):
        image.scale(size, size)
        image.filepath_raw = str(paths[key])
        image.file_format = "PNG"
        image.save()
        # Packed data must never be written back to the source extraction directory.
        texture_meta(paths[key], normal=key == "Normal")
    channels = []
    for image in (metallic, rough):
        image.colorspace_settings.name = "Non-Color"
        image.scale(512, 512)
        data = np.empty(512 * 512 * 4, dtype=np.float32)
        image.pixels.foreach_get(data)
        channels.append(data.reshape((-1, 4))[:, 0])
    packed = np.zeros((512 * 512, 4), dtype=np.float32)
    packed[:, 0] = channels[0]
    packed[:, 3] = 1 - channels[1]
    image = bpy.data.images.new(name + "_MetallicSmoothness", width=512, height=512, alpha=True)
    image.colorspace_settings.name = "Non-Color"
    image.pixels.foreach_set(packed.ravel())
    image.filepath_raw = str(paths["MetallicSmoothness"])
    image.file_format = "PNG"
    image.save()
    texture_meta(paths["MetallicSmoothness"])
    material = bpy.data.materials.new(name + "Material")
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    shader = nodes.get("Principled BSDF")
    color = nodes.new("ShaderNodeTexImage")
    color.image = albedo
    links.new(color.outputs["Color"], shader.inputs["Base Color"])
    # Export a portable single albedo material; Unity uses the supplied Standard .mat.
    shader.inputs["Roughness"].default_value = 0.36
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for poly in obj.data.polygons:
        poly.material_index = 0
    material_path = OUT / (name + ".mat")
    material_asset(material_path, paths)
    return records, paths, material_path, mean_color


def raw_fbx(path):
    """Inspect FBX-local vertices as well as Blender's independently imported mesh."""
    import numpy as np
    from io_scene_fbx import parse_fbx
    tree, version = parse_fbx.parse(str(path))
    objects = next(e for e in tree.elems if e.id == b"Objects")
    geometry = [e for e in objects.elems if e.id == b"Geometry"]
    geo = geometry[0]
    vertices = np.array(next(e for e in geo.elems if e.id == b"Vertices").props[0]).reshape((-1, 3))
    indices = np.array(next(e for e in geo.elems if e.id == b"PolygonVertexIndex").props[0])
    types = [e.id.decode() for e in geo.elems]
    video_content = [len(p) for e in objects.elems if e.id == b"Video"
                     for ch in e.elems if ch.id == b"Content" for p in ch.props if isinstance(p, bytes)]
    return {"version": version, "mesh_count": len(geometry),
            "local_bounds": bounds(vertices), "faces": int((indices < 0).sum()),
            "all_triangles": bool(np.all(np.diff(np.r_[-1, np.flatnonzero(indices < 0)]) == 3)),
            "has_tangents": any("Tangent" in t or "Binormal" in t for t in types),
            "embedded_texture_bytes": sum(video_content),
            "animation_objects": sum(e.id.startswith(b"Animation") for e in objects.elems)}


def validate(path, expected=None):
    import bpy
    import numpy as np
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_image_search=False)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    assert len(meshes) == 1, "Expected exactly one mesh"
    obj = meshes[0]
    obj.data.calc_loop_triangles()
    a = unity_coordinates(obj)
    plane = a[:, [0, 2]]
    w, v = np.linalg.eigh(np.cov(plane.T))
    axis = v[:, -1]
    angle = math.degrees(math.atan2(axis[1], axis[0])) % 180
    raw = raw_fbx(path)
    textures = [n.image for m in obj.data.materials if m and m.use_nodes
                for n in m.node_tree.nodes if n.type == "TEX_IMAGE"]
    result = {"fbx": relative(path), "bytes": path.stat().st_size,
              "sha256": digest(path), "triangles": len(obj.data.loop_triangles),
              "vertices": len(obj.data.vertices), "material_slots": len(obj.data.materials),
              "uv_layers": len(obj.data.uv_layers), "unity_bounds": bounds(a),
              "principal_angle_from_positive_x_degrees": angle,
              "texture_links": [{"name": i.name, "size": list(i.size),
                                 "exists": Path(bpy.path.abspath(i.filepath)).is_file()} for i in textures],
              "raw_fbx": raw}
    assert result["bytes"] < 10_000_000
    assert 4500 <= result["triangles"] <= 5500
    assert result["material_slots"] == 1 and result["uv_layers"] == 1
    uv = np.empty(len(obj.data.uv_layers[0].data) * 2, dtype=np.float32)
    obj.data.uv_layers[0].data.foreach_get("uv", uv)
    assert np.isfinite(uv).all()
    result["uv_finite"] = True
    assert abs(a[:, 1].min()) < 1e-5 and a[:, 1].max() > 0.008
    result["max_horizontal_radius_m"] = float(np.linalg.norm(a[:, [0, 2]], axis=1).max())
    assert result["max_horizontal_radius_m"] < 0.47
    assert abs(max(np.ptp(a[:, [0, 2]], axis=0)) - 0.65) < 0.002
    assert raw["mesh_count"] == 1 and raw["all_triangles"]
    assert not raw["has_tangents"] and raw["embedded_texture_bytes"] == 0
    assert raw["animation_objects"] == 0
    assert textures and all(t["exists"] and t["size"] == [1024, 1024] for t in result["texture_links"])
    assert max(abs(np.array(raw["local_bounds"]["size"]) - np.ptp(a, axis=0))) < 1e-5
    if expected:
        assert abs(angle - expected["target_angle"]) < 1.5
        assert raw["faces"] == expected["triangles"]
        assert result["triangles"] == expected["triangles"]
    result["passed"] = True
    return result


def preview(paths, plate):
    import bpy
    from mathutils import Vector
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objects = []
    for index, path in enumerate(paths):
        bpy.ops.import_scene.fbx(filepath=str(path), use_image_search=False)
        obj = next(o for o in bpy.context.selected_objects if o.type == "MESH")
        obj.location.z += plate["food_attach_point"][1] + index * 0.006
        objects.append(obj)
    bpy.ops.mesh.primitive_cylinder_add(vertices=128, radius=plate["mesh_size"][0] / 2,
                                       depth=0.045, location=(0, 0, 0.04))
    proxy = bpy.context.object
    mat = bpy.data.materials.new("PreviewOnlyNeutralPlate")
    mat.diffuse_color = (0.83, 0.86, 0.9, 1)
    proxy.data.materials.append(mat)
    bevel = proxy.modifiers.new("SoftEdge", "BEVEL")
    bevel.width = 0.025
    bevel.segments = 3
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.world = bpy.data.worlds.new("PreviewWorld")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.12, 0.12, 0.12, 1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = 0.5
    bpy.ops.object.light_add(type="AREA", location=(-1, -1.2, 2))
    bpy.context.object.data.energy = 100
    bpy.context.object.data.shape = "DISK"
    bpy.context.object.data.size = 2
    bpy.ops.object.camera_add(location=(0, 0, 2.4))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.15
    scene.camera = camera
    results = []
    for label, position, visibility in (
        ("combined-top", (0, 0, 2.4), (True, True)),
        ("combined-oblique", (0.9, -1.25, 1.6), (True, True)),
        ("ketchup-top", (0, 0, 2.4), (True, False)),
        ("mustard-top", (0, 0, 2.4), (False, True)),
    ):
        camera.location = position
        camera.rotation_euler = (Vector((0, 0, 0.07)) - camera.location).to_track_quat("-Z", "Y").to_euler()
        for obj, visible in zip(objects, visibility):
            obj.hide_render = not visible
        output = AUDIT / (label + ".png")
        scene.render.filepath = str(output)
        bpy.ops.render.render(write_still=True)
        results.append(relative(output))
    return results


def build(args):
    import bpy
    import bmesh
    import numpy as np
    from mathutils import Matrix
    OUT.mkdir(parents=True, exist_ok=True)
    AUDIT.mkdir(parents=True, exist_ok=True)
    meta(OUT, "DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", folder=True)
    plate = json.loads(subprocess.check_output(
        ["python3", str(Path(__file__).resolve()), "--plate-reference"], text=True))
    report = {"generator": relative(Path(__file__).resolve()), "blender": bpy.app.version_string,
              "target_triangles": args.triangles, "target_span_m": 0.65,
              "orientation_convention": "Unity XZ: angle from +X toward +Z; PCA major axis modulo 180",
              "plate_reference": plate, "models": []}
    report["visual_review"] = {
        "status": "not visually confirmed",
        "reason": "image analysis requests timed out; current conversation cannot directly read image attachments",
        "preview_material": "FBX portable albedo with uniform roughness; Unity Standard material additionally uses normal and metallic/smoothness maps"}
    report["source_filename_color_discrepancy"] = (
        "Original filenames are reversed relative to embedded albedo: 番茄.fbx is yellow/orange; "
        "黄芥末.fbx is red. Output names follow actual embedded color; sources are not modified.")
    for name, filename, angle, offset in SPECS:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        source = args.source_dir / filename
        source_hash = digest(source)
        bpy.ops.import_scene.fbx(filepath=str(source), use_image_search=False)
        meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
        assert len(meshes) == 1
        obj = meshes[0]
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        obj.data.calc_loop_triangles()
        original_triangles = len(obj.data.loop_triangles)
        source_matrix = np.array(obj.matrix_world)
        a = coordinates(obj) @ source_matrix[:3, :3].T + source_matrix[:3, 3]
        center = a.mean(axis=0)
        eigenvalues, eigenvectors = np.linalg.eigh(np.cov(a.T))
        # Thin-axis normal is signed toward source world +Z for repeatable handedness.
        normal = eigenvectors[:, 0]
        if normal[2] < 0:
            normal = -normal
        major = eigenvectors[:, 2]
        if major[np.argmax(np.abs(major))] < 0:
            major = -major
        # Unity's left-handed intermediate frame is reflected once more below
        # when converted to Blender. Their product is a proper rotation, keeping
        # winding, UV handedness and tangent-space normals consistent.
        minor = np.cross(major, normal)
        frame = np.stack((major, minor, normal), axis=1)
        projected = (a - center) @ frame
        theta = math.radians(angle)
        x = math.cos(theta) * projected[:, 0] - math.sin(theta) * projected[:, 1]
        z = math.sin(theta) * projected[:, 0] + math.cos(theta) * projected[:, 1]
        unity = np.stack((x, projected[:, 2], z), axis=1)
        scale = 0.65 / max(np.ptp(unity[:, [0, 2]], axis=0))
        unity *= scale
        unity[:, 0] -= (unity[:, 0].min() + unity[:, 0].max()) / 2
        unity[:, 2] -= (unity[:, 2].min() + unity[:, 2].max()) / 2
        unity[:, 1] -= unity[:, 1].min()
        unity[:, 0] += offset[0]
        unity[:, 2] += offset[1]
        # Blender is Z-up; FBX export bakes X/Z/-Y into Unity X/Y/Z mesh data.
        local = unity[:, [0, 2, 1]] * [1, -1, 1]
        assert np.linalg.det(frame) < 0
        obj.parent = None
        obj.matrix_world = Matrix.Identity(4)
        obj.data.vertices.foreach_set("co", local.astype(np.float32).ravel())
        obj.data.update()
        source_images, textures, material, mean_color = prepare_textures(obj, name)
        assert (mean_color[1] / mean_color[0] < 0.3) == name.endswith("Ketchup")
        obj.data.name = name + "SourceTemporary"
        # COLLAPSE interpolates existing UVs; no voxel remeshing or UV regeneration.
        modifier = obj.modifiers.new("UVPreservingReduction", "DECIMATE")
        modifier.ratio = args.triangles / original_triangles
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        triangulate = obj.modifiers.new("ExplicitTriangles", "TRIANGULATE")
        bpy.ops.object.modifier_apply(modifier=triangulate.name)
        # Collapse can create coincident faces on tiny detached droplets.
        # Remove duplicates before export so every importer reports the same count.
        before_validation_faces = len(obj.data.polygons)
        obj.data.validate(verbose=False, clean_customdata=False)
        removed_invalid = before_validation_faces - len(obj.data.polygons)
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        small = [f for f in bm.faces if f.calc_area() < 1e-10]
        bmesh.ops.delete(bm, geom=small, context="FACES")
        bm.to_mesh(obj.data)
        bm.free()
        # Recenter and normalize AFTER reduction to make actual exported bounds exact.
        low = coordinates(obj)
        # Vertex density changes during collapse. Correct its measured major axis
        # in the final mesh, rather than assuming source PCA survived decimation.
        _, vectors = np.linalg.eigh(np.cov(low[:, :2].T))
        direction = vectors[:, -1]
        current_angle = math.atan2(-direction[1], direction[0]) % math.pi
        correction = math.radians(angle) - current_angle
        c, s = math.cos(correction), math.sin(correction)
        rotation = np.array([[c, -s], [s, c]])
        low[:, :2] = low[:, :2] @ rotation
        local[:, :2] = local[:, :2] @ rotation
        shift = np.array([(low[:, 0].min() + low[:, 0].max()) / 2,
                          (low[:, 1].min() + low[:, 1].max()) / 2, low[:, 2].min()])
        low -= shift
        local -= shift
        factor = 0.65 / max(np.ptp(low[:, :2], axis=0))
        low *= factor
        local *= factor
        translation = np.array([offset[0], -offset[1], 0])
        low += translation
        local += translation
        obj.data.vertices.foreach_set("co", low.astype(np.float32).ravel())
        obj.data.update()
        obj.data.calc_loop_triangles()
        tri = len(obj.data.loop_triangles)
        quality = quality_metrics(local, low, [tuple(p.vertices) for p in obj.data.polygons])
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
        obj.name = name
        obj.data.name = name
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        path = OUT / (name + ".fbx")
        bpy.ops.export_scene.fbx(
            filepath=str(path), use_selection=True, object_types={"MESH"},
            axis_forward="-Z", axis_up="Y", global_scale=1, apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
            use_mesh_modifiers=True, mesh_smooth_type="FACE", use_tspace=False,
            use_triangles=True, bake_anim=False, add_leaf_bones=False,
            path_mode="RELATIVE", embed_textures=False, use_custom_props=False)
        model_meta(path, name)
        assert digest(source) == source_hash, "Source changed"
        record = {"name": name, "source": str(source),
                  "source_bytes": source.stat().st_size, "source_sha256": source_hash,
                  "source_triangles": original_triangles, "source_world_matrix": source_matrix.tolist(),
                  "pca_eigenvalues": eigenvalues.tolist(), "source_world_thin_normal": normal.tolist(),
                  "source_world_major_axis": major.tolist(), "source_projection_frame_columns": frame.tolist(),
                  "pca_thickness_and_spans": np.ptp(projected, axis=0).tolist(),
                  "uniform_scale_before_decimation": scale, "post_decimation_uniform_scale": factor,
                  "post_decimation_angle_correction_degrees": math.degrees(correction),
                  "removed_degenerate_faces": len(small),
                  "removed_invalid_or_duplicate_faces": removed_invalid,
                  "quality_metrics": quality,
                  "target_angle": angle, "baked_offset_xz": list(offset),
                  "triangles": tri, "fbx": relative(path), "guid": guid(path),
                  "mesh_name": name, "requested_legacy_mesh_file_id": 4300000,
                  "mesh_file_id_status": "seeded in Unity2017 recycle-name map; verify after actual Unity import",
                  "material": relative(material), "material_guid": guid(material),
                  "source_albedo_mean_decoded_rgb": mean_color,
                  "source_images": source_images,
                  "textures": [{"path": relative(p), "guid": guid(p),
                                "bytes": p.stat().st_size, "sha256": digest(p),
                                "size": [1024, 1024] if key == "Albedo" else [512, 512]}
                               for key, p in textures.items()],
                  "integration_local_position": [0, 0.006 if name.endswith("Mustard") else 0, 0],
                  "integration_local_rotation": [0, 0, 0], "integration_local_scale": [1, 1, 1]}
        report["models"].append(record)
        print("EXPORTED", name, tri, path.stat().st_size, flush=True)
    report["validation"] = [validate(OUT / (r["name"] + ".fbx"), r) for r in report["models"]]
    for record, checked in zip(report["models"], report["validation"]):
        record["fbx_bytes"] = checked["bytes"]
        record["texture_bytes"] = sum(t["bytes"] for t in record["textures"])
        record["geometry_and_texture_bytes"] = record["fbx_bytes"] + record["texture_bytes"]
        assert record["geometry_and_texture_bytes"] < 10_000_000
    report["generator_sha256"] = digest(Path(__file__).resolve())
    report["previews"] = preview([OUT / (r["name"] + ".fbx") for r in report["models"]], plate)
    (AUDIT / "report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print("BUILD_VALIDATED", relative(AUDIT / "report.json"), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build", action="store_true")
    parser.add_argument("--verify", action="store_true")
    parser.add_argument("--plate-reference", action="store_true")
    parser.add_argument("--source-dir", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--triangles", type=int, default=5000)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else None)
    if args.plate_reference:
        plate_reference()
    elif args.build:
        build(args)
    elif args.verify:
        report = json.loads((AUDIT / "report.json").read_text())
        result = [validate(OUT / (r["name"] + ".fbx"), r) for r in report["models"]]
        for record in report["models"]:
            assert digest(Path(record["source"])) == record["source_sha256"]
            assert guid(ROOT / record["fbx"]) == record["guid"]
            assert guid(ROOT / record["material"]) == record["material_guid"]
            for texture in record["textures"]:
                assert digest(ROOT / texture["path"]) == texture["sha256"]
                assert guid(ROOT / texture["path"]) == texture["guid"]
        (AUDIT / "independent-validation.json").write_text(json.dumps(result, indent=2) + "\n")
        print(json.dumps(result, indent=2))
    else:
        parser.error("Choose --build, --verify or --plate-reference")


if __name__ == "__main__":
    main()
