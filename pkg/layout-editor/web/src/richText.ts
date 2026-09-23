/** 汇总页 readme 富文本（仅文字排版，无图片/脚本）。
 *  - 存储：关卡 data 目录 readme~/readme.json = { schemaVersion, html }
 *  - 净化：DOMParser 解析后按标签白名单重建，未放行标签「拆壳保留文字」；
 *    <a> 仅放行 http(s) href；其余标签剥离全部属性，杜绝事件/样式注入。
 *  - 编辑：contenteditable + document.execCommand（本地工具页足够），粘贴一律转纯文本。 */
import { closeModal, openModal } from "./modals";

const ALLOWED_TAGS = new Set([
  "P",
  "H2",
  "H3",
  "STRONG",
  "B",
  "EM",
  "I",
  "U",
  "S",
  "STRIKE",
  "DEL",
  "UL",
  "OL",
  "LI",
  "BLOCKQUOTE",
  "BR",
  "A",
]);

/** 白名单净化：输入任意 HTML（含脏数据），输出仅含允许标签/属性的 HTML；空内容返回 ""。
 *  ⚠ 只递归清洗 body 的**子节点**——body 本身不在白名单内，若把 body 传入 clean
 *  会被「拆壳删除」，导致 doc.body 变 null（首次添加正常、编辑已有说明必崩，
 *  且弹窗按钮绑定代码因此不执行 —— 保存/取消变死按钮）。 */
export function sanitizeRichTextHtml(html: string): string {
  const raw = html ?? "";
  if (!raw.trim()) return "";
  let doc: Document | null = null;
  try {
    doc = new DOMParser().parseFromString(`<body>${raw}</body>`, "text/html");
  } catch {
    return "";
  }
  if (!doc.body) return "";

  // 删除注释节点（避免序列化回存注释载荷）
  const comments: Node[] = [];
  const it = doc.createNodeIterator(doc.body, NodeFilter.SHOW_COMMENT);
  for (let n = it.nextNode(); n; n = it.nextNode()) comments.push(n);
  for (const c of comments) c.parentNode?.removeChild(c);

  const clean = (el: Element): void => {
    for (const child of Array.from(el.children)) clean(child);
    const tag = el.tagName;
    if (!ALLOWED_TAGS.has(tag)) {
      // 未放行标签：拆壳保留内容（文字与白名单子节点已在上面递归清过）
      const parent = el.parentNode;
      if (!parent) return;
      while (el.firstChild) parent.insertBefore(el.firstChild, el);
      parent.removeChild(el);
      return;
    }
    let safeHref = "";
    if (tag === "A") {
      const href = el.getAttribute("href") ?? "";
      if (/^https?:\/\//i.test(href)) safeHref = href;
    }
    for (const attr of Array.from(el.attributes)) el.removeAttribute(attr.name);
    if (tag === "A" && safeHref) {
      el.setAttribute("href", safeHref);
      el.setAttribute("target", "_blank");
      el.setAttribute("rel", "noreferrer");
    }
  };
  for (const child of Array.from(doc.body.children)) clean(child);
  return doc.body.innerHTML.trim();
}

/** readme 编辑弹窗；onSave 收到净化后的 HTML（空串 = 清除说明），可同步可异步：
 *  异步保存失败时弹窗保持开启（保存按钮恢复可点），成功后自动关闭。 */
export function openReadmeEditorModal(initialHtml: string, onSave: (html: string) => void | Promise<void>): void {
  let editorEl: HTMLElement | null = null;
  const exec = (cmd: string, value?: string): void => {
    editorEl?.focus();
    document.execCommand(cmd, false, value);
  };
  const block = (tag: string): void => exec("formatBlock", `<${tag}>`);

  const tools: Array<{ label: string; title: string; run: () => void }> = [
    { label: "B", title: "加粗 (Ctrl+B)", run: () => exec("bold") },
    { label: "I", title: "斜体 (Ctrl+I)", run: () => exec("italic") },
    { label: "U", title: "下划线 (Ctrl+U)", run: () => exec("underline") },
    { label: "S", title: "删除线", run: () => exec("strikeThrough") },
    { label: "大标题", title: "标题 H2", run: () => block("h2") },
    { label: "小标题", title: "标题 H3", run: () => block("h3") },
    { label: "正文", title: "正文段落", run: () => block("p") },
    { label: "• 列表", title: "无序列表", run: () => exec("insertUnorderedList") },
    { label: "1. 列表", title: "有序列表", run: () => exec("insertOrderedList") },
    { label: "❝", title: "引用", run: () => block("blockquote") },
    { label: "✕ 清除格式", title: "清除文字格式", run: () => exec("removeFormat") },
  ];

  const root = openModal(
    "📝 汇总说明",
    `
    <p class="modal-hint">支持加粗 / 标题 / 列表 / 引用等文字排版（无图片）；粘贴自动转为纯文本。内容留空保存 = 清除说明。</p>
    <div class="rt-toolbar">${tools
      .map((t, i) => `<button type="button" class="rt-btn" data-rt-tool="${i}" title="${t.title}">${t.label}</button>`)
      .join("")}</div>
    <div class="rt-editor" id="rt-editor" contenteditable="true" spellcheck="false"></div>`,
    `<button type="button" class="modal-btn" data-cancel>取消</button>
     <button type="button" class="modal-btn primary" data-ok>保存</button>`,
    // 只能经 取消/保存 退出：防误触背景遮罩丢失已编辑内容
    { closeOnBackdrop: false }
  );
  root.querySelector(".modal-panel")?.classList.add("wide", "rt-modal");

  const editor = root.querySelector<HTMLElement>("#rt-editor");
  editorEl = editor;
  if (editor) {
    editor.innerHTML = sanitizeRichTextHtml(initialHtml);
    // 粘贴转纯文本：杜绝外部富文本/图片混入存储
    editor.addEventListener("paste", (e) => {
      e.preventDefault();
      const text = e.clipboardData?.getData("text/plain") ?? "";
      document.execCommand("insertText", false, text);
    });
  }

  root.querySelectorAll<HTMLButtonElement>("[data-rt-tool]").forEach((btn) => {
    btn.addEventListener("mousedown", (e) => e.preventDefault());
    btn.addEventListener("click", () => tools[Number(btn.dataset.rtTool ?? 0)]?.run());
  });
  root.querySelector("[data-cancel]")?.addEventListener("click", closeModal);
  root.querySelector<HTMLButtonElement>("[data-ok]")?.addEventListener("click", () => {
    const okBtn = root.querySelector<HTMLButtonElement>("[data-ok]");
    if (okBtn?.disabled) return;
    const html = editor ? sanitizeRichTextHtml(editor.innerHTML) : "";
    // onSave 可能是异步保存（网络请求）：失败时不关窗，让用户能重试
    try {
      const ret = onSave(html);
      if (ret && typeof (ret as Promise<void>).then === "function") {
        if (okBtn) okBtn.disabled = true;
        (ret as Promise<void>)
          .then(() => closeModal())
          .catch(() => {
            if (okBtn) okBtn.disabled = false;
          });
        return;
      }
    } catch {
      // 同步异常同样保持弹窗开启
      if (okBtn) okBtn.disabled = false;
      return;
    }
    closeModal();
  });
}
