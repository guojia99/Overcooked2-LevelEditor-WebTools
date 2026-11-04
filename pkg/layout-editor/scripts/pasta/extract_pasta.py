#!/usr/bin/env python3
"""Phase 0: extract vanilla pasta data from bundles (uIDs + plated prefab trees).

Usage: python3 extract_pasta.py [--json out.json]
"""
import json
import sys

import UnityPy

WINDOWS = "Assets/StreamingAssets/Windows"

TARGET_NODES = {
    "assets/data/orderdefinitions/cookedingredients/boiledpasta.asset",
    "assets/data/orderdefinitions/ingredients/sushifish.asset",
    "assets/data/orderdefinitions/ingredients/sushiprawn.asset",
    "assets/data/recipedata/cookingstepdata/fryingpan.asset",
    "assets/data/recipedata/cookingstepdata/pot.asset",
}
TARGET_PREFABS = {
    "assets/prefabs/plated/plated_pasta_01.prefab",
    "assets/prefabs/plated/plated_pasta_fish_01.prefab",
    "assets/prefabs/plated/plated_pasta_prawn_01.prefab",
    "assets/prefabs/meals/recipe_seafood_pasta_01.prefab",
}


def json_safe(o):
    if isinstance(o, dict):
        return {str(k): json_safe(v) for k, v in o.items()}
    if isinstance(o, (list, tuple)):
        return [json_safe(v) for v in o]
    if isinstance(o, (int, float, str, bool)) or o is None:
        return o
    return str(o)


def main():
    repo = __file__
    import os
    os.chdir(os.path.join(os.path.dirname(os.path.abspath(repo)), "..", "..", ".."))

    report = {"nodes": {}, "prefabs": {}}

    env = UnityPy.load(f"{WINDOWS}/bundle18")
    for path, obj in env.container.items():
        p = path.lower()
        if p not in TARGET_NODES:
            continue
        try:
            tree = obj.read_typetree()
            report["nodes"][p] = json_safe(tree)
        except Exception as e:
            report["nodes"][p] = {"error": repr(e)}

    env47 = UnityPy.load(f"{WINDOWS}/bundle47")
    for path, obj in env47.container.items():
        p = path.lower()
        if p not in TARGET_PREFABS:
            continue
        entry = {"type": obj.type.name}
        try:
            entry["tree"] = json_safe(obj.read_typetree())
        except Exception as e:
            entry["error"] = repr(e)
        report["prefabs"][p] = entry

    out = json.dumps(report, indent=1, default=str)
    if "--json" in sys.argv:
        with open(sys.argv[sys.argv.index("--json") + 1], "w") as f:
            f.write(out)
    else:
        print(out)


if __name__ == "__main__":
    main()
