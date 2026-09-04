/**
 * 道具 DLC 变体（换肤）统一表：左侧调色板每个家族只显示基础版一张卡片，
 * 已放置物品通过右键菜单切换为特定变体。
 *
 * 基准规则（与用户约定）：
 *  - 有核心版（common01/02）的家族用核心版：煎锅→FryPan、烤箱→Oven、玻璃杯堆→CleanGlassStack。
 *  - 纯 DLC 家族用最高 DLC 版：脏玻璃杯堆→dlc11_dirtyglassstack。
 *  - 马克杯家族默认用核心/非 DLC 版（cleanmugstack/equipment_mug_01/dirtymugstack/dirtymug/
 *    马克杯水槽/马克杯回收台真实 prefab 为 bundle210/dlc03，dlc09 为换皮变体）。
 *  - 例外不合并（各自独立出卡）：饮料机/汽水机/酱料机（dlc 各自一套可输出内容）、
 *    开关/压力开关、断头台。
 *
 * 本表是 FUNCTIONAL_BASE（recipeKnowledge.ts）与调色板合并的唯一数据源
 * （历史上两份手维护清单内容不一致，已统一于此）。
 */
import { S, EditorItem } from "./state";
import type { CatalogItem } from "../types";
import { uuid, prefabIdFromPath, escHtml, newEditorKey } from "./coords";
import { tidyCatalogNameZh } from "../displayLabels";
import { stubKindOf, STUB_KIND_BY_PREFAB_ID } from "./stubControls";
import { pushHistory } from "./historyOps";
import { draw } from "./render";
import { setStatus } from "./status";
import { setSelection } from "./selection";
import { hideContextMenu } from "./ui/overlay";

/** DLC 换皮/变体 → 家族基础版道具 id。 */
export const VARIANT_TO_BASE: Record<string, string> = {
  // 烤箱 → 核心 Oven
  dlc08_oven_02: "Oven",
  dlc09_oven: "Oven",
  oven_furnace_medieval: "Oven",
  oven_medieval: "Oven",
  workstation_furnace_01: "Oven",
  dlc13_workstation_cooker_01: "Oven",
  // 烧烤架 / 篝火 → 核心 Barbeque / Campfire
  workstation_barbeque_01: "Barbeque",
  workstation_campfire_01: "Campfire",
  // 煎锅 → 核心 FryPan
  dlc02_utensil_frying_pan: "FryPan",
  dlc05_utensil_frying_pan: "FryPan",
  dlc07_utensil_frying_pan_01: "FryPan",
  dlc08_utensil_frying_pan: "FryPan",
  dlc09_utensil_frying_pan: "FryPan",
  // 汤锅 → 核心 Pot
  dlc03_utensil_pot: "Pot",
  dlc07_utensil_pot_01: "Pot",
  dlc08_utensil_pot_01: "Pot",
  dlc09_utensil_pot: "Pot",
  // 炸篮 → 核心 FrierBasket
  dlc08_frierbasket: "FrierBasket",
  // 烤盘 / 火锅大锅 / 地炉（主版本无 dlc 前缀）
  dlc09_utensil_roasting_tray: "utensil_roasting_tray",
  web_utensil_dlc10_large_pot_01: "web_utensil_large_pot_01",
  // 可移动火锅 → 大火锅（食材配置/锅具管理等按同族处理）
  web_dlc10_cooking_region_floorburner: "web_cooking_region_floorburner",
  // 烤串签 / 烤盘 / 烤叉 → 核心 Skewer / GriddlePan / ToastingFork
  utensil_skewer_01: "Skewer",
  utensil_griddlepan: "GriddlePan",
  utensil_toasting_fork_01: "ToastingFork",
  // 搅拌碗 → 核心 MixerBowl
  dlc02_utensil_mixer: "MixerBowl",
  dlc03_utensil_mixer: "MixerBowl",
  dlc05_utensil_mixer: "MixerBowl",
  dlc07_utensil_mixer_01: "MixerBowl",
  dlc08_utensil_mixer_01: "MixerBowl",
  dlc09_utensil_mixer: "MixerBowl",
  dlc13_utensil_mixer_01: "MixerBowl",
  // 搅拌杯 → 核心 BlenderCup；搅拌机 → 核心 Mixer / Blender
  utensil_blender_01: "BlenderCup",
  workstation_blender_01: "Blender",
  workstation_mixer_01: "Mixer",
  workstation_mixer_02: "Mixer",
  workstation_mixer_03: "Mixer",
  dlc08_workstation_mixer: "Mixer",
  dlc10_workstation_mixer: "Mixer",
  dlc13_workstation_mixer_01: "Mixer",
  // 水槽 → 核心 Sink；马克杯水槽（核心版为默认，dlc09 为换皮）
  dlc13_workstation_sink_01_wood: "Sink",
  workstation_sink_01_summer: "Sink",
  dlc09_workstation_sink_mug_01_wood: "workstation_sink_mug_01_wood",
  // 洗餐盘水槽（纯 DLC 家族：普通 Sink 洗不了餐盘，不与 Sink 合并；三款皮肤合一）
  dlc08_workstation_02_tray_sink_circus: "dlc08_workstation_01_tray_sink_circus",
  dlc08_workstation_03_tray_sink_circus: "dlc08_workstation_01_tray_sink_circus",
  // 垃圾桶 → 核心 Bin
  dlc13_workstation_bin_01: "Bin",
  // 回收台 / 上菜台（马克杯回收台默认核心版，dlc09 为换皮）
  dlc13_workstation_plate_return: "PlateReturn",
  dlc09_workstation_mug_return_winter: "workstation_mug_return",
  workstation_glass_return_01: "GlassReturn",
  dlc11_workstation_glass_return_01: "GlassReturn",
  dlc13_workstation_plate_station: "ServingStation",
  // 玻璃杯家族 → 核心 CleanGlassStack / Glass
  cleanglassstack: "CleanGlassStack",
  dlc11_cleanglassstack: "CleanGlassStack",
  equipment_glass_01: "Glass",
  dlc11_equipment_glass_01: "Glass",
  // 脏玻璃杯堆（纯 DLC 家族 → 最高 dlc11）
  dirtyglassstack: "dlc11_dirtyglassstack",
  // 马克杯家族（默认用核心/非 DLC 版：cleanmugstack / equipment_mug_01 /
  // dirtymugstack / dirtymug 真实 prefab 为 bundle210/dlc03，dlc09 为换皮变体）
  dlc09_cleanmugstack: "cleanmugstack",
  dlc09_equipment_mug_01: "equipment_mug_01",
  dlc09_dirtymugstack: "dirtymugstack",
  dlc09_dirtymug: "dirtymug",
  // 工具 → 核心 FireExtinguisher / WaterGun / Bellows
  utensil_fire_extinguisher_02: "FireExtinguisher",
  dlc08_utensil_fire_extinguisher: "FireExtinguisher",
  utensil_water_gun_01: "WaterGun",
  utensil_bellows_01: "Bellows",
  // 大勺（主版本无 dlc 前缀）；奶油喷罐不合并（两款分别喷 dlc03/dlc09 奶油，
  // 与酱料机同理属「dlc 各自一套输出内容」，且 dlc03 版 m_OrderPrefab 原版即空、
  // 由 LayoutEditorIngredientSprayPatch 在 Play 期补齐）
  utensil_dlc10_big_ol_spoon: "utensil_big_ol_spoon",
  // 可推动物件（装饰层）
  web_dlc10_pushable_object: "web_utensil_large_pot_01_pushable",
};

/** 把一个 prefab id 归一化为其家族基础 id（变体→基础；非变体→自身）。 */
export function variantBaseId(id: string): string {
  return VARIANT_TO_BASE[id] ?? id;
}

function dlcOrder(id: string): number {
  const m = /^dlc(\d+)_/.exec(id ?? "");
  return m ? parseInt(m[1], 10) || 0 : 0;
}

/** 家族成员（基础版在前，其余按 DLC 序号升序）：以当前 catalog 为准，
 *  common03 条目可能被内容更新移除，缺谁少谁都能工作。 */
export function variantFamilyItems(id: string): CatalogItem[] {
  const base = variantBaseId(id);
  const members: CatalogItem[] = [];
  const baseCat = S.catalogById.get(base);
  if (baseCat) members.push(baseCat);
  for (const it of S.catalogById.values()) {
    if (it.id !== base && variantBaseId(it.id) === base) members.push(it);
  }
  members.sort((a, b) => {
    if (a.id === base) return -1;
    if (b.id === base) return 1;
    return dlcOrder(a.id) - dlcOrder(b.id) || a.id.localeCompare(b.id);
  });
  return members;
}

/** 右键菜单的「皮肤变体」区块（家族只有 1 个成员时返回空）。 */
export function itemVariantHtml(item: EditorItem): string {
  const pid = prefabIdFromPath(item.prefabAssetPath);
  if (!pid) return "";
  const fam = variantFamilyItems(pid);
  if (fam.length <= 1) return "";
  const base = variantBaseId(pid);
  const opts = fam
    .map((c) => {
      const isDefault = c.id === base;
      return `<option value="${escHtml(c.guid)}" ${c.guid === item.prefabGuid ? "selected" : ""}>${escHtml(tidyCatalogNameZh(c.nameZh, c.id))} · ${escHtml(c.id)}${isDefault ? "（默认）" : ""}</option>`;
    })
    .join("");
  return `<div class="ctx-stub"><div class="ctx-stub-title">皮肤变体（同功能换肤）</div>
    <label class="ctx-stub-row">皮肤 <select id="ctx-variant" class="ctx-input">${opts}</select></label>
    <div class="ctx-stub-row" style="font-size:11px;color:#8a909a">切换后原物品被同位置替换（参数保留），写回后生效</div></div>`;
}

export function wireItemVariant(item: EditorItem): void {
  const sel = document.getElementById("ctx-variant") as HTMLSelectElement | null;
  if (!sel) return;
  sel.addEventListener("change", () => {
    if (sel.value === item.prefabGuid) return;
    const cat = S.catalogByGuid.get(sel.value);
    if (!cat) return;
    switchItemVariant(item, cat);
  });
}

/** 切换皮肤：删旧建新（同位置/旋转/缩放），迁移兼容 stub 参数，
 *  重映射所有 instanceId 引用（开关/按钮/事件组/动画组/传送门/回收台绑定）。
 *  写回时旧场景实例不在文档中会被 RemoveUnmatchedSceneItems 删除，新实例走 CreateInstance。 */
export function switchItemVariant(item: EditorItem, cat: CatalogItem): void {
  const idx = S.items.indexOf(item);
  if (idx < 0) return;
  pushHistory();
  const oldId = item.instanceId;
  const wasSelected = S.selectedKeys.has(item._editorKey);
  const kind = stubKindOf(item);
  const newId = `new:${cat.guid}:${uuid()}`;
  const next: EditorItem = {
    instanceId: newId,
    _editorKey: newEditorKey(),
    hierarchyPath: newId,
    prefabGuid: cat.guid,
    prefabAssetPath: cat.assetPath,
    parentPath: cat.defaultParent,
    displayName: cat.id,
    localPosition: { ...item.localPosition },
    localRotationX: item.localRotationX,
    localRotationY: item.localRotationY,
    localRotationZ: item.localRotationZ,
    colliderCenter: item.colliderCenter ? { ...item.colliderCenter } : undefined,
    localScale: item.localScale ? { ...item.localScale } : undefined,
    footprint: cat.footprint,
    walkable: item.walkable,
    _wx: item._wx,
    _wz: item._wz,
    _parentWx: item._parentWx,
    _parentWz: item._parentWz,
  };
  if (item.stubKind) next.stubKind = item.stubKind;
  // 同族 stubKind 一致，参数迁移；指向旧皮肤 SO 的覆盖字段不迁移。
  // 锅具参数只在目标皮肤仍是锅具容器时迁移（否则真实 prefab 无 IngredientContainer，
  // 宿主 PseudoPrefabCookingUtensil.Setup 会 NRE）。
  if (kind === "CookingUtensil" && item.cookingUtensil && STUB_KIND_BY_PREFAB_ID[cat.id] === "CookingUtensil") {
    next.cookingUtensil = { ...item.cookingUtensil };
  } else if (kind === "CleanPlateStack" && item.cleanPlateStack) {
    next.cleanPlateStack = { plateCount: item.cleanPlateStack.plateCount };
  } else if ((kind === "PlateReturn" || kind === "GlassReturn") && item.plateReturn) {
    next.plateReturn = { ...item.plateReturn };
  }

  S.items[idx] = next;
  remapInstanceRefs(oldId, next.instanceId);
  S.paramLabels.delete(oldId);
  S.paramColors.delete(oldId);
  if (S.expandedMemberId === oldId) S.expandedMemberId = null;
  S.dirty = true;
  if (wasSelected) setSelection([next._editorKey]);
  hideContextMenu();
  draw();
  setStatus(`已切换皮肤为 ${tidyCatalogNameZh(cat.nameZh, cat.id)}（写回后生效）`);
}

/** 把所有文档级/物品级引用中的旧 instanceId 改写为新 id。 */
function remapInstanceRefs(oldId: string, newId: string): void {
  const map = (x: string) => (x === oldId ? newId : x);
  for (const l of S.switchLinks) {
    if (l.switchId === oldId) l.switchId = newId;
    if (l.targetId === oldId) l.targetId = newId;
  }
  for (const l of S.buttonLinks) {
    if (l.sourceId === oldId) l.sourceId = newId;
  }
  for (const l of S.buttonEvents) {
    if (l.sourceId === oldId) l.sourceId = newId;
    for (const g of l.groups) {
      for (const ev of g.events) {
        if (ev.targetId === oldId) ev.targetId = newId;
      }
    }
  }
  for (const mg of S.animControls) {
    mg.itemInstanceIds = mg.itemInstanceIds.map(map);
    if (mg.memberStatic) {
      for (const m of mg.memberStatic) m.instanceId = map(m.instanceId);
    }
    if (mg.memberGroups) {
      for (const g of mg.memberGroups) g.memberInstanceIds = g.memberInstanceIds.map(map);
    }
  }
  for (const it of S.items) {
    if (it.teleportal?.exitPortalInstanceId === oldId) it.teleportal.exitPortalInstanceId = newId;
    if (it.terminal?.pilotableObjectInstanceId === oldId) it.terminal.pilotableObjectInstanceId = newId;
    if (it.servingStation) {
      if (it.servingStation.plateReturnInstanceId === oldId) it.servingStation.plateReturnInstanceId = newId;
      if (it.servingStation.plateReturnInstanceIds) {
        it.servingStation.plateReturnInstanceIds = it.servingStation.plateReturnInstanceIds.map(map);
      }
    }
  }
}
