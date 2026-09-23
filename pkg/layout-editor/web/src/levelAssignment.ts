/** 菜谱分工模式页（汇总页「🧑‍🍳 分工模式」进入）。
 *
 *  - 布局：左侧菜谱备选池（约 1/3，分组 + 分数排序）+ 右侧玩家列（2P→P1/P2 … 4P→P1-P4）。
 *  - 交互：从池拖菜谱到玩家列分配；玩家卡片拖到另一列 = 移动、拖回池/点 ✕ = 移除；
 *    玩家列内自动去重；已分给任一玩家的菜谱在池中置灰（仍可拖给其他玩家）。
 *  - 配置：双人/三人/四人三套独立分工，JSON 落盘关卡 data 目录
 *    assignment~/assignment.json（`~` 目录 Unity 忽略 → 不进 AssetBundle），
 *    前端唯一读写方，后端只做大小/路径守卫（/api/level/assignment*）。
 *  - 未分配仅提醒不阻止保存；默认为空时汇总页不展示分工区块。
 *  - 导出：单模式 PNG / 已配置模式合成一张长图（离屏舞台 + DOM 快照导出引擎）。 */
import * as api from "./api";
import type {
  AssignmentMode,
  AssignmentPlayer,
  CustomRecipeConfig,
  CustomRecipeSummary,
  IngredientEntry,
  LevelAssignmentData,
  LevelDetail,
  LevelSetInfo,
  LevelSummary,
  RecipeEntry,
} from "./types";
import { navHtml, wireNav } from "./nav";
import { assignmentPath, manageLevelSummaryPath, navigateTo, parseRoute } from "./route";
import { customRecipeIconUrl } from "./editor/catalog";
import { normalizeCustomRecipeCard } from "./recipeCardCustom";
import { rlCardHtml, type RecipeWithGroups } from "./recipeCard";
import { buildSummaryGroups } from "./summaryRecipes";
import { createOffscreenStage, exportNodePng, exportScaleSelectHtml, resolveExportScale, wireExportScaleSelects } from "./domSvgExport";

// ==================== 数据模型 helpers ====================

export const ASSIGNMENT_MODES: AssignmentMode[] = ["2p", "3p", "4p"];
export const ASSIGNMENT_MODE_LABEL_ZH: Record<AssignmentMode, string> = {
  "2p": "双人",
  "3p": "三人",
  "4p": "四人",
};

export function modePlayerCount(mode: AssignmentMode): number {
  return Number(mode.charAt(0)) || 2;
}

/** 模式是否已有任意分工（任一玩家分到 ≥1 道菜）。 */
export function modeHasAssignments(mode: { players: AssignmentPlayer[] } | undefined): boolean {
  return !!mode && mode.players.some((p) => (p?.recipes ?? []).length > 0);
}

export function assignmentHasAny(data: LevelAssignmentData | null): boolean {
  return !!data && ASSIGNMENT_MODES.some((m) => modeHasAssignments(data.modes[m]));
}

export function emptyAssignmentData(): LevelAssignmentData {
  return { schemaVersion: 1, modes: {} };
}

/** 防御性归一：容忍手改/半成品 JSON（缺字段、错类型），guid 过滤为字符串。 */
export function normalizeAssignmentData(raw: unknown): LevelAssignmentData {
  const out = emptyAssignmentData();
  if (!raw || typeof raw !== "object") return out;
  const modes = (raw as LevelAssignmentData).modes;
  if (!modes || typeof modes !== "object") return out;
  for (const m of ASSIGNMENT_MODES) {
    const v = modes[m];
    if (!v || !Array.isArray(v.players)) continue;
    out.modes[m] = {
      players: v.players.map((p) => ({
        recipes: Array.isArray(p?.recipes) ? p.recipes.filter((g): g is string => typeof g === "string") : [],
      })),
    };
  }
  return out;
}

/** 取（必要时创建/补齐）某模式的可变 players 数组，长度恒等于模式人数。 */
export function ensureModePlayers(data: LevelAssignmentData, mode: AssignmentMode): AssignmentPlayer[] {
  const n = modePlayerCount(mode);
  let entry = data.modes[mode];
  if (!entry) {
    entry = { players: [] };
    data.modes[mode] = entry;
  }
  while (entry.players.length < n) entry.players.push({ recipes: [] });
  if (entry.players.length > n) entry.players.length = n;
  return entry.players;
}

/** 保存前裁剪：只保留有分工的模式（全空配置由调用方走 clear 接口）。 */
function pruneAssignmentData(data: LevelAssignmentData): LevelAssignmentData {
  const out = emptyAssignmentData();
  for (const m of ASSIGNMENT_MODES) {
    if (modeHasAssignments(data.modes[m])) out.modes[m] = data.modes[m];
  }
  return out;
}

// ==================== 共享渲染（汇总页区块 + 导出图片） ====================

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

export interface AssignmentCardSource {
  /** guid → 菜谱清单同款完整卡片 HTML（rlCardHtml）；未知 guid 由调用方给占位。 */
  card: (guid: string) => string;
  /** guid → 分组标签（官方类型名 / 自定义「category › subcategory」；与汇总页分组同源）。 */
  groupOf: (guid: string) => string;
  /** 分组标签全序（汇总页分组顺序）；玩家内部子分组按此排序，未列出的按首现垫后。 */
  groupOrder: string[];
}

/** 单个玩家条目按 groupOrder 分组（保持组内原有顺序），返回有序 [label, guids][]。 */
function groupPlayerGuids(guids: string[], src: AssignmentCardSource): Array<[string, string[]]> {
  const byLabel = new Map<string, string[]>();
  for (const g of guids) {
    const label = src.groupOf(g);
    let arr = byLabel.get(label);
    if (!arr) {
      arr = [];
      byLabel.set(label, arr);
    }
    arr.push(g);
  }
  const orderIdx = new Map(src.groupOrder.map((l, i) => [l, i]));
  const known = [...byLabel.keys()].sort((a, b) => {
    const oa = orderIdx.has(a) ? (orderIdx.get(a) as number) : 9999;
    const ob = orderIdx.has(b) ? (orderIdx.get(b) as number) : 9999;
    return oa !== ob ? oa - ob : a.localeCompare(b);
  });
  return known.map((l) => [l, byLabel.get(l) as string[]]);
}

/** 单个模式的分工区块（汇总页展示与导出图片共用）：玩家条目**从上到下**排列，
 *  每个玩家内部按汇总页分组做子分类；卡片为菜谱清单同款完整卡片（.rl-card），
 *  每行最多 3 格（.sum-as-cards）。 */
export function assignmentModeBlockHtml(
  mode: AssignmentMode,
  players: AssignmentPlayer[],
  src: AssignmentCardSource
): string {
  const n = modePlayerCount(mode);
  const rows: string[] = [];
  let total = 0;
  for (let i = 0; i < n; i++) {
    const guids = players[i]?.recipes ?? [];
    total += guids.length;
    const subs = groupPlayerGuids(guids, src)
      .map(
        ([label, list]) => `<div class="sum-as-subgroup">
          <h4 class="sum-as-sub-title">${esc(label)}<span>${list.length}</span></h4>
          <div class="sum-as-cards">${list.map((g) => src.card(g)).join("")}</div>
        </div>`
      )
      .join("");
    rows.push(`<div class="sum-as-col sum-as-p${i % 4}">
      <div class="sum-as-player">玩家 ${i + 1}<span>${guids.length} 道</span></div>
      ${subs || '<div class="sum-as-empty">（未分配）</div>'}
    </div>`);
  }
  return `<div class="sum-as-mode" data-mode="${esc(mode)}">
    <h3 class="sum-as-mode-title">${esc(ASSIGNMENT_MODE_LABEL_ZH[mode])}模式<span>${total} 道</span></h3>
    <div class="sum-as-cols">${rows.join("")}</div>
  </div>`;
}

/** 汇总页分工区块：任一模式有配置才有内容；空配置返回 ""（页面不展示）。 */
export function assignmentOverviewHtml(data: LevelAssignmentData | null, src: AssignmentCardSource): string {
  if (!assignmentHasAny(data)) return "";
  const blocks = ASSIGNMENT_MODES.filter((m) => modeHasAssignments(data!.modes[m])).map((m) =>
    assignmentModeBlockHtml(m, data!.modes[m]!.players, src)
  );
  if (!blocks.length) return "";
  return `<section class="sum-assignment" id="sum-assignment">
    <h2 class="sum-as-title">🧑‍🍳 菜谱分工</h2>
    ${blocks.join("")}
  </section>`;
}

// ==================== 页面骨架（与 levels.ts / dependencies.ts 同款惯例） ====================

function setStatus(msg: string, ok = true): void {
  const el = document.getElementById("m-status");
  if (!el) return;
  el.textContent = msg;
  el.classList.toggle("err", !ok);
  el.classList.toggle("ok", ok && msg.length > 0);
}

function setBusy(msg: string): void {
  const el = document.getElementById("manage-content");
  if (el) el.innerHTML = `<p class="muted">${esc(msg)}</p>`;
  setStatus(msg);
}

function showError(e: unknown): void {
  const msg = e instanceof Error ? e.message : String(e);
  setStatus(msg, false);
  const el = document.getElementById("manage-content");
  if (el) el.innerHTML = `<div class="m-block"><h3>出错</h3><p>${esc(msg)}</p></div>`;
}

function shell(app: HTMLElement, title: string, backLabel?: string, onBack?: () => void): HTMLElement {
  document.body.classList.add("manage-bg");
  app.innerHTML = `
    ${navHtml("manage")}
    <div class="manage-bar">
      ${backLabel ? `<button class="m-btn" id="m-back">← ${esc(backLabel)}</button>` : ""}
      <h1 class="m-title">${esc(title)}</h1>
      <span class="status" id="m-status"></span>
      <span style="flex:1"></span>
      <button class="m-btn" id="m-reload" title="触发 Unity Reload Pseudo Assets">↻ Reload</button>
    </div>
    <div class="manage-content" id="manage-content"></div>
  `;
  wireNav((target) => navigateTo(target));
  const back = document.getElementById("m-back");
  if (back && onBack) back.addEventListener("click", onBack);
  document.getElementById("m-reload")?.addEventListener("click", async () => {
    try {
      await api.reloadPseudo();
      setStatus("已触发 Unity Reload");
    } catch (e) {
      setStatus((e as Error).message, false);
    }
  });
  return document.getElementById("manage-content")!;
}

/** 等待容器内 <img> 解码完成（离屏 lazy 图永不加载，强制 eager + 5s 兜底）。
 *  与 recipeList.ts 同款（该函数未导出，遵循各页自带 helper 的惯例复制一份）。 */
async function waitForImages(root: HTMLElement): Promise<void> {
  const imgs = Array.from(root.querySelectorAll("img"));
  await Promise.all(
    imgs.map(
      (img) =>
        new Promise<void>((resolve) => {
          if (img.complete && img.naturalWidth > 0) return resolve();
          let done = false;
          const finish = (): void => {
            if (done) return;
            done = true;
            resolve();
          };
          img.addEventListener("load", finish, { once: true });
          img.addEventListener("error", finish, { once: true });
          if (img.loading === "lazy") {
            img.loading = "eager";
            const src = img.currentSrc || img.src;
            if (src) img.src = src;
          }
          if (img.complete && img.naturalWidth > 0) return finish();
          window.setTimeout(finish, 5000);
        })
    )
  );
}

// ==================== 分工模式页面 ====================

export interface AssignmentPageOptions {
  /** 返回按钮（默认回汇总页）。 */
  onBack: () => void;
}

/** 跳转到某关的分工模式页（严格路由 /assignment/{set}/{levelId}，与依赖管理同款）。
 *  levelId = LevelInfo 资产所在数据目录名。 */
export function goAssignment(setName: string, levelInfoAssetPath: string): void {
  const parts = levelInfoAssetPath.replace(/\\/g, "/").split("/");
  const levelId = parts.length >= 2 ? parts[parts.length - 2] : "";
  if (!levelId) {
    location.assign("/manage");
    return;
  }
  location.assign(assignmentPath(setName, levelId));
}

/** 由 LevelInfo 资产路径推导 levelId（其所在数据目录名）。 */
export function levelIdFromAssetPath(assetPath: string): string {
  const parts = assetPath.replace(/\\/g, "/").split("/");
  return parts.length >= 2 ? parts[parts.length - 2] : "";
}

/** 深链解析：/{set}/{levelId} → LevelInfo assetPath（与 dependencies.ts 同算法）。 */
export async function resolveLevelAssetPath(setName: string, levelId: string): Promise<string | null> {
  let levels: LevelSummary[] = [];
  try {
    levels = await api.fetchLevels(setName);
  } catch {
    return null;
  }
  const hit =
    levels.find((lv) => (lv.dataDir.split("/").pop() ?? "") === levelId) ??
    levels.find((lv) => levelIdFromAssetPath(lv.assetPath) === levelId);
  return hit?.assetPath ?? null;
}

/** 路由入口（main.ts 深链分发）：解析 /assignment/{set}/{levelId} 并渲染该关的分工页。
 *  返回汇总走整页跳转 /manage/{set}/{levelId}/summary（严格路由，可刷新/分享）；
 *  解析失败回关卡管理。 */
export async function renderAssignmentRouteView(app: HTMLElement): Promise<void> {
  const r = parseRoute();
  if (r.page !== "assignment" || !r.setId || !r.levelId) {
    location.assign("/manage");
    return;
  }
  const assetPath = await resolveLevelAssetPath(r.setId, r.levelId);
  if (!assetPath) {
    location.assign("/manage");
    return;
  }
  await renderLevelAssignment(app, r.setId, assetPath, {
    onBack: () => {
      location.assign(manageLevelSummaryPath(r.setId!, r.levelId!));
    },
  });
}

export async function renderLevelAssignment(
  app: HTMLElement,
  setName: string,
  assetPath: string,
  opts: AssignmentPageOptions
): Promise<void> {
  let dirty = false;
  const guardedBack = (): void => {
    if (dirty && !window.confirm("有未保存的分工修改，确定离开？")) return;
    opts.onBack();
  };
  const content = shell(app, `菜谱分工 · ${setName}`, "返回汇总", guardedBack);
  setBusy("加载分工配置…");

  let detail: LevelDetail;
  try {
    detail = await api.fetchLevelDetail(assetPath);
  } catch (e) {
    showError(e);
    return;
  }
  // 标题落到具体关卡（先显示关卡集名占位，detail 到手后替换）
  const titleEl = document.querySelector<HTMLElement>(".manage-bar .m-title");
  if (titleEl) titleEl.textContent = `菜谱分工 · ${detail.levelNameZH || detail.levelName || levelIdFromAssetPath(assetPath)}`;

  let level: { recipeGuids?: string[] };
  let recipes: RecipeWithGroups[];
  let ingredients: IngredientEntry[];
  let customRecipes: CustomRecipeSummary[];
  let customConfig: CustomRecipeConfig;
  let sets: LevelSetInfo[] = [];
  let saved: LevelAssignmentData | null = null;
  try {
    [level, recipes, ingredients, customRecipes, customConfig, sets, saved] = await Promise.all([
      api.fetchLevelRecipes(detail.sceneAssetPath),
      api.fetchRecipeCatalog(setName),
      api.fetchIngredients().catch(() => [] as IngredientEntry[]),
      api.fetchCustomRecipes(setName).catch(() => [] as CustomRecipeSummary[]),
      api.fetchCustomRecipeConfig(setName).catch(
        () => ({ uidPrefix: 0, nextSequence: 1, categories: [], subcategories: [] }) as CustomRecipeConfig
      ),
      api.fetchSets().catch(() => [] as LevelSetInfo[]),
      api.fetchLevelAssignment(assetPath).catch(() => null),
    ]);
  } catch (e) {
    showError(e);
    return;
  }

  // 本关菜谱池（与汇总页同口径：含全部自定义菜谱，中间产物只留自定义）
  const byGuid = new Map<string, RecipeEntry>(recipes.map((r) => [r.guid, r]));
  for (const c of customRecipes) byGuid.set(c.guid, normalizeCustomRecipeCard(c) as RecipeEntry);
  const selected = (level.recipeGuids ?? [])
    .map((g) => byGuid.get(g))
    .filter((r): r is RecipeEntry => !!r && (!r.intermediate || !!r.isCustom));
  const groups = buildSummaryGroups(selected, customConfig);
  const poolGroups: Array<{ label: string; recipes: RecipeEntry[] }> = [
    ...groups.official.map((g) => ({ label: g.label, recipes: g.recipes })),
    ...groups.custom.flatMap((g) =>
      g.subs
        ? g.subs.map((s) => ({ label: `${g.label} › ${s.label}`, recipes: s.recipes }))
        : [{ label: `自定义 · ${g.label}`, recipes: g.recipes }]
    ),
  ];
  const poolGuids = selected.map((r) => r.guid);
  /** guid → 池分组标签 + 标签全序（分工条目内部子分组与池/汇总页同源）。 */
  const groupLabelByGuid = new Map<string, string>();
  for (const g of poolGroups) for (const r of g.recipes) groupLabelByGuid.set(r.guid, g.label);
  const groupOrder = poolGroups.map((g) => g.label);

  const ingredientName = (id: string): string => ingredients.find((i) => i.id === id)?.nameZh ?? id;
  const recipeIconUrl = (r: RecipeEntry): string =>
    customRecipeIconUrl(r) ?? `/icons/recipes/${encodeURIComponent(r.id)}.png`;
  /** 分工区块/导出图用：菜谱清单同款完整卡片（与汇总页 cardOf 同参数）。 */
  const fullCardOf = (r: RecipeEntry): string =>
    rlCardHtml(r, {
      allRecipes: recipes,
      ingredientName,
      extraBadge: r.group === "levelset" ? "本关" : undefined,
      iconSrc: recipeIconUrl,
    });
  const src: AssignmentCardSource = {
    card: (g) => {
      const r = byGuid.get(g);
      return r ? fullCardOf(r) : `<div class="sum-as-card"><span>${esc(g)}（菜谱已移除）</span></div>`;
    },
    groupOf: (g) => groupLabelByGuid.get(g) ?? "未分组",
    groupOrder,
  };
  // 池条目仍用紧凑行（交互密度优先），只取图标与名称
  const poolIconUrl = (g: string): string => {
    const r = byGuid.get(g);
    return r ? recipeIconUrl(r) : "/icons/_placeholder.png";
  };

  let data = normalizeAssignmentData(saved);
  let activeMode: AssignmentMode = ASSIGNMENT_MODES.find((m) => modeHasAssignments(data.modes[m])) ?? "2p";

  const assignedSet = (mode: AssignmentMode): Set<string> =>
    new Set((data.modes[mode]?.players ?? []).flatMap((p) => p.recipes ?? []));
  const unassignedCount = (mode: AssignmentMode): number => {
    const s = assignedSet(mode);
    return poolGuids.filter((g) => !s.has(g)).length;
  };
  const modeTotal = (mode: AssignmentMode): number =>
    (data.modes[mode]?.players ?? []).reduce((n, p) => n + (p?.recipes?.length ?? 0), 0);

  const title = detail.levelNameZH || detail.levelName || "level";

  // ---- 数据操作（玩家列内去重；跨玩家互不影响） ----

  /** 分给玩家（列内已存在则跳过并提示）。返回是否发生变化。 */
  const assignTo = (mode: AssignmentMode, playerIdx: number, guid: string): boolean => {
    const players = ensureModePlayers(data, mode);
    if (!players[playerIdx]) return false;
    const arr = players[playerIdx].recipes;
    if (arr.includes(guid)) {
      setStatus(`玩家 ${playerIdx + 1} 已分过「${byGuid.get(guid)?.nameZh ?? guid}」，已跳过`, false);
      return false;
    }
    arr.push(guid);
    dirty = true;
    return true;
  };

  /** 从玩家移除。返回是否发生变化。 */
  const removeFrom = (mode: AssignmentMode, playerIdx: number, guid: string): boolean => {
    const players = data.modes[mode]?.players;
    if (!players || !players[playerIdx]) return false;
    const arr = players[playerIdx].recipes;
    const idx = arr.indexOf(guid);
    if (idx < 0) return false;
    arr.splice(idx, 1);
    dirty = true;
    return true;
  };

  // ---- 渲染：左备选池（1/3）+ 右玩家列（拖拽分配） ----

  const renderTabs = (): void => {
    const el = document.getElementById("as-tabs");
    if (!el) return;
    el.innerHTML = ASSIGNMENT_MODES.map(
      (m) => `<button type="button" class="as-tab${m === activeMode ? " active" : ""}" data-mode="${m}">
        ${ASSIGNMENT_MODE_LABEL_ZH[m]}<span>${modeTotal(m)} 道</span>
      </button>`
    ).join("");
  };

  const renderHint = (): void => {
    const el = document.getElementById("as-hint");
    if (!el) return;
    if (poolGuids.length === 0) {
      el.innerHTML = '<span class="muted">该关卡尚未配置菜谱，先在关卡编辑器「已选菜谱」中勾选。</span>';
      return;
    }
    const n = unassignedCount(activeMode);
    el.innerHTML =
      n > 0
        ? `<span class="as-warn">⚠ ${n} 道菜未分配（仍可保存，仅提醒）</span>`
        : '<span class="as-ok">✓ 本模式全部菜谱已分配</span>';
  };

  /** 池条目（draggable）：已分到任一玩家的置灰（.used），但依旧可拖给其他玩家。 */
  const poolItemHtml = (r: RecipeEntry): string =>
    `<div class="as-pool-item" draggable="true" data-guid="${esc(r.guid)}" title="${esc(r.nameZh)} · ${esc(r.id)}">
      <img loading="lazy" src="${esc(poolIconUrl(r.guid))}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">
      <span class="as-pool-name">${esc(r.nameZh)}</span>
      <span class="as-pool-score">⭐ ${r.score ?? 0}</span>
    </div>`;

  const renderPool = (): void => {
    const el = document.getElementById("as-pool");
    if (!el) return;
    el.innerHTML =
      poolGuids.length === 0
        ? '<p class="muted">（本关没有可选菜谱）</p>'
        : poolGroups
            .filter((g) => g.recipes.length > 0)
            .map(
              (g) => `<div class="as-pool-group">
              <h3 class="as-pool-group-title">${esc(g.label)}<span>${g.recipes.length}</span></h3>
              <div class="as-pool-grid">${g.recipes.map(poolItemHtml).join("")}</div>
            </div>`
            )
            .join("");
    refreshPoolStates();
  };

  /** 只更新池条目置灰态（不重建 DOM，保住滚动位置）。 */
  const refreshPoolStates = (): void => {
    const used = assignedSet(activeMode);
    document.querySelectorAll<HTMLElement>(".as-pool-item").forEach((item) => {
      item.classList.toggle("used", used.has(item.dataset.guid ?? ""));
    });
  };

  /** 玩家框内卡片：完整菜谱卡预览（与菜谱清单/汇总页同款 rlCardHtml），
 *  ✕ 悬浮在右上角移除；整个包装层可拖拽（移动/拖回池）。 */
  const colCardHtml = (guid: string, playerIdx: number): string => {
    const r = byGuid.get(guid);
    const inner = r
      ? fullCardOf(r)
      : `<div class="sum-as-card"><span>${esc(guid)}（菜谱已移除）</span></div>`;
    return `<div class="as-col-card" draggable="true" data-guid="${esc(guid)}" data-from="${playerIdx}" title="拖到另一玩家框 = 移动；拖回左侧备选区或点 ✕ = 移除">
      ${inner}
      <button type="button" class="as-card-x" data-guid="${esc(guid)}" data-from="${playerIdx}" title="从该玩家移除">✕</button>
    </div>`;
  };

  const renderColumns = (): void => {
    const el = document.getElementById("as-players");
    if (!el) return;
    const players = ensureModePlayers(data, activeMode);
    const n = modePlayerCount(activeMode);
    const cols: string[] = [];
    for (let i = 0; i < n; i++) {
      const guids = players[i]?.recipes ?? [];
      cols.push(`<div class="as-col as-p${i % 4}" data-p="${i}">
        <div class="as-col-head">玩家 ${i + 1}<span>${guids.length} 道</span></div>
        <div class="as-col-cards">
          ${guids.map((g) => colCardHtml(g, i)).join("") || '<div class="as-col-empty">把左侧菜谱拖到这里</div>'}
        </div>
      </div>`);
    }
    el.innerHTML = cols.join("");
  };

  const updateSaveBtn = (): void => {
    const btn = document.getElementById("as-save") as HTMLButtonElement | null;
    if (btn) {
      btn.disabled = !dirty;
      btn.textContent = dirty ? "💾 保存 *" : "💾 保存";
    }
  };

  const renderAll = (): void => {
    renderTabs();
    renderHint();
    renderPool();
    renderColumns();
    updateSaveBtn();
  };

  /** 拖拽/移除后的局部刷新：池置灰态 + 列 + 计数（池 DOM 不重建，滚动位置保留）。 */
  const refreshAfterChange = (changed: boolean): void => {
    refreshPoolStates();
    renderColumns();
    renderTabs();
    renderHint();
    updateSaveBtn();
    // 成功的分配/移除要清掉上一次「已跳过」之类的临时提示
    if (changed) setStatus(`本关菜谱 ${poolGuids.length} 道 · 从左侧拖到右侧玩家列分配（一道菜可分给多个玩家，玩家列内自动去重）`);
  };

  content.innerHTML = `
    <div class="as-toolbar">
      <div class="as-tabs" id="as-tabs"></div>
      <div class="as-actions">
        <button type="button" class="m-btn" id="as-export-one" title="导出当前模式的分工图">🖼 导出当前</button>
        <button type="button" class="m-btn" id="as-export-all" title="已配置的模式竖向拼成一张长图">🖼 导出全部（长图）</button>
        ${exportScaleSelectHtml("as-export-scale")}
        <button type="button" class="m-btn" id="as-clear">🗑 清空配置</button>
        <button type="button" class="m-btn primary" id="as-save">💾 保存</button>
      </div>
    </div>
    <div class="as-hint" id="as-hint"></div>
    <div class="as-workspace">
      <div class="as-pool" id="as-pool"></div>
      <div class="as-players" id="as-players"></div>
    </div>
  `;
  wireExportScaleSelects();
  renderAll();
  setStatus(`本关菜谱 ${poolGuids.length} 道 · 从左侧拖到右侧玩家列分配（一道菜可分给多个玩家，玩家列内自动去重）`);

  // ---- 拖拽交互（HTML5 DnD；拖拽载荷同时写 dataTransfer 与模块变量兜底） ----

  let dragGuid = "";
  let dragFrom = -1; // -1 = 来自左侧备选池；≥0 = 来自该玩家列

  const workspace = document.getElementById("as-workspace") ?? content;

  workspace.addEventListener("dragstart", (e) => {
    const target = (e.target as HTMLElement).closest<HTMLElement>(".as-pool-item, .as-col-card");
    if (!target) return;
    dragGuid = target.dataset.guid ?? "";
    dragFrom = target.classList.contains("as-col-card") ? Number(target.dataset.from ?? "-1") : -1;
    try {
      e.dataTransfer?.setData("text/plain", `${dragFrom >= 0 ? dragFrom : "pool"}:${dragGuid}`);
    } catch {
      /* 某些环境禁止读 dataTransfer.setData（拖拽期间），模块变量兜底 */
    }
    if (e.dataTransfer) {
      e.dataTransfer.effectAllowed = "copyMove";
    }
    target.classList.add("dragging");
  });

  workspace.addEventListener("dragend", () => {
    dragGuid = "";
    dragFrom = -1;
    workspace.querySelectorAll(".dragging, .drag-over").forEach((el) => el.classList.remove("dragging", "drag-over"));
  });

  // drop 目标：玩家列（分配/移动）与左池（移除）
  workspace.addEventListener("dragover", (e) => {
    const col = (e.target as HTMLElement).closest<HTMLElement>(".as-col");
    const pool = (e.target as HTMLElement).closest<HTMLElement>(".as-pool");
    if (!col && !pool) return;
    e.preventDefault();
    if (e.dataTransfer) e.dataTransfer.dropEffect = "move";
    workspace.querySelectorAll(".drag-over").forEach((el) => el.classList.remove("drag-over"));
    (col ?? pool)?.classList.add("drag-over");
  });

  workspace.addEventListener("drop", (e) => {
    const col = (e.target as HTMLElement).closest<HTMLElement>(".as-col");
    const pool = (e.target as HTMLElement).closest<HTMLElement>(".as-pool");
    if (!col && !pool) return;
    e.preventDefault();
    // 优先读模块变量（dataTransfer 在部分浏览器 dragover 读取受限）
    let guid = dragGuid;
    let from = dragFrom;
    if (!guid && e.dataTransfer) {
      const raw = e.dataTransfer.getData("text/plain") ?? "";
      const idx = raw.indexOf(":");
      if (idx > 0) {
        const src = raw.slice(0, idx);
        guid = raw.slice(idx + 1);
        from = src === "pool" ? -1 : Number(src);
      }
    }
    if (!guid || !byGuid.has(guid)) return;

    if (col) {
      const target = Number(col.dataset.p ?? "0");
      if (from === target) return; // 拖回原列 = 无操作
      let changed = false;
      if (from >= 0) changed = removeFrom(activeMode, from, guid) || changed; // 玩家卡片 → 移动语义
      changed = assignTo(activeMode, target, guid) || changed;
      refreshAfterChange(changed);
    } else if (pool && from >= 0) {
      const removed = removeFrom(activeMode, from, guid); // 玩家卡片拖回池 = 移除
      refreshAfterChange(removed);
    }
  });

  // 玩家卡片 ✕ 点击移除（事件委托；点 ✕ 不触发卡片拖拽）
  workspace.addEventListener("click", (e) => {
    const x = (e.target as HTMLElement).closest<HTMLButtonElement>(".as-card-x");
    if (!x) return;
    const guid = x.dataset.guid ?? "";
    const from = Number(x.dataset.from ?? "0");
    if (guid && removeFrom(activeMode, from, guid)) refreshAfterChange(true);
  });

  // ---- 顶栏按钮 ----

  document.getElementById("as-tabs")?.addEventListener("click", (e) => {
    const btn = (e.target as HTMLElement).closest<HTMLElement>(".as-tab");
    if (!btn) return;
    const m = btn.dataset.mode as AssignmentMode;
    if (!m || m === activeMode) return;
    if (dirty && !window.confirm("当前模式有未保存的修改，切换模式不会丢失（配置保存在内存），确认切换？")) return;
    activeMode = m;
    renderAll();
  });

  document.getElementById("as-save")?.addEventListener("click", async () => {
    const btn = document.getElementById("as-save") as HTMLButtonElement | null;
    if (btn) btn.disabled = true;
    setStatus("保存中…");
    try {
      const payload = pruneAssignmentData(data);
      if (Object.keys(payload.modes).length === 0) {
        await api.clearLevelAssignment(assetPath);
        data = emptyAssignmentData();
        setStatus("分工为空，已清除配置文件");
      } else {
        await api.saveLevelAssignment(assetPath, payload);
        data = payload;
        setStatus("已保存分工配置");
      }
      dirty = false;
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      renderAll();
    }
  });

  document.getElementById("as-clear")?.addEventListener("click", async (e) => {
    const btn = e.currentTarget as HTMLButtonElement | null;
    if (!btn) return;
    // 两步确认：首点布防防误触（状态存 dataset，事件只绑一次）
    if (btn.dataset.armed === "1") {
      btn.dataset.armed = "";
      btn.classList.remove("danger");
      btn.textContent = "🗑 清空配置";
      try {
        setStatus("清空中…");
        await api.clearLevelAssignment(assetPath);
        data = emptyAssignmentData();
        dirty = false;
        renderAll();
        setStatus("已清空分工配置");
      } catch (err) {
        setStatus((err as Error).message, false);
      }
      return;
    }
    btn.dataset.armed = "1";
    btn.classList.add("danger");
    btn.textContent = "确认清空？（不可撤销）";
    window.setTimeout(() => {
      if (btn.isConnected && btn.dataset.armed === "1") {
        btn.dataset.armed = "";
        btn.classList.remove("danger");
        btn.textContent = "🗑 清空配置";
      }
    }, 4000);
  });

  const exportPng = async (modes: AssignmentMode[]): Promise<void> => {
    if (modes.length === 0) {
      setStatus("暂无已配置的分工可导出，先拖几道菜给玩家再试", false);
      return;
    }
    setStatus("正在生成图片…");
    let stage: HTMLDivElement | null = null;
    try {
      const date = new Date().toISOString().slice(0, 10);
      const sub = modes.length === 1 ? `${ASSIGNMENT_MODE_LABEL_ZH[modes[0]]}模式` : "全部模式";
      const author = sets.find((s) => s.setName === setName)?.author || "—";
      // 头部与汇总页同款：关卡名 → 副标题（模式/日期）→ 作者 → 关卡截图，然后才是分工。
      const shotSrc = detail.screenshotPath ? api.imageFloorUrl(detail.screenshotPath) : "";
      const shotHtml = shotSrc
        ? `<img class="sum-shot-img" src="${esc(shotSrc)}" alt="关卡截图">`
        : '<div class="sum-shot-empty">（未上传关卡截图）</div>';
      stage = createOffscreenStage(1280, "sum-page as-export-stage");
      stage.innerHTML = `
        <header class="sum-head">
          <h1 class="sum-title">${esc(title)}</h1>
          <div class="sum-sub">${esc(detail.levelName)} · ${esc(detail.sceneName)} · 菜谱分工 · ${esc(sub)} · ${esc(date)}</div>
          <div class="sum-author">作者：${esc(author)}</div>
        </header>
        <div class="sum-shot">${shotHtml}</div>
        ${modes.map((m) => assignmentModeBlockHtml(m, ensureModePlayers(data, m), src)).join("")}`;
      await waitForImages(stage);
      const file =
        modes.length === 1 ? `${title}_分工_${ASSIGNMENT_MODE_LABEL_ZH[modes[0]]}.png` : `${title}_分工.png`;
      const scale = resolveExportScale();
      const used = await exportNodePng(stage, file, { scale });
      setStatus(used < scale ? `已导出 PNG（超画布上限，自动降至 ${used}x）` : "已导出 PNG");
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      if (stage) stage.remove();
    }
  };

  document.getElementById("as-export-one")?.addEventListener("click", () => {
    if (!modeHasAssignments(data.modes[activeMode])) {
      setStatus(`当前「${ASSIGNMENT_MODE_LABEL_ZH[activeMode]}」模式还没有任何分工`, false);
      return;
    }
    void exportPng([activeMode]);
  });

  document.getElementById("as-export-all")?.addEventListener("click", () => {
    void exportPng(ASSIGNMENT_MODES.filter((m) => modeHasAssignments(data.modes[m])));
  });
}
