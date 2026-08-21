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
  utensilCapacityOrFix
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

export function openUtensilManager() {
  // 锅具 = CookingUtensil stub + 可移动火锅（含锅 child，有 IngredientContainer，
  // 食材配置同样有效，但 stubKind 保持空由后端载体组装，不挂 CookingUtensil stub）
  const utensils = S.items.filter(
    (it) =>
      stubKindOf(it) === "CookingUtensil" ||
      prefabIdFromPath(it.prefabAssetPath) === "utensil_large_pot_01_pushable"
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
          const cap = cu.capacity ?? defaultUtensilCapacity(it);
          const allowed = cu.allowedIngredientGuids ?? [];
          const allowedTxt = allowed.length > 0 ? `额外食材：${allowed.length} 种` : "额外食材：无（处理所有主线食材）";
          const dis = arr.length < 2 ? "disabled" : "";
          return `<div class="utm-row">
            <span class="utm-name">${escHtml(itemLabel(it))}${idx + 1}</span>
            <label class="utm-cap-label">容量 <input type="number" class="utm-cap" data-key="${it._editorKey}" min="0" step="1" value="${cap}"/></label>
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
    `<p class="modal-hint">可直接修改每个锅具的容量与额外食材（不选额外食材时可处理所有主线食材，选中后可额外煮这些食材），或一键把它的参数同步给所有相同类型的锅具。仅修改前端数据，写回 Unity 后生效。</p><div class="modal-scroll">${body}</div>`,
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
  //  搅拌类→搅拌杯，含 DLC 食材如 dlc07 土豆/西芹），按功能基础 id 匹配
  // 场景锅具（含 DLC 变体），容量取原版默认并纠正历史污染，食材列表只增不删。
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
    const vesselOfItem = (it: EditorItem): string => {
      const id = S.catalogByGuid.get(it.prefabGuid ?? "")?.id ?? prefabIdFromPath(it.prefabAssetPath ?? "");
      return functionalBaseId(id ?? "");
    };

    pushHistory();
    let touched = 0;
    for (const it of S.items) {
      const isPushablePot = prefabIdFromPath(it.prefabAssetPath) === "utensil_large_pot_01_pushable";
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
      // 可移动火锅不挂 CookingUtensil stub（载体组装在 LayoutRuntimePushablePot，
      // 挂 stub 会触发宿主 Setup NRE）；只写食材配置。
      if (!isPushablePot) {
        it.stubKind = "CookingUtensil";
      }
      if (!it.cookingUtensil) it.cookingUtensil = {};
      it.cookingUtensil.capacity = utensilCapacityOrFix(it);
      const existing = new Set(it.cookingUtensil.allowedIngredientGuids ?? []);
      for (const g of add) existing.add(g);
      it.cookingUtensil.allowedIngredientGuids = [...existing];
      touched++;
    }
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
      const cap = cu.capacity ?? defaultUtensilCapacity(src);
      const allowed = cu.allowedIngredientGuids ?? [];
      pushHistory();
      let n = 0;
      for (const it of S.items) {
        if (it === src || stubKindOf(it) !== "CookingUtensil") continue;
        if (prefabIdFromPath(it.prefabAssetPath) !== pid) continue;
        it.stubKind = "CookingUtensil";
        it.cookingUtensil = { capacity: cap, allowedIngredientGuids: [...allowed] };
        n++;
      }
      draw();
      setStatus(`已把 ${itemLabel(src)} 的参数同步给 ${n} 个相同锅具（写回后生效）`);
      reopen();
    });
  });
}
