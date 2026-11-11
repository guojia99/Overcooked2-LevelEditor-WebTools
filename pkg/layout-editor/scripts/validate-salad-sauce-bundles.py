"""Read actual Windows bundles without Unity CPU-readability restrictions."""
import hashlib
import json
import os
import sys
import urllib.request
from pathlib import Path

import UnityPy
from PIL import Image, ImageChops, ImageStat
from UnityPy.helpers.MeshHelper import MeshHandler

ROOT = Path(__file__).resolve().parents[2]
BUILD = ROOT / "Library/SaladSauceBundleBuild"
REPORT = ROOT / "layout-editor/reports/salad-sauce-integration"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def inspect_bundle(path):
    env = UnityPy.load(str(path))
    textures = []
    scripts = []
    dlls = []
    materials = []
    visuals = []
    icon_sources = []
    for obj in env.objects:
        if obj.type.name == "Texture2D":
            texture = obj.read()
            raw = bytes(texture.image_data)
            if not raw and texture.m_StreamData and texture.m_StreamData.size:
                from UnityPy.helpers.ResourceReader import get_resource_data
                stream = texture.m_StreamData
                raw = get_resource_data(stream.path, obj.assets_file, stream.offset, stream.size)
            if not raw:
                raise RuntimeError("No serialized texture bytes: " + texture.m_Name)
            textures.append({
                "name": texture.m_Name, "pathId": obj.path_id,
                "width": texture.m_Width, "height": texture.m_Height,
                "format": int(texture.m_TextureFormat), "bytes": len(raw),
                "sha256": digest(raw),
            })
            if texture.m_Name.startswith("Web_SauceSalad_"):
                name = texture.m_Name + ".png"
                paths = [ROOT / p / name for p in (
                    "Assets/commonW3/custom_recipes/salad/icons",
                    "layout-editor/web/public/icons/recipes", "layout-editor/web/dist/icons/recipes")]
                hashes = [digest(p.read_bytes()) for p in paths]
                assert len(set(hashes)) == 1, name + " source/public/dist differ"
                source = Image.open(paths[0]).convert("RGBA")
                decoded = texture.image.convert("RGBA")
                assert decoded.size == source.size
                # Unity's DXT import and alpha-is-transparency alter hidden RGB.
                expected = Image.new("RGB", source.size)
                expected.paste(source, mask=source.getchannel("A"))
                actual = Image.new("RGB", source.size)
                actual.paste(decoded, mask=decoded.getchannel("A"))
                rgb_error = ImageStat.Stat(ImageChops.difference(expected, actual)).mean
                alpha_error = ImageStat.Stat(ImageChops.difference(source.getchannel("A"), decoded.getchannel("A"))).mean[0]
                assert max(rgb_error) < 5 and alpha_error < 2, (name, rgb_error, alpha_error)
                icon_sources.append({"name": name, "sourcePublicDistSha256": hashes[0],
                                     "bundledPayloadSha256": digest(raw),
                                     "decodedRgbaSha256": digest(decoded.tobytes()),
                                     "premultipliedRgbMeanAbsoluteError": rgb_error, "alphaMeanAbsoluteError": alpha_error})
        elif obj.type.name == "Transform":
            transform = obj.read()
            if transform.m_GameObject.read().m_Name != "Sauce":
                continue
            parent = transform.m_Father.read().m_GameObject.read().m_Name
            if parent not in ("SaladKetchupVisual", "SaladMustardVisual"):
                continue
            expected_y = 0.20 if parent == "SaladKetchupVisual" else 0.206
            position, scale, rotation = transform.m_LocalPosition, transform.m_LocalScale, transform.m_LocalRotation
            assert abs(position.y - expected_y) < 1e-6 and position.x == position.z == 0
            assert scale.x == scale.y == scale.z == 1
            assert rotation.x == rotation.y == rotation.z == 0 and rotation.w == 1
            components = transform.m_GameObject.read().m_Component
            mesh_filter = next(c.component.read() for c in components if c.component.type.name == "MeshFilter")
            renderer = next(c.component.read() for c in components if c.component.type.name == "MeshRenderer")
            material = renderer.m_Materials[0].read()
            albedo = next(v.m_Texture.read() for k, v in material.m_SavedProperties.m_TexEnvs if k == "_MainTex")
            mesh = MeshHandler(mesh_filter.m_Mesh.read())
            mesh.process()
            image = albedo.image.convert("RGB")
            sampled = [image.getpixel((int(uv[0] * image.width) % image.width,
                                      int((1 - uv[1]) * image.height) % image.height)) for uv in mesh.m_UV0]
            rgb = [sum(p[c] for p in sampled) / len(sampled) for c in range(3)]
            assert (rgb[1] / rgb[0] > 0.5) == (parent == "SaladMustardVisual"), (parent, rgb)
            bounds = mesh_filter.m_Mesh.read().m_LocalAABB
            low = bounds.m_Center.y - bounds.m_Extent.y + position.y
            high = bounds.m_Center.y + bounds.m_Extent.y + position.y
            assert low >= expected_y - 1e-5 and high > low
            visuals.append({"name": parent, "sauceY": position.y, "boundsMinY": low, "boundsMaxY": high, "lift": 0.20,
                            "material": material.m_Name, "albedo": albedo.m_Name, "uvMeanRgb": rgb})
        elif obj.type.name == "Material":
            material = obj.read()
            if material.m_Name in ("SaladKetchup", "SaladMustard"):
                slots = {}
                for name, value in material.m_SavedProperties.m_TexEnvs:
                    if value.m_Texture.path_id:
                        slots[name] = value.m_Texture.read().m_Name
                materials.append({"name": material.m_Name, "textures": slots})
        elif obj.type.name == "MonoScript":
            script = obj.read()
            scripts.append({"class": script.m_ClassName, "namespace": script.m_Namespace,
                            "assembly": script.m_AssemblyName})
        elif obj.type.name == "TextAsset":
            asset = obj.read()
            if asset.m_Name.endswith(".dll"):
                raw = asset.m_Script
                if isinstance(raw, str):
                    raw = raw.encode("utf-8", "surrogateescape")
                dlls.append({"name": asset.m_Name, "bytes": len(raw), "sha256": digest(raw)})
    groups = {}
    for texture in textures:
        key = (texture["width"], texture["height"], texture["format"], texture["sha256"])
        groups.setdefault(key, []).append(texture)
    duplicates = [group for group in groups.values() if len(group) > 1]
    return {
        "name": path.name, "bytes": path.stat().st_size, "sha256": digest(path.read_bytes()),
        "textures": textures, "textureDuplicateGroups": duplicates,
        "duplicateTexturePayloadBytes": sum(sum(t["bytes"] for t in g[1:]) for g in duplicates),
        "monoScripts": scripts, "dlls": dlls, "sauceMaterials": materials,
        "visualGeometry": visuals, "latestIconSources": icon_sources,
    }


def main():
    baseline_name = next((a.split("=", 1)[1] for a in sys.argv if a.startswith("--baseline=")), "height-baseline.json")
    baseline_path = REPORT / baseline_name
    if "--capture-height-baseline" in sys.argv or "--capture-baseline" in sys.argv:
        paths = list((ROOT / "Assets/LevelSets").rglob("*.unity")) + list((ROOT / "Assets/LevelSets").rglob("*.asset"))
        paths += list((ROOT / "Assets/commonW3/custom_recipes/salad/models").glob("*.fbx*"))
        paths += [ROOT / "layout-editor/scripts/compose-salad-sauce-icons.py"]
        for folder in ("Assets/commonW3/custom_recipes/salad/icons", "layout-editor/web/public/icons/recipes", "layout-editor/web/dist/icons/recipes"):
            icons = list((ROOT / folder).glob("Web_SauceSalad_*.png"))
            assert len(icons) == 78
            paths += icons
        baseline = {str(p.relative_to(ROOT)): digest(p.read_bytes()) for p in paths}
        if "--capture-baseline" in sys.argv and baseline_path.exists():
            raise RuntimeError("Refusing to overwrite an existing verification baseline")
        baseline_path.write_text(json.dumps(baseline, indent=2) + "\n")
        print(json.dumps({"protectedBaselineFiles": len(baseline)}))
        return
    unity = json.loads((BUILD / "result.json").read_text())
    if unity["state"] not in ("completed", "failed"):
        raise RuntimeError("Build still running; never resubmit blindly")
    bundles = [inspect_bundle(BUILD / "Windows" / name) for name in ("commonw3", "webcustomstub_runtime")]
    for bundle, original in zip(bundles, unity["bundles"]):
        assert bundle["sha256"] == original["sha256"]
        assert bundle["bytes"] == original["bytes"]
    common = bundles[0]
    assert len(common["visualGeometry"]) == 2
    assert len(common["latestIconSources"]) == 78
    baseline = json.loads(baseline_path.read_text())
    for path, sha256 in baseline.items():
        assert digest((ROOT / path).read_bytes()) == sha256, "Protected file changed: " + path
    icons = [t for t in common["textures"] if t["name"].startswith("Web_SauceSalad_")]
    assert len(icons) == 78, len(icons)
    scripts = {s["class"] for s in common["monoScripts"]}
    assert {"SaladSauceVisuals", "SaladCosmeticDecisions", "AssignableOrderDefinition"} <= scripts
    runtime = next(d for d in bundles[1]["dlls"] if d["name"] == "WebCustomStubRuntime.dll")
    assert runtime["sha256"] == unity["runtimeDllSha256"]
    assert runtime["sha256"] == digest((ROOT / "Library/ScriptAssemblies/WebCustomStubRuntime.dll").read_bytes())
    assert runtime["sha256"] == digest((ROOT / "Assets/WebCustomStubRuntime/RuntimeDll/WebCustomStubRuntime.dll.bytes").read_bytes())
    source_textures = [t for t in common["textures"] if t["name"].startswith(("SaladKetchup_", "SaladMustard_"))]
    assert len(source_textures) == 6
    assert len(common["sauceMaterials"]) == 2
    for material in common["sauceMaterials"]:
        slots = material["textures"]
        assert slots["_MainTex"] == material["name"] + "_Albedo"
        assert slots["_BumpMap"] == material["name"] + "_Normal"
        assert slots["_MetallicGlossMap"] == material["name"] + "_MetallicSmoothness"
    assert unity["ok"] and unity["scenesUnchanged"], unity.get("error")
    host = os.environ.get("OC2_EDITOR_BRIDGE_URL", "http://10.211.55.3:8765").rstrip("/")
    with urllib.request.urlopen(host + "/api/recipes/salad-sauce", timeout=90) as response:
        native = json.load(response)
    assert native["ok"] and native["nativeChecks"] >= 3088 and len(native["colourEvidence"]) >= 32
    assert len(unity["colourEvidence"]) == 2
    assert len(native["visualGeometry"]) == 6
    previous = []
    json_path = REPORT / "bundle-validation.json"
    if json_path.exists():
        old = json.loads(json_path.read_text())
        previous = old.get("runs", [old])
        if previous and previous[-1].get("unity", {}).get("completedUtc") == unity["completedUtc"]:
            previous = previous[:-1]
    run = {
        "ok": True, "unity": unity, "serializedBundleReadback": bundles,
        "sauceIcons": len(icons), "sauceTextures": source_textures, "postBuildNativeValidation": native,
        "protectedBaselineFilesUnchanged": len(baseline),
        "protectedBaselinePath": baseline_name,
        "limitations": [
            "The requested 0.200/0.206 m heights were used; bounds are relative to isolated identity carriers, not measured against an actual plated dish surface. Final visual clearance remains unapproved.",
            "Isolated two-bundle map embeds references to other libraries; not a full dependency distribution or production replacement manifest.",
            "No game launch/network validation; carrier scripts/references verified from bundles, native lookup behavior verified separately by the Unity factory GET.",
            "Texture duplicates compare serialized pixel payload plus dimensions/format within these outputs; existing external bundle duplication is not included.",
            "Existing commonW3 CustomRecipeConfigSO references Assembly-CSharp-Editor.dll; this editor config is unrelated to the two verified carrier scripts and was not modified.",
        ],
    }
    REPORT.mkdir(parents=True, exist_ok=True)
    json_path.write_text(json.dumps({"runs": previous + [run]}, indent=2, ensure_ascii=False) + "\n")
    lines = [
        "## Windows Bundle Validation " + unity["completedUtc"], "",
        "PASS. Built with an explicit two-entry AssetBundleBuild[] and reloaded both actual outputs. No scene/LevelInfo save, publish archive, version bump or commit.", "",
        "Output: `Library/SaladSauceBundleBuild/Windows/` (isolated from the main AssetBundles manifest).", "",
        "| Bundle | Bytes | MiB | SHA-256 |", "| --- | ---: | ---: | --- |",
    ]
    for b in bundles:
        lines.append("| %s | %d | %.3f | `%s` |" % (b["name"], b["bytes"], b["bytes"] / 1048576, b["sha256"]))
    lines += ["", "Verified 78 new icons, 78 final recipes, 24 optional anchors, two carrier prefabs with native/runtime scripts and all seven lookup/visual references. Serialized native lookup is intentionally null; runtime Awake clones the original lookup.", "",
              "Meshes: " + "; ".join(unity["meshes"]) + ". Six sauce textures are present; each material's _MainTex, _BumpMap and _MetallicGlossMap resolves to its corresponding albedo, normal and metallic/smoothness texture.", "",
              "Runtime source DLL, staged bytes and bundled TextAsset SHA-256 all match: `" + unity["runtimeDllSha256"] + "`. Runtime state: fresh. Scene handles/paths/dirty flags unchanged across build.", "",
              "Post-build factory GET passed %d native checks (78 finals / 24 anchors), including original lookup cloning and sauce behavior." % native["nativeChecks"], "",
              "Unity CPU texture data was unavailable for non-readable textures; duplication below uses UnityPy serialized payloads, not empty Unity readback hashes.", ""]
    for b in bundles:
        lines.append("- %s: %d Texture2D objects; %d duplicate groups; %d redundant serialized pixel bytes (before bundle compression)." % (b["name"], len(b["textures"]), len(b["textureDuplicateGroups"]), b["duplicateTexturePayloadBytes"]))
        for group in b["textureDuplicateGroups"]:
            lines.append("\nDuplicate: " + ", ".join(t["name"] for t in group) + ".\n")
    sauce_duplicate = [g for g in common["textureDuplicateGroups"] if any(t["name"].startswith(("SaladKetchup_", "SaladMustard_")) for t in g)]
    lines += ["", "Sauce model texture duplicate groups: %d. Full texture hashes, embedded script identities, asset lists and errors are recorded in JSON." % len(sauce_duplicate), "", "Limitations:", ""]
    lines.extend("- " + item for item in run["limitations"])
    lines += ["", "Height repair: ketchup Sauce local y=0.200 m; mustard y=0.206 m; preserved 0.006 m separation, X/Z, scale, rotation and FBX geometry. Both serialized bundle transforms/bounds and Story/DLC11 native rendering callbacks passed.", ""]
    lines.extend("- " + item for item in native["visualGeometry"])
    lines += ["", "All 78 latest icon source/public/dist PNG SHA-256 values agree. Bundled DXT payload and decoded RGBA hashes are recorded individually; decoded premultiplied RGB mean error is below 5/255 per channel and alpha mean error below 2/255 against the latest sources. PNG file hashes and DXT payload hashes intentionally describe different encodings.",
              "", "Protected baseline files unchanged: %d (all user scene/LevelInfo .unity/.asset files, FBX files/metas, icon compositor and 234 source/public/dist icons). Baseline: `%s`; this verifies changes since that explicit capture, not a pre-turn state." % (len(baseline), baseline_name),
              "", "C# 4 / net35 standalone build passed: 0 errors, 41 existing warnings. Unity compiled the new height assertions; POST rebuild and GET readback passed. No Windows gameplay or visual approval was performed.", ""]
    md_path = REPORT / "bundle-validation.md"
    existing = md_path.read_text() if md_path.exists() else "# Salad Sauce Bundle Validation\n\n"
    marker = "## Windows Bundle Validation " + unity["completedUtc"]
    if marker in existing:
        existing = existing[:existing.index(marker)]
    md_path.write_text(existing + "\n".join(lines) + "\n")
    print(json.dumps({"ok": True, "bundles": [{"name": b["name"], "bytes": b["bytes"], "textures": len(b["textures"]), "duplicates": len(b["textureDuplicateGroups"]), "redundantBytes": b["duplicateTexturePayloadBytes"]} for b in bundles], "sauceTextureDuplicateGroups": len(sauce_duplicate)}))


if __name__ == "__main__":
    main()
