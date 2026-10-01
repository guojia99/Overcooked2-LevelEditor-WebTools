import { S, EditorItem } from "../state";
import { escHtml } from "../coords";
import { counterTypeOfItem } from "../catalog";
import { pushHistory } from "../historyOps";
import { draw } from "../render";
import { setStatus } from "../status";
import { openModal, closeModal } from "../../modals";
import { cancelBtnHtml, modalBtnHtml, primaryBtnHtml, smallBtnHtml } from "../../ui/views/button";
import { itemLabel } from "../labels";
import { openModelPreview } from "../../modelPreview";
import { fetchCounter3dManifest } from "../../api";
import type { Counter3dEntry, CounterAppearanceOption } from "../../types";

/** 确保 3D 资产 manifest 已加载（开关打开 / 弹窗重试共用）：
 *  未加载时自动重试一次 fetch；仍失败且 interactive 时弹窗指引去 Unity 运行导出菜单。
 *  返回是否可用。 */
export async function ensureCounter3dLoaded(interactive: boolean): Promise<boolean> {
  if (S.counter3d) return true;
  try {
    S.counter3d = await fetchCounter3dManifest();
    draw();
    return true;
  } catch {
    S.counter3d = null;
  }
  if (!interactive) return false;
  openModal(
    "桌台皮肤 3D 资产未导出",
    `<div class="modal-hint">
      <p>未检测到俯视图 / 3D 预览数据（<code>/api/counter-skins/3d/</code> 不可用）。</p>
      <p>请在 Unity 中运行菜单：<br><b>Layout Editor / 导出桌台皮肤 3D 模型与俯视图</b></p>
      <p>导出完成后<b>重新切换一次「🎨 桌台皮肤」开关</b>或在桌台管理里点「重试加载」即可自动加载，无需刷新页面。</p>
      <p>当前开关仍然生效：已能按皮肤<b>主色</b>着色桌台；俯视图与 3D 预览在数据就绪后自动出现。</p>
    </div>`,
    cancelBtnHtml("知道了")
  );
  return false;
}

/** 桌台皮肤（counterAppearance）分组。type = counter-appearances.json byType 键。 */
interface CounterSkinGroup {
  type: string;
  typeZh: string;
  items: EditorItem[];
  options: CounterAppearanceOption[];
  /** 该类型可用主题（保持数据源顺序去重；"" = 默认外观） */
  themes: string[];
}

function optionOfTheme(options: CounterAppearanceOption[], theme: string): CounterAppearanceOption | undefined {
  return options.find((o) => o.theme === theme);
}

function optionOfGuid(options: CounterAppearanceOption[], guid: string): CounterAppearanceOption | undefined {
  return guid ? options.find((o) => o.guid === guid) : undefined;
}

function themeLabelOf(group: CounterSkinGroup, theme: string): string {
  if (!theme) return "默认";
  const opt = optionOfTheme(group.options, theme);
  return opt?.themeName || S.counterAppearances?.themeNames[theme] || theme;
}

/** 物品当前主题键（"" = 默认；"@guid" = 目录外的未知外观）。 */
function currentThemeOf(it: EditorItem, options: CounterAppearanceOption[]): string {
  const guid = it.pseudoPrefabGuid ?? "";
  if (!guid) return "";
  return options.find((o) => o.guid === guid)?.theme ?? `@${guid}`;
}

/** 皮肤数据色点（来自材质球均色，非 UI 主题色）。 */
function dotHtml(color?: string): string {
  return color ? `<span class="csm-dot" style="background:${escHtml(color)}"></span>` : "";
}

/** 皮肤缩略图：优先材质球裁切图（/icons/counter-skins/），回退均色色块，再回退空占位。
 *  lg = 组行 128px 原图预览（随下拉选择切换）；默认 28px 行内小图。 */
function thumbHtml(opt?: CounterAppearanceOption, lg = false): string {
  const cls = lg ? "csm-thumb csm-thumb-lg" : "csm-thumb";
  if (opt?.icon) {
    return `<img class="${cls}" src="/icons/counter-skins/${encodeURIComponent(opt.icon)}.png" alt="" loading="lazy" onerror="this.remove()" />`;
  }
  if (opt?.color) return `<span class="${cls}" style="background:${escHtml(opt.color)}"></span>`;
  return `<span class="${cls} csm-thumb-empty"></span>`;
}

function thumbByTheme(group: CounterSkinGroup, theme: string, lg = false): string {
  return thumbHtml(optionOfTheme(group.options, theme), lg);
}

function thumbByGuid(group: CounterSkinGroup, guid: string): string {
  return thumbHtml(optionOfGuid(group.options, guid));
}

function distLabel(group: CounterSkinGroup, theme: string): { label: string; dot: string } {
  if (!theme) return { label: "默认", dot: "" };
  if (theme.startsWith("@")) return { label: "未知外观", dot: "" };
  const opt = optionOfTheme(group.options, theme);
  return { label: themeLabelOf(group, theme), dot: dotHtml(opt?.color) };
}

function groupDistText(group: CounterSkinGroup): string {
  const counts = new Map<string, number>();
  for (const it of group.items) {
    const t = currentThemeOf(it, group.options);
    counts.set(t, (counts.get(t) ?? 0) + 1);
  }
  return [...counts.entries()].map(([t, n]) => `${distLabel(group, t).dot}${escHtml(distLabel(group, t).label)}×${n}`).join("、");
}

function themeOptionsHtml(group: CounterSkinGroup, selectedTheme: string): string {
  return group.themes
    .map((t) => `<option value="${escHtml(t)}" ${t === selectedTheme ? "selected" : ""}>${escHtml(themeLabelOf(group, t))}</option>`)
    .join("");
}

function guidOptionsHtml(group: CounterSkinGroup, selectedGuid: string): string {
  return group.options
    .map((o) => `<option value="${escHtml(o.guid)}" ${o.guid === selectedGuid ? "selected" : ""}>${escHtml(o.themeName || o.nameZh)}</option>`)
    .join("");
}

/** 把主题写入一组物品（theme="" = 恢复默认外观）；返回写入个数。 */
function applyThemeToItems(items: EditorItem[], options: CounterAppearanceOption[], theme: string): number {
  const opt = theme ? optionOfTheme(options, theme) : undefined;
  if (theme && !opt) return 0;
  for (const it of items) it.pseudoPrefabGuid = theme ? opt!.guid : undefined;
  return items.length;
}

function fileKeyOf(group: CounterSkinGroup, theme: string): string {
  return `${group.type}_${theme || "Default"}`;
}

/** 打开某 fileKey 的 3D 模型预览（Unity 导出 OBJ/MTL + 材质贴图，只读）。 */
function previewCounter3d(fileKey: string, title: string): void {
  const entry: Counter3dEntry | undefined = S.counter3d?.items[fileKey];
  if (!entry) {
    setStatus("该皮肤没有 3D 模型数据（先在 Unity 菜单运行「导出桌台皮肤 3D 模型与俯视图」）", false);
    return;
  }
  openModelPreview({
    title: `3D 预览 · ${title}`,
    resourceBase: "/api/counter-skins/3d/",
    modelFileName: entry.obj,
    // 注意：mtlUrl 必须传相对文件名——MTLLoader.load 内部会再拼 setPath(resourceBase)，
    // 传绝对 URL 会双拼成坏地址；版本防陈旧由路由的 no-cache 响应头兜底。
    mtlUrl: entry.mtl,
    readonly: true,
    readonlyReason: "皮肤模型由 Unity 导出，只读预览",
    unitySize: { x: entry.size.x, y: entry.size.y, z: entry.size.z, minY: entry.minY },
  });
}

export function openCounterSkinManager() {
  if (!S.counterAppearances) {
    setStatus("桌台外观数据未加载（桥离线且无静态回退）", false);
    return;
  }
  const groupsByType = new Map<string, EditorItem[]>();
  for (const it of S.items) {
    const ct = counterTypeOfItem(it);
    if (!ct) continue;
    const arr = groupsByType.get(ct) ?? [];
    arr.push(it);
    groupsByType.set(ct, arr);
  }
  if (!groupsByType.size) {
    setStatus("当前关卡没有桌台类物品", false);
    return;
  }

  const groups: CounterSkinGroup[] = [...groupsByType.keys()].sort().map((type) => {
    const items = groupsByType.get(type)!;
    items.sort((a, b) => a._wz - b._wz || a._wx - b._wx);
    const options = S.counterAppearances!.byType[type] ?? [];
    const themes: string[] = [];
    for (const o of options) if (!themes.includes(o.theme)) themes.push(o.theme);
    return { type, typeZh: S.counterAppearances!.typeNames[type] ?? type, items, options, themes };
  });

  // 全局主题并集（保持各类型首次出现顺序）+ 中文名
  const globalThemeLabel = new Map<string, string>();
  for (const g of groups) {
    for (const t of g.themes) {
      if (t && !globalThemeLabel.has(t)) globalThemeLabel.set(t, themeLabelOf(g, t));
    }
  }
  const globalOptions = ['<option value="">— 默认 —</option>']
    .concat([...globalThemeLabel.entries()].map(([t, label]) => `<option value="${escHtml(t)}">${escHtml(label)}</option>`))
    .join("");

  const groupHtml = groups
    .map((g) => {
      const itemRows = g.items
        .map((it) => {
          const cur = it.pseudoPrefabGuid ?? "";
          return `<div class="csm-row csm-item-row">
            <span class="csm-pos" title="${escHtml(itemLabel(it))}">#${it._wx},${it._wz}</span>
            <span class="csm-thumbwrap" data-thumb>${thumbByGuid(g, cur)}</span>
            <select class="csm-sel csm-item-sel" data-key="${it._editorKey}" data-type="${escHtml(g.type)}">
              <option value="" ${cur ? "" : "selected"}>— 默认 —</option>
              ${guidOptionsHtml(g, cur)}
            </select>
          </div>`;
        })
        .join("");
      return `<div class="csm-group" data-group="${escHtml(g.type)}">
        <div class="csm-group-title">${escHtml(g.typeZh)}（${escHtml(g.type)}）× ${g.items.length}
          <span class="csm-dist" data-dist-for="${escHtml(g.type)}">${groupDistText(g)}</span>
        </div>
        <div class="csm-row">
          <label class="csm-row-label">本组皮肤
            <select class="csm-sel csm-type-sel" data-type="${escHtml(g.type)}">
              ${themeOptionsHtml(g, "")}
            </select>
          </label>
          <span class="csm-thumbwrap" data-theme-thumb="${escHtml(g.type)}">${thumbByTheme(g, "", true)}</span>
          ${S.counter3d ? modalBtnHtml("👁 3D 预览", "modal-btn csm-3d", { "data-c3d": g.type }) : ""}
          ${modalBtnHtml(`应用到全部 ${g.items.length} 个`, "modal-btn csm-apply", { "data-type": g.type })}
        </div>
        <details class="csm-items">
          <summary>单独修改每个（${g.items.length}）</summary>
          ${itemRows}
        </details>
      </div>`;
    })
    .join("");

  openModal(
    "桌台管理 · 皮肤",
    `<p class="modal-hint">按桌台类型统一皮肤（如「木纹·中秋」），也可展开分组单独修改每一个；「统一全部桌台」时缺少所选皮肤的类型保持不变。仅修改前端数据，写回 Unity 后生效（所需外观 bundle 会自动并入依赖）。色点/缩略图来自材质球主色（需在 Unity 里运行「导出桌台皮肤图标与主色」生成）。</p>
     ${S.counter3d ? "" : `<div class="csm-missing">⚠ 俯视图 / 3D 预览数据未导出——在 Unity 菜单运行「<b>Layout Editor / 导出桌台皮肤 3D 模型与俯视图</b>」后点击重试。${smallBtnHtml("重试加载", "primary", { id: "csm-retry-3d" })}</div>`}
     <div class="csm-global">
       <label class="csm-row-label">全局主题
         <select id="csm-global-theme" class="csm-sel">${globalOptions}</select>
       </label>
       ${primaryBtnHtml("统一全部桌台", { id: "csm-apply-all" })}
     </div>
     <div class="modal-scroll">${groupHtml}</div>`,
    cancelBtnHtml("关闭")
  );
  document.querySelector(".modal-panel")?.classList.add("wide");

  const groupOf = (type: string | undefined) => groups.find((g) => g.type === type);
  const itemOf = (key: string | undefined) => S.items.find((i) => i._editorKey === key);
  const refreshDist = (g: CounterSkinGroup) => {
    const el = document.querySelector<HTMLElement>(`[data-dist-for="${g.type}"]`);
    if (el) el.innerHTML = groupDistText(g);
  };
  const themeZh = (theme: string) => (theme ? globalThemeLabel.get(theme) ?? theme : "默认");

  // 单个实例：改完不重开弹窗（避免展开态与滚动位置丢失），刷新分布统计与行内缩略图
  document.querySelectorAll<HTMLSelectElement>(".csm-item-sel").forEach((sel) => {
    sel.addEventListener("change", () => {
      const it = itemOf(sel.dataset.key);
      const g = groupOf(sel.dataset.type);
      if (!it || !g) return;
      pushHistory();
      it.pseudoPrefabGuid = sel.value || undefined;
      draw();
      refreshDist(g);
      const row = sel.closest(".csm-item-row");
      const thumb = row?.querySelector<HTMLElement>("[data-thumb]");
      if (thumb) thumb.innerHTML = thumbByGuid(g, it.pseudoPrefabGuid ?? "");
      const picked = g.options.find((o) => o.guid === sel.value);
      setStatus(`${itemLabel(it)} 皮肤已更新为 ${picked?.themeName ?? "默认外观"}（写回后生效）`);
    });
  });

  // 组级下拉：切换时同步旁边缩略图
  document.querySelectorAll<HTMLSelectElement>(".csm-type-sel").forEach((sel) => {
    sel.addEventListener("change", () => {
      const g = groupOf(sel.dataset.type);
      if (!g) return;
      const thumb = document.querySelector<HTMLElement>(`[data-theme-thumb="${g.type}"]`);
      if (thumb) thumb.innerHTML = thumbByTheme(g, sel.value, true);
    });
  });

  // 3D 资产缺失提示条的重试：加载成功后重开弹窗（出现 👁 3D 按钮）
  document.getElementById("csm-retry-3d")?.addEventListener("click", () => {
    void ensureCounter3dLoaded(false).then((ok) => {
      if (ok) {
        setStatus("桌台皮肤 3D 资产已加载");
        closeModal();
        openCounterSkinManager();
      } else {
        setStatus("仍未检测到 3D 资产（确认 Unity 菜单已导出且桥服务在运行）", false);
      }
    });
  });

  // 3D 预览：随组下拉当前所选主题解析 fileKey
  document.querySelectorAll<HTMLButtonElement>(".csm-3d").forEach((btn) => {
    btn.addEventListener("click", () => {
      const g = groupOf(btn.dataset.c3d);
      const sel = document.querySelector<HTMLSelectElement>(`.csm-type-sel[data-type="${g?.type ?? ""}"]`);
      if (!g || !sel) return;
      const theme = sel.value;
      previewCounter3d(fileKeyOf(g, theme), `${g.typeZh} · ${themeZh(theme)}`);
    });
  });

  // 组级应用：该类型全部实例统一为本组下拉所选主题
  document.querySelectorAll<HTMLButtonElement>(".csm-apply").forEach((btn) => {
    btn.addEventListener("click", () => {
      const g = groupOf(btn.dataset.type);
      const sel = document.querySelector<HTMLSelectElement>(`.csm-type-sel[data-type="${g?.type ?? ""}"]`);
      if (!g || !sel) return;
      const theme = sel.value;
      pushHistory();
      const n = applyThemeToItems(g.items, g.options, theme);
      draw();
      setStatus(`已把 ${g.typeZh} × ${n} 个统一为「${escHtml(themeZh(theme))}」（写回后生效）`);
      closeModal();
      openCounterSkinManager();
    });
  });

  // 全局统一：跳过无该主题皮肤的类型并在状态栏列出
  document.getElementById("csm-apply-all")?.addEventListener("click", () => {
    const sel = document.getElementById("csm-global-theme") as HTMLSelectElement | null;
    if (!sel) return;
    const theme = sel.value;
    pushHistory();
    let touched = 0;
    const skipped: string[] = [];
    for (const g of groups) {
      if (theme && !optionOfTheme(g.options, theme)) {
        skipped.push(g.typeZh);
        continue;
      }
      touched += applyThemeToItems(g.items, g.options, theme);
    }
    draw();
    const skipHint = skipped.length ? `；${skipped.join("、")} 无「${themeZh(theme)}」皮肤，已跳过` : "";
    setStatus(
      touched
        ? `已统一 ${touched} 个桌台为「${themeZh(theme)}」${skipHint}（写回后生效）`
        : `没有可应用的桌台${skipHint}`,
      touched > 0
    );
    closeModal();
    openCounterSkinManager();
  });
}
