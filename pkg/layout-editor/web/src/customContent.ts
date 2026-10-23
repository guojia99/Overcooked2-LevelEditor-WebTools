import * as api from "./api";
import {
  dangerBtnHtml,
  mBtnHtml,
  mPrimaryBtnHtml,
} from "./ui/views/button";
import type {
  CustomContentEdit,
  CustomContentItem,
  CustomContentMeta,
  CustomContentOptions,
  CustomContentRef,
  CustomContentTemplate,
  IngredientEntry,
  LevelSetInfo,
} from "./types";
import { hideBusy, showBusy } from "./busy";
import { navHtml, wireNav } from "./nav";
import {
  burgerPath,
  customContentFormPath,
  customContentListPath,
  fillingPath,
  parseRoute,
  recipeListPath,
} from "./route";
import { setStatus } from "./editor/status";
import { openModal, closeModal, openIngredientMultiPicker } from "./modals";

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

const KIND_META: { id: string; icon: string; zh: string; en: string; desc: string }[] = [
  { id: "ingredient", icon: "🥕", zh: "自定义食材", en: "Custom ingredients", desc: "可加工 / 不可加工 / 备餐容器食材，含 OrderNode、图标与模型" },
  { id: "counter", icon: "🧱", zh: "自定义工作台", en: "Custom counters", desc: "普通 / 切菜 / 水槽工作台，含火焰、切菜 PFX、教程图标" },
  { id: "utensil", icon: "🍳", zh: "自定义锅具", en: "Custom utensils", desc: "火锅、烤盘、煎盘、汤锅、蒸笼等；支持站台类型与锅内食材模型" },
  { id: "sink", icon: "🚰", zh: "自定义水槽", en: "Custom sinks", desc: "清洗/沥干部件与干净盘堆绑定" },
  { id: "tray", icon: "🍽️", zh: "自定义托盘", en: "Custom trays", desc: "多槽上菜容器与槽位配方" },
  { id: "prep", icon: "🥣", zh: "自定义备餐容器", en: "Preparation containers", desc: "OrderNode、容器限制与 cosmetics prefab" },
  { id: "splitter", icon: "🔀", zh: "分料机", en: "Ingredient splitter", desc: "输入/输出、容量、每份拆分数量与音效" },
];

function kindMeta(kind: string) {
  return KIND_META.find((k) => k.id === kind) ?? { id: kind, icon: "🧩", zh: kind, en: kind, desc: "" };
}

/** 模板预设：一键带入容量/站台/拆分等默认值。 */
const TEMPLATE_PRESETS: Record<string, { capacity?: number; stationTypes?: string[]; splitNum?: number; splitAudioTag?: string }> = {
  utensil_hotpot: { capacity: 4, stationTypes: ["Invalid_FloorBurner"] },
  utensil_roasting_tray: { capacity: 4, stationTypes: ["Oven"] },
  utensil_griddle_pan: { capacity: 4, stationTypes: ["FirePit"] },
  utensil_skewer: { capacity: 3, stationTypes: ["Barbeque"] },
  utensil_toasting_fork: { capacity: 1, stationTypes: ["FirePit"] },
  utensil_pot: { capacity: 3, stationTypes: ["Hob"] },
  utensil_steamer: { capacity: 4, stationTypes: ["Hob"] },
  splitter: { capacity: 8, splitNum: 2, splitAudioTag: "PutDown" },
};

function shell(app: HTMLElement, title: string, setId?: string, activeKind?: string): HTMLElement {
  document.body.classList.add("manage-bg");
  app.innerHTML = `
    ${navHtml("custom-content")}
    <div class="manage-bar">
      <h1 class="m-title">${esc(title)}</h1>
      <span class="cc-spacer"></span>
    </div>
    ${setId ? tabsHtml(setId, activeKind) : ""}
    <div class="manage-content cc-content" id="cc-content"></div>
  `;
  wireNav();
  return document.getElementById("cc-content")!;
}

function tabsHtml(setId: string, activeKind?: string): string {
  const tab = (id: string, label: string, icon: string) =>
    `<a class="cc-tab${activeKind === id ? " active" : ""}" href="${customContentListPath(setId, id || undefined)}">${icon} ${esc(label)}</a>`;
  return `<div class="cc-tabs-wrap"><nav class="cc-tabs">
    <a class="cc-tab${!activeKind ? " active" : ""}" href="${customContentListPath(setId)}">🏠 总览</a>
    ${KIND_META.map((k) => tab(k.id, k.zh, k.icon)).join("")}
  </nav></div>`;
}

function busy(msg: string): void {
  const el = document.getElementById("cc-content");
  if (el) el.innerHTML = `<p class="muted">${esc(msg)}</p>`;
  setStatus(msg);
}

function showError(e: unknown): void {
  const msg = e instanceof Error ? e.message : String(e);
  setStatus(msg, false);
  const el = document.getElementById("cc-content");
  if (el) el.innerHTML = `<div class="m-block"><h3>出错</h3><p>${esc(msg)}</p></div>`;
}

let popstateWired = false;

export async function renderCustomContentView(app: HTMLElement): Promise<void> {
  if (!popstateWired) {
    popstateWired = true;
    window.addEventListener("popstate", () => void renderRoute(app));
  }
  await renderRoute(app);
}

async function renderRoute(app: HTMLElement): Promise<void> {
  const r = parseRoute();
  if (r.page !== "custom-content") return;
  if (!r.setId) {
    await renderSetChooser(app);
    return;
  }
  if (!r.contentKind) {
    await renderHub(app, r.setId);
    return;
  }
  if (!r.contentId) {
    await renderList(app, r.setId, r.contentKind);
    return;
  }
  await renderForm(app, r.setId, r.contentKind, r.contentId);
}

async function renderSetChooser(app: HTMLElement): Promise<void> {
  const content = shell(app, "自定义管理");
  busy("加载关卡集…");
  let sets: LevelSetInfo[] = [];
  try {
    sets = await api.fetchSets();
  } catch (e) {
    showError(e);
    return;
  }
  content.innerHTML = `
    <div class="m-section-title">选择关卡集</div>
    <p class="modal-hint">自定义内容按关卡集保存（Assets/LevelSets/&lt;set&gt;/custom_content）。模板来自 common04 / common03 / commonW1，模板本身只读。</p>
    <div class="m-grid">${sets
      .map(
        (s) => `<div class="m-card cc-set-card">
          <div class="cc-set-icon">🧩</div>
          <h3>${esc(s.levelSetNameZH || s.setName)} <span class="muted">(${esc(s.levelSetName || s.setName)})</span></h3>
          <div class="m-meta">标识：${esc(s.setName)} · 关卡数：${s.levelCount}<br>作者：${esc(s.author || "—")}</div>
          <div class="m-actions">${mPrimaryBtnHtml("进入自定义管理", { "data-open": esc(s.setName) })}</div>
        </div>`
      )
      .join("") || '<p class="muted">暂无关卡集</p>'}</div>`;
  content.querySelectorAll<HTMLButtonElement>("[data-open]").forEach((b) =>
    b.addEventListener("click", () => location.assign(customContentListPath(b.dataset.open!)))
  );
}

async function renderHub(app: HTMLElement, setId: string): Promise<void> {
  const content = shell(app, `自定义管理 · ${setId}`, setId);
  content.innerHTML = `
    <div class="cc-hero">
      <div><h2>🧩 自定义内容</h2><p>选择类型进入管理。所有引用字段都使用下拉/选择器，不再手输 GUID。</p></div>
      <div class="cc-hero-actions">
        ${mBtnHtml("🍽️ 自定义菜谱", "default", { "data-nav-recipes": "" })}
        ${mBtnHtml("🍔 汉堡工作台", "default", { "data-nav-burger": "" })}
        ${mBtnHtml("🥩 夹心工作台", "default", { "data-nav-filling": "" })}
      </div>
    </div>
    <div class="m-grid cc-kind-grid">
      ${KIND_META.map(
        (k) => `<div class="m-card cc-kind-card" data-kind="${k.id}">
          <div class="cc-kind-icon">${k.icon}</div>
          <h3>${esc(k.zh)} <span class="muted">(${esc(k.en)})</span></h3>
          <div class="m-meta">${esc(k.desc)}</div>
          <div class="m-actions">${mBtnHtml("进入管理", "primary", { "data-kind": k.id })}</div>
        </div>`
      ).join("")}
    </div>`;
  content.querySelectorAll<HTMLButtonElement>("[data-kind]").forEach((b) =>
    b.addEventListener("click", () => location.assign(customContentListPath(setId, b.dataset.kind!)))
  );
  content.querySelector("[data-nav-recipes]")?.addEventListener("click", () => location.assign(recipeListPath(setId)));
  content.querySelector("[data-nav-burger]")?.addEventListener("click", () => location.assign(burgerPath(setId)));
  content.querySelector("[data-nav-filling]")?.addEventListener("click", () => location.assign(fillingPath(setId)));
}

async function renderList(app: HTMLElement, setId: string, kind: string): Promise<void> {
  const meta = kindMeta(kind);
  const content = shell(app, `${meta.icon} ${meta.zh} · ${setId}`, setId, kind);
  busy("加载列表…");
  let items: CustomContentItem[] = [];
  try {
    items = await api.fetchCustomContentList(setId, kind);
  } catch (e) {
    showError(e);
    return;
  }
  content.innerHTML = `
    <div class="cc-list-head">
      <div>
        <h2>${meta.icon} ${esc(meta.zh)} <span class="muted">(${items.length})</span></h2>
        <p>${esc(meta.desc)}</p>
      </div>
      <div class="cc-actions">${mPrimaryBtnHtml("＋ 新建 " + meta.zh, { "data-new": "" })}</div>
    </div>
    <div class="m-grid cc-item-grid">${items
      .map(
        (it) => `<div class="m-card cc-item-card">
          <div class="cc-item-top">
            <img class="cc-item-icon" src="${esc(api.customContentIconUrl(it))}" alt="" onerror="this.style.display='none'"/>
            <div><h3>${esc(it.nameZh || it.id)}</h3><div class="muted small">${esc(it.id)}</div></div>
          </div>
          <div class="m-meta">模板：${esc(it.templateId || "—")}<br><span class="cc-path">${esc(it.assetPath)}</span>
          ${it.warnings?.length ? `<div class="cc-warn">⚠ ${esc(it.warnings.join("；"))}</div>` : ""}</div>
          <div class="m-actions">
            ${mBtnHtml("编辑", "primary", { "data-edit": esc(it.id) })}
            ${dangerBtnHtml("删除", { "data-del": esc(it.id) })}
          </div>
        </div>`
      )
      .join("") || `<div class="cc-empty"><div>${meta.icon}</div><p>还没有自定义内容</p>${mPrimaryBtnHtml("从模板创建", { "data-new": "" })}</div>`}</div>`;
  content.querySelectorAll<HTMLButtonElement>("[data-new]").forEach((b) =>
    b.addEventListener("click", () => location.assign(customContentFormPath(setId, kind, "new")))
  );
  content.querySelectorAll<HTMLButtonElement>("[data-edit]").forEach((b) =>
    b.addEventListener("click", () => location.assign(customContentFormPath(setId, kind, b.dataset.edit!)))
  );
  content.querySelectorAll<HTMLButtonElement>("[data-del]").forEach((b) =>
    b.addEventListener("click", async () => {
      const id = b.dataset.del!;
      if (!confirm(`删除自定义内容「${id}」？此操作会删除其 prefab / OrderNode / 图标 / 模型。`)) return;
      try {
        await api.deleteCustomContent(setId, kind, id);
        setStatus("已删除");
        await renderList(app, setId, kind);
      } catch (e) {
        setStatus((e as Error).message || String(e), false);
      }
    })
  );
}

function refSelect(id: string, refs: CustomContentRef[], current: string, emptyLabel = "— 不设置 —", valueField: "guid" | "id" = "guid"): string {
  const seen = new Set<string>();
  const opts = refs
    .filter((r) => r && r.guid && !seen.has(r.guid) && seen.add(r.guid))
    .map((r) => {
      const value = valueField === "id" ? r.id : r.guid;
      return `<option value="${esc(value)}" ${value === current ? "selected" : ""}>${esc(r.nameZh || r.id)}${r.nameEn && r.nameEn !== r.id ? ` · ${esc(r.nameEn)}` : ""}</option>`;
    })
    .join("");
  return `<select id="${id}" class="m-select cc-select"><option value="">${esc(emptyLabel)}</option>${opts}</select>`;
}

function templateCards(templates: CustomContentTemplate[], current: string, isNew: boolean): string {
  if (!isNew) {
    const t = templates.find((x) => x.id === current);
    return `<div class="cc-template-current">当前模板：<b>${esc(t?.labelZh || current || "—")}</b> <span class="muted">(${esc(current || "—")})</span></div>`;
  }
  return `<div class="cc-template-grid">${templates
    .map(
      (t) => `<button type="button" class="cc-template-card${t.id === current ? " active" : ""}" data-template="${esc(t.id)}">
        <b>${esc(t.labelZh)}</b><span>${esc(t.labelEn || t.id)}</span><small>${esc(t.id)}</small>
      </button>`
    )
    .join("")}</div>`;
}

function stationChecks(meta: CustomContentMeta, selected: string[]): string {
  const set = new Set(selected);
  return (meta.stationTypes ?? [])
    .map(
      (s) => `<label class="cc-check"><input type="checkbox" class="cc-station" value="${esc(s.name)}" ${set.has(s.name) ? "checked" : ""}/> <span>${esc(s.labelZh)}</span></label>`
    )
    .join("");
}

function multiButton(id: string, label: string): string {
  return `<button type="button" class="m-btn default cc-multi-btn" id="${id}">${esc(label)}</button>`;
}

async function renderForm(app: HTMLElement, setId: string, kind: string, idOrNew: string): Promise<void> {
  const meta = kindMeta(kind);
  const isNew = idOrNew === "new";
  const content = shell(app, `${isNew ? "新建" : "编辑"} ${meta.zh} · ${setId}`, setId, kind);
  busy("加载模板与引用…");
  let templates: CustomContentTemplate[] = [];
  let metaInfo: CustomContentMeta = { stationTypes: [] };
  let options: CustomContentOptions = {
    setName: setId, prefabs: [], soRefs: [], modelPrefabs: [], modelSOs: [],
    orderLists: [], lookups: [], recipes: [], plating: [], audioTags: [],
  };
  let items: CustomContentItem[] = [];
  let ingredients: IngredientEntry[] = [];
  try {
    [templates, metaInfo, options, items, ingredients] = await Promise.all([
      api.fetchCustomContentTemplates(kind),
      api.fetchCustomContentMeta(),
      api.fetchCustomContentOptions(setId),
      isNew ? Promise.resolve([]) : api.fetchCustomContentList(setId, kind),
      api.fetchIngredients(setId).catch(() => []),
    ]);
  } catch (e) {
    showError(e);
    return;
  }
  const item = isNew ? undefined : items.find((x) => x.id === idOrNew);
  if (!isNew && !item) {
    showError(`未找到「${idOrNew}」`);
    return;
  }

  let selectedTemplate = item?.templateId || templates[0]?.id || "";
  let templateAssetPath = item?.assetPath || templates.find((t) => t.id === selectedTemplate)?.assetPath || "";
  let ingredientGuids: string[] = item?.allowedIngredientGuids ?? [];
  let modelGuids: string[] = item?.modelGuids ?? [];
  let modelSoGuids: string[] = item?.modelSoGuids ?? [];
  let hierarchy: string[] = [];
  let modelSource: { modelFile: string; files: string[] } | null = null;
  let iconBase64: string | undefined;
  let modelFiles: { fileName: string; base64: string }[] | undefined;

  async function loadHierarchy(path: string): Promise<void> {
    if (!path) { hierarchy = []; return; }
    try { hierarchy = (await api.fetchCustomContentHierarchy(path)).paths ?? []; }
    catch { hierarchy = []; }
  }
  await loadHierarchy(templateAssetPath);
  if (item) {
    try {
      const src = await api.fetchCustomContentModelSource(item.assetPath);
      if (src.modelFile) modelSource = src;
    } catch { modelSource = null; }
  }

  const preset = TEMPLATE_PRESETS[selectedTemplate] ?? {};
  const currentCapacity = item?.capacity ?? preset.capacity ?? 0;
  const currentSplit = item?.splitNum ?? preset.splitNum ?? 2;
  const currentAudio = item?.splitAudioTag ?? preset.splitAudioTag ?? "";
  const currentStations = item?.stationTypes ?? preset.stationTypes ?? [];

  const ingredientFields = `
    <div class="cc-section"><div class="cc-section-title">🥕 食材 / OrderNode</div>
      <div class="cc-grid">
        <label class="m-field">模板${isNew ? "" : ""}
          <div class="muted small">编辑时模板只读；新建请看上方模板卡片</div>
        </label>
        <label class="m-field">OrderNode 唯一 uID
          <input id="cc-order-uid" type="number" min="1" value="${item?.orderUid ?? 0}" class="m-input"><span class="muted small">0 = 自动分配</span>
        </label>
        <label class="m-field">装盘容器 ${refSelect("cc-plating", options.plating, item?.platingStepId ?? "", "— 不设置 —", "id")}</label>
        <label class="m-field">容器限制（OrderToPrefabLookup） ${refSelect("cc-restrictions", options.lookups, item?.restrictionsGuid ?? "")}</label>
        <label class="m-field">Cosmetics prefab ${refSelect("cc-cosmetics", options.prefabs, item?.cosmeticsPrefabGuid ?? "")}</label>
        <label class="m-field">可加工下一形态 ${refSelect("cc-next-prefab", options.prefabs, item?.nextPrefabGuid ?? "")}</label>
        <label class="m-field">食材内容 UI ${refSelect("cc-ingredient-ui", options.soRefs, item?.ingredientContentUiGuid ?? "")}</label>
        <label class="m-field">进度 UI ${refSelect("cc-progress-ui", options.soRefs, item?.progressUiGuid ?? "")}</label>
      </div>
    </div>`;

  const counterFields = `
    <div class="cc-section"><div class="cc-section-title">🧱 工作台参数</div>
      <div class="cc-grid">
        <label class="m-field">进度 UI ${refSelect("cc-progress-ui", options.soRefs, item?.progressUiGuid ?? "")}</label>
        <label class="m-field">火焰特效 ${refSelect("cc-fire-fx", options.soRefs, item?.fireEffectGuid ?? "")}</label>
        <label class="m-field">切菜 PFX ${refSelect("cc-chop-pfx", options.soRefs, item?.chopPfxGuid ?? "")}</label>
        <label class="m-field">教程图标 ${refSelect("cc-tutorial-icon", options.soRefs, item?.tutorialIconGuid ?? "")}</label>
      </div>
    </div>`;

  const utensilFields = `
    <div class="cc-section"><div class="cc-section-title">🍳 锅具参数</div>
      <div class="cc-grid">
        <label class="m-field">容量<input id="cc-capacity" type="number" min="0" value="${currentCapacity}" class="m-input"></label>
        <label class="m-field">允许站台
          <div class="cc-checks">${stationChecks(metaInfo, currentStations)}</div>
        </label>
        <label class="m-field">食材内容 UI ${refSelect("cc-ingredient-ui", options.soRefs, item?.ingredientContentUiGuid ?? "")}</label>
        <label class="m-field">Sludge 模型 ${refSelect("cc-sludge", options.soRefs, item?.sludgeGuid ?? "")}</label>
        <label class="m-field">生成特效 ${refSelect("cc-spawn-fx", options.soRefs, item?.spawnEffectGuid ?? "")}</label>
        <label class="m-field">额外食材
          <div class="cc-multi-row">${multiButton("cc-pick-ings", "选择额外食材…")}<span id="cc-ings-count" class="muted small"></span></div>
        </label>
        <label class="m-field">锅内模型 prefab
          <div class="cc-multi-row">${multiButton("cc-pick-models", "选择模型…")}<span id="cc-models-count" class="muted small"></span></div>
        </label>
        <label class="m-field">锅内模型 SO
          <div class="cc-multi-row">${multiButton("cc-pick-model-sos", "选择模型 SO…")}<span id="cc-model-sos-count" class="muted small"></span></div>
        </label>
      </div>
    </div>`;

  const sinkFields = `
    <div class="cc-section"><div class="cc-section-title">🚰 水槽参数</div>
      <div class="cc-grid">
        <label class="m-field">清洗部件
          <select id="cc-washing-part" class="m-select cc-select"><option value="">— 不设置 —</option>${hierarchy.map((p) => `<option value="${esc(p)}" ${p === (item?.washingPartPath ?? "") ? "selected" : ""}>${esc(p)}</option>`).join("")}</select>
        </label>
        <label class="m-field">沥干部件
          <select id="cc-drying-part" class="m-select cc-select"><option value="">— 不设置 —</option>${hierarchy.map((p) => `<option value="${esc(p)}" ${p === (item?.dryingPartPath ?? "") ? "selected" : ""}>${esc(p)}</option>`).join("")}</select>
        </label>
        <label class="m-field">干净盘堆 ${refSelect("cc-clean-stack", options.soRefs, item?.cleanPlateStackGuid ?? "")}</label>
      </div>
    </div>`;

  const trayFields = `
    <div class="cc-section"><div class="cc-section-title">🍽️ 托盘参数</div>
      <div class="cc-grid"><label class="m-field">槽位配置 ${refSelect("cc-tray-slot", options.orderLists, item?.traySlotGuid ?? "")}</label></div>
    </div>`;

  const splitterFields = `
    <div class="cc-section"><div class="cc-section-title">🔀 分料机参数</div>
      <div class="cc-grid">
        <label class="m-field">容量<input id="cc-capacity" type="number" min="0" value="${currentCapacity}" class="m-input"></label>
        <label class="m-field">每次拆分<input id="cc-split-num" type="number" min="1" value="${currentSplit}" class="m-input"></label>
        <label class="m-field">输入菜谱 ${refSelect("cc-input-recipe", options.recipes, item?.inputRecipeGuid ?? "")}</label>
        <label class="m-field">输出 prefab ${refSelect("cc-output-prefab", options.prefabs, item?.outputPrefabGuid ?? "")}</label>
        <label class="m-field">音效 tag
          <select id="cc-split-audio" class="m-select cc-select"><option value="">— 默认 PutDown —</option>${options.audioTags.map((t) => `<option value="${esc(t)}" ${t === currentAudio ? "selected" : ""}>${esc(t)}</option>`).join("")}</select>
        </label>
      </div>
    </div>`;

  let kindFields = "";
  if (kind === "ingredient" || kind === "prep") kindFields = ingredientFields;
  else if (kind === "counter") kindFields = counterFields;
  else if (kind === "utensil") kindFields = utensilFields + counterFields;
  else if (kind === "sink") kindFields = sinkFields + counterFields;
  else if (kind === "tray") kindFields = trayFields;
  else if (kind === "splitter") kindFields = splitterFields;

  content.innerHTML = `
    <div class="cc-form-head">
      <div><h2>${isNew ? "＋ 新建" : "✏️ 编辑"} ${meta.icon} ${esc(meta.zh)}</h2><p>${esc(meta.desc)}</p></div>
      <div class="cc-actions">
        ${mPrimaryBtnHtml("💾 保存", { "data-save": "" })}
        ${mBtnHtml("返回列表", "default", { "data-back": "" })}
      </div>
    </div>
    <div class="cc-section"><div class="cc-section-title">🎯 模板</div>
      ${templateCards(templates, selectedTemplate, isNew)}
      <p class="muted small">选择模板后会自动带入容量/站台/音效等默认值；所有引用字段均可从下拉列表选择。</p>
    </div>
    <div class="cc-section"><div class="cc-section-title">📝 基础信息</div>
      <div class="cc-grid">
        <label class="m-field">标识 id<input id="cc-id" value="${esc(item?.id ?? "")}" ${isNew ? "" : "disabled"} placeholder="字母/数字/下划线" class="m-input"></label>
        <label class="m-field">中文名<input id="cc-zh" value="${esc(item?.nameZh ?? "")}" class="m-input"></label>
        <label class="m-field">英文名<input id="cc-en" value="${esc(item?.nameEn ?? "")}" class="m-input"></label>
      </div>
    </div>
    ${kindFields}
    <div class="cc-section"><div class="cc-section-title">🖼️ 图标与模型</div>
      <div class="cc-grid">
        <label class="m-field">图标 PNG<input type="file" id="cc-icon" accept="image/png,image/jpeg" class="m-input"></label>
        <label class="m-field">模型 FBX/OBJ + 贴图<input type="file" id="cc-model" multiple accept=".fbx,.obj,.png,.jpg,.jpeg,.tga" class="m-input"></label>
        <label class="m-field">模型缩放<input type="number" id="cc-model-scale" step="0.01" value="1" class="m-input"></label>
        <label class="m-field">模型位置 X/Y/Z<input id="cc-model-pos" value="0,0,0" placeholder="0,0,0" class="m-input"></label>
        <label class="m-field">模型旋转 X/Y/Z<input id="cc-model-rot" value="0,0,0" placeholder="0,0,0" class="m-input"></label>
        <label class="m-field">模型 Pivot X/Y/Z<input id="cc-model-pivot" value="0,0,0" placeholder="0,0,0" class="m-input"></label>
        <label class="m-field">模型预览
          <div class="cc-multi-row">${modelSource ? mBtnHtml("👁 打开 3D 预览", "default", { "data-preview": "" }) : '<span class="muted small">保存并上传模型后可预览</span>'}</div>
        </label>
      </div>
    </div>
    <pre id="cc-status" class="muted cc-status"></pre>`;

  const val = (id: string) => (document.getElementById(id) as HTMLInputElement | null)?.value ?? "";
  const num = (id: string) => Number(val(id)) || 0;
  const setCount = (id: string, n: number, label: string) => {
    const el = document.getElementById(id);
    if (el) el.textContent = n > 0 ? `${n} 个${label}` : "未选择";
  };
  setCount("cc-ings-count", ingredientGuids.length, "食材");
  setCount("cc-models-count", modelGuids.length, "模型");
  setCount("cc-model-sos-count", modelSoGuids.length, "模型 SO");
  const updateTemplateSelection = async (templateId: string) => {
    selectedTemplate = templateId;
    const t = templates.find((x) => x.id === templateId);
    templateAssetPath = t?.assetPath || "";
    document.querySelectorAll<HTMLElement>(".cc-template-card").forEach((el) => {
      el.classList.toggle("active", el.dataset.template === templateId);
    });
    const p = TEMPLATE_PRESETS[templateId] ?? {};
    const cap = document.getElementById("cc-capacity") as HTMLInputElement | null;
    if (cap) cap.value = String(p.capacity ?? 0);
    const split = document.getElementById("cc-split-num") as HTMLInputElement | null;
    if (split) split.value = String(p.splitNum ?? 2);
    const audio = document.getElementById("cc-split-audio") as HTMLSelectElement | null;
    if (audio && p.splitAudioTag) audio.value = p.splitAudioTag;
    const stations = new Set(p.stationTypes ?? []);
    document.querySelectorAll<HTMLInputElement>(".cc-station").forEach((cb) => {
      cb.checked = stations.has(cb.value);
    });
    await loadHierarchy(templateAssetPath);
    const wash = document.getElementById("cc-washing-part") as HTMLSelectElement | null;
    const dry = document.getElementById("cc-drying-part") as HTMLSelectElement | null;
    for (const sel of [wash, dry]) {
      if (!sel) continue;
      const cur = sel.value;
      sel.innerHTML = `<option value="">— 不设置 —</option>${hierarchy.map((x) => `<option value="${esc(x)}">${esc(x)}</option>`).join("")}`;
      if (cur && hierarchy.includes(cur)) sel.value = cur;
    }
  };
  document.querySelectorAll<HTMLElement>(".cc-template-card").forEach((el) => {
    el.addEventListener("click", () => void updateTemplateSelection(el.dataset.template!));
  });
  document.getElementById("cc-pick-ings")?.addEventListener("click", () => {
    openIngredientMultiPicker(
      "锅内额外食材",
      "不选 = 接受所有主线食材；选择后作为额外可煮食材",
      ingredients,
      ingredientGuids,
      (guids) => {
        ingredientGuids = guids;
        setCount("cc-ings-count", ingredientGuids.length, "食材");
      },
      [],
      {}
    );
  });
  document.getElementById("cc-pick-models")?.addEventListener("click", () => {
    openRefPicker("选择锅内模型 prefab", options.modelPrefabs, modelGuids, (guids) => {
      modelGuids = guids;
      setCount("cc-models-count", modelGuids.length, "模型");
    });
  });
  document.getElementById("cc-pick-model-sos")?.addEventListener("click", () => {
    openRefPicker("选择锅内模型 SO", options.modelSOs, modelSoGuids, (guids) => {
      modelSoGuids = guids;
      setCount("cc-model-sos-count", modelSoGuids.length, "模型 SO");
    });
  });
  content.querySelectorAll("[data-back]").forEach((b) =>
    b.addEventListener("click", () => location.assign(customContentListPath(setId, kind)))
  );
  content.querySelector("[data-preview]")?.addEventListener("click", () => {
    if (!item || !modelSource) return;
    void import("./modelPreview").then((m) =>
      m.openModelPreview({
        title: item.nameZh || item.id,
        resourceBase: `/api/custom-content/model-file?assetPath=${encodeURIComponent(item.assetPath)}&file=`,
        modelFileName: modelSource.modelFile,
        fitTarget: "plate",
      })
    );
  });
  const iconInput = document.getElementById("cc-icon") as HTMLInputElement | null;
  iconInput?.addEventListener("change", async () => {
    const f = iconInput.files?.[0];
    iconBase64 = f ? await fileBase64(f) : undefined;
  });
  const modelInput = document.getElementById("cc-model") as HTMLInputElement | null;
  modelInput?.addEventListener("change", async () => {
    modelFiles = [];
    const picked = Array.from(modelInput.files ?? []);
    for (const f of picked) {
      modelFiles.push({ fileName: f.name, base64: await fileBase64(f) });
    }
    const main = picked.find((f) => /\.(fbx|obj)$/i.test(f.name));
    if (!main) return;
    const textures = picked.filter((f) => /\.(png|jpg|jpeg|tga)$/i.test(f.name));
    const buffer = await main.arrayBuffer();
    void import("./modelPreview").then((m) =>
      m.openModelPreview({
        title: main.name,
        resourceBase: "",
        modelFileName: main.name,
        localBuffer: buffer,
        localTextures: textures.length ? textures : undefined,
        fitTarget: "plate",
        scale: Number((document.getElementById("cc-model-scale") as HTMLInputElement | null)?.value) || 1,
        onAdjust: (t) => {
          const set = (id: string, v: number) => {
            const el = document.getElementById(id) as HTMLInputElement | null;
            if (el) el.value = String(Math.round(v * 1000) / 1000);
          };
          set("cc-model-scale", t.scale);
          const pos = document.getElementById("cc-model-pos") as HTMLInputElement | null;
          if (pos) pos.value = `${t.positionX},${t.positionY},${t.positionZ}`;
          const rot = document.getElementById("cc-model-rot") as HTMLInputElement | null;
          if (rot) rot.value = `${t.rotationX},${t.rotationY},${t.rotationZ}`;
          const piv = document.getElementById("cc-model-pivot") as HTMLInputElement | null;
          if (piv) piv.value = `${t.pivotX},${t.pivotY},${t.pivotZ}`;
        },
      })
    );
  });

  content.querySelectorAll("[data-save]").forEach((btn) => btn.addEventListener("click", async () => {
    const dto: CustomContentEdit = {
      setName: setId,
      kind,
      id: val("cc-id"),
      assetPath: item?.assetPath,
      nameZh: val("cc-zh"),
      nameEn: val("cc-en"),
      templateId: selectedTemplate,
      orderUid: num("cc-order-uid"),
      platingStepId: val("cc-plating"),
      restrictionsGuid: val("cc-restrictions"),
      cosmeticsPrefabGuid: val("cc-cosmetics"),
      nextPrefabGuid: val("cc-next-prefab"),
      capacity: num("cc-capacity"),
      allowedIngredientGuids: ingredientGuids,
      modelGuids,
      modelSoGuids,
      stationTypes: Array.from(document.querySelectorAll<HTMLInputElement>(".cc-station")).filter((x) => x.checked).map((x) => x.value),
      progressUiGuid: val("cc-progress-ui"),
      ingredientContentUiGuid: val("cc-ingredient-ui"),
      sludgeGuid: val("cc-sludge"),
      spawnEffectGuid: val("cc-spawn-fx"),
      fireEffectGuid: val("cc-fire-fx"),
      chopPfxGuid: val("cc-chop-pfx"),
      tutorialIconGuid: val("cc-tutorial-icon"),
      washingPartPath: val("cc-washing-part"),
      dryingPartPath: val("cc-drying-part"),
      cleanPlateStackGuid: val("cc-clean-stack"),
      traySlotGuid: val("cc-tray-slot"),
      inputRecipeGuid: val("cc-input-recipe"),
      outputPrefabGuid: val("cc-output-prefab"),
      splitNum: num("cc-split-num"),
      splitAudioTag: val("cc-split-audio"),
      modelScale: num("cc-model-scale"),
      modelPositionX: 0,
      modelPositionY: 0,
      modelPositionZ: 0,
      modelRotationX: 0,
      modelRotationY: 0,
      modelRotationZ: 0,
      modelPivotX: 0,
      modelPivotY: 0,
      modelPivotZ: 0,
      iconBase64,
      iconFileName: iconInput?.files?.[0]?.name,
      modelFiles,
    };
    const pos = val("cc-model-pos").split(",").map((s) => Number(s.trim()) || 0);
    dto.modelPositionX = pos[0] ?? 0; dto.modelPositionY = pos[1] ?? 0; dto.modelPositionZ = pos[2] ?? 0;
    const rot = val("cc-model-rot").split(",").map((s) => Number(s.trim()) || 0);
    dto.modelRotationX = rot[0] ?? 0; dto.modelRotationY = rot[1] ?? 0; dto.modelRotationZ = rot[2] ?? 0;
    const piv = val("cc-model-pivot").split(",").map((s) => Number(s.trim()) || 0);
    dto.modelPivotX = piv[0] ?? 0; dto.modelPivotY = piv[1] ?? 0; dto.modelPivotZ = piv[2] ?? 0;

    if (!dto.id && isNew) {
      document.getElementById("cc-status")!.textContent = "请填写标识 id";
      return;
    }
    showBusy("保存中…");
    try {
      if (isNew) await api.createCustomContent(dto);
      else await api.updateCustomContent(dto);
      hideBusy();
      setStatus("已保存");
      location.assign(customContentListPath(setId, kind));
    } catch (e) {
      hideBusy();
      const msg = (e as Error).message || String(e);
      setStatus(msg, false);
      document.getElementById("cc-status")!.textContent = msg;
    }
  }));
}

function openRefPicker(title: string, refs: CustomContentRef[], selected: string[], onOk: (guids: string[]) => void): void {
  const set = new Set(selected);
  const body = `<div class="cc-picker-list">${refs
    .map((r) => `<label class="cc-picker-row"><input type="checkbox" data-cc-ref="${esc(r.guid)}" ${set.has(r.guid) ? "checked" : ""}/><span>${esc(r.nameZh || r.id)} <span class="muted">${esc(r.id)}</span></span></label>`)
    .join("") || '<p class="muted">没有可选条目</p>'}</div>`;
  openModal(title, body, `${mPrimaryBtnHtml("确定", { "data-cc-pick-ok": "" })}${mBtnHtml("取消", "default", { "data-cancel": "" })}`);
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-cc-pick-ok]")?.addEventListener("click", () => {
    const guids = Array.from(document.querySelectorAll<HTMLInputElement>("[data-cc-ref]"))
      .filter((x) => x.checked)
      .map((x) => x.dataset.ccRef!);
    closeModal();
    onOk(guids);
  });
}

function fileBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const s = String(reader.result ?? "");
      resolve(s.includes(",") ? s.slice(s.indexOf(",") + 1) : s);
    };
    reader.onerror = () => reject(reader.error ?? new Error("读取文件失败"));
    reader.readAsDataURL(file);
  });
}
