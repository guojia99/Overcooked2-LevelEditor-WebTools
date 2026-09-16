#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Extract original floor materials (+ diffuse textures) from the game bundles
into Assets/common03/materials & Assets/common03/textures, so the layout
editor's floor-material catalog (LayoutEditorFloorMaterialsApi.Scan) picks
them up.

Pipeline:
  1. Load dump_bundle/manifest.json, pick floor-ish Material containers.
  2. Scan every StreamingAssets bundle once to map CAB archive name -> bundle.
  3. Load the material bundles, resolve each material's _DiffuseMap PPtr
     (external CAB + PathID) to a Texture2D in another bundle.
  4. Decode the referenced textures to Assets/common03/textures/*.png
     (deterministic GUID .meta files).
  5. Write Assets/common03/materials/*.mat (Standard shader recipe, same as
     Assets/LevelSets/test/materials/mat_city_path_01.mat) + .meta.
  6. Emit layout-editor/scripts/data/floor-materials-common03.json describing
     every created material (id/guid/assetPath/sizeTag) for the static
     frontend catalog.

Re-runnable: existing outputs are kept (skip), missing ones are created.
"""
import hashlib
import json
import os
import re
import sys

import UnityPy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
BUNDLE_DIR = os.path.join(ROOT, 'Assets', 'StreamingAssets', 'Windows')
MANIFEST = os.path.join(ROOT, 'dump_bundle', 'manifest.json')
OUT_MAT = os.path.join(ROOT, 'Assets', 'common03', 'materials')
OUT_TEX = os.path.join(ROOT, 'Assets', 'common03', 'textures')
SUMMARY = os.path.join(ROOT, 'layout-editor', 'scripts', 'data', 'floor-materials-common03.json')
STATIC_CATALOG = os.path.join(ROOT, 'layout-editor', 'web', 'public', 'floor-materials.json')

SIZE_TAG_RE = re.compile(r'_([0-9]+)x([0-9]+)(?:_|$)')

# Material names that are already covered by the existing catalog
# (Assets/LevelSets/*/materials, common01/common02) - skip to avoid duplicates.
def existing_catalog_ids():
    try:
        with open(STATIC_CATALOG, 'r', encoding='utf-8') as f:
            data = json.load(f)
        return {m['id'] for m in data.get('materials', [])}
    except Exception:
        return set()


EXCLUDE_RE = re.compile(r'map_|background|sky|wake|summary|pfx|lambert|debris|walltile|_ui_|^mat_ui_', re.I)
FLOOR_RE = re.compile(r'floor|path|road|ground|blacktiles|pavement|woodslat|moss|tile', re.I)


def is_floor_material(name):
    n = name.lower()
    if EXCLUDE_RE.search(n):
        return False
    if n.startswith(('t_', 'sh_')):
        return False
    if n.startswith('mat_') or n.startswith('dlc11_mat_'):
        return bool(FLOOR_RE.search(n))
    # legacy dev names (floortile_stone, space_floor, floortiles_kitchen_1x1_dlc, ...)
    return 'floor' in n


def guid_for(rel_path):
    return hashlib.md5(('common03:' + rel_path).encode('utf-8')).hexdigest()


def size_tag_of(mid):
    m = SIZE_TAG_RE.search(mid or '')
    return (m.group(1) + 'x' + m.group(2)) if m else ''


TEX_META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  fileIDToRecycleName: {{}}
  externalObjects: {{}}
  serializedVersion: 4
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
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
    wrapU: -1
    wrapV: -1
    wrapW: -1
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
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


def fmt_num(v):
    f = float(v)
    if f == int(f) and abs(f) < 1e15:
        return str(int(f))
    return repr(f)


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


def env_pairs(tt):
    """m_TexEnvs entries come back as (name, dict) tuples after read_typetree."""
    for entry in tt:
        if isinstance(entry, (list, tuple)):
            yield entry[0], entry[1]
        else:
            yield entry['first'], entry['second']


def cab_of(path):
    m = re.search(r'(cab-[0-9a-f]{32})', path or '', re.I)
    return m.group(1).lower() if m else None


def main():
    os.makedirs(OUT_MAT, exist_ok=True)
    os.makedirs(OUT_TEX, exist_ok=True)
    os.makedirs(os.path.dirname(SUMMARY), exist_ok=True)

    skip_ids = existing_catalog_ids()
    print('existing catalog ids:', len(skip_ids))

    with open(MANIFEST, 'r', encoding='utf-8') as f:
        manifest = json.load(f)

    targets = {}
    target_lower = {}
    for o in manifest['objects']:
        if o.get('type') != 'Material':
            continue
        container = o.get('container', '')
        if not container.endswith('.mat') or '/materials/' not in container:
            continue
        name = os.path.splitext(os.path.basename(container))[0]
        if name in skip_ids or not is_floor_material(name):
            continue
        targets.setdefault(name, o['bundle'])
        target_lower.setdefault(name.lower(), name)
    print('floor materials to import:', len(targets))
    if not targets:
        print('nothing to do')
        return

    mat_bundles = sorted(set(targets.values()))
    print('material bundles:', mat_bundles)

    # ---- Phase 1: map CAB name -> bundle file (single pass over all bundles)
    cab_to_bundle = {}
    bundle_files = sorted(
        (f for f in os.listdir(BUNDLE_DIR) if re.fullmatch(r'bundle\d+', f)),
        key=lambda s: int(s[6:]),
    )
    print('scanning', len(bundle_files), 'bundles for CAB names...')
    for bf in bundle_files:
        try:
            env = UnityPy.load(os.path.join(BUNDLE_DIR, bf))
            cabs = set()
            for f in env.files.values():
                name = getattr(f, 'name', None)
                if name:
                    cabs.add(name)
                for inner in (getattr(f, 'files', None) or {}):
                    cabs.add(inner)
            for c in cabs:
                cab = cab_of(c)
                if cab:
                    cab_to_bundle.setdefault(cab, bf)
        except Exception as e:
            print('  ! failed to scan', bf, e)
    print('CAB map entries:', len(cab_to_bundle))

    # ---- Phase 2: resolve diffuse PPtr for every target material
    # entry: name -> (bundle, cab_or_None, path_id, scale, offset)
    resolved = {}
    unresolved = []
    for bname in mat_bundles:
        env = UnityPy.load(os.path.join(BUNDLE_DIR, bname))
        objs = [o for o in env.objects if o.type.name == 'Material']
        for o in objs:
            try:
                tt = o.read_typetree()
            except Exception:
                continue
            name = tt.get('m_Name', '')
            key = name.lower()
            if key not in target_lower or key in resolved:
                continue
            sp = tt.get('m_SavedProperties', {})
            chosen = None
            for want in ('_DiffuseMap', '_Diffuse', '_Albedo', '_MainTex',
                         '_ColourAlpha', '_ForegroundColour', '_BackgroundColour',
                         '_RedColourHeight', '_BlueColourHeight', '_Colour'):
                for k, v in env_pairs(sp.get('m_TexEnvs', [])):
                    if k.lower() == want.lower() and v.get('m_Texture', {}).get('m_PathID', 0):
                        chosen = v
                        break
                if chosen:
                    break
            if not chosen:
                unresolved.append((name, 'no texenv'))
                continue
            ptr = chosen.get('m_Texture', {})
            path_id = ptr.get('m_PathID', 0)
            file_id = ptr.get('m_FileID', 0)
            if not path_id:
                unresolved.append((name, 'null ptr'))
                continue
            if file_id == 0:
                cab = cab_of(getattr(o.assets_file, 'name', ''))
            else:
                exts = list(getattr(o.assets_file, 'externals', []) or [])
                cab = None
                if 0 < file_id <= len(exts):
                    cab = cab_of(getattr(exts[file_id - 1], 'path', ''))
            scale = chosen.get('m_Scale', {'x': 1.0, 'y': 1.0})
            resolved[name] = (bname, cab, path_id, scale)
    print('resolved:', len(resolved), 'unresolved:', len(unresolved))

    # ---- Phase 2b: name-based fallback against dumped PNGs / common01
    dump_pngs = {}
    import glob
    for p in glob.glob(os.path.join(ROOT, 'dump_bundle', 'Assets', '**', '*.png'), recursive=True) + \
              glob.glob(os.path.join(ROOT, 'Assets', 'common01', 'textures', '*.png')):
        dump_pngs.setdefault(os.path.splitext(os.path.basename(p))[0].lower(), p)

    def find_tex_png(matname):
        n = matname.lower()
        if n.startswith('dlc11_'):
            n = n[6:]
        if not n.startswith('mat_'):
            return None
        stem = n[4:]
        cands = [stem]
        for s in (re.sub(r'_\d+x\d+$', '', stem),
                  re.sub(r'_\d+x\d+.*$', '', stem),
                  re.sub(r'_(\d+[_\-]\d+|\d+x\d+b?|no_decal|nodecal|broken|x\d+|outside|water|fill|fix|vs.*|copy|still.*|objectspace.*|highrenderqueue)$', '', stem)):
            if s not in cands:
                cands.append(s)
        for c in cands:
            for suf in ('_d', ''):
                if 't_' + c + suf in dump_pngs:
                    return dump_pngs['t_' + c + suf]
        return None

    fallback = {}
    for name, _reason in unresolved:
        p = find_tex_png(name)
        if p:
            fallback[name] = p
    print('fallback textures found:', len(fallback))
    for u in unresolved:
        print('  ?', u[0], '-', u[1])

    # group needed textures by bundle
    need = {}  # bundle -> {cab: set(path_ids)}
    for name, (bname, cab, pid, _s) in resolved.items():
        tb = cab_to_bundle.get(cab) if cab else bname
        if not tb:
            print('  ! no bundle for cab', cab, 'material', name)
            continue
        need.setdefault(tb, {}).setdefault(cab, set()).add(pid)

    # ---- Phase 3: extract textures
    # tex_key -> png basename (dedupe by cab+pathid)
    tex_names = {}
    png_owner = {}
    for tb in sorted(need):
        env = UnityPy.load(os.path.join(BUNDLE_DIR, tb))
        wanted = need[tb]
        for o in env.objects:
            if o.type.name != 'Texture2D':
                continue
            cab = cab_of(getattr(o.assets_file, 'name', ''))
            if cab not in wanted or o.path_id not in wanted[cab]:
                continue
            key = (cab, o.path_id)
            if key in tex_names:
                continue
            try:
                data = o.read()
                img = data.image
                tname = getattr(data, 'm_Name', '') or ('tex_%d' % o.path_id)
                if not tname.lower().endswith(('.png',)):
                    png = tname + '.png'
                else:
                    png = tname
                png = re.sub(r'[^\w\-.]+', '_', png)
                low = png.lower()
                if low in png_owner and png_owner[low] != key:
                    base, ext = os.path.splitext(png)
                    png = base + '_' + str(o.path_id % 100000) + ext
                    low = png.lower()
                png_owner[low] = key
                img.save(os.path.join(OUT_TEX, png))
                rel = 'Assets/common03/textures/' + png
                with open(os.path.join(OUT_TEX, png + '.meta'), 'w', encoding='utf-8') as f:
                    f.write(TEX_META_TEMPLATE.format(guid=guid_for(rel)))
                tex_names[key] = os.path.splitext(png)[0]
            except Exception as e:
                print('  ! texture decode failed', tb, o.path_id, e)
        print('  textures done for', tb, 'total:', len(tex_names))

    # ---- Phase 4: write materials
    import shutil
    copied_tex = {}
    entries = []
    all_names = sorted(list(resolved) + [n for n in fallback if n not in resolved])
    for name in all_names:
        tex_base = None
        if name in resolved:
            bname, cab, pid, scale = resolved[name]
            key = (cab, pid)
            if key in tex_names:
                tex_base = tex_names[key]
        else:
            scale = {'x': 1.0, 'y': 1.0}
        if tex_base is None and name in fallback:
            src = fallback[name]
            base = re.sub(r'[^\w\-.]+', '_', os.path.splitext(os.path.basename(src))[0])
            if base in copied_tex:
                tex_base = copied_tex[base]
            else:
                dst = os.path.join(OUT_TEX, base + '.png')
                shutil.copyfile(src, dst)
                rel = 'Assets/common03/textures/' + base + '.png'
                with open(dst + '.meta', 'w', encoding='utf-8') as f:
                    f.write(TEX_META_TEMPLATE.format(guid=guid_for(rel)))
                copied_tex[base] = base
                tex_base = base
        if tex_base is None:
            print('  ! no texture for', name)
            continue
        tex_guid = guid_for('Assets/common03/textures/' + tex_base + '.png')
        mat_name = re.sub(r'[^\w\-.]+', '_', name)
        mat_path = os.path.join(OUT_MAT, mat_name + '.mat')
        rel = 'Assets/common03/materials/' + mat_name + '.mat'
        sx, sy = fmt_num(scale.get('x', 1.0)), fmt_num(scale.get('y', 1.0))
        with open(mat_path, 'w', encoding='utf-8') as f:
            f.write(MAT_TEMPLATE.format(name=mat_name, tex_guid=tex_guid, sx=sx, sy=sy))
        with open(mat_path + '.meta', 'w', encoding='utf-8') as f:
            f.write(MAT_META_TEMPLATE.format(guid=guid_for(rel)))
        entries.append({
            'guid': guid_for(rel),
            'id': mat_name,
            'assetPath': rel,
            'sizeTag': size_tag_of(mat_name),
            'texture': tex_base,
            'source': 'common03',
        })

    with open(SUMMARY, 'w', encoding='utf-8') as f:
        json.dump({'materials': entries}, f, ensure_ascii=False, indent=2)
    print('materials written:', len(entries))
    print('summary ->', os.path.relpath(SUMMARY, ROOT))


if __name__ == '__main__':
    sys.exit(main())
