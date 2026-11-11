import * as api from "./api";
import {
  dangerBtnHtml,
  mBtnHtml,
  mCancelBtnHtml,
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
import { hideBusy, showBusy, withBusy } from "./busy";
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
import { S } from "./editor/state";
import { rebuildIngredientLookup } from "./editor/catalog";
import { classifyTextureName } from "./fbxTextureRename";
import type { TextureClass, TextureDiskNames } from "./fbxTextureRename";
import { fmt4, fmtCm, u2cm } from "./modelUnits";
import type { ModelTransformValues } from "./modelPreview";

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

const IDENT_RE = /^[A-Za-z_][A-Za-z0-9_]*$/;

// ---------------------------------------------------------------------------
// 类型元数据（中英 + 说明 + 素材准备清单）
// ---------------------------------------------------------------------------

interface KindMeta {
  id: string;
  icon: string;
  zh: string;
  en: string;
  desc: string;
  /** 新建前用户需要准备什么（素材准备提示卡）。 */
  prep: string[];
}

const KIND_META: KindMeta[] = [
  {
    id: "ingredient",
    icon: "🥕",
    zh: "自定义食材",
    en: "Custom ingredients",
    desc: "可加工 / 不可加工 / 备餐容器食材，含 OrderNode、图标与模型",
    prep: [
      "PNG 图标一张（建议 128×128 透明底，用于订单与道具箱）。",
      "3D 模型：FBX 或 OBJ + 贴图（PNG/JPG/TGA）。可加工食材需要「切割前 + 切割后」两套外观——切割后形态由系统自动生成默认版，也可另建食材后在「可加工下一形态」里指定。",
      "模型会自动挂到食材上，切割进度条、内容 UI 等由模板默认提供，无需自备。",
    ],
  },
  {
    id: "counter",
    icon: "🧱",
    zh: "自定义工作台",
    en: "Custom counters",
    desc: "普通 / 切菜 / 火锅灶台等工作台，含火焰、切菜 PFX、教程图标",
    prep: [
      "3D 模型：FBX 或 OBJ + 贴图（会替换台面外观，占格与碰撞沿用模板）。",
      "不需要图标；着火特效 / 切菜粒子 / 教程图标都有默认值，可后换。",
      "切菜台模板（counter_chopping）请先想好配套的「切割后食材」，切割逻辑由食材侧决定。",
    ],
  },
  {
    id: "utensil",
    icon: "🍳",
    zh: "自定义锅具",
    en: "Custom utensils",
    desc: "火锅、烤盘、煎盘、汤锅、蒸笼等；支持站台类型与锅内食材模型",
    prep: [
      "3D 模型：FBX 或 OBJ + 贴图（替换锅具外观）。",
      "想好允许放置的灶台类型（灶台 / 烤箱 / 篝火 / 烧烤架 / 火锅灶……），决定能放到哪种台上。",
      "「锅内食材展示模型」可选：不选时沿用模板默认；选择后按「额外食材」顺序一一对应。",
    ],
  },
  {
    id: "sink",
    icon: "🚰",
    zh: "自定义水槽",
    en: "Custom sinks",
    desc: "清洗/沥干部件与干净盘堆绑定",
    prep: [
      "3D 模型：FBX 或 OBJ + 贴图（替换水槽外观，清洗/沥干分区沿用模板子物体）。",
      "清洗部件 / 沥干部件通常保持默认（从模板子物体中选）；干净盘堆可用默认。",
    ],
  },
  {
    id: "tray",
    icon: "🍽️",
    zh: "自定义托盘",
    en: "Custom trays",
    desc: "多槽上菜容器与槽位配方",
    prep: [
      "3D 模型：FBX 或 OBJ + 贴图（替换托盘外观）。",
      "槽位配置（每个槽接受哪些菜谱）沿用 common04 默认 TraySlot，一般无需准备。",
    ],
  },
  {
    id: "prep",
    icon: "🥣",
    zh: "自定义备餐容器",
    en: "Preparation containers",
    desc: "OrderNode、容器限制与 cosmetics prefab",
    prep: [
      "PNG 图标一张（建议 128×128 透明底）。",
      "3D 模型：FBX 或 OBJ + 贴图。",
      "「容器限制」决定能放进哪些食材，不设置 = 接受默认集合。",
    ],
  },
  {
    id: "splitter",
    icon: "🔀",
    zh: "分料机",
    en: "Ingredient splitter",
    desc: "输入/输出、容量、每份拆分数量与音效",
    prep: [
      "3D 模型：FBX 或 OBJ + 贴图（替换机身外观）。",
      "必须先有一道「输入菜谱」（可在自定义菜谱里创建）与一个「输出 SO」（食材或菜谱成品）。",
      "容量 / 每次拆分数量 / 音效都有默认值，可后调。",
    ],
  },
];

function kindMeta(kind: string): KindMeta {
  return KIND_META.find((k) => k.id === kind) ?? { id: kind, icon: "🧩", zh: kind, en: kind, desc: "", prep: [] };
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

/** 引用下拉/选择器里常见资产的中文译名（key = 资产文件名去扩展名；
 *  后端 ScanRefs 的 nameZh 对这些条目返回的也是英文资产名，故前端补一层映射）。
 *  未收录的条目（用户自建内容、modelSO 食物模型等）按原名显示。 */
const REF_LABEL_ZH: Record<string, string> = {
  // common04/_supplies（UI / 特效 / Sludge）
  CookingUI: "烹饪进度 UI",
  HoverProgressUI: "悬停进度条",
  IngredientsContentsUI: "食材内容面板",
  TutorialIconUI: "教程图标",
  UI_Move: "移动指示 UI",
  horde_order_ui: "人潮订单 UI",
  "PFX_chop_01": "切菜粒子特效",
  "PFX_Fire_Station": "火焰特效（着火）",
  "PFX_FireStatic_Intro": "登场/生成特效",
  "PFX_throw_01": "投掷粒子",
  Sludge: "焦糊物（无匹配菜谱）",
  // 装盘容器（PlatingStepData）
  Plate: "盘子",
  Glass: "玻璃杯",
  Mug: "马克杯",
  // 槽位配置
  TraySlot: "托盘槽位配置（默认）",
  // common04 模板 prefab（会被 prefab 类下拉扫到）
  custom_counter: "工作台模板（普通）",
  custom_counter_chopping: "工作台模板（切菜）",
  custom_counter_sink: "工作台模板（水槽）",
  custom_ingredient_workable: "食材模板（可加工）",
  custom_ingredient_unworkable: "食材模板（不可加工）",
  custom_ingredient_preparation: "食材模板（备餐容器）",
  custom_utensil_plate: "盘子（模板）",
  custom_utensil_tray: "托盘（模板）",
  custom_utensil_clean_plate_stack: "干净盘堆",
  custom_utensil_dirty_plate: "单个脏盘",
  custom_utensil_dirty_plate_stack: "脏盘堆",
  custom_utensil_clean_tray_stack: "干净托盘堆",
  custom_utensil_dirty_tray: "单个脏托盘",
  custom_utensil_dirty_tray_stack: "脏托盘堆",
  // commonW1 火锅
  web_cooking_region_floorburner: "火锅灶台",
  "web_dlc10_cooking_region_floorburner": "火锅灶台（DLC10）",
  web_utensil_large_pot_01: "火锅大锅",
  "web_utensil_large_pot_01_pushable": "火锅大锅（可推动）",
  "web_utensil_dlc10_large_pot_01": "火锅大锅（DLC10）",
  web_dlc10_pushable_object: "可推动物件（DLC10）",
};

function refLabelZh(r: CustomContentRef): string {
  return REF_LABEL_ZH[r.id] ?? (r.nameZh && r.nameZh !== r.id ? r.nameZh : r.id);
}

function refOptionLabel(r: CustomContentRef): string {
  const zh = refLabelZh(r);
  const en = r.nameEn && r.nameEn !== r.id ? r.nameEn : r.id;
  return zh === en ? zh : `${zh} · ${en}`;
}

/** 字段使用频率徽标：常用 = 新建时可能要动；进阶 = 99% 保持默认。 */
function fieldBadge(type: "common" | "adv"): string {
  return type === "common"
    ? '<span class="cc-tag cc-tag-common">常用</span>'
    : '<span class="cc-tag cc-tag-adv">进阶·保持默认</span>';
}

/** 每个模板的选择提示（新建时的模板卡片下方）。 */
const TEMPLATE_TIPS: Record<string, string> = {
  ingredient_workable: "可以拿去切/加工的食材（如整番茄）。系统会自动生成切割后形态，也可稍后在「可加工下一形态」里换成你自己做的。",
  ingredient_unworkable: "直接使用的食材（如生菜叶），不能切割加工。",
  ingredient_preparation: "备餐容器型食材（如碗/盘子类），可限制能放入的食材。",
  counter: "普通工作台：可放置食材/锅具，不着火。",
  counter_chopping: "切菜工作台：玩家可在此切割食材。配套的「切割后形态」在食材侧设置。",
  counter_sink: "水槽工作台模板（一般请用「自定义水槽」分类创建）。",
  hotpot_burner: "火锅专用灶台（FloorBurner），与火锅大锅配套。",
  utensil_plate: "盘子（装盘容器），一般无需自定义。",
  utensil_tray: "托盘（多槽上菜容器）。",
  utensil_hotpot: "火锅大锅：允许站台自动设为火锅灶（FloorBurner），容量 4。",
  utensil_roasting_tray: "烤箱烤盘：放烤箱（Oven）上使用。",
  utensil_griddle_pan: "煎盘：放篝火/明火（FirePit）上使用。",
  utensil_skewer: "烤串架：烧烤架（Barbeque），容量 3。",
  utensil_toasting_fork: "烤面包叉：篝火（FirePit），容量 1。",
  utensil_pot: "汤锅：普通灶台（Hob），容量 3。",
  utensil_steamer: "蒸笼：普通灶台（Hob），容量 4。",
  sink: "标准水槽：清洗（左）+ 沥干（右）两段。",
  tray: "多槽托盘：每个槽可放置一道菜谱成品。",
  prep: "备餐容器：自带 OrderNode，可限制能放入的食材。",
  splitter: "分料机：投入输入菜谱成品，按份数拆分输出。",
  clean_plate_stack: "干净盘堆（水槽沥干侧返回物）。",
  dirty_plate: "单个脏盘。",
  dirty_plate_stack: "脏盘堆（返回脏盘配置用）。",
  clean_tray_stack: "干净托盘堆。",
  dirty_tray: "单个脏托盘。",
  dirty_tray_stack: "脏托盘堆。",
};

/** 模板自带内容（参考用：克隆后立即具备的行为/组件/默认值）。 */
const TEMPLATE_INCLUDES: Record<string, string[]> = {
  ingredient_workable: ["WorkableItem（可被切割/加工）", "ThrowableItem / CarryableItem（可拿可扔）", "切割进度条 + 内容 UI 默认", "自动生成「切割后形态」与独立 OrderNode（uID/图标）"],
  ingredient_unworkable: ["ThrowableItem / CarryableItem", "IngredientPropertiesComponent + 独立 OrderNode", "内容 UI 默认"],
  ingredient_preparation: ["PreparationContainer（可放入食材）", "独立 OrderNode + 默认容器限制", "Cosmetics 外观默认"],
  counter: ["可放置食材/锅具（AttachStation）", "Flammable（长时间无人处理会着火）", "进度条 / 火焰特效 / 教程图标默认"],
  counter_chopping: ["工作台全部功能", "Workstation：玩家可在此切割食材", "切菜粒子特效默认"],
  counter_sink: ["清洗（左）+ 沥干/返盘（右）两段分区", "干净盘堆默认", "进度条默认"],
  hotpot_burner: ["FloorBurner 烹饪区（火锅大锅专属灶台）", "占格 1×1，可与工作台组合排布"],
  utensil_hotpot: ["容量 4，允许站台=火锅灶（FloorBurner）", "内置 BoxCollider 锅体判定", "内容 UI / Sludge / 生成特效默认"],
  utensil_roasting_tray: ["容量 4，允许站台=烤箱（Oven）", "内容 UI / Sludge / 生成特效默认"],
  utensil_griddle_pan: ["容量 4，允许站台=篝火（FirePit）", "内容 UI / Sludge / 生成特效默认"],
  utensil_skewer: ["容量 3，允许站台=烧烤架（Barbeque）", "内容 UI / Sludge / 生成特效默认"],
  utensil_toasting_fork: ["容量 1，允许站台=篝火（FirePit）", "内容 UI / Sludge / 生成特效默认"],
  utensil_pot: ["容量 3，允许站台=普通灶台（Hob）", "内容 UI / Sludge / 生成特效默认"],
  utensil_steamer: ["容量 4，允许站台=普通灶台（Hob）", "内容 UI / Sludge / 生成特效默认"],
  utensil_plate: ["盘子（装盘容器）", "可放置成品菜谱"],
  utensil_tray: ["TrayPlacementContainer 多槽（默认 TraySlot 配置）", "每个槽可放一道菜谱成品", "Sludge 默认"],
  sink: ["清洗（WashingStation）+ 沥干（PlateReturnStation）分区", "干净盘堆默认", "进度条默认"],
  tray: ["多槽上菜容器（默认 TraySlot 配置）", "可拿起/放置多道成品", "Sludge 默认"],
  prep: ["PreparationContainer（限制可放入食材）", "独立 OrderNode + 图标", "Cosmetics 外观默认"],
  splitter: ["IngredientSplitter 组件（投入→拆分）", "4 档填充模型（按存量显示）", "进度条 UI 默认（CookingUI）"],
  clean_plate_stack: ["水槽沥干侧的干净盘堆返回物"],
  dirty_plate: ["单个脏盘（返回系统用）"],
  dirty_plate_stack: ["脏盘堆（返回系统用）"],
  clean_tray_stack: ["干净托盘堆（返回系统用）"],
  dirty_tray: ["单个脏托盘（返回系统用）"],
  dirty_tray_stack: ["脏托盘堆（返回系统用）"],
};

/** 模板占格（镜像后端 FootprintOf；供参考展示）。 */
function templateFootprint(t: CustomContentTemplate): string {
  if (t.id === "utensil_hotpot") return "2×2";
  if (t.id === "sink" || t.id === "counter_sink") return "2×1";
  if (t.kind === "utensil" || t.kind === "tray") return "0.9×0.9（手持/放置道具）";
  return "1×1";
}

/** 模板特有的补充准备说明（选中模板后追加到「开始前请准备」卡）。 */
const TEMPLATE_PREP: Record<string, string[]> = {
  ingredient_workable: [
    "切割链路一步完成：在「🔪 切片形态」区一并上传切片模型（+可选切片图标），保存时自动创建切片食材并关联——例：整番茄 → 切片番茄",
    "不传切片模型 = 自动生成默认切割形态（模板外观）；之后也可编辑本条，在「可加工下一形态」里换成自制切片食材",
  ],
  counter_chopping: ["切菜行为由食材侧决定：场景里需存在「可加工食材」才能验证切割"],
  utensil_hotpot: ["允许站台已自动设为火锅灶（FloorBurner）；场景需放置火锅灶台配套验证（模板 hotpot_burner 或原版火锅灶）"],
  utensil_roasting_tray: ["允许站台已自动设为烤箱（Oven）"],
  utensil_griddle_pan: ["允许站台已自动设为篝火（FirePit）"],
  utensil_skewer: ["允许站台已自动设为烧烤架（Barbeque）"],
  utensil_toasting_fork: ["允许站台已自动设为篝火（FirePit）"],
  utensil_pot: ["允许站台已自动设为普通灶台（Hob）"],
  utensil_steamer: ["允许站台已自动设为普通灶台（Hob）"],
  splitter: ["前置：先有一道自定义菜谱（输入）与一个输出 SO（如自定义食材）——没有就先去对应页面创建"],
};

// ---------------------------------------------------------------------------
// 测试素材（随 dist 分发，来源 Docs/test/assets/，可用生成脚本复现）
// ---------------------------------------------------------------------------

const TEST_ASSET_BASE = "/test-assets";

/** 按 kind 给出对应的测试素材（食材类=整球，站台类=盒子）。 */
function testAssetsForKind(kind: string): { icon: string; modelStem: string; modelFiles: string[] } {
  const modelStem = kind === "ingredient" || kind === "prep" ? "model_test_ingredient" : "model_test_counter";
  return {
    icon: `${TEST_ASSET_BASE}/icon_ingredient_128.png`,
    modelStem,
    modelFiles: [`${modelStem}.obj`, `${modelStem}.mtl`, `${modelStem}_base_color.png`],
  };
}

async function fetchAsFile(url: string, name: string): Promise<File> {
  const r = await fetch(url);
  if (!r.ok) throw new Error("下载测试素材失败：" + url);
  return new File([await r.blob()], name, { type: r.headers.get("Content-Type") ?? "application/octet-stream" });
}

// ---------------------------------------------------------------------------
// SPA 外壳与路由
// ---------------------------------------------------------------------------

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
  if (el) el.innerHTML = `<div class="m-block"><h3>出错 · Error</h3><p>${esc(msg)}</p></div>`;
}

/** SPA 导航：pushState 后重渲染（不整页刷新，保留滚动与上下文）。 */
function go(path: string, app: HTMLElement): void {
  if (location.pathname !== path) history.pushState(null, "", path);
  void renderRoute(app);
}

function syncPath(path: string): void {
  if (location.pathname !== path) history.pushState(null, "", path);
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

// ---------------------------------------------------------------------------
// 关卡集选择
// ---------------------------------------------------------------------------

async function renderSetChooser(app: HTMLElement): Promise<void> {
  syncPath("/custom-content");
  const content = shell(app, "自定义内容管理 · Custom Content");
  busy("加载关卡集…");
  let sets: LevelSetInfo[] = [];
  try {
    sets = await api.fetchSets();
  } catch (e) {
    showError(e);
    return;
  }
  content.innerHTML = `
    <div class="m-section-title">选择关卡集 <span class="cc-en">Choose a level set</span></div>
    <p class="modal-hint">自定义内容按关卡集保存（Assets/LevelSets/&lt;set&gt;/custom_content）。模板来自 common04 / common02 / common03 / commonW1，模板本身只读、永不修改。</p>
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
    b.addEventListener("click", () => go(customContentListPath(b.dataset.open!), app))
  );
}

// ---------------------------------------------------------------------------
// 总览 Hub
// ---------------------------------------------------------------------------

async function renderHub(app: HTMLElement, setId: string): Promise<void> {
  const content = shell(app, `自定义内容 · ${setId}`, setId);
  content.innerHTML = `
    <div class="cc-hero">
      <div><h2>🧩 自定义内容 <span class="cc-en">Custom content</span></h2>
        <p>选择类型进入管理。所有引用字段都用下拉/选择器选择，无需手输 GUID。</p></div>
      <div class="cc-hero-actions">
        ${mBtnHtml("🍽️ 自定义菜谱", "default", { "data-nav-recipes": "" })}
        ${mBtnHtml("🍔 汉堡工作台", "default", { "data-nav-burger": "" })}
        ${mBtnHtml("🥩 夹心工作台", "default", { "data-nav-filling": "" })}
      </div>
    </div>
    <p class="modal-hint">💡 命名规则：标识 id 仅限英文字母/数字/下划线，且在同一关卡集内全局唯一（跨类型也不能重名）。模板克隆自 common04 等素材库，创建后可以自由修改自己的副本。</p>
    <div class="m-grid cc-kind-grid">
      ${KIND_META.map(
        (k) => `<div class="m-card cc-kind-card" data-kind="${k.id}">
          <div class="cc-kind-icon">${k.icon}</div>
          <h3>${esc(k.zh)} <span class="muted">(${esc(k.en)})</span></h3>
          <div class="m-meta">${esc(k.desc)}</div>
          <div class="m-actions">${mBtnHtml("进入管理 · Manage", "primary", { "data-kind": k.id })}</div>
        </div>`
      ).join("")}
    </div>`;
  content.querySelectorAll<HTMLButtonElement>("[data-kind]").forEach((b) =>
    b.addEventListener("click", () => go(customContentListPath(setId, b.dataset.kind!), app))
  );
  content.querySelector("[data-nav-recipes]")?.addEventListener("click", () => go(recipeListPath(setId), app));
  content.querySelector("[data-nav-burger]")?.addEventListener("click", () => go(burgerPath(setId), app));
  content.querySelector("[data-nav-filling]")?.addEventListener("click", () => go(fillingPath(setId), app));
}

// ---------------------------------------------------------------------------
// 列表页
// ---------------------------------------------------------------------------

function templateLabel(templateId?: string): string {
  return templateId || "—";
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
  let search = "";
  const filtered = (): CustomContentItem[] => {
    const q = search.trim().toLowerCase();
    if (!q) return items;
    return items.filter((it) =>
      [it.nameZh, it.nameEn, it.id, it.templateId].some((v) => (v ?? "").toLowerCase().includes(q))
    );
  };
  const renderGrid = (): void => {
    const list = filtered();
    const grid = document.getElementById("cc-item-grid")!;
    grid.innerHTML = list
      .map(
        (it) => `<div class="m-card cc-item-card">
          <div class="cc-item-top">
            <img class="cc-item-icon" src="${esc(api.customContentIconUrl(it))}" alt="" onerror="this.style.visibility='hidden'"/>
            <div>
              <h3>${esc(it.nameZh || it.nameEn || it.id)}</h3>
              <div class="muted small">${esc(it.nameEn && it.nameEn !== it.id ? it.nameEn + " · " : "")}${esc(it.id)}</div>
            </div>
          </div>
          <div class="cc-card-foot">
            <span class="cc-chip-tag">📦 ${esc(templateLabel(it.templateId))}</span>
            ${it.orderUid ? `<span class="cc-chip-tag">uID ${it.orderUid}</span>` : ""}
            ${it.hasModel ? '<span class="cc-chip-tag ok">👁 有模型</span>' : '<span class="cc-chip-tag dim">无模型</span>'}
          </div>
          <div class="m-meta"><span class="cc-path">${esc(it.assetPath)}</span>
            ${it.warnings?.length ? `<div class="cc-warn">⚠ ${esc(it.warnings.join("；"))}</div>` : ""}</div>
          <div class="m-actions">
            ${mBtnHtml("编辑 · Edit", "primary", { "data-edit": esc(it.id) })}
            ${it.hasModel ? mBtnHtml("👁", "default", { "data-preview": esc(it.id), title: "3D 预览" }) : ""}
            ${dangerBtnHtml("删除", { "data-del": esc(it.id) })}
          </div>
        </div>`
      )
      .join("") ||
      `<div class="cc-empty"><div>${meta.icon}</div><p>${search ? "没有匹配的自定义内容" : "还没有自定义内容 · Nothing here yet"}</p>
        ${search ? "" : mPrimaryBtnHtml("📋 从模板创建（先看模板参考）", { "data-browse": "" })}</div>`;
    wireCards(grid);
  };
  content.innerHTML = `
    <div class="cc-list-head">
      <div>
        <h2>${meta.icon} ${esc(meta.zh)} <span class="muted">(${esc(meta.en)}) · ${items.length}</span></h2>
        <p>${esc(meta.desc)}</p>
      </div>
      <div class="cc-actions">
        <input type="search" id="cc-search" class="rl-search" placeholder="搜索名称 / id / 模板…">
        ${mBtnHtml("📋 模板参考", "default", { "data-browse": "" })}
        ${mPrimaryBtnHtml("＋ 新建 " + meta.zh, { "data-new": "" })}
      </div>
    </div>
    <div class="m-grid cc-item-grid" id="cc-item-grid"></div>`;
  document.getElementById("cc-search")?.addEventListener("input", (ev) => {
    search = (ev.target as HTMLInputElement).value;
    renderGrid();
  });
  const openBrowser = (): void => {
    openTemplateBrowser(kind, (templateId) => {
      pendingTemplateId = templateId;
      go(customContentFormPath(setId, kind, "new"), app);
    });
  };
  content.querySelectorAll<HTMLButtonElement>("[data-browse]").forEach((b) =>
    b.addEventListener("click", openBrowser)
  );
  const wireCards = (root: HTMLElement): void => {
    root.querySelectorAll<HTMLButtonElement>("[data-new]").forEach((b) =>
      b.addEventListener("click", () => go(customContentFormPath(setId, kind, "new"), app))
    );
    root.querySelectorAll<HTMLButtonElement>("[data-edit]").forEach((b) =>
      b.addEventListener("click", () => go(customContentFormPath(setId, kind, b.dataset.edit!), app))
    );
    root.querySelectorAll<HTMLButtonElement>("[data-del]").forEach((b) =>
      b.addEventListener("click", () => {
        const id = b.dataset.del!;
        const item = items.find((x) => x.id === id);
        confirmDeleteItem(setId, kind, id, item, () => void renderList(app, setId, kind));
      })
    );
    root.querySelectorAll<HTMLButtonElement>("[data-preview]").forEach((b) =>
      b.addEventListener("click", () => {
        const item = items.find((x) => x.id === b.dataset.preview!);
        if (item) void previewSavedModel(item);
      })
    );
  };
  renderGrid();
  content.querySelectorAll<HTMLButtonElement>("[data-new]").forEach((b) =>
    b.addEventListener("click", () => go(customContentFormPath(setId, kind, "new"), app))
  );
}

function confirmDeleteItem(
  setId: string,
  kind: string,
  id: string,
  item: CustomContentItem | undefined,
  onDone: () => void
): void {
  const name = item?.nameZh || item?.nameEn || id;
  openModal(
    `删除 · ${esc(name)}`,
    `<p>将永久删除该自定义内容及其 <b>prefab / OrderNode / 图标 / 模型文件</b>，<b>不可恢复</b>。</p>
     <p class="modal-hint">⚠️ 若已有关卡放置了它，对应场景位置会失效（变回空位）；请先在编辑器里移除相关摆放再删除。</p>`,
    `${mCancelBtnHtml()}${dangerBtnHtml("确认删除", { "data-ok": "" })}`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-ok]")?.addEventListener("click", async () => {
    showBusy("删除中…");
    try {
      await api.deleteCustomContent(setId, kind, id);
      closeModal();
      setStatus("已删除");
      if (kind === "ingredient" || kind === "prep") refreshEditorIngredients(setId);
      onDone();
    } catch (e) {
      setStatus((e as Error).message || String(e), false);
    } finally {
      hideBusy();
    }
  });
}

// ---------------------------------------------------------------------------
// 3D 预览（已保存模型）
// ---------------------------------------------------------------------------

function transformOf(item: CustomContentItem | undefined): ModelTransformValues {
  if (!item?.hasModel) {
    return { scale: 1, rotationX: 0, rotationY: 0, rotationZ: 0, positionX: 0, positionY: 0, positionZ: 0, pivotX: 0, pivotY: 0, pivotZ: 0 };
  }
  return {
    scale: item.modelScale ?? 1,
    rotationX: item.modelRotationX ?? 0,
    rotationY: item.modelRotationY ?? 0,
    rotationZ: item.modelRotationZ ?? 0,
    positionX: item.modelPositionX ?? 0,
    positionY: item.modelPositionY ?? 0,
    positionZ: item.modelPositionZ ?? 0,
    pivotX: item.modelPivotX ?? 0,
    pivotY: item.modelPivotY ?? 0,
    pivotZ: item.modelPivotZ ?? 0,
  };
}

async function previewSavedModel(item: CustomContentItem): Promise<void> {
  await withBusy("正在加载 3D 预览…", async () => {
    const src = await api.fetchCustomContentModelSource(item.assetPath);
    if (!src.modelFile) {
      setStatus("该条目没有已保存的模型文件。", false);
      return;
    }
    const textures = src.files
      .filter((n) => /\.(png|jpg|jpeg|tga)$/i.test(n))
      .map((n) => api.customContentModelFileUrl(item.assetPath, n));
    const { openModelPreview } = await import("./modelPreview");
    openModelPreview({
      title: item.nameZh || item.id,
      resourceBase: `/api/custom-content/model-file?assetPath=${encodeURIComponent(item.assetPath)}&file=`,
      modelFileName: src.modelFile,
      remoteTextures: textures.length ? textures : undefined,
      fitTarget: "plate",
      showReference: item.kind === "ingredient" || item.kind === "prep",
      ...transformOf(item),
    });
  });
}

// ---------------------------------------------------------------------------
// 表单：字段组件
// ---------------------------------------------------------------------------

function refOptions(refs: CustomContentRef[], current: string, emptyLabel: string, valueField: "guid" | "id"): string {
  const seen = new Set<string>();
  const opts = refs
    .filter((r) => {
      if (!r) return false;
      const v = valueField === "id" ? r.id : r.guid;
      if (!v || seen.has(v)) return false;
      seen.add(v);
      return true;
    })
    .map((r) => {
      const value = valueField === "id" ? r.id : r.guid;
      return `<option value="${esc(value)}" ${value === current ? "selected" : ""}>${esc(refOptionLabel(r))}</option>`;
    })
    .join("");
  return `<option value="">${esc(emptyLabel)}</option>${opts}`;
}

/** 带提示与「清除」能力的引用下拉字段。clearKey 提供时编辑态可标记清除（保存后生效）。 */
function refField(opts: {
  label: string;
  id: string;
  refs: CustomContentRef[];
  current: string;
  hint: string;
  emptyLabel?: string;
  valueField?: "guid" | "id";
  clearKey?: string;
  editable: boolean;
  badge?: "common" | "adv";
}): string {
  const empty = opts.emptyLabel ?? "— 不设置 —";
  const vf = opts.valueField ?? "guid";
  const badge = opts.badge ? ` ${fieldBadge(opts.badge)}` : "";
  const clearBtn =
    opts.clearKey && opts.editable
      ? `<button type="button" class="m-btn small default cc-clear-btn" data-clear-field="${esc(opts.clearKey)}" style="${opts.current ? "" : "display:none"}">✖ 清除</button>`
      : "";
  return `<label class="m-field">${esc(opts.label)}${badge}
    <div class="cc-field-row">
      <select id="${opts.id}" class="m-select cc-select">${refOptions(opts.refs, opts.current, empty, vf)}</select>
      ${clearBtn}
    </div>
    <span class="muted small">${esc(opts.hint)}</span>
    <span class="cc-cleared-note muted small" data-cleared-note="${esc(opts.clearKey ?? "")}" style="display:none">已标记清除，保存后生效</span>
  </label>`;
}

function templateCards(templates: CustomContentTemplate[], current: string, isNew: boolean): string {
  if (!isNew) {
    const t = templates.find((x) => x.id === current);
    return `<div class="cc-template-current">当前模板：<b>${esc(t?.labelZh || current || "—")}</b> <span class="muted">(${esc(t?.labelEn || "")} · ${esc(current || "—")})</span>
      <div class="muted small">模板在创建后不可更换；如需其它模板请新建一条。</div>
      ${t ? mBtnHtml("🔍 查看模板参考", "default", { "data-tpl-view": esc(t.id) }) : ""}
    </div>`;
  }
  return `<div class="cc-template-grid">${templates
    .map((t) => {
      const tip = TEMPLATE_TIPS[t.id] ? `<p class="muted small cc-template-tip">${esc(TEMPLATE_TIPS[t.id])}</p>` : "";
      return `<button type="button" class="cc-template-card${t.id === current ? " active" : ""}" data-template="${esc(t.id)}">
        <b>${esc(t.labelZh)} <span class="muted">· ${esc(t.labelEn)}</span></b><small>${esc(t.id)}</small>${tip}
        <span class="cc-tpl-view" data-tpl-view="${esc(t.id)}" title="查看模板详情（不选择）">🔍 详情</span>
      </button>`;
    })
    .join("")}</div>`;
}

function stationChecks(metaInfo: CustomContentMeta, selected: string[]): string {
  const set = new Set(selected);
  return (metaInfo.stationTypes ?? [])
    .map(
      (s) => `<label class="cc-check" title="${esc(s.labelEn)}"><input type="checkbox" class="cc-station" value="${esc(s.name)}" ${set.has(s.name) ? "checked" : ""}/> <span>${esc(s.labelZh)}</span></label>`
    )
    .join("");
}

function multiButton(id: string, label: string): string {
  return `<button type="button" class="m-btn default cc-multi-btn" id="${id}">${esc(label)}</button>`;
}

// ---------------------------------------------------------------------------
// 表单主体
// ---------------------------------------------------------------------------

interface PendingTexture {
  cls: TextureClass;
  diskName: string;
  bytes: Uint8Array;
  file: File;
}

async function renderForm(app: HTMLElement, setId: string, kind: string, idOrNew: string): Promise<void> {
  const meta = kindMeta(kind);
  const isNew = idOrNew === "new";
  const content = shell(app, `${isNew ? "新建" : "编辑"}${meta.zh} · ${setId}`, setId, kind);
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
    showError(`未找到「${idOrNew}」，可能已被删除。`);
    return;
  }

  let selectedTemplate = item?.templateId || pendingTemplateId || templates[0]?.id || "";
  if (pendingTemplateId && !templates.some((t) => t.id === pendingTemplateId)) selectedTemplate = templates[0]?.id ?? "";
  const fromTemplateBrowser = isNew && !!pendingTemplateId;
  pendingTemplateId = "";
  let templateAssetPath = item?.assetPath || templates.find((t) => t.id === selectedTemplate)?.assetPath || "";
  let ingredientGuids: string[] = item?.allowedIngredientGuids ?? [];
  let modelGuids: string[] = item?.modelGuids ?? [];
  let modelSoGuids: string[] = item?.modelSoGuids ?? [];
  let hierarchy: string[] = [];
  let modelSource: { modelFile: string; files: string[] } | null = null;
  let modelT = transformOf(item);

  // 待上传状态
  const clearPending = new Set<string>();
  let pendingModel: { name: string; bytes: Uint8Array; mtl: string | null } | null = null;
  let pendingTextures: PendingTexture[] = [];
  let iconBase64: string | undefined;
  let iconPreviewUrl: string | undefined;

  // 🔪 切片形态（可加工食材 + 新建）：一并创建「切割后食材」并自动关联
  let slicedPending: { name: string; bytes: Uint8Array; mtl: string | null } | null = null;
  let slicedTextures: PendingTexture[] = [];
  let slicedIconBase64: string | undefined;
  let slicedT: ModelTransformValues = transformOf(undefined);
  const showSlicedSection = kind === "ingredient" && isNew;

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

  // ---- 各 kind 参数区块（每个字段都带提示） ----

  const isPrepKind = kind === "prep";
  const ingredientFields = `
    <div class="cc-section"><div class="cc-section-title">🥕 食材参数 <span class="cc-en">Ingredient settings</span></div>
      <p class="modal-hint">✅ <b>普通食材这里全部保持默认即可</b>——模板已带全套默认。只有三种情况需要改：
        ① 食材要被切、且切完变成<b>指定</b>食材 → 填「可加工下一形态」（例：整番茄 → 切片番茄）；
        ② 上桌容器不是普通盘子（饮料→玻璃杯、汤→马克杯）→ 改「装盘容器」；
        ③ 备餐容器要限制能放哪些食材 → 设「容器限制」。
        其余字段是引擎内部 UI / 装饰引用，属进阶选项。</p>
      <div class="cc-grid">
        ${refField({ label: "装盘容器 · Plating", id: "cc-plating", refs: options.plating, current: item?.platingStepId ?? "", valueField: "id", clearKey: "platingStepId", editable: !isNew, badge: "common",
          hint: "该食材作为订单出现时用的容器：盘子（默认）/ 玻璃杯（饮料类）/ 马克杯（汤类）。普通食材不用改。" })}
        ${refField({ label: "可加工下一形态 · Next form（切割后）", id: "cc-next-prefab", refs: options.prefabs, current: item?.nextPrefabGuid ?? "", clearKey: "nextPrefabGuid", editable: !isNew, badge: "common",
          hint: "仅「可加工食材」模板生效：切完变成它（例：整番茄 → 切片番茄）。需先建好切割后食材，可先保存本条再回来指定；留空 = 自动生成的默认切割形态。" })}
        ${isPrepKind
          ? refField({ label: "容器限制 · Restrictions", id: "cc-restrictions", refs: options.lookups, current: item?.restrictionsGuid ?? "", clearKey: "restrictionsGuid", editable: !isNew, badge: "common",
              hint: "备餐容器核心参数：限制能放进本容器的食材集合（如「只许放生菜/番茄」的沙拉碗）。不设置 = 模板默认集合。" })
          : ""}
        <label class="m-field">OrderNode 唯一 uID ${fieldBadge("adv")}
          <input id="cc-order-uid" type="number" min="0" value="${item?.orderUid ?? 0}" class="cc-num">
          <span class="muted small">0 = 自动分配（推荐，保持 0 即可）。手填必须全工程唯一（与原版食材也不能重复），重复会被拒绝。</span>
        </label>
      </div>
      <details class="cc-advanced">
        <summary>高级选项 · Advanced（引擎内部引用，保持默认）</summary>
        <div class="cc-grid">
          ${isPrepKind
            ? ""
            : refField({ label: "容器限制 · Restrictions", id: "cc-restrictions", refs: options.lookups, current: item?.restrictionsGuid ?? "", clearKey: "restrictionsGuid", editable: !isNew, badge: "adv",
                hint: "仅备餐容器生效：限制能放入本容器的食材集合（OrderToPrefabLookup）。普通食材无效果，保持默认。" })}
          ${refField({ label: "Cosmetics 外观 · Cosmetics prefab", id: "cc-cosmetics", refs: options.prefabs, current: item?.cosmeticsPrefabGuid ?? "", clearKey: "cosmeticsPrefabGuid", editable: !isNew, badge: "adv",
            hint: "食材/容器附带的外部装饰件（如碗沿装饰）。99% 情况保持默认。" })}
          ${refField({ label: "食材内容 UI · Content UI", id: "cc-ingredient-ui", refs: options.soRefs, current: item?.ingredientContentUiGuid ?? "", clearKey: "ingredientContentUiGuid", editable: !isNew, badge: "adv",
            hint: "拿起食材时显示内容的小面板（引擎 UI）。保持默认。" })}
          ${refField({ label: "进度 UI · Progress UI", id: "cc-progress-ui", refs: options.soRefs, current: item?.progressUiGuid ?? "", clearKey: "progressUiGuid", editable: !isNew, badge: "adv",
            hint: "切割/加工时的进度条（引擎 UI）。保持默认。" })}
        </div>
      </details>
    </div>`;

  const commonUiFields = `
    <div class="cc-section"><div class="cc-section-title">🎨 通用 UI / 特效 <span class="cc-en">Common UI & FX</span></div>
      <p class="modal-hint">✅ 模板已带全套默认（进度条 / 着火火焰 / 切菜粒子 / 教程图标）。<b>通常整个区块都不用打开</b>——只有想换特效风格时才需要。</p>
      <details class="cc-advanced">
        <summary>高级选项 · Advanced（4 项引擎引用，保持默认）</summary>
        <div class="cc-grid">
          ${refField({ label: "进度 UI · Progress UI", id: "cc-progress-ui", refs: options.soRefs, current: item?.progressUiGuid ?? "", clearKey: "progressUiGuid", editable: !isNew, badge: "adv",
            hint: "进度条（清洗/烹饪/燃烧通用）。保持默认。" })}
          ${refField({ label: "火焰特效 · Fire FX", id: "cc-fire-fx", refs: options.soRefs, current: item?.fireEffectGuid ?? "", clearKey: "fireEffectGuid", editable: !isNew, badge: "adv",
            hint: "工作台长时间无人处理后着火的火焰特效。保持默认。" })}
          ${refField({ label: "切菜粒子 · Chop PFX", id: "cc-chop-pfx", refs: options.soRefs, current: item?.chopPfxGuid ?? "", clearKey: "chopPfxGuid", editable: !isNew, badge: "adv",
            hint: "切菜工作台的切割粒子特效。保持默认。" })}
          ${refField({ label: "教程图标 · Tutorial icon", id: "cc-tutorial-icon", refs: options.soRefs, current: item?.tutorialIconGuid ?? "", clearKey: "tutorialIconGuid", editable: !isNew, badge: "adv",
            hint: "教学模式中悬停提示的图标。保持默认。" })}
        </div>
      </details>
    </div>`;

  const utensilFields = `
    <div class="cc-section"><div class="cc-section-title">🍳 锅具参数 <span class="cc-en">Utensil settings</span></div>
      <div class="cc-grid">
        <label class="m-field">容量 · Capacity
          <input id="cc-capacity" type="number" min="0" value="${currentCapacity}" class="cc-num">
          <span class="muted small">锅内能同时放的食材数（0 = 模板默认）。</span>
        </label>
        <label class="m-field">允许站台 · Allowed stations
          <div class="cc-checks">${stationChecks(metaInfo, currentStations)}</div>
          <span class="muted small">决定锅能放到哪种灶台上：火锅=FloorBurner、烤箱=Oven、篝火/煎盘=FirePit、烤串=Barbeque、普通灶=Hob。必须至少勾一个，否则真机无法放置。</span>
        </label>
        <label class="m-field">额外食材 · Extra ingredients（多选）
          <div class="cc-multi-row">${multiButton("cc-pick-ings", "选择额外食材…")}<span id="cc-ings-count" class="muted small"></span></div>
          <span class="muted small">限制本锅能煮哪些食材。不选 = 接受所有主线食材。</span>
        </label>
        <label class="m-field">锅内模型 prefab（多选）
          <div class="cc-multi-row">${multiButton("cc-pick-models", "选择模型 prefab…")}<span id="cc-models-count" class="muted small"></span></div>
          <span class="muted small">锅内食材的 3D 展示模型，顺序与「额外食材」一一对应；某个留空时用该食材默认模型。</span>
        </label>
        <label class="m-field">锅内模型 SO（多选）
          <div class="cc-multi-row">${multiButton("cc-pick-model-sos", "选择模型 SO…")}<span id="cc-model-sos-count" class="muted small"></span></div>
          <span class="muted small">同上，但引用 PseudoPrefabSO（跨 bundle 模型）。与 prefab 二选一即可。</span>
        </label>
      </div>
      <details class="cc-advanced">
        <summary>高级选项 · Advanced（3 项引擎引用，保持默认）</summary>
        <div class="cc-grid">
          ${refField({ label: "食材内容 UI · Content UI", id: "cc-ingredient-ui", refs: options.soRefs, current: item?.ingredientContentUiGuid ?? "", clearKey: "ingredientContentUiGuid", editable: !isNew, badge: "adv",
            hint: "锅内内容显示面板（引擎 UI）。保持默认。" })}
          ${refField({ label: "Sludge 焦糊模型 · Sludge", id: "cc-sludge", refs: options.soRefs, current: item?.sludgeGuid ?? "", clearKey: "sludgeGuid", editable: !isNew, badge: "adv",
            hint: "锅里放了不匹配食材时显示的「糊」模型。保持默认。" })}
          ${refField({ label: "生成特效 · Spawn FX", id: "cc-spawn-fx", refs: options.soRefs, current: item?.spawnEffectGuid ?? "", clearKey: "spawnEffectGuid", editable: !isNew, badge: "adv",
            hint: "锅具重置/生成时的特效。保持默认。" })}
        </div>
      </details>
    </div>`;

  const sinkFields = `
    <div class="cc-section"><div class="cc-section-title">🚰 水槽参数 <span class="cc-en">Sink settings</span></div>
      <div class="cc-grid">
        <label class="m-field">清洗部件 · Washing part
          <select id="cc-washing-part" class="m-select cc-select"><option value="">— 不设置 —</option>${hierarchy.map((p) => `<option value="${esc(p)}" ${p === (item?.washingPartPath ?? "") ? "selected" : ""}>${esc(p)}</option>`).join("")}</select>
          <span class="muted small">水槽左侧「清洗」功能的子物体。通常保持模板默认。</span>
        </label>
        <label class="m-field">沥干部件 · Drying part
          <select id="cc-drying-part" class="m-select cc-select"><option value="">— 不设置 —</option>${hierarchy.map((p) => `<option value="${esc(p)}" ${p === (item?.dryingPartPath ?? "") ? "selected" : ""}>${esc(p)}</option>`).join("")}</select>
          <span class="muted small">水槽右侧「沥干/返盘」功能的子物体。通常保持模板默认。</span>
        </label>
        ${refField({ label: "干净盘堆 · Clean plate stack", id: "cc-clean-stack", refs: options.soRefs, current: item?.cleanPlateStackGuid ?? "", clearKey: "cleanPlateStackGuid", editable: !isNew, badge: "adv",
          hint: "沥干完成后弹出的干净盘子堆（引擎引用）。保持默认。" })}
      </div>
    </div>`;

  const trayFields = `
    <div class="cc-section"><div class="cc-section-title">🍽️ 托盘参数 <span class="cc-en">Tray settings</span></div>
      <div class="cc-grid">
        ${refField({ label: "槽位配置 · Slot config", id: "cc-tray-slot", refs: options.orderLists, current: item?.traySlotGuid ?? "", clearKey: "traySlotGuid", editable: !isNew,
          hint: "每个槽接受哪些菜谱成品（OrderToPrefabListSO）。当前版本所有槽位共用此配置，一般保持默认 TraySlot。" })}
        ${refField({ label: "Sludge 焦糊模型 · Sludge", id: "cc-sludge", refs: options.soRefs, current: item?.sludgeGuid ?? "", clearKey: "sludgeGuid", editable: !isNew, badge: "adv",
          hint: "托盘内无匹配菜谱时的「糊」模型（引擎引用）。保持默认。" })}
      </div>
    </div>`;

  const splitterFields = `
    <div class="cc-section"><div class="cc-section-title">🔀 分料机参数 <span class="cc-en">Splitter settings</span></div>
      <p class="modal-hint">分料机把一份「输入菜谱成品」拆成多份「输出 SO」。例：输入=一份米饭菜谱，输出=米饭食材 ×2；输出为 optional 组装定义时会自动套用（如三明治底）。</p>
      <div class="cc-grid">
        <label class="m-field">容量 · Capacity
          <input id="cc-capacity" type="number" min="1" value="${currentCapacity}" class="cc-num">
          <span class="muted small">机内最多容纳的份数（默认 8）。</span>
        </label>
        <label class="m-field">每次拆分 · Split count
          <input id="cc-split-num" type="number" min="1" value="${currentSplit}" class="cc-num">
          <span class="muted small">投入 1 份拆出的数量（默认 2）。</span>
        </label>
        ${refField({ label: "输入菜谱 · Input recipe（必填）", id: "cc-input-recipe", refs: options.recipes, current: item?.inputRecipeGuid ?? "", clearKey: "inputRecipeGuid", editable: !isNew,
          hint: "玩家投入什么会被拆分。只列自定义菜谱；没有合适的菜谱请先去「自定义菜谱」创建。" })}
        ${refField({ label: "输出食材/菜谱 · Output SO（必填）", id: "cc-output-prefab", refs: options.soRefs, current: item?.outputPrefabGuid ?? "", clearKey: "outputPrefabGuid", editable: !isNew,
          hint: "拆分后生成什么（PseudoPrefabSO；支持可选组装菜谱自动套用，如自定义食材、米饭或三明治底）。" })}
        <label class="m-field">音效 tag · Audio tag
          <select id="cc-split-audio" class="m-select cc-select"><option value="">— 默认 PutDown —</option>${options.audioTags.map((t) => `<option value="${esc(t)}" ${t === currentAudio ? "selected" : ""}>${esc(t)}</option>`).join("")}</select>
          <span class="muted small">拆分完成时播放的音效。默认 PutDown。</span>
        </label>
      </div>
    </div>`;

  let kindFields = "";
  if (kind === "ingredient" || kind === "prep") kindFields = ingredientFields;
  else if (kind === "counter") kindFields = commonUiFields;
  else if (kind === "utensil") kindFields = utensilFields + commonUiFields;
  else if (kind === "sink") kindFields = sinkFields + commonUiFields;
  else if (kind === "tray") kindFields = trayFields;
  else if (kind === "splitter") kindFields = splitterFields;

  const transformSummary = (): string => {
    const t = modelT;
    return `缩放 ${fmt4(t.scale)} · 旋转 (${t.rotationX.toFixed(0)}, ${t.rotationY.toFixed(0)}, ${t.rotationZ.toFixed(0)})° · 位置 (${fmtCm(u2cm(t.positionX))}, ${fmtCm(u2cm(t.positionY))}, ${fmtCm(u2cm(t.positionZ))}) cm`;
  };

  content.innerHTML = `
    <div class="m-actions-row">
      ${mBtnHtml("← 返回列表", "default", { "data-back": "" })}
      <span class="muted">关卡集：<b>${esc(setId)}</b> · ${isNew ? `新建 ${esc(meta.zh)}` : `编辑 ${esc(item?.nameZh || item!.id)}`}</span>
      <span style="flex:1"></span>
      ${mBtnHtml("💾 保存 · Save", "primary", { "data-save": "" })}
    </div>
    <div class="cc-form">
      <div class="cc-section cc-prep">
        <div class="cc-section-title">🧾 开始前请准备 <span class="cc-en">Before you start</span>
          <span style="flex:1"></span>
          ${mBtnHtml("🧪 一键填充测试素材", "default", { "data-test-fill": "" })}
        </div>
        <ul class="cc-prep-list" id="cc-prep-list"></ul>
        <p class="muted small" style="margin:8px 0 0">🧪 会自动填入：图标（PNG）+ 模型（OBJ/MTL/贴图，走完整改名与预览管线）+ 中文名示例——之后直接点「💾 保存」即可体验全流程；素材也可在 <span class="cc-path">Docs/test/assets/</span> 查看。</p>
      </div>
      <div class="cc-section"><div class="cc-section-title">🎯 模板 <span class="cc-en">Template</span></div>
        ${templateCards(templates, selectedTemplate, isNew)}
        <p class="muted small">选择模板后自动带入容量/站台/音效等默认值；模板来自 common04 等素材库（只读），克隆副本完全归你。</p>
      </div>
      <div class="cc-section"><div class="cc-section-title">📝 基本信息 <span class="cc-en">Basics</span></div>
        <div class="cc-grid">
          <label class="m-field">标识 id（仅字母/数字/下划线，创建后不可改）
            <input id="cc-id" value="${esc(item?.id ?? "")}" ${isNew ? "" : "disabled"} placeholder="MyItem" class="cc-text">
            <span class="muted small">用于目录与资产名；同一关卡集内全局唯一（跨类型也不能重名）。</span>
          </label>
          <label class="m-field">中文名（必填）<input id="cc-zh" value="${esc(item?.nameZh ?? "")}" placeholder="我的食材" class="cc-text"></label>
          <label class="m-field">英文名 English name<input id="cc-en" value="${esc(item?.nameEn ?? "")}" placeholder="My item" class="cc-text"></label>
        </div>
      </div>
      ${kindFields}
      <div class="cc-section"><div class="cc-section-title">🖼️ 图标 <span class="cc-en">Icon</span></div>
        <div class="cc-grid">
          <label class="m-field">图标图片（PNG，建议 128×128 透明底）
            <input type="file" id="cc-icon" accept="image/png,image/jpeg" class="cc-text">
            <span class="muted small">食材/备餐容器的订单图标与道具箱盖图。JPG 会自动转换为 PNG。</span>
          </label>
          <label class="m-field">当前图标
            <div class="cc-icon-preview"><img id="cc-icon-img" src="${esc(item ? api.customContentIconUrl(item, item.updatedAt) : iconPreviewUrl ?? "")}" alt="" onerror="this.style.display='none'"><span class="muted small" id="cc-icon-note">${item?.iconUrl ? "" : "未设置（用模板默认）"}</span></div>
          </label>
        </div>
      </div>
      <div class="cc-section"><div class="cc-section-title">📦 模型（3D） <span class="cc-en">3D model</span></div>
        <div class="cc-model-block">
          <label class="m-field">上传模型与贴图（FBX / OBJ + .mtl + PNG/JPG/TGA 贴图，可多选）
            <input type="file" id="cc-model" multiple accept=".fbx,.obj,.mtl,.png,.jpg,.jpeg,.tga" class="cc-text">
            <span class="muted small">贴图按文件名自动分类（base_color/normal/roughness/metallic…，含关键词即可）；中文/空格文件名会自动改写为安全名，模型内引用同步修正。</span>
          </label>
          <div class="cc-model-state" id="cc-model-state"></div>
          <div class="cc-model-tools">
            <label class="m-field">在线预览与调整
              <div class="cc-multi-row" id="cc-preview-row">
                ${mBtnHtml("👁 预览并调整方向/大小", "default", { "data-preview-local": "", style: pendingModel ? "" : "display:none" })}
                ${mBtnHtml("👁 预览已保存模型", "default", { "data-preview-saved": "", style: !pendingModel && modelSource ? "" : "display:none" })}
                ${!pendingModel && !modelSource ? '<span class="muted small">上传模型或保存后可预览</span>' : ""}
              </div>
              <span class="muted small">变换（缩放/旋转/位置）请在 3D 预览里用「✨自动适配 / ✅应用」调整，保存时写回。</span>
            </label>
          </div>
          <div class="muted small" id="cc-transform-summary">${transformSummary()}</div>
        </div>
      </div>
      ${showSlicedSection ? `
      <div class="cc-section" id="cc-sliced-section" style="${selectedTemplate === "ingredient_workable" ? "" : "display:none"}">
        <div class="cc-section-title">🔪 切片形态（切割后食材） <span class="cc-en">Sliced form</span> ${fieldBadge("common")}</div>
        <p class="modal-hint">可加工食材专属：在这里一并上传切片模型，保存时<b>自动创建「切割后食材」条目并自动关联</b>——整番茄 → 切片番茄一步完成（切片条目之后可单独编辑升级）。全部留空 = 系统自动生成默认切割形态（模板外观）。</p>
        <div class="cc-grid">
          <label class="m-field">切片中文名<input id="cc-sliced-zh" class="cc-text" placeholder="切片·我的食材"></label>
          <label class="m-field">切片 id（默认 = 主 id + _sliced）<input id="cc-sliced-id" class="cc-text" placeholder="my_item_sliced"></label>
          <label class="m-field">切片英文名<input id="cc-sliced-en" class="cc-text" placeholder="Sliced My item"></label>
          <label class="m-field">切片图标（可选）<input type="file" id="cc-sliced-icon" accept="image/png,image/jpeg" class="cc-text">
            <span class="muted small">默认沿用主食材图标。</span></label>
          <label class="m-field">切片模型（OBJ/FBX + .mtl + 贴图）<input type="file" id="cc-sliced-model" multiple accept=".fbx,.obj,.mtl,.png,.jpg,.jpeg,.tga" class="cc-text"></label>
        </div>
        <div class="cc-model-state" id="cc-sliced-state"></div>
        <div class="cc-multi-row" style="margin-top:8px">
          ${mBtnHtml("🧪 切片测试素材", "default", { "data-sliced-test": "" })}
          ${mBtnHtml("👁 预览切片模型", "default", { "data-sliced-preview": "", style: "display:none" })}
          <span class="muted small" id="cc-sliced-icon-note"></span>
        </div>
      </div>` : ""}
    </div>
    <div class="cc-form-footer">
      ${mBtnHtml("← 返回列表", "default", { "data-back": "" })}
      <span style="flex:1"></span>
      ${mBtnHtml("💾 保存 · Save", "primary", { "data-save": "" })}
    </div>
    <pre id="cc-status" class="muted cc-status"></pre>`;

  const val = (id: string) => (document.getElementById(id) as HTMLInputElement | HTMLSelectElement | null)?.value ?? "";
  const num = (id: string) => Number(val(id)) || 0;
  const status = (msg: string, ok = true): void => {
    const el = document.getElementById("cc-status");
    if (el) el.textContent = msg;
    setStatus(msg, ok);
  };

  const setCount = (id: string, n: number, label: string) => {
    const el = document.getElementById(id);
    if (el) el.textContent = n > 0 ? `已选 ${n} 个${label}` : "未选择";
  };
  setCount("cc-ings-count", ingredientGuids.length, "食材");
  setCount("cc-models-count", modelGuids.length, "模型");
  setCount("cc-model-sos-count", modelSoGuids.length, "模型 SO");

  /** 素材准备清单：kind 通用 + 当前模板特有（切换模板时联动）。 */
  const renderPrepList = (): void => {
    const el = document.getElementById("cc-prep-list");
    if (!el) return;
    const tplPrep = TEMPLATE_PREP[selectedTemplate] ?? [];
    el.innerHTML = [...meta.prep, ...tplPrep].map((p) => `<li>${esc(p)}</li>`).join("");
  };
  renderPrepList();

  /** 中文名/英文名占位提示随模板联动。 */
  const applyNamePlaceholders = (): void => {
    const t = templates.find((x) => x.id === selectedTemplate);
    const zhEl = document.getElementById("cc-zh") as HTMLInputElement | null;
    const enEl = document.getElementById("cc-en") as HTMLInputElement | null;
    if (zhEl) zhEl.placeholder = t ? `我的${t.labelZh}` : "我的食材";
    if (enEl) enEl.placeholder = t ? `My ${t.labelEn}` : "My item";
  };
  applyNamePlaceholders();
  if (fromTemplateBrowser) {
    const p = TEMPLATE_PRESETS[selectedTemplate];
    const bits = [
      p?.capacity != null ? `容量 ${p.capacity}` : "",
      p?.stationTypes?.length ? `允许站台 ${p.stationTypes.join(" / ")}` : "",
    ]
      .filter(Boolean)
      .join(" · ");
    status(
      `已按模板「${templates.find((x) => x.id === selectedTemplate)?.labelZh ?? selectedTemplate}」进入${bits ? "，预填：" + bits : ""}。可点「🧪 一键填充测试素材」直接体验，或按上方清单自备素材`
    );
  }

  const renderModelState = (): void => {
    const el = document.getElementById("cc-model-state");
    if (!el) return;
    const parts: string[] = [];
    if (pendingModel) {
      parts.push(`📥 待上传：${esc(pendingModel.name)}${pendingModel.mtl ? " + .mtl" : ""}`);
      for (const t of pendingTextures) parts.push(`🖼 ${t.cls} → ${t.diskName}`);
    } else if (modelSource) {
      parts.push(`✅ 已保存模型：${esc(modelSource.modelFile)}${modelSource.files.length > 1 ? `（目录共 ${modelSource.files.length} 个文件）` : ""}`);
    } else {
      parts.push('<span class="muted">未上传模型（使用模板默认外观）</span>');
    }
    el.innerHTML = parts.map((p) => `<div>${p}</div>`).join("");
    const localBtn = document.querySelector<HTMLButtonElement>("[data-preview-local]");
    const savedBtn = document.querySelector<HTMLButtonElement>("[data-preview-saved]");
    if (localBtn) localBtn.style.display = pendingModel ? "" : "none";
    if (savedBtn) savedBtn.style.display = !pendingModel && modelSource ? "" : "none";
  };
  renderModelState();

  const updateTransformSummary = (): void => {
    const el = document.getElementById("cc-transform-summary");
    if (el) el.textContent = transformSummary();
  };

  // ---- 模板切换 ----
  const updateTemplateSelection = async (templateId: string): Promise<void> => {
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
    renderPrepList();
    applyNamePlaceholders();
    const slicedSection = document.getElementById("cc-sliced-section");
    if (slicedSection) slicedSection.style.display = templateId === "ingredient_workable" ? "" : "none";
  };
  document.querySelectorAll<HTMLElement>(".cc-template-card").forEach((el) => {
    el.addEventListener("click", () => void updateTemplateSelection(el.dataset.template!));
  });
  // 模板卡片上的「🔍 详情」：只看详情，不触发选择；编辑态「当前模板」行的详情按钮同款。
  content.querySelectorAll<HTMLElement>("[data-tpl-view]").forEach((el) => {
    el.addEventListener("click", (ev) => {
      ev.stopPropagation();
      const t = templates.find((x) => x.id === el.dataset.tplView);
      if (!t) return;
      openTemplateDetailModal(t, isNew ? () => void updateTemplateSelection(t.id) : undefined);
    });
  });

  // ---- 引用字段「清除」按钮 ----
  const wireClearButtons = (): void => {
    content.querySelectorAll<HTMLButtonElement>("[data-clear-field]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const key = btn.dataset.clearField!;
        const select = btn.parentElement?.querySelector("select") as HTMLSelectElement | null;
        const note = content.querySelector(`[data-cleared-note="${CSS.escape(key)}"]`) as HTMLElement | null;
        if (clearPending.has(key)) {
          clearPending.delete(key);
          if (select) select.disabled = false;
          if (note) note.style.display = "none";
          btn.textContent = "✖ 清除";
          btn.classList.remove("danger");
        } else {
          clearPending.add(key);
          if (select) {
            select.value = "";
            select.disabled = true;
          }
          if (note) note.style.display = "";
          btn.textContent = "↩ 撤销清除";
          btn.classList.add("danger");
        }
      });
    });
  };
  wireClearButtons();

  // ---- 多选选择器 ----
  document.getElementById("cc-pick-ings")?.addEventListener("click", () => {
    openIngredientMultiPicker(
      "锅内额外食材",
      "不选 = 接受所有主线食材；选择后仅这些食材可入锅（自定义食材也在列表中）。",
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
    openRefPicker("选择锅内模型 prefab · Model prefabs", "顺序与「额外食材」一一对应；选中项可拖动排序。", options.modelPrefabs, modelGuids, (guids) => {
      modelGuids = guids;
      setCount("cc-models-count", modelGuids.length, "模型");
    }, true);
  });
  document.getElementById("cc-pick-model-sos")?.addEventListener("click", () => {
    openRefPicker("选择锅内模型 SO · Model SOs", "PseudoPrefabSO 形式的模型引用（跨 bundle）。", options.modelSOs, modelSoGuids, (guids) => {
      modelSoGuids = guids;
      setCount("cc-model-sos-count", modelSoGuids.length, "模型 SO");
    }, true);
  });

  // ---- 返回 ----
  content.querySelectorAll("[data-back]").forEach((b) =>
    b.addEventListener("click", () => go(customContentListPath(setId, kind), app))
  );

  // ---- 图标：选择后即时预览，JPG 自动转 PNG（文件选择与测试素材填充共用） ----
  const iconInput = document.getElementById("cc-icon") as HTMLInputElement | null;
  const fillIconFrom = async (f: File, noteSuffix = ""): Promise<void> => {
    const { base64, width, height } = await iconToPngBase64(f);
    iconBase64 = base64;
    if (iconPreviewUrl) URL.revokeObjectURL(iconPreviewUrl);
    iconPreviewUrl = URL.createObjectURL(f);
    const img = document.getElementById("cc-icon-img") as HTMLImageElement | null;
    if (img) {
      img.src = iconPreviewUrl;
      img.style.display = "";
    }
    const note = document.getElementById("cc-icon-note");
    if (note)
      note.textContent = `${width}×${height}${/\.jpe?g$/i.test(f.name) ? "（已转 PNG）" : noteSuffix}`;
  };
  iconInput?.addEventListener("change", async () => {
    const f = iconInput.files?.[0];
    if (!f) {
      iconBase64 = undefined;
      return;
    }
    try {
      await fillIconFrom(f);
    } catch (e) {
      status(`图标读取失败：${(e as Error).message}`, false);
    }
  });

  // ---- 模型：文件摄取（分类贴图→改名管线→本地 3D 预览），文件选择与测试素材填充共用 ----
  const ingestModelFiles = (picked: File[]): void => {
    if (picked.length === 0) return;
    const main = picked.find((f) => /\.(fbx|obj)$/i.test(f.name));
    if (!main) {
      status("请至少包含一个 .fbx 或 .obj 模型文件。", false);
      return;
    }
    void withBusy("正在读取模型…", async () => {
      const mtlFile = picked.find((f) => /\.mtl$/i.test(f.name));
      pendingModel = {
        name: main.name,
        bytes: new Uint8Array(await main.arrayBuffer()),
        mtl: mtlFile ? new TextDecoder("utf-8").decode(await mtlFile.arrayBuffer()) : null,
      };
      pendingTextures = [];
      const idBase = (isNew ? val("cc-id") || "model" : item!.id).replace(/[^A-Za-z0-9_]/g, "");
      const usedClasses = new Set<TextureClass>();
      for (const f of picked) {
        if (f === main || f === mtlFile || !/\.(png|jpg|jpeg|tga)$/i.test(f.name)) continue;
        const cls = classifyTextureName(f.name);
        if (usedClasses.has(cls)) {
          status(`贴图「${f.name}」与其它 ${cls} 贴图重复，仅保留第一个。`, false);
          continue;
        }
        usedClasses.add(cls);
        const ext = "." + (f.name.split(".").pop() ?? "png").toLowerCase();
        pendingTextures.push({
          cls,
          diskName: `${idBase}_${cls}${ext}`,
          bytes: new Uint8Array(await f.arrayBuffer()),
          file: f,
        });
      }
      renderModelState();
      openLocalModelPreview();
    });
  };
  const modelInput = document.getElementById("cc-model") as HTMLInputElement | null;
  modelInput?.addEventListener("change", () => ingestModelFiles(Array.from(modelInput.files ?? [])));

  // ---- 🧪 一键填充测试素材：图标 + 模型（OBJ/MTL/贴图，走完整管线）+ 名称/id 示例 ----
  content.querySelector("[data-test-fill]")?.addEventListener("click", () => {
    void withBusy("正在载入测试素材…", async () => {
      try {
        const assets = testAssetsForKind(kind);
        const files = await Promise.all(
          assets.modelFiles.map((n) => fetchAsFile(`${TEST_ASSET_BASE}/${n}`, n))
        );
        ingestModelFiles(files);
        await fillIconFrom(await fetchAsFile(assets.icon, "icon_ingredient_128.png"), "（测试素材）");
        if (isNew) {
          const t = templates.find((x) => x.id === selectedTemplate);
          const zhEl = document.getElementById("cc-zh") as HTMLInputElement | null;
          const enEl = document.getElementById("cc-en") as HTMLInputElement | null;
          const idEl = document.getElementById("cc-id") as HTMLInputElement | null;
          if (zhEl && !zhEl.value.trim()) zhEl.value = `测试${t?.labelZh ?? meta.zh}`;
          if (enEl && !enEl.value.trim()) enEl.value = `Test${(t?.labelEn ?? meta.en).replace(/\s+/g, "")}`;
          if (idEl && !idEl.value.trim())
            idEl.value = `test_${(t?.id ?? kind).replace(/[^a-z0-9_]/gi, "")}`;
        }
        status("🧪 测试素材已填入（图标 + 模型 + 名称示例）：在 3D 预览里 ✨自动适配 后点「💾 保存」即可跑通全流程");
      } catch (e) {
        status((e as Error).message, false);
      }
    });
  });

  const writeTransformBack = (t: ModelTransformValues): void => {
    modelT = t;
    updateTransformSummary();
  };

  const openLocalModelPreview = (): void => {
    const model = pendingModel;
    if (!model) return;
    void withBusy("正在加载 3D 预览…", async () => {
      const { openModelPreview } = await import("./modelPreview");
      const texFiles = pendingTextures.map((t) => t.file);
      openModelPreview({
        title: model.name,
        resourceBase: "",
        modelFileName: model.name,
        localBuffer: model.bytes.buffer.slice(
          model.bytes.byteOffset,
          model.bytes.byteOffset + model.bytes.byteLength
        ) as ArrayBuffer,
        localMtl: model.mtl ?? undefined,
        localTextures: texFiles.length ? texFiles : undefined,
        fitTarget: "plate",
        showReference: kind === "ingredient" || kind === "prep",
        ...modelT,
        onAdjust: writeTransformBack,
      });
    });
  };

  document.querySelector("[data-preview-local]")?.addEventListener("click", openLocalModelPreview);
  document.querySelector("[data-preview-saved]")?.addEventListener("click", () => {
    if (!item || !modelSource) return;
    void withBusy("正在加载 3D 预览…", async () => {
      const textures = modelSource!.files
        .filter((n) => /\.(png|jpg|jpeg|tga)$/i.test(n))
        .map((n) => api.customContentModelFileUrl(item.assetPath, n));
      const { openModelPreview } = await import("./modelPreview");
      openModelPreview({
        title: item.nameZh || item.id,
        resourceBase: `/api/custom-content/model-file?assetPath=${encodeURIComponent(item.assetPath)}&file=`,
        modelFileName: modelSource!.modelFile,
        remoteTextures: textures.length ? textures : undefined,
        fitTarget: "plate",
        showReference: kind === "ingredient" || kind === "prep",
        ...modelT,
        onAdjust: writeTransformBack,
      });
    });
  });

  // ---- 🔪 切片形态：摄取 / 预览 / 测试素材（与主模型同一条改名管线） ----
  const renderSlicedState = (): void => {
    const el = document.getElementById("cc-sliced-state");
    if (!el) return;
    const parts: string[] = [];
    if (slicedPending) {
      parts.push(`📥 待上传切片模型：${esc(slicedPending.name)}${slicedPending.mtl ? " + .mtl" : ""}`);
      for (const t of slicedTextures) parts.push(`🖼 ${t.cls} → ${t.diskName}`);
    } else {
      parts.push('<span class="muted">未上传切片模型（保存后 = 自动默认切割形态）</span>');
    }
    el.innerHTML = parts.map((p) => `<div>${p}</div>`).join("");
    const btn = document.querySelector<HTMLButtonElement>("[data-sliced-preview]");
    if (btn) btn.style.display = slicedPending ? "" : "none";
  };
  renderSlicedState();

  const ingestSlicedFiles = (picked: File[]): void => {
    if (picked.length === 0) return;
    const main = picked.find((f) => /\.(fbx|obj)$/i.test(f.name));
    if (!main) {
      status("切片请至少包含一个 .fbx 或 .obj 模型文件。", false);
      return;
    }
    void withBusy("正在读取切片模型…", async () => {
      const mtlFile = picked.find((f) => /\.mtl$/i.test(f.name));
      slicedPending = {
        name: main.name,
        bytes: new Uint8Array(await main.arrayBuffer()),
        mtl: mtlFile ? new TextDecoder("utf-8").decode(await mtlFile.arrayBuffer()) : null,
      };
      slicedTextures = [];
      const idBase = (val("cc-sliced-id").trim() || val("cc-id").trim() || "model").replace(/[^A-Za-z0-9_]/g, "");
      const usedClasses = new Set<TextureClass>();
      for (const f of picked) {
        if (f === main || f === mtlFile || !/\.(png|jpg|jpeg|tga)$/i.test(f.name)) continue;
        const cls = classifyTextureName(f.name);
        if (usedClasses.has(cls)) continue;
        usedClasses.add(cls);
        const ext = "." + (f.name.split(".").pop() ?? "png").toLowerCase();
        slicedTextures.push({ cls, diskName: `${idBase}_sliced_${cls}${ext}`, bytes: new Uint8Array(await f.arrayBuffer()), file: f });
      }
      renderSlicedState();
      openSlicedPreview();
    });
  };
  const slicedModelInput = document.getElementById("cc-sliced-model") as HTMLInputElement | null;
  slicedModelInput?.addEventListener("change", () => ingestSlicedFiles(Array.from(slicedModelInput.files ?? [])));

  const slicedIconInput = document.getElementById("cc-sliced-icon") as HTMLInputElement | null;
  slicedIconInput?.addEventListener("change", async () => {
    const f = slicedIconInput.files?.[0];
    if (!f) {
      slicedIconBase64 = undefined;
      return;
    }
    try {
      const { base64 } = await iconToPngBase64(f);
      slicedIconBase64 = base64;
      const note = document.getElementById("cc-sliced-icon-note");
      if (note) note.textContent = `✓ 切片图标已选（${f.name}）`;
    } catch (e) {
      status(`切片图标读取失败：${(e as Error).message}`, false);
    }
  });

  const openSlicedPreview = (): void => {
    const model = slicedPending;
    if (!model) return;
    void withBusy("正在加载切片 3D 预览…", async () => {
      const { openModelPreview } = await import("./modelPreview");
      const texFiles = slicedTextures.map((t) => t.file);
      openModelPreview({
        title: `切片 · ${model.name}`,
        resourceBase: "",
        modelFileName: model.name,
        localBuffer: model.bytes.buffer.slice(model.bytes.byteOffset, model.bytes.byteOffset + model.bytes.byteLength) as ArrayBuffer,
        localMtl: model.mtl ?? undefined,
        localTextures: texFiles.length ? texFiles : undefined,
        fitTarget: "plate",
        showReference: true,
        ...slicedT,
        onAdjust: (t) => {
          slicedT = t;
        },
      });
    });
  };
  document.querySelector("[data-sliced-preview]")?.addEventListener("click", openSlicedPreview);

  document.querySelector("[data-sliced-test]")?.addEventListener("click", () => {
    void withBusy("正在载入切片测试素材…", async () => {
      try {
        const files = await Promise.all(
          ["model_test_sliced.obj", "model_test_sliced.mtl", "model_test_sliced_base_color.png"].map((n) =>
            fetchAsFile(`${TEST_ASSET_BASE}/${n}`, n)
          )
        );
        ingestSlicedFiles(files);
        slicedIconBase64 = await (async () => {
          const f = await fetchAsFile(`${TEST_ASSET_BASE}/icon_sliced_128.png`, "icon_sliced_128.png");
          const { base64 } = await iconToPngBase64(f);
          const note = document.getElementById("cc-sliced-icon-note");
          if (note) note.textContent = "✓ 切片图标已选（测试素材）";
          return base64;
        })();
      } catch (e) {
        status((e as Error).message, false);
      }
    });
  });

  // 切片名称/id 占位随主条目联动
  const syncSlicedPlaceholders = (): void => {
    const mainId = val("cc-id").trim();
    const mainZh = val("cc-zh").trim();
    const idEl = document.getElementById("cc-sliced-id") as HTMLInputElement | null;
    const zhEl = document.getElementById("cc-sliced-zh") as HTMLInputElement | null;
    const enEl = document.getElementById("cc-sliced-en") as HTMLInputElement | null;
    if (idEl) idEl.placeholder = `${mainId || "my_item"}_sliced`;
    if (zhEl) zhEl.placeholder = `切片·${mainZh || "我的食材"}`;
    if (enEl) enEl.placeholder = `Sliced ${val("cc-en").trim() || "My item"}`;
  };
  ["cc-id", "cc-zh", "cc-en"].forEach((id) => {
    document.getElementById(id)?.addEventListener("input", syncSlicedPlaceholders);
  });

  content.querySelectorAll("[data-save]").forEach((btn) => btn.addEventListener("click", () => void doSave()));

  const doSave = async (): Promise<void> => {
    const idVal = isNew ? val("cc-id").trim() : item!.id;
    if (isNew) {
      if (!idVal) return status("请填写标识 id。", false);
      if (!IDENT_RE.test(idVal))
        return status("标识 id 仅允许英文字母/数字/下划线，且不能以数字开头。", false);
    }
    const zh = val("cc-zh").trim();
    if (!zh) return status("请填写中文名。", false);
    if (kind === "splitter") {
      if (!val("cc-input-recipe") && !clearPending.has("inputRecipeGuid"))
        return status("分料机必须选择「输入菜谱」。没有合适的菜谱请先到「自定义菜谱」创建。", false);
      if (!val("cc-output-prefab") && !clearPending.has("outputPrefabGuid"))
        return status("分料机必须选择「输出食材/菜谱 SO」（如自定义食材）。", false);
    }
    if (kind === "utensil") {
      const stations = Array.from(document.querySelectorAll<HTMLInputElement>(".cc-station")).filter((x) => x.checked);
      if (stations.length === 0)
        return status("锅具至少勾选一个「允许站台」，否则真机无法放置到任何灶台。", false);
    }
    const uid = num("cc-order-uid");
    if (uid < 0 || !Number.isInteger(uid))
      return status("OrderNode uID 必须是非负整数（0 = 自动分配）。", false);

    const dto: CustomContentEdit = {
      setName: setId,
      kind,
      id: idVal,
      assetPath: item?.assetPath,
      nameZh: zh,
      nameEn: val("cc-en").trim(),
      templateId: selectedTemplate,
      orderUid: uid,
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
      modelScale: modelT.scale,
      modelPositionX: modelT.positionX,
      modelPositionY: modelT.positionY,
      modelPositionZ: modelT.positionZ,
      modelRotationX: modelT.rotationX,
      modelRotationY: modelT.rotationY,
      modelRotationZ: modelT.rotationZ,
      modelPivotX: modelT.pivotX,
      modelPivotY: modelT.pivotY,
      modelPivotZ: modelT.pivotZ,
      iconBase64,
      clearFields: isNew ? undefined : Array.from(clearPending),
    };

    // 待上传模型：改写贴图引用为落盘名（FBX 二进制改写 / OBJ+MTL 文本改写）
    if (pendingModel) {
      try {
        dto.modelFiles = await buildRenamedModelFiles(idVal, pendingModel, pendingTextures);
      } catch (e) {
        return status(`模型处理失败：${(e as Error).message}`, false);
      }
    }

    // 🔗 切片联动：先创建「切割后食材」条目，拿到 GUID 后再创建主食材（显式关联，
    // 后端据此跳过自动克隆）。切片创建成功而主食材失败时保留切片条目并提示。
    let slicedCreatedId = "";
    if (showSlicedSection && slicedPending) {
      if (selectedTemplate !== "ingredient_workable")
        return status("切片模型仅在「可加工食材」模板下生效；请清空切片模型或改用可加工模板。", false);
      const slicedId = val("cc-sliced-id").trim() || `${idVal}_sliced`;
      if (!IDENT_RE.test(slicedId))
        return status(`切片 id「${slicedId}」非法（仅英文字母/数字/下划线，且不能以数字开头）。`, false);
      const slicedZh = val("cc-sliced-zh").trim() || `切片·${zh}`;
      const slicedEn = val("cc-sliced-en").trim() || `Sliced ${val("cc-en").trim() || idVal}`;
      showBusy("正在创建切片食材…");
      try {
        const slicedFiles = await buildRenamedModelFiles(slicedId, slicedPending, slicedTextures);
        await api.createCustomContent({
          setName: setId,
          kind: "ingredient",
          id: slicedId,
          nameZh: slicedZh,
          nameEn: slicedEn,
          templateId: "ingredient_unworkable",
          orderUid: 0,
          iconBase64: slicedIconBase64 ?? iconBase64,
          modelFiles: slicedFiles,
          modelScale: slicedT.scale,
          modelPositionX: slicedT.positionX,
          modelPositionY: slicedT.positionY,
          modelPositionZ: slicedT.positionZ,
          modelRotationX: slicedT.rotationX,
          modelRotationY: slicedT.rotationY,
          modelRotationZ: slicedT.rotationZ,
          modelPivotX: slicedT.pivotX,
          modelPivotY: slicedT.pivotY,
          modelPivotZ: slicedT.pivotZ,
        });
        slicedCreatedId = slicedId;
        const list = await api.fetchCustomContentList(setId, "ingredient");
        const hit = list.find((x) => x.id === slicedId);
        if (!hit) throw new Error("切片食材创建成功但读取失败（可稍后在「可加工下一形态」里手动关联）");
        dto.nextPrefabGuid = hit.guid;
      } catch (e) {
        hideBusy();
        return status(`切片食材创建失败：${(e as Error).message}`, false);
      }
    }

    showBusy("保存中…");
    try {
      if (isNew) await api.createCustomContent(dto);
      else await api.updateCustomContent(dto);
      hideBusy();
      status(`已保存 ${zh}${dto.modelFiles ? "（含模型/贴图）" : ""}${slicedCreatedId ? `，并自动创建切片食材「${slicedCreatedId}」` : ""}`);
      if (kind === "ingredient" || kind === "prep") refreshEditorIngredients(setId);
      go(customContentListPath(setId, kind), app);
    } catch (e) {
      hideBusy();
      const msg = (e as Error).message || String(e);
      status(
        slicedCreatedId
          ? `${msg}（切片条目「${slicedCreatedId}」已创建：可删除后重试，或编辑本条在「可加工下一形态」手动关联）`
          : msg,
        false
      );
    }
  };
}

// ---------------------------------------------------------------------------
// 引用多选器（带搜索 + 中英文 + 已选置顶排序）
// ---------------------------------------------------------------------------

function openRefPicker(
  title: string,
  hint: string,
  refs: CustomContentRef[],
  selected: string[],
  onOk: (guids: string[]) => void,
  orderable = false
): void {
  const sel = new Set(selected);
  let query = "";
  /** 保序：原选中顺序在前，新勾选追加在后。 */
  const orderedSelection = (): string[] => [
    ...selected.filter((g) => sel.has(g)),
    ...Array.from(sel).filter((g) => !selected.includes(g)),
  ];
  const visible = (): CustomContentRef[] => {
    const q = query.trim().toLowerCase();
    if (!q) return refs;
    return refs.filter((r) => [r.nameZh, r.nameEn, r.id].some((v) => (v ?? "").toLowerCase().includes(q)));
  };
  const renderList = (): string => visible()
    .map((r) => {
      const checked = sel.has(r.guid);
      const order = checked && orderable ? orderedSelection().indexOf(r.guid) + 1 : 0;
      return `<label class="cc-picker-row${checked ? " picked" : ""}">
        <input type="checkbox" data-cc-ref="${esc(r.guid)}" ${checked ? "checked" : ""}/>
        <span class="cc-picker-name">${esc(refLabelZh(r))}</span>
        <span class="cc-picker-en muted">${esc(r.nameEn && r.nameEn !== r.id ? r.nameEn : r.id)}</span>
        ${order > 0 ? `<span class="cc-picker-order">#${order}</span>` : ""}
      </label>`;
    })
    .join("") || '<p class="muted">没有可选条目</p>';
  openModal(
    title,
    `<p class="modal-hint">${esc(hint)}</p>
     <input type="search" id="cc-picker-search" class="rl-search" placeholder="搜索名称 / id…" style="width:100%;margin-bottom:8px">
     <div class="cc-picker-list" id="cc-picker-list">${renderList()}</div>`,
    `${mCancelBtnHtml()}${mBtnHtml("清空已选", "default", { "data-cc-clear": "" })}${mPrimaryBtnHtml("确定", { "data-cc-pick-ok": "" })}`
  );
  const rerender = (): void => {
    const list = document.getElementById("cc-picker-list");
    if (list) list.innerHTML = renderList();
    bindRows();
  };
  const bindRows = (): void => {
    document.querySelectorAll<HTMLInputElement>("[data-cc-ref]").forEach((cb) => {
      cb.addEventListener("change", () => {
        const g = cb.dataset.ccRef!;
        if (cb.checked) sel.add(g);
        else sel.delete(g);
        const row = cb.closest(".cc-picker-row") as HTMLElement | null;
        if (row) row.classList.toggle("picked", cb.checked);
      });
    });
  };
  bindRows();
  document.getElementById("cc-picker-search")?.addEventListener("input", (ev) => {
    query = (ev.target as HTMLInputElement).value;
    rerender();
  });
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-cc-clear]")?.addEventListener("click", () => {
    sel.clear();
    rerender();
  });
  document.querySelector("[data-cc-pick-ok]")?.addEventListener("click", () => {
    closeModal();
    onOk(orderedSelection());
  });
}

// ---------------------------------------------------------------------------
// 模板参考：详情弹窗 + 模板浏览器
// ---------------------------------------------------------------------------

/** 跨页预选：从模板浏览器「用此模板创建」进入表单时携带。 */
let pendingTemplateId = "";

function hierarchyTreeHtml(paths: string[]): string {
  if (!paths.length) return '<p class="muted small">（模板无子物体）</p>';
  return paths
    .map((p) => {
      const depth = p.split("/").length - 1;
      const name = p.split("/").pop() ?? p;
      return `<div class="cc-tree-row" style="padding-left:${6 + depth * 14}px" title="${esc(p)}">${depth === 0 ? "📦" : "·"} ${esc(name)}</div>`;
    })
    .join("");
}

/** 模板详情：说明 / 自带内容 / 占格 / 预设 / 子物体结构。onUse 提供时显示「用此模板创建」。 */
function openTemplateDetailModal(t: CustomContentTemplate, onUse?: () => void): void {
  const includes = TEMPLATE_INCLUDES[t.id] ?? [];
  const preset = TEMPLATE_PRESETS[t.id];
  const presetLine = preset
    ? [
        preset.capacity != null ? `容量 ${preset.capacity}` : "",
        preset.stationTypes?.length ? `允许站台 ${preset.stationTypes.join(" / ")}` : "",
        preset.splitNum != null ? `每次拆分 ${preset.splitNum}` : "",
        preset.splitAudioTag ? `音效 ${preset.splitAudioTag}` : "",
      ]
        .filter(Boolean)
        .join(" · ")
    : "";
  openModal(
    `${esc(t.labelZh)} <span class="muted">· ${esc(t.labelEn)}</span>`,
    `
    <div class="cc-tpl-detail">
      <p>${esc(TEMPLATE_TIPS[t.id] ?? "")}</p>
      ${includes.length
        ? `<div class="cc-tpl-meta"><span class="cc-tpl-meta-k">模板自带</span><ul>${includes.map((i) => `<li>${esc(i)}</li>`).join("")}</ul></div>`
        : ""}
      <div class="cc-tpl-meta"><span class="cc-tpl-meta-k">占格</span><span>${esc(templateFootprint(t))}</span></div>
      ${presetLine ? `<div class="cc-tpl-meta"><span class="cc-tpl-meta-k">预设默认</span><span>${esc(presetLine)}</span></div>` : ""}
      <div class="cc-tpl-meta"><span class="cc-tpl-meta-k">来源</span><span class="cc-path">${esc(t.assetPath)}（模板库只读，克隆副本归你）</span></div>
      <div class="cc-tpl-meta"><span class="cc-tpl-meta-k">子物体结构</span>
        <div class="cc-tree" id="cc-tpl-hierarchy"><span class="muted small">加载中…</span></div>
      </div>
    </div>`,
    `${mCancelBtnHtml()}${onUse ? mPrimaryBtnHtml("用此模板创建 →", { "data-tpl-use": "" }) : ""}`
  );
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  document.querySelector("[data-tpl-use]")?.addEventListener("click", () => {
    closeModal();
    onUse?.();
  });
  void api
    .fetchCustomContentHierarchy(t.assetPath)
    .then((h) => {
      const el = document.getElementById("cc-tpl-hierarchy");
      if (el) el.innerHTML = hierarchyTreeHtml(h.paths ?? []);
    })
    .catch(() => {
      const el = document.getElementById("cc-tpl-hierarchy");
      if (el) el.innerHTML = '<span class="muted small">（结构加载失败）</span>';
    });
}

/** 模板浏览器：网格卡片 + 详情 + 「用此模板创建」。onUse(id) 里做导航与预选。 */
function openTemplateBrowser(kind: string, onUse: (templateId: string) => void): void {
  const meta = kindMeta(kind);
  const body = `<p class="modal-hint">以下是「${esc(meta.zh)}」可用的标准模板（来自 common04 等素材库，只读）。
    点击卡片右下角「🔍 详情」查看模板自带内容 / 占格 / 子物体结构，再决定用哪个创建。</p>
    <div class="modal-scroll"><div class="cc-template-grid" id="cc-tpl-browser-grid"><p class="muted">加载模板…</p></div></div>`;
  openModal(`📋 模板参考 · ${esc(meta.zh)} <span class="muted">${esc(meta.en)}</span>`, body, mCancelBtnHtml());
  document.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  void api.fetchCustomContentTemplates(kind).then((templates) => {
    const grid = document.getElementById("cc-tpl-browser-grid");
    if (!grid) return;
    grid.innerHTML = templates
      .map(
        (t) => `<div class="cc-template-card cc-tpl-browser-card">
          <b>${esc(t.labelZh)} <span class="muted">· ${esc(t.labelEn)}</span></b>
          <small>${esc(t.id)}</small>
          <p class="muted small cc-template-tip">${esc(TEMPLATE_TIPS[t.id] ?? "")}</p>
          <div class="cc-tpl-browser-actions">
            ${mBtnHtml("🔍 详情", "default", { "data-tpl-view": esc(t.id) })}
            ${mPrimaryBtnHtml("用此模板创建 →", { "data-tpl-create": esc(t.id) })}
          </div>
        </div>`
      )
      .join("") || '<p class="muted">该类型暂无可用模板。</p>';
    grid.querySelectorAll<HTMLButtonElement>("[data-tpl-view]").forEach((b) =>
      b.addEventListener("click", () => {
        const t = templates.find((x) => x.id === b.dataset.tplView);
        if (t) openTemplateDetailModal(t);
      })
    );
    grid.querySelectorAll<HTMLButtonElement>("[data-tpl-create]").forEach((b) =>
      b.addEventListener("click", () => {
        closeModal();
        onUse(b.dataset.tplCreate!);
      })
    );
  });
}

// ---------------------------------------------------------------------------
// 工具
// ---------------------------------------------------------------------------

/** 图标 → PNG base64（JPG 在浏览器端转换，规避后端 PNG 校验）。 */
async function iconToPngBase64(file: File): Promise<{ base64: string; width: number; height: number }> {
  if (file.type === "image/png" || /\.png$/i.test(file.name)) {
    const b64 = await fileBase64(file);
    const dim = await imageSize(file);
    return { base64: b64, ...dim };
  }
  const bitmap = await createImageBitmap(file);
  const canvas = document.createElement("canvas");
  canvas.width = bitmap.width;
  canvas.height = bitmap.height;
  const ctx = canvas.getContext("2d");
  if (!ctx) throw new Error("无法创建画布（Canvas）");
  ctx.drawImage(bitmap, 0, 0);
  const dataUrl = canvas.toDataURL("image/png");
  return { base64: dataUrl.slice(dataUrl.indexOf(",") + 1), width: bitmap.width, height: bitmap.height };
}

function imageSize(file: File): Promise<{ width: number; height: number }> {
  return new Promise((resolve) => {
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      URL.revokeObjectURL(url);
      resolve({ width: img.naturalWidth, height: img.naturalHeight });
    };
    img.onerror = () => {
      URL.revokeObjectURL(url);
      resolve({ width: 0, height: 0 });
    };
    img.src = url;
  });
}

/** 主模型重命名为 <id>.fbx/.obj，贴图落盘名 <id>_<class>.<ext>，并改写模型内贴图引用。 */
async function buildRenamedModelFiles(
  id: string,
  model: { name: string; bytes: Uint8Array; mtl: string | null },
  textures: PendingTexture[]
): Promise<{ fileName: string; base64: string }[]> {
  const safeId = (id || "model").replace(/[^A-Za-z0-9_]/g, "") || "model";
  const diskNames: TextureDiskNames = {};
  for (const t of textures) diskNames[t.cls] = t.diskName;
  const out: { fileName: string; base64: string }[] = [];
  const isFbx = /\.fbx$/i.test(model.name);
  if (isFbx) {
    const { renameFbxTextureRefs } = await import("./fbxTextureRename");
    const r = renameFbxTextureRefs(model.bytes, diskNames);
    out.push({ fileName: `${safeId}.fbx`, base64: bytesToBase64(r.bytes) });
  } else {
    const { renameMtlTextureRefs, ensureObjMtllib } = await import("./mtlTextureRename");
    let mtlText = model.mtl;
    if (mtlText != null) mtlText = renameMtlTextureRefs(mtlText, diskNames, {}).mtl;
    const objText = new TextDecoder("utf-8").decode(model.bytes);
    const finalObj = mtlText != null ? ensureObjMtllib(objText, `${safeId}.mtl`) : objText;
    out.push({ fileName: `${safeId}.obj`, base64: bytesToBase64(new TextEncoder().encode(finalObj)) });
    if (mtlText != null) out.push({ fileName: `${safeId}.mtl`, base64: bytesToBase64(new TextEncoder().encode(mtlText)) });
  }
  for (const t of textures) out.push({ fileName: t.diskName, base64: bytesToBase64(t.bytes) });
  return out;
}

function bytesToBase64(bytes: Uint8Array): string {
  let bin = "";
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) {
    bin += String.fromCharCode.apply(null, Array.from(bytes.subarray(i, i + chunk)) as unknown as number[]);
  }
  return btoa(bin);
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

/** 食材类内容增删改后同步编辑器共享缓存：已打开的编辑页无需重开场景，
 *  食材箱/食材选择器即可看到（或移除）自定义食材。 */
function refreshEditorIngredients(setId: string): void {
  void api
    .fetchIngredients(setId)
    .then((list) => {
      S.ingredientsCache = list;
      rebuildIngredientLookup();
    })
    .catch(() => {});
}
