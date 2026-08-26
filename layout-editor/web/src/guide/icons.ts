import type { IngredientEntry, RecipeEntry, FoodGroup } from "../types";
import { STEP_ICON_SRC } from "../recipeCard";
import { foodGroupLabel } from "../ingredientLabels";

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

export function iconUrl(kind: "ingredients" | "recipes" | "catalog", id: string, hasIcon = true): string {
  if (!id || hasIcon === false) return "/icons/_placeholder.png";
  return `/icons/${kind}/${encodeURIComponent(id)}.png`;
}

export function foodIconHtml(src: string, alt = ""): string {
  return `<img class="food-icon" loading="lazy" src="${esc(src)}" alt="${esc(alt)}" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`;
}

/** Curated ingredient IDs per catalog group (6–12 each). */
export const GUIDE_INGREDIENT_SAMPLES: Record<string, string[]> = {
  core: ["TomatoSO", "MeatSO", "CheeseSO", "LettuceSO", "FlourSO", "EggSO", "PotatoSO", "MushroomSO", "PastaSO", "FishSO"],
  dlc02: ["DLC02_ChoppedBun", "KebobChicken", "KebobTomato", "Melon", "SmoothieStrawberry", "Banana"],
  dlc03: ["dlc03_chocolate", "marshmallow", "orange", "whippedcream", "driedfruit", "milk"],
  dlc04: ["dlc04_orange", "dlc04_peach", "corn", "grapes"],
  dlc05: ["DLC05_Dough", "DLC05_Egg", "DLC05_Marshmallow", "DLC05_Strawberry", "DLC05_Banana", "DLC05_Crackers"],
  dlc07: ["dlc07_potato", "broccoli", "CarrotSO"],
  dlc08: ["dlc08_bun", "dlc08_chicken"],
  dlc09: ["dlc09_flour", "dlc09_egg", "dlc09_potato", "dlc09_orange"],
  dlc10: ["dlc10_orange", "dlc10_grapes", "dlc10_peach"],
  dlc11: ["dlc11_tomato", "dlc11_lettuce", "dlc11_hotdogbun", "dlc11_frankfurter", "dlc11_ketchup"],
  dlc13: ["dlc13_flour", "dlc13_egg", "dlc13_strawberry", "dlc13_orange"],
};

/** Representative recipe IDs for guide cards. */
export const GUIDE_RECIPE_SAMPLES = [
  "Burger_Plain_SO",
  "Pizza_Mushroom_80_SO",
  "MixedFlourEggBlueberry",
  "Soup_Tomato_SO",
  "Kebob_ChickenTomato",
];

export const GUIDE_UTENSIL_IDS = Object.keys(STEP_ICON_SRC);

export function renderIconPathsBlock(): string {
  return `
    <table class="guide-table">
      <thead><tr><th>路径</th><th>用途</th></tr></thead>
      <tbody>
        <tr><td><code>/icons/ingredients/&lt;id&gt;.png</code></td><td>食材图标</td></tr>
        <tr><td><code>/icons/recipes/&lt;id&gt;.png</code></td><td>官方菜谱成品图</td></tr>
        <tr><td><code>/icons/catalog/&lt;id&gt;.png</code></td><td>锅具 / prefab 调色板缩略图</td></tr>
        <tr><td><code>/icons/_placeholder.png</code></td><td>缺失时的回退图</td></tr>
      </tbody>
    </table>
    <p class="guide-note">目录数据中 <code>icon: true</code> 表示已解包对应 PNG；否则前端显示占位图。</p>`;
}

export function renderIngredientSamples(ingredients: IngredientEntry[]): string {
  const byId = new Map(ingredients.map((i) => [i.id, i]));
  const sections: string[] = [];
  for (const [group, ids] of Object.entries(GUIDE_INGREDIENT_SAMPLES)) {
    const label = foodGroupLabel(group as FoodGroup);
    const cells = ids
      .map((id) => {
        const ing = byId.get(id);
        const name = ing?.nameZh ?? id;
        const src = iconUrl("ingredients", id, ing?.icon !== false);
        return `<div class="guide-icon-cell" title="${esc(id)}">${foodIconHtml(src, name)}<span>${esc(name)}</span></div>`;
      })
      .join("");
    sections.push(`
      <div class="guide-icon-group">
        <h4>${esc(label)}</h4>
        <div class="guide-icon-grid">${cells}</div>
      </div>`);
  }
  return sections.join("");
}

export function renderRecipeSamples(recipes: RecipeEntry[]): string {
  const byId = new Map(recipes.map((r) => [r.id, r]));
  const cards = GUIDE_RECIPE_SAMPLES.map((id) => {
    const r = byId.get(id);
    if (!r) return "";
    const src = iconUrl("recipes", r.id, r.icon !== false);
    const ings = (r.ingredients ?? []).slice(0, 6);
    const ingHtml = ings
      .map((iid) => {
        const ing = ingredientsNameHint(iid);
        return foodIconHtml(iconUrl("ingredients", iid), ing);
      })
      .join("");
    const score = r.score != null ? `<span class="guide-score">${r.score} 分</span>` : `<span class="guide-score muted">中间产物</span>`;
    return `
      <div class="guide-recipe-card">
        <div class="guide-recipe-head">${foodIconHtml(src, r.nameZh ?? id)}<div>
          <b>${esc(r.nameZh ?? id)}</b>
          ${r.nameEn ? `<span class="muted">${esc(r.nameEn)}</span>` : ""}
          ${score}
        </div></div>
        <div class="guide-recipe-ings">${ingHtml || '<span class="muted">—</span>'}</div>
      </div>`;
  }).filter(Boolean);
  return `<div class="guide-recipe-grid">${cards.join("")}</div>`;
}

function ingredientsNameHint(id: string): string {
  return id.replace(/SO$/, "").replace(/^dlc\d+_/, "");
}

export function renderUtensilIcons(): string {
  const labels: Record<string, string> = {
    Pot: "煮锅",
    FryingPan: "煎锅",
    DeepFatFryer: "炸篮",
    OvenTray: "烤箱",
    Steamer: "蒸笼",
    Mixer: "搅拌碗",
    Blender: "搅拌杯",
    MixingBowl: "搅拌碗",
    GriddlePan: "煎烤盘",
    KebabSkewer: "烤串",
    ToastingFork: "烤棉花糖叉",
    HotPot: "大火锅",
    RoastingTray: "烤托盘",
  };
  const cells = GUIDE_UTENSIL_IDS.map((id) => {
    const src = STEP_ICON_SRC[id] ?? "/icons/_placeholder.png";
    return `<div class="guide-icon-cell">${foodIconHtml(src, labels[id] ?? id)}<span>${esc(labels[id] ?? id)}</span></div>`;
  }).join("");
  return `<div class="guide-icon-grid guide-icon-grid-wide">${cells}</div>`;
}
