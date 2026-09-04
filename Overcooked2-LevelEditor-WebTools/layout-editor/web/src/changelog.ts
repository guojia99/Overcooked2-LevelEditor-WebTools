/** 「更新日志」页面：读取 /UPDATE_LOG.md（由 build-catalog.mjs 从 layout-editor/UPDATE_LOG.md
 *  拷入 public/dist），按其格式解析出版本（## v0.7.0）、日期（### 2026-09-04，
 *  可带时间）、每个子标题（#### 子标题，或独立的 **加粗** 行）与条目列表，
 *  以时间轴卡片展示。 */
import { navHtml, wireNav } from "./nav";

export interface ChangelogItem {
  text: string;
  children: string[];
}

export interface ChangelogSection {
  title: string;
  items: ChangelogItem[];
}

export interface ChangelogEntry {
  version: string;
  date: string;
  sections: ChangelogSection[];
}

/** 去掉子标题里的 ** 包裹（如「#### **修复**」→「修复」）。 */
function cleanTitle(s: string): string {
  return s.replace(/\*\*/g, "").trim();
}

/** 解析 UPDATE_LOG.md。规则（与现有文件格式对齐）：
 *  - `# 标题` 页面大标题，忽略；
 *  - `## vX.Y.Z` 开启一个版本条目；
 *  - `### 日期`（可带 hh:mm）写入当前条目时间；
 *  - `#### 子标题` 或整行 `**子标题**` 开启一个小节；
 *  - `- 条目` 归入当前小节，缩进的 `- 子条目` 归入上一条目；
 *  - 小节出现前的条目归入无标题小节（标题留空，渲染时不显示小节头）。 */
export function parseChangelog(md: string): ChangelogEntry[] {
  const entries: ChangelogEntry[] = [];
  let entry: ChangelogEntry | null = null;
  let section: ChangelogSection | null = null;
  let item: ChangelogItem | null = null;

  const ensureSection = (): void => {
    if (!entry) return;
    if (!section) {
      section = { title: "", items: [] };
      entry.sections.push(section);
    }
  };

  for (const raw of md.split(/\r?\n/)) {
    const line = raw.trimEnd();

    const h2 = line.match(/^##(?!#)\s+(.*)$/);
    if (h2) {
      entry = { version: h2[1].trim(), date: "", sections: [] };
      entries.push(entry);
      section = null;
      item = null;
      continue;
    }

    const h3 = line.match(/^###(?!#)\s+(.*)$/);
    if (h3) {
      if (entry) entry.date = h3[1].trim();
      continue;
    }

    const h4 = line.match(/^####\s+(.*)$/);
    if (h4) {
      if (entry) {
        section = { title: cleanTitle(h4[1]), items: [] };
        entry.sections.push(section);
      }
      item = null;
      continue;
    }

    const bold = line.trim().match(/^\*\*(.+?)\*\*:?\s*$/);
    if (bold) {
      if (entry) {
        section = { title: cleanTitle(bold[1]), items: [] };
        entry.sections.push(section);
      }
      item = null;
      continue;
    }

    const li = line.match(/^(\s*)-\s+(.*)$/);
    if (li && entry) {
      const indented = li[1].length >= 2;
      const text = li[2].trim();
      if (indented && item) {
        item.children.push(text);
      } else {
        ensureSection();
        item = { text, children: [] };
        if (section) section.items.push(item);
      }
    }
  }
  return entries;
}

function esc(s: string): string {
  return s
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

/** 行内 **加粗** → <b>（先转义）。 */
function inline(s: string): string {
  return esc(s).replace(/\*\*(.+?)\*\*/g, "<b>$1</b>");
}

/** 子标题小图标：按关键词挑选，未命中用默认。 */
function sectionIcon(title: string): string {
  const t = title.toLowerCase();
  if (t.includes("修复") || t.includes("bug")) return "🛠️";
  if (t.includes("新增") || t.includes("新功能") || t.includes("功能")) return "✨";
  if (t.includes("优化")) return "⚡";
  if (t.includes("关键") || t.includes("说明")) return "📌";
  return "📌";
}

function sectionHtml(sec: ChangelogSection): string {
  const list = sec.items
    .map(
      (it) =>
        `<li>${inline(it.text)}${
          it.children.length ? `<ul class="clog-sublist">${it.children.map((c) => `<li>${inline(c)}</li>`).join("")}</ul>` : ""
        }</li>`
    )
    .join("");
  const head = sec.title
    ? `<h3 class="clog-section-title"><span class="clog-section-ico">${sectionIcon(sec.title)}</span>${esc(sec.title)}</h3>`
    : "";
  return `<div class="clog-section">${head}<ul class="clog-list">${list}</ul></div>`;
}

function entryHtml(e: ChangelogEntry, index: number): string {
  const latest = index === 0 ? '<span class="clog-latest">最新</span>' : "";
  return `<section class="clog-entry${index === 0 ? " clog-entry-latest" : ""}">
    <div class="clog-rail"><span class="clog-dot"></span></div>
    <div class="clog-card">
      <header class="clog-head">
        <span class="clog-version">${esc(e.version)}</span>
        ${latest}
        <span class="clog-date">🕒 ${esc(e.date)}</span>
      </header>
      ${e.sections.map(sectionHtml).join("")}
    </div>
  </section>`;
}

export async function renderChangelogView(app: HTMLElement): Promise<void> {
  document.body.classList.add("manage-bg");

  let md = "";
  try {
    const r = await fetch("/UPDATE_LOG.md");
    if (r.ok) md = await r.text();
  } catch {
    /* 拉取失败按无数据处理 */
  }
  const entries = parseChangelog(md);

  app.innerHTML = `
    ${navHtml("changelog")}
    <div class="manage-bar">
      <h1 class="m-title">📜 更新日志</h1>
      <span class="muted small">共 ${entries.length} 个版本</span>
    </div>
    <div class="manage-content changelog-content">
      ${
        entries.length
          ? `<div class="clog-timeline">${entries.map(entryHtml).join("")}</div>`
          : '<p class="modal-hint">未读取到 UPDATE_LOG.md：请先运行 <code>node layout-editor/scripts/build-catalog.mjs</code> 生成到 web/public。</p>'
      }
    </div>`;

  wireNav((target) => {
    if (target === "layout") {
      location.hash = "#/layout";
      location.reload();
    } else if (target === "manage") {
      location.hash = "#/manage";
      location.reload();
    } else if (target === "dependencies") {
      location.hash = "#/dependencies";
      location.reload();
    } else if (target === "custom-recipes") {
      location.hash = "#/custom-recipes";
      location.reload();
    } else if (target === "recipes") {
      location.href = "/recipes";
    } else if (target === "guide") {
      location.hash = "#/guide";
      location.reload();
    }
  });
}
