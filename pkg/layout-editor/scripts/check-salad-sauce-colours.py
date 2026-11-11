"""Independent native identity/UV evidence; never edits bundles or user assets."""
import hashlib
import json
from pathlib import Path

import UnityPy
from PIL import Image, ImageStat
from UnityPy.helpers.MeshHelper import MeshHandler

ROOT = Path(__file__).resolve().parents[2]
REPORT = ROOT / "layout-editor/reports/salad-sauce-integration/colour-native-evidence.json"


def main():
    bundles = ["bundle351", "bundle354", "bundle358", "bundle359", "bundle427", "bundle428"]
    env = UnityPy.load(*[str(ROOT / "Assets/StreamingAssets/Windows" / n) for n in bundles])
    manifest = json.loads((ROOT / "layout-editor/scripts/data/salad-sauce-recipes.json").read_text())
    policy = manifest["sauceUidPolicy"]
    assert policy["canonical"] == {"ketchup": 890084, "mustard": 890076}
    assert policy["saladOnlyAliases"] == {"158482": 890084, "17094": 890076}
    assert policy["nativeDispenserVisualSlots"] == {"mustard": 0, "ketchup": 1}
    result = {"ok": True, "nativeNodes": [], "machines": [], "hotdogs": [], "saladTextures": []}

    def asset(suffix):
        return next(v for k, v in env.container.items() if k.endswith(suffix))

    def walk(go):
        yield go
        for c in go.m_Component:
            if c.component.type.name == "Transform":
                for child in c.component.read().m_Children:
                    yield from walk(child.read().m_GameObject.read())

    def uv_mean(go):
        cs = [c.component for c in go.m_Component]
        mesh = next(c.read().m_Mesh.read() for c in cs if c.type.name == "MeshFilter")
        material = next(c.read().m_Materials[0].read() for c in cs if c.type.name == "MeshRenderer")
        texture = next(v.m_Texture.read() for k, v in material.m_SavedProperties.m_TexEnvs
                       if k in ("_MainTex", "_ColourAlpha") and v.m_Texture.path_id)
        handler = MeshHandler(mesh)
        handler.process()
        image = texture.image.convert("RGB")
        pixels = [image.getpixel((int(u[0] * image.width) % image.width,
                                 int((1 - u[1]) * image.height) % image.height)) for u in handler.m_UV0]
        return {"mesh": mesh.m_Name, "material": material.m_Name, "texture": texture.m_Name,
                "uvMeanRgb": [sum(p[c] for p in pixels) / len(pixels) for c in range(3)]}

    for family, ids in (("dlc08", (17094, 158482)), ("dlc11", (890076, 890084))):
        prefix = "" if family == "dlc08" else "dlc11_"
        nodes = []
        for sauce, uid in zip(("mustard", "ketchup"), ids):
            p = asset("/ingredients/" + prefix + sauce + ".asset")
            data = p.read_typetree()
            node = p.read()
            icon = node.m_iconSprite.read().m_Name
            icon_rgb = ImageStat.Stat(node.m_iconSprite.read().image.convert("RGB")).mean
            assert (icon_rgb[1] - icon_rgb[2] > 15) == (sauce == "mustard")
            assert (icon_rgb[0] - icon_rgb[1] > 25) == (sauce == "ketchup")
            assert data["m_uID"] == uid and sauce in data["m_Name"].lower() and sauce in icon.lower()
            colour = data["m_colour"]
            assert (colour["g"] / colour["r"] > 0.6) == (sauce == "mustard")
            result["nativeNodes"].append({"family": family, "uid": uid, "name": data["m_Name"],
                                          "icon": icon, "iconMeanRgb": icon_rgb, "colour": colour, "pathId": p.path_id})
            nodes.append(node)
        machine = asset("/" + family + "_condiment_dispenser.prefab").read()
        components = [c.component.read() for c in machine.m_Component if c.component.type.name == "MonoBehaviour"]
        switcher = next(c for c in components if hasattr(c, "m_ingredients"))
        effects = next(c for c in components if hasattr(c, "m_particleEffects"))
        assert [p.read().m_uID for p in switcher.m_ingredients] == list(ids)
        slots = []
        for slot, sauce in enumerate(("mustard", "ketchup")):
            bottle = next(g for g in walk(machine) if g.m_Name == "Condiment_" + str(slot + 1))
            evidence = uv_mean(bottle)
            r, g, b = evidence["uvMeanRgb"]
            assert (g / r > 0.85) == (slot == 0) and r > b * 1.5
            effect = effects.m_particleEffects[slot].read().m_Name
            assert sauce in effect.lower()
            slots.append({"slot": slot, "uid": ids[slot], "trigger": "Condiment" + str(slot + 1),
                          "effect": effect, **evidence})
        result["machines"].append({"family": family, "slots": slots})
        models = []
        for suffix, sauce in (("k", "ketchup"), ("m", "mustard")):
            recipe = asset("/recipeitems/" + prefix + "hotdog_" + sauce + ".asset").read()
            assert any(p.read().m_uID == ids[0 if sauce == "mustard" else 1] for p in recipe.m_composition)
            model = recipe.m_platingPrefab.read()
            assert ("_" + suffix + "_01") in model.m_Name.lower()
            evidence = uv_mean(model)
            models.append(evidence)
            result["hotdogs"].append({"family": family, "recipe": recipe.m_Name, "sauce": sauce, **evidence})
        k, m = (v["uvMeanRgb"] for v in models)
        assert k[1] / k[0] < 0.52 and m[1] / m[0] > 0.58
    model_report = json.loads((ROOT / "layout-editor/reports/salad-sauce-models/report.json").read_text())
    for name, yellow in (("SaladKetchup", False), ("SaladMustard", True)):
        path = ROOT / "Assets/commonW3/custom_recipes/salad/models" / (name + "_Albedo.png")
        mean = ImageStat.Stat(Image.open(path).convert("RGB")).mean
        assert (mean[1] / mean[0] > 0.5) == yellow
        record = next(r for r in model_report["models"] if r["name"] == name)
        source = Path(record["source"])
        assert hashlib.sha256(source.read_bytes()).hexdigest() == record["source_sha256"]
        assert (record["source_albedo_mean_decoded_rgb"][1] / record["source_albedo_mean_decoded_rgb"][0] > 0.3) == yellow
        result["saladTextures"].append({"name": name, "meanRgb": mean, "source": str(source),
                                        "sourceSha256": record["source_sha256"],
                                        "sourceEmbeddedDecodedRgb": record["source_albedo_mean_decoded_rgb"]})
    REPORT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"ok": True, "nativeNodes": 4, "machineSlots": 4, "nativeHotdogModels": 4, "report": str(REPORT)}))


if __name__ == "__main__":
    main()
