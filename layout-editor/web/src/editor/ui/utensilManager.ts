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
  defaultUtensilCapacity
} from "../stubControls";

export function openUtensilManager() {
  const utensils = S.items.filter((it) => stubKindOf(it) === "CookingUtensil");
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
    `<button type="button" class="modal-btn" data-cancel>关闭</button>`
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
          ensureUtensil(it).allowedIngredientGuids = guids;
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
