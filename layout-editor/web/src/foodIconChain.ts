/**
 * foodIconChain.ts —— 食物图标的逐级降级链（工作台与夹心页共用）。
 *
 * 背景：commonW2 的 24 个夹心/中间产物全部共用占位图 `FriedGeneric.png`
 * （实际画的是一整颗洋葱，被 26 个资产引用），另有若干中间产物根本没有图标。
 * 直接用 `/api/custom-recipes/icon` 会得到「清一色洋葱」或 404 占位。
 *
 * 后端因此下发 `iconState`（own / generic / none）与叶食材 id 列表，这里据此降级：
 *   own     → 专属图标
 *   generic → **跳过**专属图标（它返回 200，onerror 不会触发），直接走静态图
 *   none    → 同上
 * 静态图顺序：成品图 `/icons/recipes/<id>.png` → 叶食材图 `/icons/ingredients/<食材>.png`
 * 最后兜底 placeholder。实测 `ingredients/` 有 177 张，洋葱/培根/虾/芝士等均可区分。
 */

const PLACEHOLDER = "/icons/_placeholder.png";

export interface FoodIconSource {
  id: string;
  /** 自定义菜谱资产路径（有专属图标时经桥接读取）。 */
  assetPath?: string;
  iconState?: "own" | "generic" | "none";
  /** 叶食材 id（递归展开），按序作为回退。 */
  iconFallbackIds?: string[];
  /** 是否自定义菜谱（决定要不要尝试 /api/custom-recipes/icon）。 */
  isCustom?: boolean;
}

export function foodIconChain(src: FoodIconSource): string[] {
  const chain: string[] = [];
  const push = (u: string): void => {
    if (u && !chain.includes(u)) chain.push(u);
  };
  if (src.isCustom && src.iconState === "own" && src.assetPath) {
    push(`/api/custom-recipes/icon?assetPath=${encodeURIComponent(src.assetPath)}`);
  }
  push(`/icons/recipes/${encodeURIComponent(src.id)}.png`);
  for (const ing of src.iconFallbackIds ?? []) {
    push(`/icons/ingredients/${encodeURIComponent(ing)}.png`);
  }
  if (!src.isCustom) push(`/icons/ingredients/${encodeURIComponent(src.id)}.png`);
  push(PLACEHOLDER);
  return chain;
}

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

/** 生成 `src` + `data-fallback` + `onerror`：加载失败时自动取链上的下一个 URL。 */
export function foodIconAttrs(src: FoodIconSource): string {
  const chain = foodIconChain(src);
  const rest = chain.slice(1);
  return `src="${esc(chain[0])}" data-fallback="${esc(rest.join("|"))}" onerror="(function(img){var f=(img.getAttribute('data-fallback')||'').split('|').filter(Boolean);if(!f.length){img.onerror=null;img.src='${PLACEHOLDER}';return;}img.setAttribute('data-fallback',f.slice(1).join('|'));img.src=f[0];})(this)"`;
}

/** 首选 URL（需要单个 src 的场景，如 rlCardHtml 的 iconSrc 回调）。 */
export function foodIconSrc(src: FoodIconSource): string {
  return foodIconChain(src)[0] ?? PLACEHOLDER;
}
