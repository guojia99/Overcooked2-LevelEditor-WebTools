import type { IngredientEntry, LayoutItem, RecipeEntry } from "./types";
import { ingredientOptionLabel } from "./ingredientLabels";

export function ensureModalRoot(): HTMLElement {
  let root = document.getElementById("modal-root");
  if (!root) {
    root = document.createElement("div");
    root.id = "modal-root";
    document.body.appendChild(root);
  }
  return root;
}

export function openModal(title: string, bodyHtml: string, footerHtml: string): HTMLElement {
  const root = ensureModalRoot();
  root.innerHTML = `
    <div class="modal-backdrop" data-modal-backdrop>
      <div class="modal-panel" role="dialog">
        <h2 class="modal-title">${title}</h2>
        <div class="modal-body">${bodyHtml}</div>
        <div class="modal-footer">${footerHtml}</div>
      </div>
    </div>
  `;
  root.querySelector("[data-modal-backdrop]")?.addEventListener("click", (e) => {
    if (e.target === e.currentTarget) closeModal();
  });
  return root;
}

export function closeModal() {
  const root = document.getElementById("modal-root");
  if (root) root.innerHTML = "";
}

export function openIngredientPicker(
  ingredients: IngredientEntry[],
  currentGuid: string | undefined,
  onSave: (guid: string) => void
) {
  const options = ingredients
    .map(
      (ing) =>
        `<option value="${ing.guid}" ${ing.guid === currentGuid ? "selected" : ""}>${ingredientOptionLabel(ing)}</option>`
    )
    .join("");

  openModal(
    "食材箱 · 选择食材",
    `<label class="modal-field">食材 (spawnerItemPrefabSO)<select id="modal-ingredient" class="modal-select">${options}</select></label>`,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn primary" data-ok>确定</button>`
  );

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", () => {
    const sel = document.getElementById("modal-ingredient") as HTMLSelectElement;
    onSave(sel.value);
    closeModal();
  });
}

export function openFoodSpawnerEditor(
  item: LayoutItem,
  ingredients: IngredientEntry[],
  onSave: (patch: NonNullable<LayoutItem["foodSpawner"]>) => void
) {
  const fs = item.foodSpawner ?? {};
  const selected = new Set(fs.attachmentPrefabGuids ?? []);
  const list = ingredients
    .map(
      (ing) =>
        `<label class="modal-check"><input type="checkbox" value="${ing.guid}" ${selected.has(ing.guid) ? "checked" : ""} /> ${ingredientOptionLabel(ing)}</label>`
    )
    .join("");

  openModal(
    "食材生成器 · 参数",
    `
    <label class="modal-check"><input type="checkbox" id="modal-spawn-order" ${fs.spawnInOrder !== false ? "checked" : ""} /> 按顺序生成 (spawnInOrder)</label>
    <label class="modal-check"><input type="checkbox" id="modal-trigger-start" ${fs.triggerAtStart !== false ? "checked" : ""} /> 开局触发 (triggerAtStart)</label>
    <label class="modal-field">触发间隔 triggerTime (秒)<input type="number" id="modal-trigger-time" step="0.5" min="0" value="${fs.triggerTime ?? 5}" /></label>
    <p class="modal-hint">attachmentPrefabSOs（勾选食材，权重均分）</p>
    <div class="modal-scroll">${list}</div>
    `,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn primary" data-ok>确定</button>`
  );

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", () => {
    const guids: string[] = [];
    document.querySelectorAll<HTMLInputElement>(".modal-scroll input:checked").forEach((el) => {
      guids.push(el.value);
    });
    const n = guids.length;
    const weights = n > 0 ? guids.map(() => 1 / n) : [];
    onSave({
      spawnInOrder: (document.getElementById("modal-spawn-order") as HTMLInputElement).checked,
      triggerAtStart: (document.getElementById("modal-trigger-start") as HTMLInputElement).checked,
      triggerTime: Number((document.getElementById("modal-trigger-time") as HTMLInputElement).value),
      attachmentPrefabGuids: guids,
      weights,
    });
    closeModal();
  });
}

export function openRecipePicker(
  recipes: RecipeEntry[],
  selectedGuids: string[],
  levelName: string,
  onSave: (guids: string[]) => void
) {
  const set = new Set(selectedGuids);
  const list = recipes
    .map(
      (r) =>
        `<label class="modal-check"><input type="checkbox" value="${r.guid}" ${set.has(r.guid) ? "checked" : ""} /> ${r.nameZh} <span class="muted">(${r.id})</span></label>`
    )
    .join("");

  openModal(
    `关卡菜谱 · ${levelName || "未命名"}`,
    `<p class="modal-hint">勾选本关订单菜谱（写入 LevelInfoSO.recipes）</p><div class="modal-scroll">${list}</div>`,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn primary" data-ok>保存</button>`
  );

  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", () => {
    const guids: string[] = [];
    document.querySelectorAll<HTMLInputElement>(".modal-scroll input:checked").forEach((el) => {
      guids.push(el.value);
    });
    onSave(guids);
    closeModal();
  });
}
