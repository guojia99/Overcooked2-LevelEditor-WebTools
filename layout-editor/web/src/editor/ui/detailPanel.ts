import {
  S,
  CELL,
  EditorItem
} from "../state";
import type { CatalogItem } from "../../types";
import { dom } from "../dom";
import {
  normalizeRot,
  prefabIdFromPath,
  resolveFootprint,
  itemUniformScale,
  setItemUniformScale,
  stepDisplayDecimals,
  escHtml
} from "../coords";
import { itemLabel } from "../labels";
import { counterAppearanceOptions, catalogItemForGuidOrPath } from "../catalog";
import {
  isSurfaceItem,
  surfaceKindLabelZh
} from "../../floorColors";
import { tidyCatalogNameZh } from "../../displayLabels";
import { ingredientNameZh, ingredientOptionLabel } from "../../ingredientLabels";
import {
  findStackHost,
  hostRuleLabelZh
} from "../../stacking";
import { pushHistory } from "../historyOps";
import { draw } from "../render";
import { updateFloorBar } from "../floorPalette";
import {
  isConveyorItem,
  isTeleportalItem
} from "../renderItems";
import {
  isGlassReturnItem,
  computeReturnLabels,
  servingPlateReturn,
  servingGlassReturn,
  servingStationsForReturn
} from "../servingLinks";
import {
  stubKindOf,
  defaultUtensilCapacity
} from "../stubControls";
import {
  BURNER_FIRE_MODES,
  PORTAL_COLOR_NAMES
} from "./constants";

export function positionFloating(el: HTMLElement, clientX: number, clientY: number) {
  const margin = 8;
  let left = clientX + margin;
  let top = clientY + margin;
  el.style.left = `${left}px`;
  el.style.top = `${top}px`;
  requestAnimationFrame(() => {
    const rect = el.getBoundingClientRect();
    if (rect.right > window.innerWidth - margin) {
      left = Math.max(margin, clientX - rect.width - margin);
      el.style.left = `${left}px`;
    }
    if (rect.bottom > window.innerHeight - margin) {
      top = Math.max(margin, clientY - rect.height - margin);
      el.style.top = `${top}px`;
    }
  });
}

export function positionDetail(clientX: number, clientY: number) {
  positionFloating(dom.detailEl, clientX, clientY);
}

export function stackDetailHtml(item: EditorItem, cat: CatalogItem | undefined): string {
  if (!cat?.stack) return "";
  const host = findStackHost(item, cat, item._wx, item._wz, S.items, S.catalogByGuid);
  const ruleLabel = hostRuleLabelZh(cat.stack.hostRule);
  if (host) {
    const hostCat = S.catalogByGuid.get(host.prefabGuid);
    const hostLabel = itemLabel(host);
    const hostId = prefabIdFromPath(host.prefabAssetPath) || hostCat?.id || "—";
    return `<dt>叠放</dt><dd>堆叠于「${hostLabel}」（${hostId}）之上；规则：${ruleLabel}；本地高度 Y=${cat.stack.y}</dd>`;
  }
  return `<dt>叠放</dt><dd>应堆叠在${ruleLabel}上（当前未对齐到有效载体）；本地高度 Y=${cat.stack.y}</dd>`;
}

export function extraStubDetailHtml(item: EditorItem): string {
  if (isConveyorItem(item)) {
    const sp = item.conveyor?.conveySpeed ?? 0.5;
    return `<dt>传送带</dt><dd>速度 ${sp.toFixed(2)}（${sp < 0 ? "反向" : "正向"}，箭头指示传送方向）</dd>`;
  }
  if (isTeleportalItem(item)) {
    const exitId = item.teleportal?.exitPortalInstanceId;
    const partner = exitId ? S.items.find((i) => i.instanceId === exitId) : undefined;
    const myLabel = S.teleportalLabels.get(item.instanceId) ?? "?";
    const colorName = PORTAL_COLOR_NAMES[item.teleportal?.portalColor ?? 0] ?? String(item.teleportal?.portalColor ?? 0);
    const pairTxt = partner
      ? `已绑定 →「${itemLabel(partner)}」（同组 ${myLabel}）`
      : exitId
        ? "绑定目标不在当前场景"
        : "未绑定";
    const ds = item.teleportal?.doubleSided ? " · 双向" : "";
    return `<dt>传送门</dt><dd>${pairTxt} · 颜色 ${colorName}${ds}</dd>`;
  }
  switch (stubKindOf(item)) {
    case "AttachingFoodSpawner": {
      const fs = item.foodSpawner ?? {};
      const n = (fs.attachmentPrefabGuids ?? []).length;
      return `<dt>食材生成器</dt><dd>${fs.spawnInOrder !== false ? "按顺序" : "随机"}生成 · ${fs.triggerAtStart !== false ? "开局触发" : "不开局触发"} · 间隔 ${fs.triggerTime ?? 5}s · ${n} 种食材（右键直接修改）</dd>`;
    }
    case "CookingUtensil": {
      const cu = item.cookingUtensil ?? {};
      const allowed = (cu.allowedIngredientGuids ?? []).length;
      return `<dt>锅具</dt><dd>最多 ${cu.capacity ?? defaultUtensilCapacity(item)} 个食材 · 额外食材：${allowed > 0 ? `${allowed} 种` : "无（处理所有主线食材）"}（右键直接修改）</dd>`;
    }
    case "Travelator":
      return `<dt>移动地板</dt><dd>速度 ${(item.travelator?.speed ?? 2.5).toFixed(2)}（右键直接修改）</dd>`;
    case "Flamethrower":
      return `<dt>喷火器</dt><dd>烹饪速率 ${(item.flamethrower?.cookingRate ?? 4).toFixed(1)}（右键直接修改）</dd>`;
    case "CleanPlateStack":
      return `<dt>盘子堆</dt><dd>${item.cleanPlateStack?.plateCount ?? 5} 个盘子（右键直接修改）</dd>`;
    case "Burner": {
      const b = item.burner ?? {};
      return `<dt>火焰喷射器</dt><dd>${BURNER_FIRE_MODES[b.fireMode ?? 1]} · 空中时间 ${b.airTime ?? 2}s${b.randomTargetOrder ? " · 随机目标" : ""}${b.hideVisual ? " · 隐藏模型" : ""}（右键直接修改）</dd>`;
    }
    case "Cannon": {
      const free = item.cannon?.freeRotation;
      return `<dt>大炮</dt><dd>${free ? "360° 自由旋转" : "固定小角度（±45°）"}（右键直接修改）</dd>`;
    }
    case "Player": {
      const pid = item.player?.playerID ?? 11;
      return `<dt>玩家</dt><dd>${pid === 11 ? "自动（按加入顺序）" : `玩家 ${pid + 1}`} · 固定出生点，仅可拖动位置</dd>`;
    }
    case "ServingStation": {
      const labels = computeReturnLabels();
      const byInst = new Map(S.items.map((i) => [i.instanceId, i]));
      const nameOf = (id: string | undefined) =>
        id ? (byInst.get(id) ? `${labels.get(id) ?? "?"}（${itemLabel(byInst.get(id)!)}）` : "（回收台不在场景）") : "未绑定";
      return `<dt>上菜口</dt><dd>脏盘台：${nameOf(servingPlateReturn(item))} · 脏杯台：${nameOf(servingGlassReturn(item))}（右键直接修改）</dd>`;
    }
    case "PlateReturn":
    case "GlassReturn": {
      const isGlass = isGlassReturnItem(item);
      const typeZh = isGlass ? "脏杯台" : "脏盘台";
      const bound = servingStationsForReturn(item.instanceId);
      const txt = bound.length
        ? `被 ${bound.length} 个上菜台共用：${bound.map((s) => `上菜台${S.paramLabels.get(s.instanceId) ?? "?"}`).join("、")}`
        : "未被任何上菜台绑定";
      const cleanTxt = item.plateReturn?.returnClean ? ` · 直接返回干净${isGlass ? "杯子" : "盘子"}` : "";
      return `<dt>${typeZh}</dt><dd>${txt}${cleanTxt}（右键直接修改，绑定在上菜台右键菜单里设置）</dd>`;
    }
    case "Switch":
    case "CannonSwitch": {
      const sw = item.switchStub ?? {};
      const matLookup = new Map(S.switchMaterialsCache.map((m) => [m.guid, m]));
      const activeName = matLookup.get(sw.activeMaterialGuid ?? "")?.nameZh ?? "默认";
      const inactiveName = matLookup.get(sw.inactiveMaterialGuid ?? "")?.nameZh ?? "默认";
      return `<dt>${stubKindOf(item) === "CannonSwitch" ? "大炮开关" : "开关"}</dt><dd>初始状态：${sw.startEnabled !== false ? "开启" : "关闭"} · 未按下外观：${activeName} · 按下外观：${inactiveName}（右键直接修改）</dd>`;
    }
    case "PressureSwitch": {
      const ps = item.pressureSwitch ?? {};
      const matLookup = new Map(S.switchMaterialsCache.map((m) => [m.guid, m]));
      const occName = matLookup.get(ps.occupiedMaterialGuid ?? "")?.nameZh ?? "默认";
      const unoccName = matLookup.get(ps.unoccupiedMaterialGuid ?? "")?.nameZh ?? "默认";
      return `<dt>压力开关</dt><dd>按下外观：${occName} · 松开外观：${unoccName}（右键直接修改）</dd>`;
    }
    case "Terminal": {
      const targetId = item.terminal?.pilotableObjectInstanceId;
      const target = targetId ? S.items.find((i) => i.instanceId === targetId) : undefined;
      const name = target ? itemLabel(target) : (targetId ? "不在当前场景" : "未绑定");
      return `<dt>控制终端</dt><dd>控制目标：${name}（右键直接修改）</dd>`;
    }
    default: {
      let html = "";
      const appearOptions = counterAppearanceOptions(item);
      if (appearOptions.length) {
        const nameLookup = new Map(appearOptions.map((o) => [o.guid, o]));
        const curName = nameLookup.get(item.pseudoPrefabGuid ?? "")?.nameZh ?? "默认外观";
        html += `<dt>外观</dt><dd>${escHtml(curName)}（右键直接修改）</dd>`;
      }
      return html;
    }
  }
}

export function showSurfaceItemDetail(item: EditorItem, clientX: number, clientY: number) {
  const cat = S.catalogByGuid.get(item.prefabGuid);
  const fp = resolveFootprint(item);
  const uScale = itemUniformScale(item);
  dom.detailEl.innerHTML = `
    <h3>${surfaceKindLabelZh(cat?.surfaceKind)} · ${itemLabel(item)}</h3>
    <dl>
      <dt>类型</dt><dd>${surfaceKindLabelZh(cat?.surfaceKind)}（地板层 prefab）</dd>
      <dt>占地</dt><dd>${fp.cellsX} × ${fp.cellsZ} 格</dd>
      <dt>坐标</dt><dd>x ${item._wx.toFixed(2)}, z ${item._wz.toFixed(2)}</dd>
      <dt>旋转</dt><dd>${normalizeRot(item.localRotationY)}°</dd>
      <dt>缩放</dt><dd id="si-scale-val">${uScale.toFixed(2)}×</dd>
    </dl>
    <div class="floor-edit-row">
      <label>缩放 <input type="number" min="0.5" step="0.1" id="si-scale" value="${uScale.toFixed(2)}" /></label>
      <span class="muted" style="align-self:center;font-size:11px">即时生效</span>
    </div>
    <p class="close-hint">右键菜单可微移/旋转 · R/Shift+R 旋转90° · Del 删除 · Esc 关闭</p>
  `;
  dom.detailEl.classList.remove("hidden");
  positionDetail(clientX, clientY);

  const scaleInput = document.getElementById("si-scale") as HTMLInputElement;
  let scalePushed = false;
  const applyScale = () => {
    const v = parseFloat(scaleInput.value);
    if (!isFinite(v) || v < 0.5) return;
    if (!scalePushed) {
      pushHistory();
      scalePushed = true;
    }
    setItemUniformScale(item, v);
    const el = document.getElementById("si-scale-val");
    if (el) el.textContent = `${v.toFixed(2)}×`;
    draw();
    updateFloorBar();
  };
  scaleInput.addEventListener("input", applyScale);
  scaleInput.addEventListener("change", applyScale);
}

/** 酱料机/饮料机：显示 soArray 多选列表（开关循环切换）；其他食材箱显示单项。 */
function dispenserDetailHtml(item: EditorItem): string {
  if (item.stubKind !== "Dispenser") return "";
  const pid = prefabIdFromPath(item.prefabAssetPath) ?? "";
  const isSpecial =
    pid === "dlc08_drink_machine" ||
    pid === "dlc11_drink_dispenser" ||
    pid === "dlc08_condiment_dispenser" ||
    pid === "dlc11_condiment_dispenser";
  if (isSpecial && item.soArray?.pseudoPrefabGuids?.length) {
    const names = item.soArray.pseudoPrefabGuids.map((g) => {
      const ing = S.ingredientsCache.find((i) => i.guid === g);
      return ing ? escHtml(ingredientOptionLabel(ing)) : "?";
    });
    return `<dt>输出（开关循环）</dt><dd>${names.join(" → ")}</dd>`;
  }
  return `<dt>食材</dt><dd>${ingredientNameZh(S.ingredientsCache, item.dispenser?.spawnerItemPrefabGuid)}</dd>`;
}

export function showDetail(item: EditorItem, clientX: number, clientY: number) {
  const cat = catalogItemForGuidOrPath(item.prefabGuid, item.prefabAssetPath);
  const fp = resolveFootprint(item);
  const id = prefabIdFromPath(item.prefabAssetPath) || cat?.id || "—";

  dom.detailEl.innerHTML = `
    <h3>${itemLabel(item)}</h3>
    <dl>
      <dt>Prefab ID</dt><dd>${id}</dd>
      <dt>中文名</dt><dd>${cat?.nameZh ? tidyCatalogNameZh(cat.nameZh, cat.id) : "—"}</dd>
      <dt>资源路径</dt><dd>${item.prefabAssetPath || "—"}</dd>
      <dt>层级路径</dt><dd>${item.hierarchyPath}</dd>
      <dt>父节点</dt><dd>${item.parentPath || "—"}</dd>
      <dt>占地</dt><dd>${fp.cellsX} × ${fp.cellsZ} 格 (${(fp.cellsX * CELL).toFixed(1)} × ${(fp.cellsZ * CELL).toFixed(1)} m)</dd>
      <dt>本地坐标</dt><dd>x ${item.localPosition.x.toFixed(stepDisplayDecimals(S.freeSnapStep))}, y ${item.localPosition.y.toFixed(stepDisplayDecimals(S.freeSnapStep))}, z ${item.localPosition.z.toFixed(stepDisplayDecimals(S.freeSnapStep))}</dd>
      <dt>旋转 Y</dt><dd>${normalizeRot(item.localRotationY)}°</dd>
      ${isSurfaceItem(cat) ? `<dt>缩放</dt><dd>${itemUniformScale(item).toFixed(2)}×（右键菜单可调整大小）</dd>` : ""}
      <dt>分类</dt><dd>${isSurfaceItem(cat) ? surfaceKindLabelZh(cat?.surfaceKind) + "（地板层）" : cat?.layoutTier === "decor" ? "装饰道具" : "核心玩法"} · ${cat?.nameZh ? tidyCatalogNameZh(cat.nameZh, cat.id) : cat?.category ?? "—"}</dd>
      ${stackDetailHtml(item, cat)}
      ${dispenserDetailHtml(item)}
      ${extraStubDetailHtml(item)}
    </dl>
    <p class="close-hint">Esc 关闭</p>
  `;

  const margin = 8;
  let left = clientX + margin;
  let top = clientY + margin;
  dom.detailEl.classList.remove("hidden");
  dom.detailEl.style.left = `${left}px`;
  dom.detailEl.style.top = `${top}px`;

  requestAnimationFrame(() => {
    const rect = dom.detailEl.getBoundingClientRect();
    if (rect.right > window.innerWidth - margin) {
      left = Math.max(margin, clientX - rect.width - margin);
      dom.detailEl.style.left = `${left}px`;
    }
    if (rect.bottom > window.innerHeight - margin) {
      top = Math.max(margin, clientY - rect.height - margin);
      dom.detailEl.style.top = `${top}px`;
    }
  });
}
