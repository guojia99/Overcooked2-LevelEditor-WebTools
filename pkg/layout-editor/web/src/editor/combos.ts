import { CELL, S } from "./state";
import type { ComboDef, EditorItem } from "./state";
import type { AnimGroup } from "../types";
import { catalogItemById, ingredientGuidById } from "./catalog";
import { addFromCatalog } from "./items";
import { setServingReturnOfType } from "./servingLinks";
import type { ServingReturnKind } from "./servingLinks";
import { pushHistory } from "./historyOps";
import { setSelection } from "./selection";
import { setStatus } from "./status";
import { draw } from "./render";
import { escHtml, prefabIdFromPath, uuid } from "./coords";

/** 上菜台 → 回收台（脏盘/脏杯/马克杯/餐盘）自动绑定。 */
function linkServing(kind: ServingReturnKind) {
  return (items: EditorItem[]) => {
    setServingReturnOfType(items[0], items[1].instanceId, kind);
  };
}

/**
 * 开关 → 目标（断头台/果汁机/酱料机），写文档级 switchLinks。
 * 触发消息用目标机器的原生触发名（断头台 Chop、饮料机/酱料机 Next——机器包装
 *  prefab 自带 TriggerOnObject 翻译层，自定义名真机不响应），ingredientIds 非空时
 *  按 id 解析 guid 写入机器 soArray（多选列表，开关循环切换）。
 *
 * ⚠ id→guid 必须走 `ingredientGuidById`（别名感知）而不是 `find(i => i.id === id)`：
 *  食材目录 id 会随资产改名 / prefabName 归一而变（2026-09-10 起 DLC08_Drink01→drink01），
 *  精确匹配会静默解析不到 → 组合放下去不带任何默认饮料/酱料（2026-09-15 修）。
 */
function linkSwitch(ingredientIds?: string[]) {
  return (items: EditorItem[]) => {
    const target = items[0];
    const sw = items[1];
    const guids: string[] = [];
    for (const id of ingredientIds ?? []) {
      const g = ingredientGuidById(id);
      // 去重：清单同时写了「目录 id + common03 正式版 id」两代写法，两者解析到同一
      // guid，去重后顺序即开关循环顺序（首次出现为准）
      if (g && !guids.includes(g)) guids.push(g);
    }
    if ((ingredientIds ?? []).length > 0 && guids.length === 0) {
      console.warn(
        `[combo] 默认输出食材一个都没解析到（${(ingredientIds ?? []).join(", ")}）——` +
          "机器将使用内置列表。多半是食材目录 id 变了，请更新 combos/recipeKnowledge 的清单。"
      );
    }
    if (guids.length) {
      target.stubKind = "Dispenser";
      target.dispenser = { spawnerItemPrefabGuid: guids[0] };
      target.soArray = { pseudoPrefabGuids: guids };
    }
    const targetPrefabId = prefabIdFromPath(target.prefabAssetPath) ?? "item";
    const trigger =
      targetPrefabId === "workstation_guillotine_01" ? "Chop" : "Next";
    S.switchLinks.push({
      switchId: sw.instanceId,
      targetId: target.instanceId,
      trigger,
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

/** 传送带站 + 按钮（开关动画组 v2）：
 *  一个「节点环」开关动画组（triggerMode=button + advanceMode=press）：两个 rotate
 *  节点 +180°/−180°，按钮每按一次只推进一个节点 —— 第一次按翻转传送方向、再按转回。
 *  旋转由动画组烘焙的原生 Animator 驱动传送带伪根（组员），站体 child 随动；
 *  ConveyorDirectionSync（buttonControlled tag）只做旋转后的相邻接收器刷新。
 *  同一传送带站绝不能拆成两个动画组（烘焙 reparent 互斥）——往返翻转必须在
 *  同一组内用节点环表达。 */
function linkConveyorSwitch(items: EditorItem[]): void {
  const conveyor = items[0];
  const sw = items[1];
  conveyor.conveyor = { ...(conveyor.conveyor ?? {}), buttonControlled: true };
  sw.stubKind = "Switch";
  if (!sw.switchStub) sw.switchStub = {};
  sw.switchStub.startEnabled = true;

  // 组名去重（重复套用组合时追加序号）。
  let name = `传送带开关 ${S.animControls.length + 1}`;
  if (S.animControls.some((g) => g.displayName === name)) {
    let n = 1;
    while (S.animControls.some((g) => g.displayName === `传送带开关 ${S.animControls.length + 1} (${n})`)) n++;
    name = `传送带开关 ${S.animControls.length + 1} (${n})`;
  }

  const group: AnimGroup = {
    id: uuid(),
    displayName: name,
    groupKind: "members",
    triggerMode: "button",
    advanceMode: "press",
    itemInstanceIds: [conveyor.instanceId],
    floorInstanceIds: [],
    objectInstanceIds: [],
    memberOffsets: [],
    memberStatic: [],
    memberGroups: [],
    startDelay: 0,
    loop: false,
    loopDelay: 2,
    waitForFinished: true,
    waypoints: [],
    events: [
      {
        id: uuid(),
        type: "rotate",
        triggerName: "FlipOn",
        delay: 0,
        startTime: 0,
        rotateDegrees: 180,
        rotateDirection: "cw",
        rotateSeconds: 0.4,
      },
      {
        id: uuid(),
        type: "rotate",
        triggerName: "FlipOff",
        delay: 0,
        startTime: 1,
        rotateDegrees: 180,
        rotateDirection: "ccw",
        rotateSeconds: 0.4,
      },
    ],
  };
  S.animControls.push(group);

  // 同一开关旧的传送带 ButtonLink / v1 双组表示 / v3 Animate 直连一并清理。
  const legacyNames = new Set<string>();
  for (const g of S.animControls) {
    if (
      g.id !== group.id &&
      g.itemInstanceIds.includes(conveyor.instanceId) &&
      (g.displayName.startsWith("传送带开关 ") || g.triggerMode === "button")
    ) {
      legacyNames.add(g.displayName);
    }
  }
  if (legacyNames.size > 0) {
    S.animControls = S.animControls.filter((g) => !legacyNames.has(g.displayName));
    S.buttonLinks = S.buttonLinks.filter(
      (link) => !link.groupNames.some((n) => legacyNames.has(n))
    );
  }
  S.switchLinks = S.switchLinks.filter(
    (l) => !(l.targetId === conveyor.instanceId && l.trigger === "Animate")
  );
  S.buttonLinks = S.buttonLinks.filter(
    (l) => !(l.sourceId === sw.instanceId && (l.groupNames.some((n) => legacyNames.has(n)) || l.groupNames.length === 0))
  );

  S.buttonLinks.push({
    id: uuid(),
    sourceId: sw.instanceId,
    groupNames: [group.displayName],
    sequenceMode: "loop",
    lockUntilFinished: true,
  });
}

/** 传送带阵 ×4 + 双按钮（共控·同按）：4 个**独立**动画组（每站一组、+90°/−90°
 *  两个逐节点，方便单独编辑/控制），同按联动让一次按压同时启动全部 4 组（各自
 *  推进一个节点 = 4 站同时旋转 90°，再按全部转回）；两个按钮接线到同一联动
 *  （sharedSourceIds），任一按压都推进。每站带 buttonControlled 监视器（旋转停稳
 *  后刷新投递目标）。 */
function linkConveyorArraySwitch(items: EditorItem[]): void {
  const conveyors = items.slice(0, 4);
  const swA = items[4];
  const swB = items[5];
  for (const c of conveyors) {
    c.conveyor = { ...(c.conveyor ?? {}), buttonControlled: true };
  }
  for (const sw of [swA, swB]) {
    sw.stubKind = "Switch";
    if (!sw.switchStub) sw.switchStub = {};
    sw.switchStub.startEnabled = true;
  }

  // 组名基号去重（重复套用组合时）。
  let base = S.animControls.length + 1;
  const taken = (k: number) => S.animControls.some((g) => g.displayName.startsWith(`传送带阵 ${k} `));
  while (taken(base)) base++;

  const groupNames: string[] = [];
  conveyors.forEach((c, i) => {
    const name = `传送带阵 ${base} · ${i + 1}`;
    const group: AnimGroup = {
      id: uuid(),
      displayName: name,
      groupKind: "members",
      triggerMode: "button",
      advanceMode: "press",
      itemInstanceIds: [c.instanceId],
      floorInstanceIds: [],
      objectInstanceIds: [],
      memberOffsets: [],
      memberStatic: [],
      memberGroups: [],
      startDelay: 0,
      loop: false,
      loopDelay: 2,
      waitForFinished: true,
      waypoints: [],
      events: [
        {
          id: uuid(),
          type: "rotate",
          triggerName: "FlipOn",
          delay: 0,
          startTime: 0,
          rotateDegrees: 90,
          rotateDirection: "cw",
          rotateSeconds: 0.4,
        },
        {
          id: uuid(),
          type: "rotate",
          triggerName: "FlipOff",
          delay: 0,
          startTime: 1,
          rotateDegrees: 90,
          rotateDirection: "ccw",
          rotateSeconds: 0.4,
        },
      ],
    };
    S.animControls.push(group);
    groupNames.push(name);
  });

  // 重复套用组合时清掉旧的传送带阵组（成员与本次任一传送带重叠即视为旧组）。
  const newIds = new Set(conveyors.map((c) => c.instanceId));
  const legacyNames = new Set<string>();
  for (const g of S.animControls) {
    if (groupNames.includes(g.displayName)) continue;
    if (g.displayName.startsWith("传送带阵") && g.itemInstanceIds.some((id) => newIds.has(id))) {
      legacyNames.add(g.displayName);
    }
  }
  if (legacyNames.size > 0) {
    S.animControls = S.animControls.filter((g) => !legacyNames.has(g.displayName));
    S.buttonLinks = S.buttonLinks.filter((l) => !l.groupNames.some((n2) => legacyNames.has(n2)));
  }
  S.buttonLinks = S.buttonLinks.filter((l) => l.sourceId !== swA.instanceId && l.sourceId !== swB.instanceId);

  S.buttonLinks.push({
    id: uuid(),
    sourceId: swA.instanceId,
    sharedSourceIds: [swB.instanceId],
    groupNames,
    sequenceMode: "loop",
    lockUntilFinished: true,
    simultaneous: true,
  });
}

/** 传送门配对：默认【单向 A→B】——a 是入口，b 仅作为出口（回指 a 占位，
 *  运行时由 CustomStub.TeleportalExitOnly 把 b 的出口清回 null）。
 *  需要双向在入口门参数里勾「双向传送」即可。 */
function linkTeleportalPair(items: EditorItem[]): void {
  const [a, b] = items;
  if (a.teleportal) {
    a.teleportal.exitPortalInstanceId = b.instanceId;
    a.teleportal.exitOnly = false;
  }
  if (b.teleportal) {
    b.teleportal.exitPortalInstanceId = a.instanceId;
    b.teleportal.exitOnly = true;
  }
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
    // 清单写两代 id（目录现行小写 id 在前决定循环顺序，common03 正式版 id 兜底，
    // 解析到同一 guid 时自动去重）
    link: linkSwitch(["drink01", "drink02", "drink03", "DLC08_Drink01", "DLC08_Drink02", "DLC08_Drink03"]),
  },
  {
    id: "drink_switch_icecream",
    nameZh: "汽水饮料机 + 开关",
    hint: "自动联动：默认饮料 橙味汽水+沙士汽水，按下开关循环切换（switch_dlc11_drink_dispenser_N）",
    parts: [
      { id: "dlc11_drink_dispenser", dx: 0, dz: 0 },
      { id: "Switch", dx: 2, dz: 0 },
    ],
    link: linkSwitch(["orangesoda", "rootbeer", "DLC11_OrangeSoda", "DLC11_RootBeer"]),
  },
  {
    id: "condiment_switch",
    nameZh: "酱料机 + 开关",
    hint: "自动联动：默认酱料 芥末酱+番茄酱，按下开关循环切换（switch_dlc08_condiment_dispenser_N）",
    parts: [
      { id: "dlc08_condiment_dispenser", dx: 0, dz: 0 },
      { id: "Switch", dx: 2, dz: 0 },
    ],
    link: linkSwitch(["mustard", "ketchup", "DLC08_Mustard", "DLC08_Ketchup"]),
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
    id: "conveyor_switch",
    nameZh: "传送带站 + 按钮",
    hint: "开关动画组（节点环）：按一次旋转 180° 切换传送方向、再按转回；绝不自动播放，动画期间按钮锁定。在右侧「🔘 触发源」可查看联动",
    parts: [
      { id: "ConveyorStation", dx: 0, dz: 0 },
      { id: "Switch", dx: 2, dz: 0 },
    ],
    link: linkConveyorSwitch,
  },
  {
    id: "conveyor_array_switch",
    nameZh: "传送带阵 ×4 + 双按钮",
    hint: "4 个独立动画组（每站一组，可单独编辑）+ 同按联动：按任一按钮，4 站同时旋转 90°、再按全部转回（每站投递方向自动跟随）",
    parts: [
      { id: "ConveyorStation", dx: 0, dz: 0 },
      { id: "ConveyorStation", dx: 1, dz: 0 },
      { id: "ConveyorStation", dx: 2, dz: 0 },
      { id: "ConveyorStation", dx: 3, dz: 0 },
      { id: "Switch", dx: 1, dz: 2 },
      { id: "Switch", dx: 2, dz: 2 },
    ],
    link: linkConveyorArraySwitch,
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
    nameZh: "传送门 × 2（配对）",
    hint: "自动配对：左=入口 → 右=出口（单向）；右键入口门勾「双向传送」可改双向",
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
