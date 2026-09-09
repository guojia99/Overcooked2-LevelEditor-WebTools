/**
 * burgerMaker.ts —— 汉堡组装工作台（/custom-recipes/burger-maker）。
 */
import { navHtml, wireNav } from "./nav";
import { closeModal, openModal } from "./modals";
import * as api from "./api";
import { showBusy, hideBusy } from "./busy";
import type {
  BurgerCandidate,
  BurgerDefinitionList,
  LevelSetInfo,
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
}

const SET_KEY = "burgerMakerSetName";
const LOAD_PRODUCT_KEY = "burgerMakerLoadAssetPath";

function setStatus(ok: boolean, msg: string): void {
  const el = document.getElementById("bm-status");
  if (!el) return;
  el.textContent = msg;
  el.classList.toggle("err", !ok);
  el.classList.toggle("ok", ok && msg.length > 0);
}

function isBunId(id: string, bunIds: Set<string>): boolean {
  if (bunIds.has(id)) return true;
  if (id === "ChoppedBunSO" || id.toLowerCase() === "dlc08_bun") return true;
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

function candIconSrc(c: BurgerCandidate): string {
  if (c.kind === "custom") {
    return `/api/custom-recipes/icon?assetPath=${encodeURIComponent(c.assetPath)}`;
  }
  if (c.kind === "official-recipe") {
    return `/icons/recipes/${encodeURIComponent(c.id)}.png`;
  }
  return `/icons/ingredients/${encodeURIComponent(c.id)}.png`;
}

function layerIcon(c: BurgerCandidate | undefined): string {
  if (!c) {
    return `<img class="food-icon bm-layer-icon" src="/icons/_placeholder.png" alt="">`;
  }
  const src = candIconSrc(c);
  return `<img class="food-icon bm-layer-icon" loading="lazy" src="${esc(src)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`;
}

function pickCardIcon(c: BurgerCandidate): string {
  const src = candIconSrc(c);
  return `<img class="food-icon" loading="lazy" src="${esc(src)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`;
}

function renderCandCard(c: BurgerCandidate): string {
  const en = c.nameEn?.trim();
  const searchKey = (c.nameZh + " " + (en ?? "") + " " + c.id).toLowerCase();
  return `<button type="button" class="pick-card bm-pick-card" data-id="${esc(c.id)}" data-name="${esc(searchKey)}" title="${esc(c.id)}">
    <span class="pc-head">${pickCardIcon(c)}<span class="pc-name">${esc(c.nameZh)}${en ? ` <span class="muted pc-en">${esc(en)}</span>` : ""}</span></span>
    <span class="muted small">${esc(c.id)}</span>
  </button>`;
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
  const state: MakerState = {
    setName: sessionStorage.getItem(SET_KEY) ?? sets[0].setName,
    category: "burger_local",
    defPath: "",
    stack: [],
    loadedProductId: "",
    loadedProductAssetPath: "",
    loadedRecipeName: "",
    loadedNameZh: "",
    loadedNameEn: "",
    loadedScore: 0,
  };
  if (!sets.some((s) => s.setName === state.setName)) state.setName = sets[0].setName;

  let data: BurgerDefinitionList;
  try {
    showBusy("加载汉堡数据…");
    data = await api.fetchBurgerDefinitions(state.setName);
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

  function renderCandListHtml(): string {
    const groups: [string, BurgerCandidate[]][] = [
      ["bun", data.buns ?? []],
      ["custom", data.candidates.filter((c) => c.kind === "custom")],
      ["ingredient", data.candidates.filter((c) => c.kind === "ingredient")],
    ];
    const label: Record<string, string> = {
      bun: "🍞 汉堡皮",
      custom: "🥩 中间产物（commonW2）",
      ingredient: "🥬 官方食材（commonW2 组装定义允许）",
    };
    return groups
      .map(([k, arr]) => {
        if (arr.length === 0) return "";
        return `<div class="bm-pick-section" data-bm-group="${esc(k)}">
          <div class="bm-pick-section-title">${label[k]} <span class="rl-cnt">${arr.length}</span></div>
          <div class="pick-grid bm-pick-grid">${arr.map(renderCandCard).join("")}</div>
        </div>`;
      })
      .join("");
  }

  function renderStackSection(): string {
    const cands = allCandidates(data);
    const buns = bunIdSet();
    const stackRows = state.stack
      .map((id, i) => {
        const c = findCandidate(cands, id);
        const bunTag = isBunId(id, buns) ? ' <span class="muted small">（上下两片）</span>' : "";
        return `<div class="bm-layer">
          ${layerIcon(c)}
          <span class="muted small">${i + 1}</span>
          <span class="bm-layer-name">${esc(candidateName(cands, id))}${bunTag}
            <span class="muted small">${esc(candidateEn(cands, id))} · ${esc(id)}</span></span>
          <button class="m-btn small bm-up" data-i="${i}" ${i === 0 ? "disabled" : ""} title="下移">↓</button>
          <button class="m-btn small bm-down" data-i="${i}" ${i === state.stack.length - 1 ? "disabled" : ""} title="上移">↑</button>
          <button class="m-btn small danger bm-remove" data-i="${i}">×</button>
        </div>`;
      })
      .join("");
    const previewSlices = buildPreviewSlices(state.stack, buns);
    return `
      <div class="bm-layer-list">
        ${stackRows || '<p class="muted">点击下方「+ 添加层」开始堆叠（需至少一层汉堡面包）…</p>'}
      </div>
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
      <p class="muted small bm-stack-meta">共 ${state.stack.length} 层${stackHasBun(state.stack, buns) ? "" : " · ⚠️ 缺少汉堡面包"}</p>
    `;
  }

  function renderPropsSection(): string {
    const suggested = Math.min(600, 20 + state.stack.length * 20);
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
          <div class="bm-def-row"><span class="muted">分数</span><input id="bm-score" type="number" class="rl-select" style="width:90px" value="${scoreVal}"><span class="muted small">建议 ${suggested}</span></div>
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
        <div class="bm-dev-banner mp-status">
          ⚠️ <b>功能开发中</b>：汉堡组装工作台仍在完善，部分交互与生成结果可能变动，请以 Unity 写回后的资产为准。
        </div>
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
          <h3 class="bm-section-title">🍔 堆叠编辑</h3>
          ${renderStackSection()}
          <div class="bm-actions-row">
            <button type="button" class="m-btn primary" id="bm-open-cand">+ 添加层</button>
          </div>
          <hr class="bm-divider">
          ${renderPropsSection()}
        </div>
      </div>
    `;
  }

  function wireStackButtons(root: ParentNode = document): void {
    root.querySelectorAll<HTMLButtonElement>(".bm-remove").forEach((b) =>
      b.addEventListener("click", () => {
        state.stack.splice(parseInt(b.dataset.i!, 10), 1);
        renderAll();
      })
    );
    root.querySelectorAll<HTMLButtonElement>(".bm-up").forEach((b) =>
      b.addEventListener("click", () => {
        const i = parseInt(b.dataset.i!, 10);
        if (i > 0) {
          [state.stack[i - 1], state.stack[i]] = [state.stack[i], state.stack[i - 1]];
          renderAll();
        }
      })
    );
    root.querySelectorAll<HTMLButtonElement>(".bm-down").forEach((b) =>
      b.addEventListener("click", () => {
        const i = parseInt(b.dataset.i!, 10);
        if (i < state.stack.length - 1) {
          [state.stack[i + 1], state.stack[i]] = [state.stack[i], state.stack[i + 1]];
          renderAll();
        }
      })
    );
  }

  function openCandModal(): void {
    openModal(
      "添加堆叠层",
      `<p class="modal-hint">点击卡片加入堆叠，可连续添加多个；汉堡皮（ChoppedBun）在预览中显示为上下两片。</p>
       <input type="search" id="bm-cand-search" class="rl-search" placeholder="搜索名称 / 英文名 / ID…" autocomplete="off" style="width:100%;margin-bottom:8px">
       <div class="modal-scroll bm-pick-scroll">
         <div id="bm-cand-list">${renderCandListHtml()}</div>
       </div>`,
      `<span class="muted" id="bm-pick-count">当前堆叠 ${state.stack.length} 层</span>
       <button type="button" class="m-btn primary" data-cancel>完成</button>`
    );
    document.querySelector("[data-cancel]")?.addEventListener("click", () => {
      closeModal();
      renderAll();
    });
    const panel = document.querySelector(".modal-panel");
    if (panel) {
      panel.classList.add("wide");
      panel.classList.add("bm-pick-panel");
    }
    const list = document.getElementById("bm-cand-list")!;
    const countEl = document.getElementById("bm-pick-count");
    const updatePickCount = (): void => {
      if (countEl) countEl.textContent = `当前堆叠 ${state.stack.length} 层`;
    };
    list.addEventListener("click", (e) => {
      const card = (e.target as HTMLElement).closest<HTMLButtonElement>(".bm-pick-card");
      if (!card?.dataset.id) return;
      state.stack.push(card.dataset.id);
      updatePickCount();
      const cands = allCandidates(data);
      setStatus(true, `已添加「${candidateName(cands, card.dataset.id)}」`);
      card.classList.add("selected");
      window.setTimeout(() => card.classList.remove("selected"), 500);
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
    state.loadedNameEn = p.recipeName || p.id;
    state.loadedScore = p.score;
    return true;
  }

  function wirePanel(): void {
    document.getElementById("bm-set")?.addEventListener("change", (e) => {
      void (async () => {
        state.setName = (e.target as HTMLSelectElement).value;
        sessionStorage.setItem(SET_KEY, state.setName);
        showBusy("切换关卡集…");
        try {
          data = await api.fetchBurgerDefinitions(state.setName);
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

    let iconPreviewObjectUrl = "";
    document.getElementById("bm-icon")?.addEventListener("change", (e) => {
      const file = (e.target as HTMLInputElement).files?.[0];
      const preview = document.getElementById("bm-icon-preview") as HTMLImageElement | null;
      if (iconPreviewObjectUrl) {
        URL.revokeObjectURL(iconPreviewObjectUrl);
        iconPreviewObjectUrl = "";
      }
      if (!preview) return;
      if (!file) {
        preview.hidden = true;
        preview.removeAttribute("src");
        return;
      }
      iconPreviewObjectUrl = URL.createObjectURL(file);
      preview.src = iconPreviewObjectUrl;
      preview.hidden = false;
    });

    document.getElementById("bm-open-cand")?.addEventListener("click", () => openCandModal());

    document.getElementById("bm-load-product")?.addEventListener("change", (e) => {
      const id = (e.target as HTMLSelectElement).value;
      if (!id) return;
      const p = data.products.find((x) => x.id === id);
      if (!p) return;
      if (!loadProductIntoState(p)) return;
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
          setStatus(
            true,
            (result.updated ? "✅ 已保存「" : "✅ 汉堡「") +
              (nameZh || recipeName) +
              (result.updated ? "」的修改" : "」已生成到关卡集「" + state.setName + "」") +
              (result.updated ? "" : `（uID ${result.uID}）`) +
              iconNote
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

  function renderAll(): void {
    content.innerHTML = renderMain();
    wirePanel();
  }

  renderAll();

  const pendingLoad = sessionStorage.getItem(LOAD_PRODUCT_KEY);
  if (pendingLoad) {
    sessionStorage.removeItem(LOAD_PRODUCT_KEY);
    const p =
      data.products.find((x) => x.assetPath === pendingLoad) ??
      data.products.find((x) => pendingLoad.endsWith("/" + x.id + ".asset"));
    if (p && loadProductIntoState(p)) {
      setStatus(true, `已从自定义菜谱打开「${p.nameZh}」，修改后点「保存修改」。`);
      renderAll();
    } else {
      setStatus(false, "无法在汉堡工作台载入该菜谱（请确认属于当前关卡集且为成品汉堡）。");
    }
  }
}
