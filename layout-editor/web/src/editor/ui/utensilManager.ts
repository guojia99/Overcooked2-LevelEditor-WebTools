import {
  S,
  EditorItem
} from "../state";
import {
  prefabIdFromPath,
  escHtml
} from "../coords";
import { itemLabel } from "../labels";
import { pushHistory } from "../historyOps";
import { draw } from "../render";
import { setStatus } from "../status";
import {
  openModal,
  closeModal
} from "../../modals";
import { openIngredientMultiPicker } from "../../modals";
import {
  stubKindOf,
  defaultUtensilCapacity,
  utensilCapacityOrFix,
  utensilTimingKind,
  utensilTimingInputsHtml,
  UTENSIL_TIME_KEYS,
  readUtensilTimeInput
} from "../stubControls";
import {
  computeUtensilIngredientFill,
  functionalBaseId
} from "../recipeKnowledge";
import {
  fetchRecipeCatalog,
  fetchLevelRecipes
} from "../../api";
import type { RecipeEntry } from "../../types";

export type UtensilIngredientFill = Map<string, { ings: string[]; intermediates: string[] }>;

/** 把 computeUtensilIngredientFill 的结果写进场景锅具的 allowedIngredientSOs。
 *  两个入口共用：锅具管理页「按菜谱自动填充」与菜谱弹窗「安装缺失」。
 *  ings 按 id→食材 guid、intermediates 按 id→菜谱 guid 解析；返回写入的锅具数。
 *  可移动火锅（pushable）只写食材配置、不挂 CookingUtensil stub（挂 stub 会触发
 *  宿主 Setup NRE，载体组装在 LayoutRuntimePushablePot）。 */
export function applyUtensilIngredientFill(
  fill: UtensilIngredientFill,
  ingGuid: Map<string, string>,
  recipeGuid: Map<string, string>
): number {
  const vesselOfItem = (it: EditorItem): string => {
    const id = S.catalogByGuid.get(it.prefabGuid ?? "")?.id ?? prefabIdFromPath(it.prefabAssetPath ?? "");
    return functionalBaseId(id ?? "");
  };
  let touched = 0;
  for (const it of S.items) {
    const isPushablePot = prefabIdFromPath(it.prefabAssetPath) === "web_utensil_large_pot_01_pushable";
    if (stubKindOf(it) !== "CookingUtensil" && !isPushablePot) continue;
    const f = fill.get(vesselOfItem(it));
    if (!f) continue;
    const add: string[] = [];
    for (const iid of f.ings) {
      const g = ingGuid.get(iid);
      if (g) add.push(g);
    }
    for (const iid of f.intermediates) {
      const g = recipeGuid.get(iid);
      if (g) add.push(g);
    }
    if (!add.length) continue;
    if (!isPushablePot) it.stubKind = "CookingUtensil";
    if (!it.cookingUtensil) it.cookingUtensil = {};
    it.cookingUtensil.capacity = utensilCapacityOrFix(it);
    it.cookingUtensil.allowedIngredientGuids = [...new Set(add)];
    touched++;
  }
  return touched;
}

export function openUtensilManager() {
  // 锅具 = CookingUtensil stub + 可移动火锅（含锅 child，有 IngredientContainer，
  //  食材配置同样有效，但 stubKind 保持空由后端载体组装，不挂 CookingUtensil stub）
  // 食材配置同样有效，但 stubKind 保持空由后端载体组装，不挂 CookingUtensil stub）
  const utensils = S.items.filter(
    (it) =>
      stubKindOf(it) === "CookingUtensil" ||
      prefabIdFromPath(it.prefabAssetPath) === "web_utensil_large_pot_01_pushable"
  );
  if (!utensils.length) {
    setStatus("当前关卡没有锅具", false);
    return;
  }

  const groups = new Map<string, EditorItem[]>();
  for (const it of utensils) {
    const pid = prefabIdFromPath(it.prefabAssetPath);
    const arr = groups.get(pid) ?? [];
    arr.push(it);
    groups.set(pid, arr);
  }
  const sortedGroups = [...groups.entries()].sort((a, b) => a[0].localeCompare(b[0]));

  const body = sortedGroups
    .map(([pid, arr]) => {
      arr.sort((a, b) => a._wz - b._wz || a._wx - b._wx);
      const rows = arr
        .map((it, idx) => {
          const cu = it.cookingUtensil ?? {};
          const cap = (cu.capacity ?? 0) > 0 ? cu.capacity! : defaultUtensilCapacity(it);
          const allowed = cu.allowedIngredientGuids ?? [];
          const allowedTxt = allowed.length > 0 ? `额外食材：${allowed.length} 种` : "额外食材：无（处理所有主线食材）";
          const dis = arr.length < 2 ? "disabled" : "";
          return `<div class="utm-row" data-key="${it._editorKey}">
            <span class="utm-name">${escHtml(itemLabel(it))}${idx + 1}</span>
            <label class="utm-cap-label">容量 <input type="number" class="utm-cap" data-key="${it._editorKey}" min="0" step="1" value="${cap}"/></label>
            ${utensilTimingInputsHtml(it, "", "utm")}
            <button type="button" class="modal-btn utm-ings" data-key="${it._editorKey}">${allowedTxt}…</button>
            <button type="button" class="modal-btn utm-sync" data-key="${it._editorKey}" ${dis}>同步给其他 ${arr.length - 1} 个</button>
          </div>`;
        })
        .join("");
      return `<div class="utm-group"><div class="utm-group-title">${escHtml(itemLabel(arr[0]))}（${escHtml(pid)}）× ${arr.length}</div>${rows}</div>`;
    })
    .join("");

  openModal(
    "锅具管理 · 参数同步",
    `<p class="modal-hint">可直接修改每个锅具的容量、时间与额外食材（不选额外食材时可处理所有主线食材，选中后可额外煮这些食材），或一键把它的参数同步给所有相同类型的锅具。时间留空 = 原版默认（输入框内灰色占位显示该锅具真实原版时间，煮糊/过混默认 2× 煮熟/混合）；特殊煮糊时间随关卡包分发。仅修改前端数据，写回 Unity 后生效。</p><div class="modal-scroll">${body}</div>`,
    `<button type="button" class="modal-btn primary" id="utm-auto-fill">🧺 按菜谱自动填充</button>
     <button type="button" class="modal-btn" data-cancel>关闭</button>`
  );
  document.querySelector(".modal-panel")?.classList.add("wide");

  const utensilByKey = (key: string | undefined) => S.items.find((i) => i._editorKey === key);
  const ensureUtensil = (it: EditorItem) => {
    it.stubKind = "CookingUtensil";
    if (!it.cookingUtensil) it.cookingUtensil = {};
    return it.cookingUtensil;
  };
  const reopen = () => {
    closeModal();
    openUtensilManager();
  };

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);

  // 按菜谱自动填充：读取当前关卡已选菜谱 → 数据驱动计算各锅具应装的食材
  // （汤料→汤锅、香肠→汤锅、洋葱→煎锅、面糊食材→搅拌碗、面糊节点→炸篮、
  //  搅拌类→搅拌杯，含 DLC 食材如 dlc07 土豆/西芹；Cooked 型中间产物按其自身
  //  烹饪步骤节点+叶生食材双填进终锅，如 EggSausage→早餐锅、FriedMeat→煎锅），
  //  按功能基础 id 匹配场景锅具（含 DLC 变体），容量默认 4；按菜谱自动填充覆盖写入食材列表。
  document.getElementById("utm-auto-fill")?.addEventListener("click", async () => {
    if (!S.scenePath) {
      setStatus("未选择场景，无法读取关卡菜谱", false);
      return;
    }
    let recipes: RecipeEntry[] | null = null;
    let guids: string[] = [];
    try {
      const [catalog, level] = await Promise.all([
        fetchRecipeCatalog(S.currentLevelSet),
        fetchLevelRecipes(S.scenePath),
      ]);
      recipes = catalog;
      guids = level?.recipeGuids ?? [];
    } catch (e) {
      setStatus(`读取关卡菜谱失败：${(e as Error).message}`, false);
      return;
    }
    const byGuid = new Map(recipes.map((r) => [r.guid, r]));
    const recs = guids.map((g) => byGuid.get(g)).filter((r): r is RecipeEntry => !!r);
    if (!recs.length) {
      setStatus("当前关卡未选择菜谱，先在「选择菜谱」里勾选", false);
      return;
    }
    S.intermediatesCache = recipes.filter((r) => r.intermediate || r.isCustom);

    const fill = computeUtensilIngredientFill(recs);
    if (!fill.size) {
      setStatus("所选菜谱无需锅具装填（沙拉/拼盘等组装类）", false);
      return;
    }
    const ingGuid = new Map(S.ingredientsCache.map((i) => [i.id, i.guid]));
    const recipeGuid = new Map(recipes.map((r) => [r.id, r.guid]));

    pushHistory();
    const touched = applyUtensilIngredientFill(fill, ingGuid, recipeGuid);
    draw();
    const parts = [...fill.entries()].map(([v, f]) => `${v}×${f.ings.length + f.intermediates.length}`);
    setStatus(
      touched
        ? `已按 ${recs.length} 道菜谱填充 ${touched} 个锅具（${parts.join("、")}；写回后生效）`
        : `场景中没有匹配的锅具（需要：${parts.join("、")}）`,
      touched > 0
    );
    reopen();
  });

  document.querySelectorAll<HTMLInputElement>(".utm-cap").forEach((input) => {
    input.addEventListener("change", () => {
      const it = utensilByKey(input.dataset.key);
      const v = parseInt(input.value, 10);
      if (!it || !isFinite(v) || v < 0) return;
      pushHistory();
      ensureUtensil(it).capacity = v;
      draw();
      setStatus(`${itemLabel(it)} 容量已设为 ${v}（写回后生效）`);
      reopen();
    });
  });

  // 时间参数（煮熟/煮糊 · 混合/过度混合，按锅具类型分组显示）：
  // 空串 = 清除回原版默认（糊/过混回退 2× 联动），数值 > 0 生效。
  // Unity JsonUtility 会把未配置序列化成 0——0 一律按未配置处理。
  document.querySelectorAll<HTMLInputElement>(".utm-time").forEach((input) => {
    input.addEventListener("change", () => {
      const row = input.closest(".utm-row") as HTMLElement | null;
      const it = utensilByKey(row?.dataset.key ?? input.dataset.key);
      if (!it) return;
      const key = UTENSIL_TIME_KEYS[input.dataset.utime ?? ""];
      if (!key) return;
      const curRaw = it.cookingUtensil?.[key];
      const cur = curRaw != null && curRaw > 0 ? curRaw : undefined;
      const next = readUtensilTimeInput(input.value);
      if (cur === next) return;
      pushHistory();
      ensureUtensil(it);
      if (next == null) delete it.cookingUtensil?.[key];
      else it.cookingUtensil![key] = next;
      draw();
      const kind = utensilTimingKind(it);
      const names: Record<string, string> = {
        cookTime: kind === "both" ? "烤熟时间" : "煮熟时间",
        burnTime: kind === "both" ? "烤糊时间" : "煮糊时间",
        mixTime: "混合时间",
        overMixTime: "过度混合时间"
      };
      setStatus(
        next == null
          ? `${itemLabel(it)} ${names[key]}已恢复默认（写回后生效）`
          : `${itemLabel(it)} ${names[key]}已设为 ${next}秒（写回后生效）`
      );
      reopen();
    });
  });

  document.querySelectorAll<HTMLButtonElement>(".utm-ings").forEach((btn) => {
    btn.addEventListener("click", () => {
      const it = utensilByKey(btn.dataset.key);
      if (!it) return;
      openIngredientMultiPicker(
        `锅具 · 额外食材（${itemLabel(it)}）`,
        "allowedIngredientSOs（不选 = 处理所有主线食材；选中的作为额外可煮食材）",
        S.ingredientsCache,
        it.cookingUtensil?.allowedIngredientGuids ?? [],
        (guids) => {
          pushHistory();
          const cu = ensureUtensil(it);
          cu.allowedIngredientGuids = guids;
          // capacity 缺省会写回 0（后端 int）——兜底原版默认
          if (cu.capacity == null) cu.capacity = defaultUtensilCapacity(it);
          draw();
          setStatus(`${itemLabel(it)} 额外食材已更新（写回后生效）`);
          setTimeout(reopen, 0);
        },
        S.intermediatesCache
      );
    });
  });

  document.querySelectorAll<HTMLButtonElement>(".utm-sync").forEach((btn) => {
    btn.addEventListener("click", () => {
      const src = utensilByKey(btn.dataset.key);
      if (!src) return;
      const pid = prefabIdFromPath(src.prefabAssetPath);
      const cu = src.cookingUtensil ?? {};
      const cap = (cu.capacity ?? 0) > 0 ? cu.capacity! : defaultUtensilCapacity(src);
      const allowed = cu.allowedIngredientGuids ?? [];
      pushHistory();
      let n = 0;
      for (const it of S.items) {
        if (it === src || stubKindOf(it) !== "CookingUtensil") continue;
        if (prefabIdFromPath(it.prefabAssetPath) !== pid) continue;
        it.stubKind = "CookingUtensil";
        // 时间字段按目标锅具类型过滤（变体同族类型一致，这里防御性拷贝全部字段，
        // 混合/烹饪字段互不干扰：无对应 CookingHandler/MixingHandler 时后端忽略；
        // 0 = 未配置不拷贝，保持目标原有留空语义）
        it.cookingUtensil = {
          capacity: cap,
          allowedIngredientGuids: [...allowed],
          ...(cu.cookTime != null && cu.cookTime > 0 ? { cookTime: cu.cookTime } : {}),
          ...(cu.burnTime != null && cu.burnTime > 0 ? { burnTime: cu.burnTime } : {}),
          ...(cu.mixTime != null && cu.mixTime > 0 ? { mixTime: cu.mixTime } : {}),
          ...(cu.overMixTime != null && cu.overMixTime > 0 ? { overMixTime: cu.overMixTime } : {})
        };
        n++;
      }
      draw();
      setStatus(`已把 ${itemLabel(src)} 的参数同步给 ${n} 个相同锅具（写回后生效）`);
      reopen();
    });
  });
}
