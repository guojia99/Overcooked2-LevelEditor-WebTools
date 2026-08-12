import {
  S,
  EditorItem
} from "./state";
import { dom } from "./dom";
import {
  prefabIdFromPath,
  escHtml
} from "./coords";
import { itemLabel } from "./labels";
import { pushHistory } from "./historyOps";
import { draw } from "./render";
import { setStatus } from "./status";
import { hideContextMenu } from "./ui/overlay";
import {
  openFoodSpawnerEditor,
  openIngredientMultiPicker
} from "../modals";
import { ingredientOptionLabel } from "../ingredientLabels";
import {
  counterTypeOfItem,
  counterAppearanceOptions
} from "./catalog";
import {
  isGlassReturnItem,
  plateReturns,
  glassReturns,
  computeReturnLabels,
  servingPlateReturn,
  servingGlassReturn,
  setServingReturnOfType,
  servingStationsForReturn
} from "./servingLinks";
import { teleportals } from "./renderItems";
import {
  computeParamLabels,
  computeTeleportalLabels
} from "./renderItems";
import {
  PORTAL_COLOR_NAMES,
  BURNER_FIRE_MODES
} from "./ui/constants";

export const STUB_KIND_BY_PREFAB_ID: Record<string, string> = {
  Dispenser: "Dispenser",
  Backpack: "Dispenser",
  AttachingFoodSpawner: "AttachingFoodSpawner",
  ConveyorStation: "Conveyor",
  Teleportal: "Teleportal",
  Pot: "CookingUtensil",
  FryPan: "CookingUtensil",
  Steamer: "CookingUtensil",
  FrierBasket: "CookingUtensil",
  MixerBowl: "CookingUtensil",
  ToastingFork: "CookingUtensil",
  GriddlePan: "CookingUtensil",
  Skewer: "CookingUtensil",
  Blender: "CookingUtensil",
  BlenderCup: "CookingUtensil",
  CleanPlateStack: "CleanPlateStack",
  CleanGlassStack: "CleanPlateStack",
  Travelator: "Travelator",
  Flamethrower: "Flamethrower",
  Burner: "Burner",
  Player: "Player",
  ServingStation: "ServingStation",
  PlateReturn: "PlateReturn",
  GlassReturn: "GlassReturn",
  Switch: "Switch",
  PressureSwitch: "PressureSwitch",
  MultiControlTerminal: "Terminal",
};

export function stubKindOf(item: EditorItem): string {
  if (item.stubKind) return item.stubKind;
  const prefabId = prefabIdFromPath(item.prefabAssetPath);
  return STUB_KIND_BY_PREFAB_ID[prefabId] ?? "";
}

export function isCollisionItem(it: EditorItem): boolean {
  return stubKindOf(it) === "Collision";
}

export function defaultUtensilCapacity(item: EditorItem): number {
  const prefabId = prefabIdFromPath(item.prefabAssetPath);
  if (prefabId === "MixerBowl" || prefabId === "GriddlePan" || prefabId === "BlenderCup") return 4;
  if (prefabId === "Skewer") return 3;
  return 1;
}

export function counterAppearanceHtml(item: EditorItem): string {
  const options = counterAppearanceOptions(item);
  if (!options.length) return "";
  const cur = item.pseudoPrefabGuid ?? "";
  const nameLookup = new Map(options.map((o) => [o.guid, o]));
  const curName = nameLookup.get(cur)?.nameZh ?? (cur ? "未知外观" : "默认外观");
  const opts = ['<option value="">— 默认外观 —</option>']
    .concat(
      options.map(
        (o) => `<option value="${o.guid}" ${o.guid === cur ? "selected" : ""}>${escHtml(o.nameZh || o.id)}</option>`
      )
    )
    .join("");
  const ct = counterTypeOfItem(item);
  const typeName = S.counterAppearances?.typeNames[ct!] ?? ct ?? "桌台";
  return `<div class="ctx-stub"><div class="ctx-stub-title">${typeName}外观</div>
    <label class="ctx-stub-row">外观 <select id="ctx-appear" class="ctx-input">${opts}</select></label>
    <div class="ctx-stub-row" style="font-size:11px;color:#8a909a">当前：${escHtml(curName)}</div></div>`;
}

export function switchMaterialHtml(item: EditorItem): string {
  if (!S.switchMaterialsCache.length) return "";
  const mats = S.switchMaterialsCache;
  const sw = item.switchStub ?? {};
  const activeOpts = ['<option value="">— 默认 —</option>']
    .concat(
      mats.map(
        (m) => `<option value="${m.guid}" ${(sw.activeMaterialGuid ?? "") === m.guid ? "selected" : ""}>${escHtml(m.nameZh || m.id)}</option>`
      )
    )
    .join("");
  const inactiveOpts = ['<option value="">— 默认 —</option>']
    .concat(
      mats.map(
        (m) => `<option value="${m.guid}" ${(sw.inactiveMaterialGuid ?? "") === m.guid ? "selected" : ""}>${escHtml(m.nameZh || m.id)}</option>`
      )
    )
    .join("");
  return `<label class="ctx-stub-row">未按下外观 <select id="ctx-sw-active" class="ctx-input">${activeOpts}</select></label>
    <label class="ctx-stub-row">按下外观 <select id="ctx-sw-inactive" class="ctx-input">${inactiveOpts}</select></label>`;
}

export function pressureSwitchMaterialHtml(item: EditorItem): string {
  if (!S.switchMaterialsCache.length) return "";
  const mats = S.switchMaterialsCache;
  const ps = item.pressureSwitch ?? {};
  const occOpts = ['<option value="">— 默认 —</option>']
    .concat(
      mats.map(
        (m) => `<option value="${m.guid}" ${(ps.occupiedMaterialGuid ?? "") === m.guid ? "selected" : ""}>${escHtml(m.nameZh || m.id)}</option>`
      )
    )
    .join("");
  const unoccOpts = ['<option value="">— 默认 —</option>']
    .concat(
      mats.map(
        (m) => `<option value="${m.guid}" ${(ps.unoccupiedMaterialGuid ?? "") === m.guid ? "selected" : ""}>${escHtml(m.nameZh || m.id)}</option>`
      )
    )
    .join("");
  return `<label class="ctx-stub-row">按下外观 <select id="ctx-ps-occ" class="ctx-input">${occOpts}</select></label>
    <label class="ctx-stub-row">松开外观 <select id="ctx-ps-unocc" class="ctx-input">${unoccOpts}</select></label>`;
}

export function stubControlsHtml(item: EditorItem): string {
  const kind = stubKindOf(item);
  computeParamLabels(); // refresh per-type sequence numbers so menus match the canvas
  switch (kind) {
    case "Dispenser": {
      const cur = item.dispenser?.spawnerItemPrefabGuid ?? "";
      const opts = ['<option value="">— 未设置 —</option>']
        .concat(
          S.ingredientsCache.map(
            (ing) =>
              `<option value="${ing.guid}" ${ing.guid === cur ? "selected" : ""}>${escHtml(ingredientOptionLabel(ing))}</option>`
          )
        )
        .join("");
      return `<div class="ctx-stub"><div class="ctx-stub-title">食材箱参数</div>
        <label class="ctx-stub-row">食材 <select id="ctx-stub-ing" class="ctx-input">${opts}</select></label></div>`;
    }
    case "AttachingFoodSpawner": {
      const fs = item.foodSpawner ?? {};
      return `<div class="ctx-stub"><div class="ctx-stub-title">食材生成器参数</div>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-fs-order" ${fs.spawnInOrder !== false ? "checked" : ""}/> 按顺序生成</label>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-fs-start" ${fs.triggerAtStart !== false ? "checked" : ""}/> 开局触发</label>
        <label class="ctx-stub-row">触发间隔 <input type="number" id="ctx-fs-time" class="ctx-input" step="0.5" min="0" value="${fs.triggerTime ?? 5}"/> 秒</label>
        <button type="button" class="ctx-btn" id="ctx-fs-ings">食材列表…</button></div>`;
    }
    case "CookingUtensil": {
      const cu = item.cookingUtensil ?? {};
      const allowed = (cu.allowedIngredientGuids ?? []).length;
      return `<div class="ctx-stub"><div class="ctx-stub-title">厨具参数</div>
        <label class="ctx-stub-row">最多食材数 <input type="number" id="ctx-cu-cap" class="ctx-input" min="0" step="1" value="${cu.capacity ?? defaultUtensilCapacity(item)}"/></label>
        <button type="button" class="ctx-btn" id="ctx-cu-ings">额外食材 (${allowed > 0 ? `${allowed} 种` : "无 · 处理所有主线食材"})…</button></div>`;
    }
    case "Conveyor": {
      const sp = item.conveyor?.conveySpeed ?? 0.5;
      return `<div class="ctx-stub"><div class="ctx-stub-title">传送带参数</div>
        <label class="ctx-stub-row">速度 <input type="number" id="ctx-cv-speed" class="ctx-input" step="0.1" value="${sp}"/>（负值反向）</label></div>`;
    }
    case "Teleportal": {
      const tp = item.teleportal ?? {};
      S.teleportalLabels = computeTeleportalLabels();
      const colorOpts = PORTAL_COLOR_NAMES.map(
        (n, i) => `<option value="${i}" ${(tp.portalColor ?? 0) === i ? "selected" : ""}>${n}</option>`
      ).join("");
      const others = teleportals().filter((t) => t._editorKey !== item._editorKey);
      const exitOpts = ['<option value="">— 未绑定 —</option>']
        .concat(
          others.map(
            (t) =>
              `<option value="${t.instanceId}" ${tp.exitPortalInstanceId === t.instanceId ? "selected" : ""}>传送门 ${S.teleportalLabels.get(t.instanceId) ?? "?"}（${escHtml(itemLabel(t))}）</option>`
          )
        )
        .join("");
      return `<div class="ctx-stub"><div class="ctx-stub-title">传送门参数</div>
        <label class="ctx-stub-row">颜色 <select id="ctx-tp-color" class="ctx-input">${colorOpts}</select></label>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-tp-ds" ${tp.doubleSided ? "checked" : ""}/> 双向</label>
        <label class="ctx-stub-row">出口 <select id="ctx-tp-exit" class="ctx-input">${exitOpts}</select></label></div>`;
    }
    case "Travelator": {
      const sp = item.travelator?.speed ?? 2.5;
      return `<div class="ctx-stub"><div class="ctx-stub-title">移动地板参数</div>
        <label class="ctx-stub-row">速度 <input type="number" id="ctx-tv-speed" class="ctx-input" step="0.1" min="0" value="${sp}"/></label></div>`;
    }
    case "Flamethrower": {
      const rate = item.flamethrower?.cookingRate ?? 4;
      return `<div class="ctx-stub"><div class="ctx-stub-title">喷火器参数</div>
        <label class="ctx-stub-row">烹饪速率 <input type="number" id="ctx-ft-rate" class="ctx-input" step="0.5" min="0" value="${rate}"/></label></div>`;
    }
    case "CleanPlateStack": {
      const ps = item.cleanPlateStack ?? {};
      const title = prefabIdFromPath(item.prefabAssetPath) === "CleanGlassStack" ? "杯堆参数" : "盘子堆参数";
      return `<div class="ctx-stub"><div class="ctx-stub-title">${title}</div>
        <label class="ctx-stub-row">数量 <input type="number" id="ctx-ps-count" class="ctx-input" min="0" step="1" value="${ps.plateCount ?? 5}"/></label></div>`;
    }
    case "Burner": {
      const b = item.burner ?? {};
      const modeOpts = BURNER_FIRE_MODES.map(
        (n, i) => `<option value="${i}" ${(b.fireMode ?? 1) === i ? "selected" : ""}>${n}</option>`
      ).join("");
      return `<div class="ctx-stub"><div class="ctx-stub-title">火焰喷射器参数</div>
        <label class="ctx-stub-row">开火模式 <select id="ctx-bn-mode" class="ctx-input">${modeOpts}</select></label>
        <label class="ctx-stub-row">空中时间 <input type="number" id="ctx-bn-air" class="ctx-input" step="0.1" min="0" value="${b.airTime ?? 2}"/> 秒</label>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-bn-rand" ${b.randomTargetOrder ? "checked" : ""}/> 随机目标顺序</label>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-bn-hide" ${b.hideVisual ? "checked" : ""}/> 隐藏模型</label></div>`;
    }
    case "Switch": {
      const sw = item.switchStub ?? {};
      const matHtml = switchMaterialHtml(item);
      return `<div class="ctx-stub"><div class="ctx-stub-title">开关参数</div>
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-sw-start" ${sw.startEnabled !== false ? "checked" : ""}/> 初始开启</label>
        ${matHtml}</div>`;
    }
    case "PressureSwitch": {
      const matHtml = pressureSwitchMaterialHtml(item);
      return `<div class="ctx-stub"><div class="ctx-stub-title">压力开关参数</div>
        ${matHtml || '<div class="ctx-stub-row">此物件无用户可配置参数，配置内置于预制件中</div>'}</div>`;
    }
    case "Terminal": {
      const t = item.terminal ?? {};
      const targetId = t.pilotableObjectInstanceId;
      let targetOpts = ['<option value="">— 未绑定 —</option>'];
      for (const i of S.items) {
        const kind = stubKindOf(i);
        if (kind === "Player") continue;
        if (!i.instanceId) continue;
        targetOpts.push(`<option value="${i.instanceId}" ${targetId === i.instanceId ? "selected" : ""}>${escHtml(itemLabel(i))}</option>`);
      }
      return `<div class="ctx-stub"><div class="ctx-stub-title">控制终端参数</div>
        <label class="ctx-stub-row">控制目标 <select id="ctx-tm-target" class="ctx-input">${targetOpts.join("")}</select></label>
        <div class="ctx-stub-row" style="font-size:11px;color:#8a909a">选择一个场景物件作为控制终端的目标</div></div>`;
    }
    case "Player": {
      return `<div class="ctx-stub"><div class="ctx-stub-title">玩家</div>
        <div class="ctx-stub-row">玩家固定在场景中，仅可拖动调整位置</div></div>`;
    }
    case "ServingStation": {
      const myNum = S.paramLabels.get(item.instanceId) ?? "?";
      const labels = computeReturnLabels();
      const plateId = servingPlateReturn(item);
      const glassId = servingGlassReturn(item);
      const plateOpts = ['<option value="">— 未绑定 —</option>']
        .concat(
          plateReturns().map(
            (r) =>
              `<option value="${r.instanceId}" ${plateId === r.instanceId ? "selected" : ""}>${labels.get(r.instanceId) ?? "?"}（${escHtml(itemLabel(r))}）</option>`
          )
        )
        .join("");
      const glassOpts = ['<option value="">— 未绑定 —</option>']
        .concat(
          glassReturns().map(
            (r) =>
              `<option value="${r.instanceId}" ${glassId === r.instanceId ? "selected" : ""}>${labels.get(r.instanceId) ?? "?"}（${escHtml(itemLabel(r))}）</option>`
          )
        )
        .join("");
      return `<div class="ctx-stub"><div class="ctx-stub-title">上菜台${myNum} · 绑定回收台（各至多一个）</div>
        <label class="ctx-stub-row">脏盘台 <select id="ctx-ss-plate" class="ctx-input">${plateOpts}</select></label>
        <label class="ctx-stub-row">脏杯台 <select id="ctx-ss-glass" class="ctx-input">${glassOpts}</select></label>
        <div class="ctx-stub-row" style="font-size:11px;color:#8a909a">一个上菜台最多绑一个脏盘台 + 一个脏杯台；一个回收台可被多个上菜台共用</div></div>`;
    }
    case "PlateReturn":
    case "GlassReturn": {
      const labels = computeReturnLabels();
      const isGlass = isGlassReturnItem(item);
      const typeZh = isGlass ? "脏杯台" : "脏盘台";
      const myNum = labels.get(item.instanceId) ?? "?";
      const bound = servingStationsForReturn(item.instanceId);
      const rows = bound
        .map((s) => {
          const sNum = S.paramLabels.get(s.instanceId) ?? "?";
          return `<div class="ctx-stub-row">· 上菜台${sNum}（${escHtml(itemLabel(s))}）</div>`;
        })
        .join("");
      return `<div class="ctx-stub"><div class="ctx-stub-title">${typeZh}${myNum} · 被上菜台绑定（可一对多）</div>
        ${rows || '<div class="ctx-stub-row">未被任何上菜台绑定</div>'}
        <label class="ctx-stub-row"><input type="checkbox" id="ctx-pr-clean" ${item.plateReturn?.returnClean ? "checked" : ""}/> 直接返回干净${isGlass ? "杯子" : "盘子"}（returnClean）</label>
        <div class="ctx-stub-row" style="font-size:11px;color:#8a909a">请在对应上菜台的右键菜单里设置绑定</div></div>`;
    }
    default:
      return "";
  }
}

export function wireStubControls(item: EditorItem) {
  const kind = stubKindOf(item);

  const num = (id: string): HTMLInputElement | null =>
    document.getElementById(id) as HTMLInputElement | null;

  if (kind) {
  switch (kind) {
    case "Dispenser": {
      num("ctx-stub-ing")?.addEventListener("change", (e) => {
        pushHistory();
        item.stubKind = "Dispenser";
        item.dispenser = { spawnerItemPrefabGuid: (e.target as HTMLSelectElement).value };
        draw();
        setStatus("已设置食材箱食材（写回后生效）");
      });
      break;
    }
    case "AttackingFoodSpawner": {
      const ensure = () => {
        item.stubKind = "AttackingFoodSpawner";
        if (!item.foodSpawner) item.foodSpawner = {};
        return item.foodSpawner;
      };
      num("ctx-fs-order")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().spawnInOrder = (e.target as HTMLInputElement).checked;
      });
      num("ctx-fs-start")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().triggerAtStart = (e.target as HTMLInputElement).checked;
      });
      num("ctx-fs-time")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (isFinite(v) && v >= 0) {
          pushHistory();
          ensure().triggerTime = v;
        }
      });
      document.getElementById("ctx-fs-ings")?.addEventListener("click", () => {
        ensure();
        hideContextMenu();
        openFoodSpawnerEditor(item, S.ingredientsCache, (patch) => {
          pushHistory();
          item.foodSpawner = patch;
          draw();
          setStatus("已更新食材生成器参数（写回后生效）");
        });
      });
      break;
    }
    case "CookingUtensil": {
      const ensure = () => {
        item.stubKind = "CookingUtensil";
        if (!item.cookingUtensil) item.cookingUtensil = {};
        if (item.cookingUtensil.capacity == null)
          item.cookingUtensil.capacity = defaultUtensilCapacity(item);
        return item.cookingUtensil;
      };
      num("ctx-cu-cap")?.addEventListener("change", (e) => {
        const v = parseInt((e.target as HTMLInputElement).value, 10);
        if (isFinite(v) && v >= 0) {
          pushHistory();
          ensure().capacity = v;
          setStatus(`锅具容量已设为 ${v}（写回后生效）`);
        }
      });
      document.getElementById("ctx-cu-ings")?.addEventListener("click", () => {
        ensure();
        hideContextMenu();
        openIngredientMultiPicker(
          "锅具 · 额外食材",
          "allowedIngredientSOs（不选 = 处理所有主线食材；选中的作为额外可煮食材）",
          S.ingredientsCache,
          item.cookingUtensil?.allowedIngredientGuids ?? [],
          (guids) => {
            pushHistory();
            ensure().allowedIngredientGuids = guids;
            draw();
            setStatus("已更新锅具额外食材（写回后生效）");
          },
          S.intermediatesCache
        );
      });
      break;
    }
    case "Conveyor": {
      num("ctx-cv-speed")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (!isFinite(v)) return;
        pushHistory();
        item.stubKind = "Conveyor";
        item.conveyor = { conveySpeed: v };
        draw();
        setStatus(`传送带速度已设为 ${v}（写回后生效）`);
      });
      break;
    }
    case "Teleportal": {
      const ensure = () => {
        item.stubKind = "Teleportal";
        if (!item.teleportal) item.teleportal = { exitPortalInstanceId: "", portalColor: 0, doubleSided: false };
        return item.teleportal;
      };
      num("ctx-tp-color")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().portalColor = parseInt((e.target as HTMLSelectElement).value, 10) || 0;
        draw();
      });
      num("ctx-tp-ds")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().doubleSided = (e.target as HTMLInputElement).checked;
        draw();
      });
      num("ctx-tp-exit")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().exitPortalInstanceId = (e.target as HTMLSelectElement).value;
        draw();
        setStatus("已更新传送门配对（写回后生效）");
      });
      break;
    }
    case "Travelator": {
      num("ctx-tv-speed")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (!isFinite(v) || v < 0) return;
        pushHistory();
        item.stubKind = "Travelator";
        item.travelator = { speed: v };
        setStatus(`移动地板速度已设为 ${v}（写回后生效）`);
      });
      break;
    }
    case "Flamethrower": {
      num("ctx-ft-rate")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (!isFinite(v) || v < 0) return;
        pushHistory();
        item.stubKind = "Flamethrower";
        item.flamethrower = { cookingRate: v };
        setStatus(`喷火器烹饪速率已设为 ${v}（写回后生效）`);
      });
      break;
    }
    case "CleanPlateStack": {
      num("ctx-ps-count")?.addEventListener("change", (e) => {
        const v = parseInt((e.target as HTMLInputElement).value, 10);
        if (!isFinite(v) || v < 0) return;
        pushHistory();
        item.stubKind = "CleanPlateStack";
        if (!item.cleanPlateStack) item.cleanPlateStack = {};
        item.cleanPlateStack.plateCount = v;
        setStatus(`盘子堆数量已设为 ${v}（写回后生效）`);
      });
      break;
    }
    case "Burner": {
      const ensure = () => {
        item.stubKind = "Burner";
        if (!item.burner) item.burner = {};
        if (item.burner.fireMode == null) item.burner.fireMode = 1;
        if (item.burner.airTime == null) item.burner.airTime = 2;
        if (item.burner.randomTargetOrder == null) item.burner.randomTargetOrder = false;
        if (item.burner.hideVisual == null) item.burner.hideVisual = false;
        return item.burner;
      };
      num("ctx-bn-mode")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().fireMode = parseInt((e.target as HTMLSelectElement).value, 10) || 0;
      });
      num("ctx-bn-air")?.addEventListener("change", (e) => {
        const v = parseFloat((e.target as HTMLInputElement).value);
        if (isFinite(v) && v >= 0) {
          pushHistory();
          ensure().airTime = v;
        }
      });
      num("ctx-bn-rand")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().randomTargetOrder = (e.target as HTMLInputElement).checked;
      });
      num("ctx-bn-hide")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().hideVisual = (e.target as HTMLInputElement).checked;
      });
      break;
    }
    case "Player": {
      break;
    }
    case "ServingStation": {
      num("ctx-ss-plate")?.addEventListener("change", (e) => {
        const id = (e.target as HTMLSelectElement).value;
        pushHistory();
        setServingReturnOfType(item, id || undefined, false);
        draw();
        setStatus("已更新脏盘台绑定（写回后生效）");
      });
      num("ctx-ss-glass")?.addEventListener("change", (e) => {
        const id = (e.target as HTMLSelectElement).value;
        pushHistory();
        setServingReturnOfType(item, id || undefined, true);
        draw();
        setStatus("已更新脏杯台绑定（写回后生效）");
      });
      break;
    }
    case "PlateReturn":
    case "GlassReturn": {
      num("ctx-pr-clean")?.addEventListener("change", (e) => {
        pushHistory();
        item.stubKind = isGlassReturnItem(item) ? "GlassReturn" : "PlateReturn";
        if (!item.plateReturn) item.plateReturn = {};
        item.plateReturn.returnClean = (e.target as HTMLInputElement).checked;
        draw();
        setStatus(`已${item.plateReturn.returnClean ? "开启" : "关闭"}直接返回干净${isGlassReturnItem(item) ? "杯子" : "盘子"}（写回后生效）`);
      });
      break;
    }
    case "Switch": {
      const ensure = () => {
        item.stubKind = "Switch";
        if (!item.switchStub) item.switchStub = {};
        return item.switchStub;
      };
      num("ctx-sw-start")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().startEnabled = (e.target as HTMLInputElement).checked;
        setStatus(`开关初始状态已设为 ${item.switchStub!.startEnabled ? "开启" : "关闭"}（写回后生效）`);
      });
      num("ctx-sw-active")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().activeMaterialGuid = (e.target as HTMLSelectElement).value || undefined;
        setStatus("已更新开关未按下外观（写回后生效）");
      });
      num("ctx-sw-inactive")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().inactiveMaterialGuid = (e.target as HTMLSelectElement).value || undefined;
        setStatus("已更新开关按下外观（写回后生效）");
      });
      break;
    }
    case "PressureSwitch": {
      const ensure = () => {
        item.stubKind = "PressureSwitch";
        if (!item.pressureSwitch) item.pressureSwitch = {};
        return item.pressureSwitch;
      };
      num("ctx-ps-occ")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().occupiedMaterialGuid = (e.target as HTMLSelectElement).value || undefined;
        setStatus("已更新压力开关按下外观（写回后生效）");
      });
      num("ctx-ps-unocc")?.addEventListener("change", (e) => {
        pushHistory();
        ensure().unoccupiedMaterialGuid = (e.target as HTMLSelectElement).value || undefined;
        setStatus("已更新压力开关松开外观（写回后生效）");
      });
      break;
    }
    case "Terminal": {
      num("ctx-tm-target")?.addEventListener("change", (e) => {
        pushHistory();
        item.stubKind = "Terminal";
        if (!item.terminal) item.terminal = {};
        item.terminal.pilotableObjectInstanceId = (e.target as HTMLSelectElement).value;
        draw();
        setStatus("已更新控制终端目标（写回后生效）");
      });
      break;
    }
  }
  }

  wireCounterAppearance(item);
}

export function wireCounterAppearance(item: EditorItem) {
  const sel = document.getElementById("ctx-appear") as HTMLSelectElement | null;
  if (!sel) return;
  sel.addEventListener("change", () => {
    pushHistory();
    item.pseudoPrefabGuid = sel.value || undefined;
    draw();
    const options = counterAppearanceOptions(item);
    const nameLookup = new Map(options.map((o) => [o.guid, o]));
    const name = nameLookup.get(item.pseudoPrefabGuid ?? "")?.nameZh ?? "默认外观";
    setStatus(`已更新外观为：${name}（写回后生效）`);
    // Re-show context menu to update the display
    dom.ctxMenuEl.classList.add("hidden");
  });
}
