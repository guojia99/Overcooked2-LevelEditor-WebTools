/**
 * burgerMaker.ts —— 汉堡组装工作台（/custom-recipes/burger-maker）。
 */
import { navHtml, wireNav } from "./nav";
import { closeModal, openModal } from "./modals";
import * as api from "./api";
import { showBusy, hideBusy } from "./busy";
import { burgerPath, fillingPath, parseRoute } from "./route";
import { openRecipeModelPreview } from "./recipeModelPreview";
import { foodIconAttrs, type FoodIconSource } from "./foodIconChain";
import { rlCardHtml, type RecipeWithGroups } from "./recipeCard";
import type {
  BurgerCandidate,
  BurgerDefinitionList,
  LevelSetInfo,
  RecipeEntry,
} from "./types";

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function showError(e: unknown): void {
  setStatus(false, (e as Error).message ?? String(e));
}

interface MakerState {
  setName: string;
  category: string;
  /** 后台创建仍需要组装定义路径（自动选取，UI 不展示）。 */
  defPath: string;
  /** 完整堆叠（含汉堡面包 + 夹心，顺序 = compositionSOs）。 */
  stack: string[];
  loadedProductId: string;
  loadedProductAssetPath: string;
  loadedRecipeName: string;
  loadedNameZh: string;
  loadedNameEn: string;
  loadedScore: number;
  /** 用户是否手动改过分数：改过就不再被「建议分数」覆盖。 */
  scoreTouched: boolean;
}

/** 推荐的食材数上限（含汉堡面包；复合层按其叶食材数计入，如培根煎蛋香肠早餐 = 3）。
 *  不是硬限制 —— 超过只提醒：游戏订单 UI 的食材图标是横向排布的，
 *  食材过多会把卡片撑爆/图标溢出（见 /docs/burger-filling-overflow.png 实拍）。 */
const FILLING_SOFT_LIMIT = 32;

function setStatus(ok: boolean, msg: string): void {
  const el = document.getElementById("bm-status");
  if (!el) return;
  el.textContent = msg;
  el.classList.toggle("err", !ok);
  el.classList.toggle("ok", ok && msg.length > 0);
}

/** 面包层判定：后端给的 buns 候选集优先；兜底按 id 含 "choppedbun"
 *  （核心 ChoppedBunSO / DLC02_ChoppedBun / DLC8 dlc08_choppedbun 三种写法全覆盖，
 *  历史别名 dlc08_bun 已统一改名，无需特判）。 */
function isBunId(id: string, bunIds: Set<string>): boolean {
  if (bunIds.has(id)) return true;
  return /ChoppedBun/i.test(id);
}

function allCandidates(data: BurgerDefinitionList): BurgerCandidate[] {
  return [...(data.buns ?? []), ...(data.candidates ?? [])];
}

function findCandidate(cands: BurgerCandidate[], id: string): BurgerCandidate | undefined {
  return cands.find((x) => x.id === id);
}

function candidateName(cands: BurgerCandidate[], id: string): string {
  const c = findCandidate(cands, id);
  return c ? c.nameZh : id;
}

function candidateEn(cands: BurgerCandidate[], id: string): string {
  const c = findCandidate(cands, id);
  return c?.nameEn?.trim() || id;
}

/** 候选 → 共享图标降级链（实现见 foodIconChain.ts）。 */
function candIconSource(c: BurgerCandidate): FoodIconSource {
  return {
    id: c.id,
    assetPath: c.assetPath,
    iconState: c.iconState,
    iconFallbackIds: c.iconFallbackIds,
    isCustom: c.kind === "custom",
  };
}

function candIconAttrs(c: BurgerCandidate): string {
  return foodIconAttrs(candIconSource(c));
}

function layerIcon(c: BurgerCandidate | undefined): string {
  if (!c) {
    return `<img class="food-icon bm-layer-icon" src="/icons/_placeholder.png" alt="">`;
  }
  return `<img class="food-icon bm-layer-icon" loading="lazy" alt="" ${candIconAttrs(c)}>`;
}

function pickCardIcon(c: BurgerCandidate): string {
  return `<img class="food-icon" loading="lazy" alt="" ${candIconAttrs(c)}>`;
}

/** 候选徽标：模型状态 / DLC 归属 / bundle 缺失 / 推荐。 */
function candBadges(c: BurgerCandidate): string {
  const out: string[] = [];
  if (c.recommended) out.push('<span class="bm-badge ok" title="commonW2 汉堡大全既有夹心">荐</span>');
  if (c.modelState === "none" && c.kind === "custom")
    out.push('<span class="bm-badge warn" title="没有堆叠模型，游戏里该层不可见">⚠无模型</span>');
  if (c.modelState === "own")
    out.push('<span class="bm-badge ok" title="使用该菜谱自带的模型">自带模型</span>');
  if (c.matchlistKey) out.push(`<span class="bm-badge dlc" title="需要 ${esc(c.matchlistKey)} 匹配表（保存菜谱时自动补）">${esc(c.matchlistKey)}</span>`);
  if (c.bundleAvailable === false)
    out.push('<span class="bm-badge err" title="bundle 未构建，选用会导致运行时崩溃">bundle缺失</span>');
  return out.join("");
}

/** 候选卡片。
 *  卡片本身 = 「+1」；右上角计数徽标显示**当前堆叠中该项的数量**，数量 >0 时
 *  出现「−」按钮可减一 —— 原先点一次只是静默往堆叠尾部追加，用户看不到任何反馈。 */
function renderCandCard(c: BurgerCandidate, count: number): string {
  const en = c.nameEn?.trim();
  const searchKey = (c.nameZh + " " + (en ?? "") + " " + c.id).toLowerCase();
  const disabled = c.bundleAvailable === false;
  const eye = c.previewable
    ? `<span class="bm-cand-eye" data-preview-path="${esc(c.assetPath)}" data-preview-name="${esc(c.nameZh)}" title="3D 预览该夹心模型">👁</span>`
    : "";
  return `<div class="bm-pick-cell${count > 0 ? " picked" : ""}" data-cell-id="${esc(c.id)}">
    <button type="button" class="pick-card bm-pick-card${disabled ? " disabled" : ""}" ${disabled ? "disabled" : ""} data-id="${esc(c.id)}" data-name="${esc(searchKey)}" title="${esc(c.id)}　点击 +1">
      <span class="pc-head">${pickCardIcon(c)}<span class="pc-name">${esc(c.nameZh)}${en ? ` <span class="muted pc-en">${esc(en)}</span>` : ""}</span>${eye}</span>
      <span class="muted small">${esc(c.id)}</span>
      <span class="bm-badges">${candBadges(c)}</span>
    </button>
    <span class="bm-cand-count${count > 0 ? "" : " zero"}" data-count-for="${esc(c.id)}">×${count}</span>
    <button type="button" class="bm-cand-minus${count > 0 ? "" : " hidden"}" data-minus="${esc(c.id)}" title="移除一层">−</button>
  </div>`;
}

/** 夹心预览色：按 id / 中英文名关键词匹配，未命中则按 id 哈希取稳定色相。 */
interface FillPalette {
  bg1: string;
  bg2: string;
  border: string;
}

const FILL_COLOR_RULES: { test: RegExp; palette: FillPalette }[] = [
  {
    test: /meat|patty|beef|steak|pork|chicken|sausage|bacon|fried|grill|肉|排|鸡|猪|肠|培根|虾|fish|鱼|蟹|crab|shrimp/i,
    palette: { bg1: "rgba(160,82,45,0.62)", bg2: "rgba(101,50,14,0.5)", border: "rgba(210,120,70,0.75)" },
  },
  {
    test: /cheese|芝士|奶酪/i,
    palette: { bg1: "rgba(244,208,63,0.62)", bg2: "rgba(212,172,13,0.48)", border: "rgba(255,220,90,0.8)" },
  },
  {
    test: /lettuce|生菜|菜叶|spinach|菠菜|cabbage|卷心菜/i,
    palette: { bg1: "rgba(88,214,141,0.58)", bg2: "rgba(39,174,96,0.45)", border: "rgba(130,235,170,0.75)" },
  },
  {
    test: /tomato|番茄|tomatoes/i,
    palette: { bg1: "rgba(231,76,60,0.58)", bg2: "rgba(192,57,43,0.48)", border: "rgba(255,120,100,0.78)" },
  },
  {
    test: /onion|洋葱|shallot/i,
    palette: { bg1: "rgba(195,155,211,0.55)", bg2: "rgba(142,68,173,0.42)", border: "rgba(210,170,230,0.72)" },
  },
  {
    test: /pickle|cucumber|黄瓜|gherkin/i,
    palette: { bg1: "rgba(130,224,170,0.55)", bg2: "rgba(30,132,73,0.45)", border: "rgba(150,240,190,0.72)" },
  },
  {
    test: /pineapple|菠萝|banana|香蕉|melon|瓜|fruit|果/i,
    palette: { bg1: "rgba(249,231,159,0.58)", bg2: "rgba(241,196,15,0.45)", border: "rgba(255,230,120,0.78)" },
  },
  {
    test: /mushroom|蘑菇|fung/i,
    palette: { bg1: "rgba(210,215,211,0.58)", bg2: "rgba(149,165,166,0.45)", border: "rgba(230,235,230,0.72)" },
  },
  {
    test: /egg|蛋|omelet/i,
    palette: { bg1: "rgba(255,248,220,0.62)", bg2: "rgba(245,230,180,0.5)", border: "rgba(255,250,210,0.78)" },
  },
  {
    test: /potato|土豆|chip|薯|fries/i,
    palette: { bg1: "rgba(245,203,167,0.58)", bg2: "rgba(211,152,90,0.48)", border: "rgba(255,210,150,0.75)" },
  },
  {
    test: /pepper|椒|chili|jalapeno/i,
    palette: { bg1: "rgba(255,120,80,0.55)", bg2: "rgba(192,57,43,0.42)", border: "rgba(255,150,110,0.72)" },
  },
  {
    test: /mixed|搅拌|blend/i,
    palette: { bg1: "rgba(187,143,206,0.52)", bg2: "rgba(125,90,140,0.42)", border: "rgba(200,160,215,0.7)" },
  },
];

function fillerPalette(id: string, c?: BurgerCandidate): FillPalette {
  const hay = `${id} ${c?.nameZh ?? ""} ${c?.nameEn ?? ""}`;
  for (const rule of FILL_COLOR_RULES) {
    if (rule.test.test(hay)) return rule.palette;
  }
  let h = 0;
  for (let i = 0; i < id.length; i++) h = (h * 31 + id.charCodeAt(i)) >>> 0;
  const hue = h % 360;
  return {
    bg1: `hsla(${hue}, 48%, 42%, 0.55)`,
    bg2: `hsla(${hue}, 48%, 32%, 0.45)`,
    border: `hsla(${hue}, 55%, 58%, 0.68)`,
  };
}

function fillerSliceStyle(id: string, c?: BurgerCandidate): string {
  const p = fillerPalette(id, c);
  return `--bm-fill-bg1:${p.bg1};--bm-fill-bg2:${p.bg2};--bm-fill-border:${p.border}`;
}

function stackHasBun(stack: string[], bunIds: Set<string>): boolean {
  return stack.some((id) => isBunId(id, bunIds));
}

/** 预览片：一个 ChoppedBun id → 底片 + …夹心… + 顶片（同 id）。 */
function buildPreviewSlices(
  stack: string[],
  bunIds: Set<string>
): { id: string; part?: "bottom" | "top" }[] {
  const slices: { id: string; part?: "bottom" | "top" }[] = [];
  let bunBottomDone = false;
  for (const id of stack) {
    if (isBunId(id, bunIds)) {
      if (!bunBottomDone) {
        slices.push({ id, part: "bottom" });
        bunBottomDone = true;
      }
    } else {
      slices.push({ id });
    }
  }
  const firstBun = stack.find((id) => isBunId(id, bunIds));
  if (firstBun) slices.push({ id: firstBun, part: "top" });
  return slices;
}

export async function renderBurgerMakerView(app: HTMLElement): Promise<void> {
  document.body.classList.add("manage-bg");
  app.innerHTML = `
    ${navHtml("custom-recipes")}
    <div class="manage-bar">
      <h1 class="m-title">🍔 汉堡组装工作台 <span class="muted" style="font-size:13px;font-weight:normal">产出归属当前关卡集</span></h1>
      <span class="status" id="bm-status"></span>
      <span style="flex:1"></span>
      <a class="m-btn" href="/custom-recipes">← 返回菜谱管理</a>
    </div>
    <div class="manage-content" id="bm-content"><p class="muted">加载中…</p></div>
  `;
  wireNav();

  const content = document.getElementById("bm-content")!;

  let sets: LevelSetInfo[] = [];
  try {
    sets = await api.fetchSets();
  } catch {
    /* fetchBurgerDefinitions 会再次暴露桥接错误 */
  }
  if (sets.length === 0) {
    content.innerHTML = `<div class="m-block"><h3>没有可用关卡集</h3>
      <p class="muted">汉堡产出的菜谱归属关卡集。请先在「关卡管理」创建关卡集，并打开过一次其自定义菜谱页（初始化配置）。</p></div>`;
    return;
  }
  // 严格路由 /custom-recipes/burger-maker/{set}[/{burgerId}]：按 URL 定位关卡集与待载入的成品汉堡
  const route = parseRoute();
  const routeSet = route.page === "burger-maker" ? route.setId ?? "" : "";
  const routeBurgerId = route.page === "burger-maker" ? route.burgerId ?? "" : "";
  const routeSetValid = routeSet !== "" && sets.some((s) => s.setName === routeSet);
  const state: MakerState = {
    setName: routeSetValid ? routeSet : sets[0].setName,
    category: "burger_local",
    defPath: "",
    stack: [],
    loadedProductId: "",
    loadedProductAssetPath: "",
    loadedRecipeName: "",
    loadedNameZh: "",
    loadedNameEn: "",
    loadedScore: 0,
    scoreTouched: false,
  };
  // 裸路径 / 非法集：replaceState 成严格 URL（本页只 replace 不 push，不产生历史噪声）
  history.replaceState(null, "", burgerPath(state.setName, routeSetValid ? routeBurgerId || undefined : undefined));


  /** 菜谱目录：给 rlCardHtml 做组成展开与工序推导的上下文（取不到则降级为空）。 */
  let catalog: RecipeEntry[] = [];
  let data: BurgerDefinitionList;
  try {
    showBusy("加载汉堡数据…");
    // 菜谱目录用于卡片预览的组成展开/工序推导；取不到就降级（卡片仍出，只是工序框简化）
    const [defs, cat] = await Promise.all([
      api.fetchBurgerDefinitions(state.setName),
      api.fetchRecipeCatalog(state.setName).catch(() => [] as RecipeEntry[]),
    ]);
    data = defs;
    catalog = cat;
  } catch (e) {
    hideBusy();
    content.innerHTML = `<div class="m-block"><h3>加载失败</h3><p class="muted">${esc(
      (e as Error).message
    )}</p><p class="muted">请确认 Unity 编辑器已启动且桥接服务运行中（Tools → Layout Editor → 启动服务）。</p></div>`;
    return;
  } finally {
    hideBusy();
  }

  if (data.definitions.length === 0) {
    content.innerHTML = `<div class="m-block"><h3>未找到汉堡组装定义</h3>
      <p class="muted">commonW2 共享库中没有 CustomRecipeOptionalBurgerSO 资产。请先在 Unity 中完成 Burger大全 迁移。</p></div>`;
    return;
  }

  const pickShellDefinition = () =>
    data.definitions.find((d) => d.id === "OptionalBurger") ??
    data.definitions.find((d) => d.id === "ChickenBurgerAssembly") ??
    data.definitions[0];

  state.defPath = pickShellDefinition()?.assetPath ?? "";

  function bunIdSet(): Set<string> {
    return new Set((data.buns ?? []).map((b) => b.id));
  }

  /** 已选订单图标的本地预览 URL（ObjectURL）：卡片预览与图标缩略图共用。 */
  let iconPreviewUrl = "";

  /** 「查看超规格实拍」按钮随堆叠区重绘而重建，需每次重新绑定。 */
  function wireOverflowButton(): void {
    document.getElementById("bm-show-overflow")?.addEventListener("click", () => openOverflowExample());
  }

  /** 候选面板过滤：只看推荐。
   *  「无模型」的自定义菜谱由**后端**默认过滤（选了运行时也看不见），
   *  勾「显示无模型」会带 includeModelless=1 重新请求一次。 */
  let candOnlyRecommended = false;
  let candShowModelless = false;

  function candPasses(c: BurgerCandidate): boolean {
    if (candOnlyRecommended && !c.recommended) return false;
    return true;
  }

  /** 当前堆叠中某候选的层数（候选卡片的计数徽标）。 */
  function stackCountOf(id: string): number {
    let n = 0;
    for (const x of state.stack) if (x === id) n++;
    return n;
  }

  /** 只刷新候选面板里的计数徽标与「−」按钮，不重建列表
   *  （重建会丢失滚动位置与搜索过滤状态）。 */
  function refreshCandCounts(): void {
    document.querySelectorAll<HTMLElement>("[data-count-for]").forEach((el) => {
      const id = el.dataset.countFor ?? "";
      const n = stackCountOf(id);
      el.textContent = `×${n}`;
      el.classList.toggle("zero", n === 0);
      el.closest(".bm-pick-cell")?.classList.toggle("picked", n > 0);
    });
    document.querySelectorAll<HTMLElement>("[data-minus]").forEach((el) => {
      el.classList.toggle("hidden", stackCountOf(el.dataset.minus ?? "") === 0);
    });
    const countEl = document.getElementById("bm-pick-count");
    if (countEl) countEl.textContent = `当前堆叠 ${totalIngredientCount()} 食材 · ${state.stack.length} 层`;
  }

  /** 候选分组与优先级（从常用到少用，减少翻找）：
   *   1. 🍞 汉堡皮
   *   2. ⭐ 常用夹心 —— commonW2 汉堡大全实际用过的夹心（recommended），最可能被选
   *   3. 🥬 官方食材 —— 生菜/番茄/芝士这类默认食材
   *   4. 🥩 中间产物 —— commonW2 等自定义 0 分半成品（未被 commonW2 汉堡用过的）
   *   5. 🍽 官方菜谱节点 —— 常态为空：官方成品菜不再作为夹心候选，
   *        Burger大全在用的那 2 条早餐拼盘会归入第 2 组
   *   6. 🍔 成品菜 —— 常态为空：自定义 score>0 的成品（汤/披萨/月饼…）同样不作夹心 */
  function renderCandListHtml(): string {
    const cands = (data.candidates ?? []).filter(candPasses);
    const rec = cands.filter((c) => c.recommended);
    const rest = cands.filter((c) => !c.recommended);
    const groups: [string, BurgerCandidate[]][] = [
      ["bun", data.buns ?? []],
      ["recommended", rec],
      ["ingredient", rest.filter((c) => c.kind === "ingredient")],
      ["custom-inter", rest.filter((c) => c.kind === "custom" && (c.score ?? 0) <= 0)],
      ["official-recipe", rest.filter((c) => c.kind === "official-recipe")],
      ["custom-done", rest.filter((c) => c.kind === "custom" && (c.score ?? 0) > 0)],
    ];
    const label: Record<string, string> = {
      bun: "🍞 汉堡皮",
      recommended: "⭐ 常用夹心（Burger大全在用）",
      ingredient: "🥬 官方食材",
      "custom-inter": "🥩 中间产物 / 夹心（自定义菜谱）",
      "official-recipe": "🍽 官方菜谱节点（仅「显示全部候选」时出现）",
      "custom-done": "🍔 成品菜（仅「显示全部候选」时出现）",
    };
    const sections = groups
      .map(([k, arr]) => {
        if (arr.length === 0) return "";
        return `<div class="bm-pick-section" data-bm-group="${esc(k)}">
          <div class="bm-pick-section-title">${label[k]} <span class="rl-cnt">${arr.length}</span></div>
          <div class="pick-grid bm-pick-grid">${arr.map((c) => renderCandCard(c, stackCountOf(c.id))).join("")}</div>
        </div>`;
      })
      .join("");
    return sections || '<p class="muted">没有符合当前筛选的候选。</p>';
  }

  // ---------------------------------------------------------------- 菜谱卡片实时预览
  //
  // 与「新建菜谱」编辑器上方的「组装效果（实时预览）」同款：都用共享的 rlCardHtml，
  // 传入一个**临时拼出来的** RecipeWithGroups（不落盘），这样工序分组/食材展开/
  // 徽标逻辑与菜谱管理页、菜谱清单页完全一致。

  /** 把堆叠层递归展开成叶食材 id（子菜谱 → 其 ingredients）。 */
  function expandLeafIds(ids: string[]): string[] {
    const byId = new Map<string, RecipeEntry>();
    for (const r of catalog) if (r.id && !byId.has(r.id)) byId.set(r.id, r);
    const out: string[] = [];
    const walk = (id: string, depth: number): void => {
      if (depth > 6) return;
      const sub = byId.get(id);
      const subIngs = sub?.ingredients ?? [];
      if (sub && subIngs.length > 0) {
        for (const x of subIngs) walk(x, depth + 1);
        return;
      }
      out.push(id);
    };
    for (const id of ids) walk(id, 0);
    return out;
  }

  function renderCardPreview(): string {
    const rname = state.loadedRecipeName;
    const zh = state.loadedNameZh;
    const en = state.loadedNameEn;
    const score = state.loadedScore > 0 ? state.loadedScore : suggestedScore();
    const preview: RecipeWithGroups = {
      guid: "",
      id: rname || "preview",
      nameZh: zh || rname || "未命名汉堡",
      nameEn: en || undefined,
      assetPath: "",
      isCustom: true,
      group: "levelset",
      score,
      intermediate: false,
      // 汉堡是 Composite：自身无烹饪步骤，各层由 deriveCompositionGroups 分别成组
      cookingStep: undefined,
      compositionIds: state.stack.slice(),
      ingredients: expandLeafIds(state.stack),
      type: "burger",
    };
    const cands = allCandidates(data);
    const nameOf = (id: string): string => findCandidate(cands, id)?.nameZh ?? id;
    try {
      return rlCardHtml(preview, {
        allRecipes: catalog as RecipeWithGroups[],
        ingredientName: nameOf,
        iconSrc: iconPreviewUrl
          ? () => iconPreviewUrl
          : state.loadedProductAssetPath
            ? () => `/api/custom-recipes/icon?assetPath=${encodeURIComponent(state.loadedProductAssetPath)}`
            : undefined,
      });
    } catch (e) {
      return `<p class="muted">卡片预览失败：${esc((e as Error).message)}</p>`;
    }
  }

  /** 只重绘卡片预览区（堆叠/名称/分数/图标变化时调用）。 */
  function refreshCardPreview(): void {
    const el = document.getElementById("bm-card-preview");
    if (el) el.innerHTML = renderCardPreview();
  }

  function renderStackSection(): string {
    const cands = allCandidates(data);
    const buns = bunIdSet();
    const stackRows = state.stack
      .map((id, i) => {
        const c = findCandidate(cands, id);
        const bunTag = isBunId(id, buns) ? ' <span class="muted small">（上下两片）</span>' : "";
        const warn =
          c && c.kind === "custom" && c.modelState === "none"
            ? ' <span class="bm-badge warn" title="该夹心没有堆叠模型，游戏里这一层看不见">⚠无模型</span>'
            : "";
        const dlc = c?.matchlistKey
          ? ` <span class="bm-badge dlc" title="需要 ${esc(c.matchlistKey)} 匹配表（保存菜谱时自动补）">${esc(c.matchlistKey)}</span>`
          : "";
        const ingN = ingredientCountOf(id);
        const ingTag =
          ingN > 1
            ? ` <span class="bm-badge" title="复合层：这一层展开为 ${ingN} 个食材，按 ${ingN} 计入食材数与 ${FILLING_SOFT_LIMIT} 上限">×${ingN} 食材</span>`
            : "";
        const eye = c?.previewable
          ? `<button class="m-btn small bm-layer-preview" data-preview-path="${esc(c.assetPath)}" data-preview-name="${esc(c.nameZh)}" title="3D 预览这一层的模型">👁</button>`
          : "";
        return `<div class="bm-layer" draggable="true" data-i="${i}">
          <span class="bm-drag-handle" title="按住拖动排序">⋮⋮</span>
          ${layerIcon(c)}
          <span class="muted small">${i + 1}</span>
          <span class="bm-layer-name">${esc(candidateName(cands, id))}${bunTag}${warn}${dlc}${ingTag}
            <span class="muted small">${esc(candidateEn(cands, id))} · ${esc(id)}</span></span>
          ${eye}
          <button class="m-btn small bm-up" data-i="${i}" ${i === 0 ? "disabled" : ""} title="下移">↓</button>
          <button class="m-btn small bm-down" data-i="${i}" ${i === state.stack.length - 1 ? "disabled" : ""} title="上移">↑</button>
          <button class="m-btn small danger bm-remove" data-i="${i}">×</button>
        </div>`;
      })
      .join("");
    const previewSlices = buildPreviewSlices(state.stack, buns);
    return `
      <div class="bm-stack-panes">
        <div class="bm-burger-pane">
          <div class="bm-pane-title">🍔 虚拟汉堡</div>
          <div class="bm-preview-wrap">
            <div class="bm-stack-preview">
              ${previewSlices
                .map((s) => {
                  const part =
                    s.part === "bottom" ? "（底）" : s.part === "top" ? "（顶）" : "";
                  const partCls =
                    s.part === "bottom" ? " bun-bottom" : s.part === "top" ? " bun-top" : "";
                  if (s.part) {
                    return `<div class="bm-stack-slice bun${partCls}"><span class="bm-stack-slice-label">${esc(candidateName(cands, s.id))}${part}</span></div>`;
                  }
                  const fillC = findCandidate(cands, s.id);
                  return `<div class="bm-stack-slice bm-fill" style="${fillerSliceStyle(s.id, fillC)}" title="${esc(s.id)}"><span class="bm-stack-slice-label">${esc(candidateName(cands, s.id))}</span></div>`;
                })
                .join("")}
            </div>
          </div>
          <p class="muted small bm-stack-meta">共 ${totalIngredientCount()} 食材 · ${
            state.stack.length
          } 层（夹心 ${fillerIngredientCount()} 食材）${
            stackHasBun(state.stack, buns) ? "" : " · ⚠️ 缺少汉堡面包"
          }</p>
        </div>
        <div class="bm-layers-pane">
          <div class="bm-pane-title">☰ 层清单<span class="muted small">（⋮⋮ 拖拽排序）</span></div>
          <div class="bm-layer-list">
            ${stackRows || '<p class="muted">点击下方「+ 添加层」开始堆叠（需至少一层汉堡面包）…</p>'}
          </div>
        </div>
      </div>
      ${overLimitHtml()}
    `;
  }

  /** 该层贡献的食材数：复合层（如培根煎蛋香肠早餐 = 3 食材）按叶食材数计，普通食材/面包 = 1。
   *  数据源：候选的 iconFallbackIds（后端 CustomIngredients 递归展开的自定义菜谱叶食材）；
   *  候选没带时回退菜谱目录 expandLeafIds（官方复合菜谱）。 */
  function ingredientCountOf(id: string): number {
    const c = findCandidate(allCandidates(data), id);
    const fromCandidate = (c?.iconFallbackIds ?? []).length;
    if (fromCandidate > 0) return fromCandidate;
    const leaves = expandLeafIds([id]);
    return leaves.length > 0 ? leaves.length : 1;
  }

  /** 食材总数（含汉堡面包）：上限 32 的计数口径。 */
  function totalIngredientCount(): number {
    return state.stack.reduce((n, id) => n + ingredientCountOf(id), 0);
  }

  /** 夹心食材数 = 总食材数 − 汉堡面包（面包是容器，不算夹心）。 */
  function fillerIngredientCount(): number {
    const buns = bunIdSet();
    return state.stack.reduce((n, id) => (isBunId(id, buns) ? n : n + ingredientCountOf(id)), 0);
  }

  /** 超过推荐上限时的提醒条（不阻断，只提示 + 给实拍示例）。 */
  function overLimitHtml(): string {
    const n = totalIngredientCount();
    if (n <= FILLING_SOFT_LIMIT) return "";
    return `<div class="bm-overflow-warn">
      ⚠️ 食材已有 <b>${n}</b> 个（含汉堡面包，复合层按叶食材数计入），超过推荐上限 <b>${FILLING_SOFT_LIMIT}</b>。
      游戏订单 UI 的食材图标是横向排布的，食材过多会导致<b>图标溢出卡片、订单条被撑爆</b>。
      仍可继续添加（不设硬上限），但请自行确认游戏内表现。
      <button type="button" class="m-btn small" id="bm-show-overflow">📷 查看超规格实拍</button>
    </div>`;
  }

  /** 超规格示例弹窗（游戏内实拍截图）。 */
  function openOverflowExample(): void {
    openModal(
      "📷 食材过多导致的 UI 超规格",
      `<p class="modal-hint">下图是游戏内实拍：食材（含复合层展开）过多时，订单卡片里的食材图标会横向溢出，
        把订单条撑得过宽甚至超出屏幕，玩家难以辨认。推荐把食材总数（含汉堡面包）控制在
        <b>${FILLING_SOFT_LIMIT}</b> 个以内。</p>
      <div class="bm-overflow-shot">
        <img src="/docs/burger-filling-overflow.png" alt="汉堡夹心过多导致订单 UI 溢出的游戏内截图"
             onerror="this.onerror=null;this.replaceWith(Object.assign(document.createElement('p'),{className:'muted',textContent:'截图未找到：/docs/burger-filling-overflow.png'}))">
      </div>`,
      `<button type="button" class="m-btn primary" data-cancel>知道了</button>`
    );
    document.querySelector(".modal-panel")?.classList.add("wide");
    document.querySelector("[data-cancel]")?.addEventListener("click", () => closeModal());
  }

  function suggestedScore(): number {
    // 建议分数随食材数变化（复合层按叶食材数计）
    return Math.min(600, 20 + totalIngredientCount() * 20);
  }

  function renderPropsSection(): string {
    const suggested = suggestedScore();
    const editing = Boolean(state.loadedProductAssetPath);
    const nameVal = state.loadedRecipeName;
    const zhVal = state.loadedNameZh;
    const enVal = state.loadedNameEn;
    const scoreVal = state.loadedScore > 0 ? state.loadedScore : suggested;
    const existingIconSrc =
      editing && state.loadedProductAssetPath
        ? `/api/custom-recipes/icon?assetPath=${encodeURIComponent(state.loadedProductAssetPath)}`
        : "";
    return `
      <details class="bm-props-details" open>
        <summary class="bm-props-summary">📝 成品信息${editing ? " <span class=\"muted small\">（编辑模式）</span>" : ""}</summary>
        <div class="bm-props-body">
          <div class="bm-def-row"><span class="muted">关卡集</span>
            <select id="bm-set" class="rl-select" style="flex:1">
              ${sets.map((s) => `<option value="${esc(s.setName)}" ${s.setName === state.setName ? "selected" : ""}>${esc(s.levelSetNameZH || s.setName)}（${esc(s.setName)}）</option>`).join("")}
            </select>
          </div>
          <div class="bm-def-row"><span class="muted">分类</span><input id="bm-category" class="rl-select" style="flex:1" value="${esc(state.category)}" placeholder="关卡集内分类 id，如 burger_local"></div>
          <div class="bm-def-row"><span class="muted">标识</span><input id="bm-name" class="rl-select" style="flex:1" value="${esc(nameVal)}" placeholder="如 CheeseChickenBurger（字母数字下划线）"></div>
          <div class="bm-def-row"><span class="muted">中文名</span><input id="bm-name-zh" class="rl-select" style="flex:1" value="${esc(zhVal)}" placeholder="如 芝士鸡肉汉堡"></div>
          <div class="bm-def-row"><span class="muted">英文名</span><input id="bm-name-en" class="rl-select" style="flex:1" value="${esc(enVal)}" placeholder="默认同标识"></div>
          <div class="bm-def-row"><span class="muted">分数</span><input id="bm-score" type="number" class="rl-select" style="width:90px" value="${scoreVal}"><span class="muted small" id="bm-score-hint">建议 ${suggested}</span></div>
          <div class="bm-def-row bm-icon-row"><span class="muted">图标</span>
            <div style="flex:1;display:flex;align-items:center;gap:8px;flex-wrap:wrap">
              <input type="file" id="bm-icon" accept="image/png,image/jpeg" class="rl-select" style="flex:1;min-width:160px">
              ${existingIconSrc ? `<img id="bm-icon-existing" class="food-icon" loading="lazy" src="${esc(existingIconSrc)}" alt="" onerror="this.hidden=true">` : ""}
              <img id="bm-icon-preview" class="food-icon" hidden alt="">
            </div>
          </div>
          <div class="bm-def-row"><span style="flex:1"></span><button class="m-btn primary" id="bm-save">${editing ? "💾 保存修改" : "🍔 生成汉堡菜谱"}</button></div>
        </div>
      </details>
    `;
  }

  function fileToBase64(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => {
        const result = reader.result as string;
        const comma = result.indexOf(",");
        resolve(comma >= 0 ? result.substring(comma + 1) : result);
      };
      reader.onerror = () => reject(new Error("图标读取失败"));
      reader.readAsDataURL(file);
    });
  }

  function renderMain(): string {
    const productOpts = data.products
      .filter((p) => stackHasBun(p.compositionIds ?? [], bunIdSet()))
      .map(
        (p) =>
          `<option value="${esc(p.id)}">${esc(p.nameZh)}（${p.compositionIds.length} 层 · ${p.score} 分）</option>`
      )
      .join("");
    return `
      <div class="bm-page">
        <div class="bm-main-card">
          <div class="bm-toolbar">
            <label class="bm-toolbar-label bm-toolbar-grow">
              <span class="muted small">载入已有汉堡</span>
              <select id="bm-load-product" class="rl-select">
                <option value="">选择已有汉堡改层另存…</option>${productOpts}
              </select>
            </label>
            ${state.loadedProductId ? `<button type="button" class="m-btn small" id="bm-clear-loaded">清除载入</button>` : ""}
          </div>
          <h3 class="bm-section-title">🧾 菜谱卡片</h3>
          <div id="bm-card-preview" class="cr-preview">${renderCardPreview()}</div>
          <hr class="bm-divider">
          <h3 class="bm-section-title">🍔 堆叠编辑</h3>
          <div id="bm-stack-host">${renderStackSection()}</div>
          <div class="bm-actions-row">
            <button type="button" class="m-btn primary" id="bm-open-cand">+ 添加层</button>
          </div>
          <hr class="bm-divider">
          ${renderPropsSection()}
        </div>
      </div>
    `;
  }

  /** 堆叠行拖放排序。
   *
   *  ⚠ 不要依赖 `drop` 事件：行内嵌了图标 / 徽标 / 多个按钮，drop 在这种结构下
   *  经常不触发（表现就是「能拖，松手没反应」）。这里沿用 levels.ts 里已验证的做法 ——
   *  在 dragover 时**直接移动 DOM 节点**做实时换位，dragend 再按 DOM 现有顺序回写
   *  state.stack 并重绘一次。全程不需要 drop。
   *
   *  行上的 data-i 是**拖动开始前的原始下标**，拖动过程中不renumber，
   *  所以 dragend 时按 DOM 顺序取 data-i 就能还原出新的层序；refreshStack 重绘后
   *  data-i 与显示序号会重新连续。 */
  function wireStackDrag(root: ParentNode): void {
    const list = root.querySelector<HTMLElement>(".bm-layer-list");
    if (!list) return;
    let dragRow: HTMLElement | null = null;

    const commit = (): void => {
      const order = Array.from(list.querySelectorAll<HTMLElement>(".bm-layer"))
        .map((r) => Number(r.dataset.i));
      if (order.length !== state.stack.length || order.some((i) => !Number.isInteger(i))) return;
      const next = order.map((i) => state.stack[i]);
      if (next.some((x) => x == null)) return;
      if (next.join("\u0000") === state.stack.join("\u0000")) return;
      state.stack = next;
      refreshStack();
      setStatus(true, "已调整层序。");
    };

    list.querySelectorAll<HTMLElement>(".bm-layer").forEach((row) => {
      row.addEventListener("dragstart", (e) => {
        dragRow = row;
        row.classList.add("dragging");
        const dt = (e as DragEvent).dataTransfer;
        if (dt) {
          dt.effectAllowed = "move";
          // Firefox 必须 setData 才会真正开始拖动
          dt.setData("text/plain", row.dataset.i ?? "");
        }
      });
      row.addEventListener("dragend", () => {
        row.classList.remove("dragging");
        dragRow = null;
        commit();
      });
      row.addEventListener("dragover", (e) => {
        e.preventDefault();
        if (!dragRow || dragRow === row) return;
        const rect = row.getBoundingClientRect();
        const before = (e as DragEvent).clientY < rect.top + rect.height / 2;
        if (before) list.insertBefore(dragRow, row);
        else list.insertBefore(dragRow, row.nextSibling);
      });
    });
  }

  function wireStackButtons(root: ParentNode = document): void {
    wireStackDrag(root);
    root.querySelectorAll<HTMLButtonElement>(".bm-layer-preview").forEach((b) =>
      b.addEventListener("click", () => {
        const path = b.dataset.previewPath;
        if (!path) return;
        void openRecipeModelPreview(path, b.dataset.previewName ?? "夹心模型", {
          fitTarget: "plate",
          onError: (msg) => setStatus(false, msg),
        });
      })
    );
    // 以下三个都只刷新堆叠区（refreshStack），绝不整页重渲染 ——
    // 否则「成品信息」里已填的标识/中文名/分数会被清空，已选的图标文件更是无法恢复。
    root.querySelectorAll<HTMLButtonElement>(".bm-remove").forEach((b) =>
      b.addEventListener("click", () => {
        state.stack.splice(parseInt(b.dataset.i!, 10), 1);
        refreshStack();
        refreshCandCounts();
      })
    );
    root.querySelectorAll<HTMLButtonElement>(".bm-up").forEach((b) =>
      b.addEventListener("click", () => {
        const i = parseInt(b.dataset.i!, 10);
        if (i > 0) {
          [state.stack[i - 1], state.stack[i]] = [state.stack[i], state.stack[i - 1]];
          refreshStack();
        }
      })
    );
    root.querySelectorAll<HTMLButtonElement>(".bm-down").forEach((b) =>
      b.addEventListener("click", () => {
        const i = parseInt(b.dataset.i!, 10);
        if (i < state.stack.length - 1) {
          [state.stack[i + 1], state.stack[i]] = [state.stack[i], state.stack[i + 1]];
          refreshStack();
        }
      })
    );
  }

  /** 夹心候选弹窗（+ 添加层）：点击卡片加入堆叠。 */
  function openCandModal(): void {
    openModal(
      "添加堆叠层",
      `<p class="modal-hint">点击卡片加入左侧堆叠，可连续添加多个；汉堡皮（ChoppedBun）在预览中显示为上下两片。
        夹心 = <b>0 分的中间产物</b>。默认已过滤掉两类：<b>全部成品菜</b>（官方与自定义都算——汤/套餐/寿司/披萨/火锅/月饼/甜甜圈…整道菜夹进汉堡没有意义），
        以及<b>没有堆叠模型的夹心</b>（游戏里那层看不见）。仅 Burger大全已在用的条目不受影响（如 2 条早餐拼盘，归在「⭐ 常用夹心」）。
        要看全部可点「显示全部候选」；要现做一个带模型的夹心，点右上角「🥩 夹心工作台」。
        标了 DLC 徽标的层需要对应匹配表，保存菜谱时后端会自动补上。</p>
       <div class="bm-cand-toolbar">
         <input type="search" id="bm-cand-search" class="rl-search" placeholder="搜索名称 / 英文名 / ID…" autocomplete="off">
         <button type="button" class="rl-chip-btn bm-cand-filter${candOnlyRecommended ? " active" : ""}" data-filter="rec">仅推荐</button>
         <button type="button" class="rl-chip-btn bm-cand-filter${candShowModelless ? " active" : ""}" data-filter="modelless" title="放行被默认过滤的候选：无堆叠模型的夹心（游戏里那层看不见），以及全部成品菜（官方 + 自定义）">显示全部候选</button>
         <span style="flex:1"></span>
         <button type="button" class="m-btn primary" id="bm-new-filling" title="在新标签页打开夹心工作台，做好后回来刷新即可选用">🥩 夹心工作台 ↗</button>
       </div>
       <div class="modal-scroll bm-pick-scroll">
         <div id="bm-cand-list">${renderCandListHtml()}</div>
       </div>`,
      `<span class="muted" id="bm-pick-count">当前堆叠 ${totalIngredientCount()} 食材 · ${state.stack.length} 层</span>
       <button type="button" class="m-btn primary" data-cancel>完成</button>`
    );
    document.querySelector("[data-cancel]")?.addEventListener("click", () => {
      // 堆叠区在每次 +/- 时已实时刷新，这里不再整页重渲染（会清空成品信息表单）
      closeModal();
    });
    const panel = document.querySelector(".modal-panel");
    if (panel) {
      panel.classList.add("wide");
      panel.classList.add("bm-pick-panel");
    }
    const list = document.getElementById("bm-cand-list")!;
    list.addEventListener("click", (e) => {
      const target = e.target as HTMLElement;
      // 👁 预览：不触发「加入堆叠」
      const eye = target.closest<HTMLElement>(".bm-cand-eye");
      if (eye?.dataset.previewPath) {
        e.preventDefault();
        e.stopPropagation();
        void openRecipeModelPreview(eye.dataset.previewPath, eye.dataset.previewName ?? "夹心模型", {
          fitTarget: "plate",
          onError: (msg) => setStatus(false, msg),
        });
        return;
      }
      const cands = allCandidates(data);

      // 「−」：从堆叠里移除该项的**最后一次**出现
      const minus = target.closest<HTMLElement>("[data-minus]");
      if (minus?.dataset.minus) {
        e.preventDefault();
        e.stopPropagation();
        const id = minus.dataset.minus;
        const idx = state.stack.lastIndexOf(id);
        if (idx >= 0) {
          state.stack.splice(idx, 1);
          refreshCandCounts();
          refreshStack();
          setStatus(true, `已移除一层「${candidateName(cands, id)}」（剩 ${stackCountOf(id)} 层）`);
        }
        return;
      }

      const card = target.closest<HTMLButtonElement>(".bm-pick-card");
      if (!card?.dataset.id) return;
      const id = card.dataset.id;
      state.stack.push(id);
      refreshCandCounts();
      refreshStack();
      setStatus(true, `已添加「${candidateName(cands, id)}」，当前 ${stackCountOf(id)} 层`);
      card.classList.add("selected");
      window.setTimeout(() => card.classList.remove("selected"), 500);
    });
    const refreshCandList = (): void => {
      list.innerHTML = renderCandListHtml();
    };
    document.querySelectorAll<HTMLButtonElement>(".bm-cand-filter").forEach((b) => {
      b.addEventListener("click", () => {
        if (b.dataset.filter === "rec") {
          candOnlyRecommended = !candOnlyRecommended;
          b.classList.toggle("active");
          refreshCandList();
          return;
        }
        // 「显示无模型」需要后端重新下发（默认请求不含这些条目）
        candShowModelless = !candShowModelless;
        b.classList.toggle("active");
        void (async () => {
          showBusy("加载候选…");
          try {
            data = await api.fetchBurgerDefinitions(state.setName, candShowModelless);
            refreshCandList();
          } catch (e) {
            showError(e);
          } finally {
            hideBusy();
          }
        })();
      });
    });
    // 夹心有独立工作台页（复用「新建菜谱」编辑器）：新开一页，不打断当前堆叠编辑
    document.getElementById("bm-new-filling")?.addEventListener("click", () => {
      window.open(fillingPath(state.setName), "_blank");
    });
    document.getElementById("bm-cand-search")?.addEventListener("input", (e) => {
      const q = (e.target as HTMLInputElement).value.trim().toLowerCase();
      list.querySelectorAll<HTMLElement>(".bm-pick-section").forEach((sec) => {
        let visible = 0;
        sec.querySelectorAll<HTMLElement>(".bm-pick-card").forEach((card) => {
          const show = !q || (card.dataset.name ?? "").includes(q);
          card.hidden = !show;
          if (show) visible++;
        });
        sec.hidden = visible === 0;
      });
    });
  }

  function loadProductIntoState(p: (typeof data.products)[number]): boolean {
    const ids = p.compositionIds ?? [];
    if (!stackHasBun(ids, bunIdSet())) {
      setStatus(false, `「${p.nameZh}」的组成中没有汉堡面包，无法载入。`);
      return false;
    }
    state.stack = ids.slice();
    state.loadedProductId = p.id;
    state.loadedProductAssetPath = p.assetPath ?? "";
    state.loadedRecipeName = p.recipeName || p.id;
    state.loadedNameZh = p.nameZh;
    state.loadedNameEn = p.nameEn || p.recipeName || p.id;
    state.loadedScore = p.score;
    // 已有汉堡的分数是作者定好的，不能被「建议分数」在加层时改掉
    state.scoreTouched = true;
    // 分类跟随该菜谱实际所在目录（custom_recipes/<分类>/xxx.asset），
    // 否则表单里显示的是默认 burger_local，与实际位置不符、容易误导
    const m = /\/custom_recipes\/([^/]+)\//.exec((p.assetPath ?? "").replace(/\\/g, "/"));
    if (m) state.category = m[1];
    return true;
  }

  function wirePanel(): void {
    document.getElementById("bm-set")?.addEventListener("change", (e) => {
      void (async () => {
        state.setName = (e.target as HTMLSelectElement).value;
        // 切集 = 新工作台状态：URL 去掉已载入的汉堡 id（replace 不产生历史）
        history.replaceState(null, "", burgerPath(state.setName));
        showBusy("切换关卡集…");
        try {
          data = await api.fetchBurgerDefinitions(state.setName);
          catalog = await api.fetchRecipeCatalog(state.setName).catch(() => [] as RecipeEntry[]);
          state.defPath = pickShellDefinition()?.assetPath ?? "";
          state.stack = [];
          state.loadedProductId = "";
          state.loadedProductAssetPath = "";
          state.loadedRecipeName = "";
          state.loadedNameZh = "";
          state.loadedNameEn = "";
          state.loadedScore = 0;
          renderAll();
        } catch (e2) {
          showError(e2);
        } finally {
          hideBusy();
        }
      })();
    });
    document.getElementById("bm-category")?.addEventListener("change", (e) => {
      state.category = (e.target as HTMLInputElement).value.trim() || "burger_local";
    });
    // 用户手动改过分数后，就不再被「建议分数」自动覆盖
    document.getElementById("bm-score")?.addEventListener("input", () => {
      state.scoreTouched = true;
    });

    document.getElementById("bm-icon")?.addEventListener("change", (e) => {
      const file = (e.target as HTMLInputElement).files?.[0];
      const preview = document.getElementById("bm-icon-preview") as HTMLImageElement | null;
      if (iconPreviewUrl) {
        URL.revokeObjectURL(iconPreviewUrl);
        iconPreviewUrl = "";
      }
      if (!file) {
        if (preview) {
          preview.hidden = true;
          preview.removeAttribute("src");
        }
        refreshCardPreview();
        return;
      }
      iconPreviewUrl = URL.createObjectURL(file);
      if (preview) {
        preview.src = iconPreviewUrl;
        preview.hidden = false;
      }
      // 选了图标后卡片预览同步换图
      refreshCardPreview();
    });

    // 名称/分数改动 → 卡片实时跟随（与「新建菜谱」编辑器一致）
    for (const id of ["bm-name", "bm-name-zh", "bm-score"]) {
      document.getElementById(id)?.addEventListener("input", () => {
        captureForm();
        refreshCardPreview();
      });
    }
    wireOverflowButton();

    document.getElementById("bm-open-cand")?.addEventListener("click", () => openCandModal());

    document.getElementById("bm-load-product")?.addEventListener("change", (e) => {
      const id = (e.target as HTMLSelectElement).value;
      if (!id) return;
      const p = data.products.find((x) => x.id === id);
      if (!p) return;
      if (!loadProductIntoState(p)) return;
      history.replaceState(null, "", burgerPath(state.setName, p.id));
      setStatus(true, `已载入「${p.nameZh}」的 ${state.stack.length} 层堆叠，修改后点「保存修改」更新成品菜谱。`);
      renderAll();
    });

    document.getElementById("bm-clear-loaded")?.addEventListener("click", () => {
      state.loadedProductId = "";
      state.loadedProductAssetPath = "";
      state.loadedRecipeName = "";
      state.loadedNameZh = "";
      state.loadedNameEn = "";
      state.loadedScore = 0;
      state.stack = [];
      history.replaceState(null, "", burgerPath(state.setName));
      setStatus(true, "已清除载入，可重新堆叠新汉堡。");
      renderAll();
    });

    wireStackButtons();

    document.getElementById("bm-save")?.addEventListener("click", () => {
      void (async () => {
        const recipeName = (document.getElementById("bm-name") as HTMLInputElement).value.trim();
        const nameZh = (document.getElementById("bm-name-zh") as HTMLInputElement).value.trim();
        const nameEn = (document.getElementById("bm-name-en") as HTMLInputElement).value.trim();
        const score = parseInt((document.getElementById("bm-score") as HTMLInputElement).value, 10) || 0;
        if (!/^[A-Za-z0-9_]+$/.test(recipeName)) {
          setStatus(false, "标识只能包含字母、数字和下划线。");
          return;
        }
        if (state.stack.length === 0) {
          setStatus(false, "至少添加一层。");
          return;
        }
        if (!stackHasBun(state.stack, bunIdSet())) {
          setStatus(false, "堆叠中至少包含一层汉堡面包。");
          return;
        }
        let iconBase64 = "";
        const iconFile = (document.getElementById("bm-icon") as HTMLInputElement).files?.[0];
        if (iconFile) {
          iconBase64 = await fileToBase64(iconFile);
        }
        try {
          const editing = Boolean(state.loadedProductAssetPath);
          showBusy(editing ? "保存汉堡修改…" : "生成汉堡菜谱…");
          const result = await api.createBurger({
            setName: state.setName,
            category: (document.getElementById("bm-category") as HTMLInputElement).value.trim() || "burger_local",
            definitionAssetPath: state.defPath,
            recipeName,
            nameZh,
            nameEn,
            score,
            layerIds: state.stack,
            updateAssetPath: state.loadedProductAssetPath || undefined,
          });
          let iconError = result.iconError ?? "";
          const savedPath = result.assetPath;
          if (iconFile && savedPath) {
            try {
              await api.uploadCustomRecipeIcon(state.setName, savedPath, iconFile.name, iconBase64);
            } catch (e) {
              iconError = (e as Error).message ?? String(e);
            }
          }
          data = await api.fetchBurgerDefinitions(state.setName);
          const shell = pickShellDefinition();
          if (shell) state.defPath = shell.assetPath;
          if (!editing) {
            state.stack = [];
            state.loadedProductId = "";
            state.loadedProductAssetPath = "";
            state.loadedRecipeName = "";
            state.loadedNameZh = "";
            state.loadedNameEn = "";
            state.loadedScore = 0;
          } else {
            state.loadedRecipeName = recipeName;
            state.loadedNameZh = nameZh;
            state.loadedNameEn = nameEn;
            state.loadedScore = score;
          }
          const iconNote = iconError ? `；⚠️ 图标未写入：${iconError}` : iconFile && savedPath ? "；订单图标已保存" : "";
          // 后端非阻断告警：无堆叠模型 / 需要 DLC 匹配表
          const allWarnings = [...(result.warnings ?? [])];
          if (totalIngredientCount() > FILLING_SOFT_LIMIT) {
            allWarnings.push(
              `食材 ${totalIngredientCount()} 个（含汉堡面包）已超过推荐上限 ${FILLING_SOFT_LIMIT}，游戏订单 UI 可能出现图标溢出`
            );
          }
          const warnNote = allWarnings.length > 0 ? "；⚠️ " + allWarnings.join("；") : "";
          setStatus(
            true,
            (result.updated ? "✅ 已保存「" : "✅ 汉堡「") +
              (nameZh || recipeName) +
              (result.updated ? "」的修改" : "」已生成到关卡集「" + state.setName + "」") +
              (result.updated ? "" : `（uID ${result.uID}）`) +
              iconNote +
              warnNote
          );
          // 编辑态 URL 指向该汉堡（可刷新/分享）；新生成后回到新建态（URL 保持集级）
          history.replaceState(
            null,
            "",
            burgerPath(state.setName, editing ? state.loadedProductId || recipeName : undefined)
          );
          renderAll();
        } catch (e) {
          showError(e);
        } finally {
          hideBusy();
        }
      })();
    });
  }

  /** 把「成品信息」表单的当前输入写回 state。
   *  任何整页重渲染前必须先调用，否则用户填的标识/中文名/分数会被 state 里的旧值覆盖。 */
  function captureForm(): void {
    const val = (id: string): string =>
      (document.getElementById(id) as HTMLInputElement | null)?.value ?? "";
    const name = val("bm-name").trim();
    const zh = val("bm-name-zh").trim();
    const en = val("bm-name-en").trim();
    const cat = val("bm-category").trim();
    const score = parseInt(val("bm-score"), 10);
    if (document.getElementById("bm-name")) {
      state.loadedRecipeName = name;
      state.loadedNameZh = zh;
      state.loadedNameEn = en;
      if (cat) state.category = cat;
      if (Number.isFinite(score)) {
        state.loadedScore = score;
        state.scoreTouched = true;
      }
    }
  }

  /** 只重绘「堆叠编辑」区（增删层 / 拖动排序后调用）。
   *  不动「成品信息」表单 —— 整页重渲染会清空用户已填的标识/中文名/分数，
   *  而且 <input type="file"> 选的图标**无法**用 JS 还原，重建即永久丢失。 */
  function refreshStack(): void {
    const host = document.getElementById("bm-stack-host");
    if (!host) {
      renderAll();
      return;
    }
    host.innerHTML = renderStackSection();
    wireStackButtons(host);
    wireOverflowButton();
    refreshCardPreview();
    // 建议分数随层数变化：仅在用户没手动改过分数时跟随
    const scoreEl = document.getElementById("bm-score") as HTMLInputElement | null;
    const hint = document.getElementById("bm-score-hint");
    const suggested = suggestedScore();
    if (hint) hint.textContent = `建议 ${suggested}`;
    if (scoreEl && !state.scoreTouched) scoreEl.value = String(suggested);
  }

  /** 整页重渲染。
   *  ⚠ 这里**不能**调 captureForm()：所有调用点（切关卡集 / 载入汉堡 / 清除载入 /
   *  保存后）都是先显式设置好 state 再调本函数，回读当时还是旧值的 DOM 只会把刚设好的
   *  名称/标识/分数覆盖掉 —— 载入已有汉堡时名字与 ID 消失就是这么来的。
   *  用户输入的保持靠两条：改堆叠只走 refreshStack（根本不重绘表单）；
   *  表单 input 事件实时 captureForm() 同步到 state。 */
  function renderAll(): void {
    content.innerHTML = renderMain();
    wirePanel();
  }

  renderAll();

  // 深链 /custom-recipes/burger-maker/{set}/{burgerId}：按 id 载入已保存的成品汉堡
  if (routeBurgerId && routeSetValid) {
    const p =
      data.products.find((x) => x.id === routeBurgerId) ??
      data.products.find((x) =>
        (x.assetPath ?? "").replace(/\\/g, "/").endsWith("/" + routeBurgerId + ".asset")
      );
    if (p && loadProductIntoState(p)) {
      setStatus(true, `已从自定义菜谱打开「${p.nameZh}」，修改后点「保存修改」。`);
      renderAll();
    } else {
      setStatus(false, "无法在汉堡工作台载入该菜谱（请确认属于当前关卡集且为成品汉堡）。");
    }
  }
}
