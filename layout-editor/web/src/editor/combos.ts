import { CELL, S } from "./state";
import type { ComboDef, EditorItem } from "./state";
import { catalogItemById } from "./catalog";
import { addFromCatalog } from "./items";
import { setServingReturnOfType } from "./servingLinks";
import type { ServingReturnKind } from "./servingLinks";
import { pushHistory } from "./historyOps";
import { setSelection } from "./selection";
import { setStatus } from "./status";
import { draw } from "./render";
import { escHtml, prefabIdFromPath } from "./coords";

/** 上菜台 → 回收台（脏盘/脏杯/马克杯/餐盘）自动绑定。 */
function linkServing(kind: ServingReturnKind) {
  return (items: EditorItem[]) => {
    setServingReturnOfType(items[0], items[1].instanceId, kind);
  };
}

/**
 * 开关 → 目标（断头台/果汁机/酱料机），写文档级 switchLinks。
 * 触发消息命名为 switch_{目标prefabId}_{N}（N = 该开关已有链接数 + 1），
 * ingredientIds 非空时按 id 解析 guid 写入机器 soArray（多选列表，开关循环切换）。
 */
function linkSwitch(ingredientIds?: string[]) {
  return (items: EditorItem[]) => {
    const target = items[0];
    const sw = items[1];
    const guids = (ingredientIds ?? [])
      .map((id) => S.ingredientsCache.find((i) => i.id === id)?.guid)
      .filter((g): g is string => !!g);
    if (guids.length) {
      target.stubKind = "Dispenser";
      target.dispenser = { spawnerItemPrefabGuid: guids[0] };
      target.soArray = { pseudoPrefabGuids: guids };
    }
    const targetPrefabId = prefabIdFromPath(target.prefabAssetPath) ?? "item";
    const n = S.switchLinks.filter((l) => l.switchId === sw.instanceId).length + 1;
    S.switchLinks.push({
      switchId: sw.instanceId,
      targetId: target.instanceId,
      trigger: `switch_${targetPrefabId}_${n}`,
    });
  };
}

/** 大炮 + 大炮开关（1:1）：星形发射按钮联动大炮，触发消息 Launch
 *  （对应 ServerCannon.m_launchTrigger，Play 期补丁同步）。 */
function linkCannonSwitch(items: EditorItem[]): void {
  const cannon = items[0];
  const sw = items[1];
  sw.stubKind = "CannonSwitch";
  if (!sw.switchStub) sw.switchStub = {};
  sw.switchStub.startEnabled = true;
  S.switchLinks.push({ switchId: sw.instanceId, targetId: cannon.instanceId, trigger: "Launch" });
}

/** 大炮 + 摇杆（多路控制终端）+ 大炮开关：终端绑定大炮（瞄准权归终端玩家，
 *  炮内玩家不再控角度），发射按钮联动大炮（trigger: Launch，1:1）。 */
function linkCannonTerminal(items: EditorItem[]): void {
  const cannon = items[0];
  const terminal = items[1];
  const sw = items[2];
  cannon.stubKind = "Cannon";
  terminal.stubKind = "Terminal";
  terminal.terminal = { pilotableObjectInstanceId: cannon.instanceId };
  sw.stubKind = "CannonSwitch";
  if (!sw.switchStub) sw.switchStub = {};
  sw.switchStub.startEnabled = true;
  S.switchLinks.push({ switchId: sw.instanceId, targetId: cannon.instanceId, trigger: "Launch" });
}

/** 两个传送门互为出口（双向传送）。 */
function linkTeleportalPair(items: EditorItem[]): void {
  const [a, b] = items;
  if (a.teleportal) a.teleportal.exitPortalInstanceId = b.instanceId;
  if (b.teleportal) b.teleportal.exitPortalInstanceId = a.instanceId;
}

/**
 * 联合组合表（palette「联合组合」分类）。
 * 部件偏移按格数（1 格 = 1.2m）：上菜台 2×1 → 搭档 dx=3；断头台 2×1 为中心 pivot
 * （磁吸落半格奇偶位，根在 0.6 mod 1.2）→ 搭档须用半格偏移 dx=2.5 才落整格；
 * 其余 1×1 → dx=2。
 * 新增组合只需在表里加一条（id 用目录物品 id，缺失/未放开时卡片自动置灰）。
 */
export const COMBOS: ComboDef[] = [
  {
    id: "serving_plate",
    nameZh: "上菜台 + 脏盘台",
    hint: "自动绑定：上菜台 → 脏盘台",
    parts: [
      { id: "ServingStation", dx: 0, dz: 0 },
      { id: "PlateReturn", dx: 3, dz: 0 },
    ],
    link: linkServing("plate"),
  },
  {
    id: "serving_glass",
    nameZh: "上菜台 + 脏杯台",
    hint: "自动绑定：上菜台 → 脏杯台",
    parts: [
      { id: "ServingStation", dx: 0, dz: 0 },
      { id: "GlassReturn", dx: 3, dz: 0 },
    ],
    link: linkServing("glass"),
  },
  {
    id: "drink_switch",
    nameZh: "饮料机 + 开关",
    hint: "自动联动：默认饮料 饮料1+饮料2+饮料3，按下开关循环切换（switch_dlc08_drink_machine_N）",
    parts: [
      { id: "dlc08_drink_machine", dx: 0, dz: 0 },
      { id: "Switch", dx: 2, dz: 0 },
    ],
    link: linkSwitch(["drink01", "drink02", "drink03"]),
  },
  {
    id: "drink_switch_icecream",
    nameZh: "汽水饮料机 + 开关",
    hint: "自动联动：默认饮料 橙味汽水+沙士汽水，按下开关循环切换（switch_dlc11_drink_dispenser_N）",
    parts: [
      { id: "dlc11_drink_dispenser", dx: 0, dz: 0 },
      { id: "Switch", dx: 2, dz: 0 },
    ],
    link: linkSwitch(["orangesoda", "rootbeer"]),
  },
  {
    id: "condiment_switch",
    nameZh: "酱料机 + 开关",
    hint: "自动联动：默认酱料 芥末酱+番茄酱，按下开关循环切换（switch_dlc08_condiment_dispenser_N）",
    parts: [
      { id: "dlc08_condiment_dispenser", dx: 0, dz: 0 },
      { id: "Switch", dx: 2, dz: 0 },
    ],
    link: linkSwitch(["mustard", "ketchup"]),
  },
  {
    id: "guillotine_switch",
    nameZh: "断头台 + 开关",
    hint: "自动联动：按下开关触发断头台落刀（switch_workstation_guillotine_01_N）",
    parts: [
      { id: "workstation_guillotine_01", dx: 0, dz: 0 },
      { id: "Switch", dx: 2.5, dz: 0 },
    ],
    link: linkSwitch(),
  },
  {
    id: "cannon_switch",
    nameZh: "大炮 + 大炮开关",
    hint: "自动联动：星形发射按钮按下 → 大炮发射（trigger: Launch，1:1）",
    parts: [
      { id: "dlc08_cannon", dx: 0, dz: 0 },
      { id: "p_dlc08_button_cannon", dx: 3, dz: 0 },
    ],
    link: linkCannonSwitch,
  },
  {
    id: "cannon_terminal_switch",
    nameZh: "大炮 + 摇杆 + 发射按钮",
    hint: "自动联动：多路控制终端绑定大炮（终端玩家遥控瞄准，炮内玩家不控角度），星形按钮按下 → 发射（Launch）",
    parts: [
      { id: "dlc08_cannon", dx: 0, dz: 0 },
      { id: "MultiControlTerminal", dx: 3, dz: 0 },
      { id: "p_dlc08_button_cannon", dx: 5, dz: 0 },
    ],
    link: linkCannonTerminal,
  },
  {
    id: "teleportal_pair",
    nameZh: "传送门 × 2（互配）",
    hint: "自动配对：互为出口，双向传送",
    parts: [
      { id: "Teleportal", dx: 0, dz: 0 },
      { id: "Teleportal", dx: 2, dz: 0 },
    ],
    link: linkTeleportalPair,
  },
];

export function comboById(id: string): ComboDef | undefined {
  return COMBOS.find((c) => c.id === id);
}

/** 组合不可用原因（部件缺失）；null = 可用。 */
export function comboDisabledReason(def: ComboDef): string | null {
  for (const p of def.parts) {
    const cat = catalogItemById(p.id);
    if (!cat) return `目录中缺少物品：${p.id}`;
  }
  return null;
}

/** 放置组合：所有部件一次落位（单次撤销），全部成功后自动完成联动配置。 */
export function addCombo(def: ComboDef, wx: number, wz: number): void {
  pushHistory();
  const placed: EditorItem[] = [];
  for (const p of def.parts) {
    const cat = catalogItemById(p.id);
    if (!cat) continue;
    const it = addFromCatalog(cat, wx + p.dx * CELL, wz + p.dz * CELL, false);
    if (it) placed.push(it);
  }
  if (placed.length === def.parts.length) {
    def.link(placed);
    setSelection(placed.map((i) => i._editorKey));
    S.dirty = true;
    draw();
    setStatus(`已放置组合「${def.nameZh}」并自动完成联动（写回后生效）`);
  } else if (placed.length > 0) {
    setSelection(placed.map((i) => i._editorKey));
    draw();
    setStatus(`组合「${def.nameZh}」部分物品未能放置（与玩家重叠？），请手动配置联动`, false);
  }
}

/** palette 的「联合组合」分组（核心层置顶）；无匹配时返回 null。 */
export function buildComboPaletteGroup(filter: string): HTMLElement | null {
  const q = filter.trim().toLowerCase();
  const list = COMBOS.filter(
    (c) =>
      !q ||
      c.nameZh.toLowerCase().includes(q) ||
      c.id.toLowerCase().includes(q) ||
      c.hint.toLowerCase().includes(q)
  );
  if (list.length === 0) return null;

  const details = document.createElement("details");
  details.className = "cat-group";
  details.dataset.tier = "core";
  details.open = true;
  const summary = document.createElement("summary");
  summary.textContent = `联合组合 (${list.length})`;
  summary.title = "一次放置多个物品，自动完成联动配置（绑定/配对/触发），无需手动配置";
  details.appendChild(summary);

  const grid = document.createElement("div");
  grid.className = "palette-grid";
  details.appendChild(grid);

  for (const def of list) {
    const row = document.createElement("div");
    row.className = "palette-card palette-cat-combo";
    const disabledReason = comboDisabledReason(def);
    const disabled = disabledReason !== null;
    if (disabled) {
      row.classList.add("palette-card-disabled");
      row.title = disabledReason ?? "";
    }
    row.draggable = !disabled;
    row.dataset.combo = def.id;
    const partNames = def.parts.map((p) => catalogItemById(p.id)?.nameZh || p.id).join(" + ");
    const badge = disabled
      ? ` <span class="disabled-badge" title="${escHtml(disabledReason ?? "")}">⛔ 禁用</span>`
      : "";
    row.innerHTML = `<div class="zh">${escHtml(def.nameZh)} <span class="variant-badge">组合</span>${badge}</div><div class="id">${escHtml(partNames)}</div><div class="sub">${escHtml(def.hint)}</div>`;
    if (!disabled) {
      row.addEventListener("dragstart", (e) => {
        S.dragCombo = def;
        e.dataTransfer?.setData("text/plain", `combo:${def.id}`);
      });
      row.addEventListener("dragend", () => {
        S.dragCombo = null;
      });
    }
    grid.appendChild(row);
  }
  return details;
}
