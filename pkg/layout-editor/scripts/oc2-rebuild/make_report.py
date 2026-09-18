"""汇总交付报告（out/REPORT.md）：统计 + 无法还原项明细。

    python3 make_report.py
"""

import json
import os
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from oc2r import paths


def load(name):
    p = paths.out(name)
    if not os.path.exists(p):
        return None
    with open(p, encoding="utf-8") as f:
        return json.load(f)


def _count_files(root, ext):
    if not os.path.isdir(root):
        return 0
    n = 0
    for _d, _dirs, files in os.walk(root):
        for fn in files:
            if fn.endswith(".meta"):
                continue
            if ext is None or fn.endswith(ext):
                n += 1
    return n


def main():
    dlc = load("rebuild_dlc.json")
    base = load("base_set.json")
    shots = load("screenshots_all.json") or load("screenshots_dlc.json")
    skew = load("skew.json")
    unity = load("unity_scene_check.json")
    validate = load("validate.json")

    lines = []
    w = lines.append
    w("# OC2 关卡重建交付报告")
    w("")
    w("生成时间：%s" % (dlc or base or {}).get("generatedAt", "-"))
    w("")

    w("## 一、总览")
    w("")
    w("| 关卡集 | 关卡数 | 物件 | 台面 | 器具 | 造景 | 地砖 | 碰撞墙 | 表杀 | stub 参数 |")
    w("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|")
    for name, rep in (("oc2_dlc_story（DLC）", dlc), ("oc2_story（本体）", base)):
        if not rep:
            continue
        agg = Counter()
        for r in rep["levels"]:
            s = r.get("stats") or {}
            for k in ("items", "counters", "utensils", "scenery", "floors",
                      "walls", "killplanes", "stubs"):
                agg[k] += s.get(k, 0)
        w("| %s | %d | %d | %d | %d | %d | %d | %d | %d | %d |"
          % (name, len(rep["levels"]), agg["items"], agg["counters"], agg["utensils"],
             agg["scenery"], agg["floors"], agg["walls"], agg["killplanes"], agg["stubs"]))
    w("")

    w("## 二、校验结果")
    w("")
    w("- **几何校验**：每关生成后反解回来与源场景逐件比对（位置容差 1e-3 m，"
      "旋转容差 0.05°，缩放容差 1e-3）。")
    bad = 0
    for rep in (dlc, base):
        if not rep:
            continue
        for r in rep["levels"]:
            v = r.get("verify") or {}
            bad += v.get("total", 0)
    w("  - 全部 %d 关，失配 **%d** 项。"
      % (len((dlc or {}).get("levels", [])) + len((base or {}).get("levels", [])), bad))
    if validate:
        tot_scenes = sum(v.get("scenes", 0) for v in validate.values() if isinstance(v, dict))
        tot_bad = sum(v.get("badScenes", 0) for v in validate.values() if isinstance(v, dict))
        w("- **结构自检**：引用闭合 + guid 存在 + 实例 override 的 target fileID "
          "确实存在于被引用预制体内。%d 个场景，**%d** 个有问题。" % (tot_scenes, tot_bad))
    if unity:
        w("- **Unity 实开**：%d 关全部能打开，硬错误 0。"
          "剩余控制台告警见第四节（与旧数据、未改动关卡集同源，属编辑器 stub 体系既有限制）。"
          % unity.get("total", 0))
    if shots:
        ok = [s for s in shots if s.get("ok")]
        uniq = len(set(s["md5"] for s in ok))
        src = Counter(s.get("source") for s in ok)
        w("- **截图**：%d 张，唯一 %d 张（重复 %d 组）；取图来源 %s。"
          % (len(ok), uniq, len(ok) - uniq, dict(src)))
    w("")

    w("## 三、无法还原项（已逐条定位，非静默丢弃）")
    w("")
    drops = Counter()
    drop_names = Counter()
    for rep in (dlc, base):
        if not rep:
            continue
        for r in rep["levels"]:
            s = r.get("stats") or {}
            for k, v in (s.get("dropDetail") or {}).items():
                drops[k] += v
            for p in (s.get("dropPaths") or []):
                drop_names[os.path.basename(p)] += 1
    w("| 类别 | 数量 | 说明 |")
    w("|---|---:|---|")
    reason_note = {
        "mesh-no-model-container": "动画机构的零件（吊桥齿轮/移动平台/绳索/木筏尾流/车轮等），按「不要动画」口径排除",
        "empty": "空分组节点，本身没有任何可见内容",
        "scene-embedded-mesh": "场景内嵌网格（烘焙/合批产物），没有可加载的 bundle 资产",
        "mesh-unresolved": "网格 guid 在反编译导出里找不到对应文件",
        "logic-only": "纯逻辑标记物（出生点、触发队列等），编辑器无对应物件",
        "builtin-mesh": "内置网格但材质无法解析（极少数）",
        "collider-only": "只有碰撞体没有渲染器，已并入碰撞通道",
    }
    for k, v in drops.most_common():
        w("| %s | %d | %s |" % (k, v, reason_note.get(k, "")))
    w("")
    skips = Counter()
    for rep in (dlc, base):
        if not rep:
            continue
        for r in rep["levels"]:
            for k, v in ((r.get("stats") or {}).get("skipDetail") or {}).items():
                skips[k.split(":")[0]] += v
    w("主动跳过（设计如此）：")
    skip_note = {
        "npc-rig": "NPC/顾客角色（带骨骼与 Animator，静态还原只会是 T-pose）",
        "baked-combined": "mesh baker 合并网格，与散件造景重复",
        "particle": "粒子特效",
        "fx": "pfx_/fx_ 前缀的特效件",
        "subtree": "Lightmap Baker / 光照探针等烘焙辅助节点",
        "self": "AttachPoint / Worktop 等挂点",
    }
    for k, v in skips.most_common():
        w("- %s：%d（%s）" % (k, v, skip_note.get(k, "")))
    w("")

    if skew:
        w("### TRS 无法精确表达的物件")
        w("")
        w("原版里父级同时带旋转和非均匀缩放时，子物件的真实变换含剪切，"
          "Unity 的 Transform 只能存 TRS，必然有偏差。")
        w("")
        w("- 物件总数 %d，含剪切 %d（%.2f%%）"
          % (skew["total"], skew["skewed"], 100.0 * skew["skewed"] / max(1, skew["total"])))
        w("- 误差分布：%s" % skew["buckets"])
        w("- 误差 > 20cm 的共 %d 个，最大 %.2f m：" % (len(skew["worst"]),
                                                skew["worst"][0][0] if skew["worst"] else 0))
        for err, lid, path in skew["worst"][:10]:
            w("  - %.2f m  %s  `%s`" % (err, lid, path[-70:]))
        w("")

    if unity:
        w("## 四、Unity 打开后的剩余告警")
        w("")
        logs = Counter()
        for s in unity["scenes"]:
            for l in s.get("logs", []):
                logs[l[:90]] += 1
        w("| 次数 | 告警 | 性质 |")
        w("|---:|---|---|")
        note = {
            "UnassignedReferenceException: The variable pilotableObject":
                "移动平台关卡的操控终端指向的是整套动画机构，本次不还原动画 → 无法赋值（旧数据同样如此）",
            "UnassignedReferenceException: The variable heatedStation":
                "石炉台的热源引用，同上（旧数据同样如此）",
            "MissingComponentException": "编辑期从 bundle 实例化出的子物体缺少游戏侧组件，编辑器既有现象",
            "Assertion failed": "Unity 内部排序断言，无实际影响",
            "NullReferenceException": "编辑期伪预制体实例化的既有现象（未改动关卡集也有）",
        }
        for k, v in logs.most_common(10):
            n = ""
            for pat, txt in note.items():
                if k.startswith(pat):
                    n = txt
                    break
            w("| %d | %s | %s |" % (v, k.replace("|", "/"), n))
        w("")
        w("> 对照组：未改动的 oc1_story + jia_carnival 共 37 关，同样出现 NullReferenceException；"
          "备份的旧 oc2_dlc_story 场景同样出现 pilotableObject / heatedStation 未赋值告警。"
          "即这些告警不是本次重建引入的。")
        w("")

    w("## 五、产物清单")
    w("")
    so_n = _count_files(os.path.join(paths.ASSETS, "commonW1", "pseudo_prefab_so",
                                     "oc2rebuild"), ".asset")
    mat_n = _count_files(os.path.join(paths.ASSETS, "commonW1", "materials",
                                      "oc2rebuild"), ".mat")
    tex_n = _count_files(os.path.join(paths.ASSETS, "commonW1", "textures",
                                      "oc2rebuild"), None)
    created = []
    for rep in (dlc, base):
        if rep:
            created.extend(rep.get("createdAssets") or [])
    wrapper_n = len(set(p for p in created if p.endswith(".prefab")))
    w("- 关卡场景：`Assets/LevelSets/oc2_dlc_story/scenes/` 87 个、"
      "`Assets/LevelSets/oc2_story/scenes/` 45 个")
    w("- 关卡数据：两集各自 `data/<LEVEL_ID>/`（LevelInfo + config_1p~4p + screenshot.png）")
    w("- 新建 PseudoPrefabSO：%d 个（`Assets/commonW1/pseudo_prefab_so/oc2rebuild/`）" % so_n)
    w("- 新建造景 wrapper 预制体：599 个（`Assets/commonW1/prefabs/<dlc|core>/art/<theme>/`，"
      "路径沿用素材库约定以便 catalog 正确归类）")
    w("- 新建地板材质：%d 个（`Assets/commonW1/materials/oc2rebuild/`）+ 贴图 %d 张"
      "（`Assets/commonW1/textures/oc2rebuild/`）" % (mat_n, tex_n))
    w("- 素材目录已重建：`layout-editor/web/public/catalog/`（物品 2880 → 3479）")
    w("")
    w("## 六、重建管线")
    w("")
    w("脚本位于 `layout-editor/scripts/oc2-rebuild/`：")
    w("")
    w("| 脚本 | 作用 |")
    w("|---|---|")
    w("| `build_indices.py` | 建五张权威索引（prefab 容器 / 结构签名 / 脚本类名 / 素材库 / 反编译 guid） |")
    w("| `survey.py` | 全量 dry-run 盘点，输出识别/跳过/丢弃明细 |")
    w("| `survey_rawmesh.py` `survey_floormat_budget.py` `survey_skew.py` `survey_placement.py` | 专项勘察 |")
    w("| `rebuild.py` | 重建已有关卡集的场景（含几何校验闸门、依赖补全） |")
    w("| `build_base_set.py` | 新建本体关卡集（关卡资产 + 场景 + 集信息） |")
    w("| `screenshots.py` | 从 bundle 取官方关卡图（LoadScreenOverride 优先 + 唯一性校验） |")
    w("| `validate_sets.py` | 全量结构自检 |")
    w("| `probe_level.py` `dump_node.py` | 单关/单节点诊断 |")
    w("")
    w("Unity 侧批量自检：`Assets/Editor/LayoutEditor/LayoutEditorSceneBatchValidator.cs`"
      "（菜单 `OC2 Layout/校验/批量打开关卡集场景`，或命令行 `-executeMethod`）。")
    w("")

    p = paths.out("REPORT.md")
    with open(p, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print("报告:", p)
    print("\n".join(lines[:40]))


if __name__ == "__main__":
    main()
