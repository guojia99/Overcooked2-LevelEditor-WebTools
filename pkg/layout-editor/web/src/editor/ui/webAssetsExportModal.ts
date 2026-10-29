import * as api from "../../api";
import { openModal, closeModal } from "../../modals";
import { mCancelBtnHtml, mPrimaryBtnHtml } from "../../ui/views/button";
import { setStatus } from "../status";

function esc(s: unknown): string {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

/** 一键导出 Web 前端资源：音频 / 桌台图标 / 桌台 3D / 装饰双视图。
 *  产物写入 exports/（git 忽略），由 /api/web-assets/* 只读提供。 */
export function openWebAssetsExportModal(): void {
  const CATS: Array<{ id: api.WebAssetCategory; label: string; hint: string }> = [
    { id: "audio", label: "音频", hint: "BGM / 音效集 → exports/audio" },
    { id: "counter-icons", label: "桌台图标", hint: "材质缩略图 → exports/counter-skins/icons" },
    { id: "counter-3d", label: "桌台 3D", hint: "俯视图 + OBJ/MTL → exports/counter-skins" },
    { id: "decor", label: "装饰双视图 + 高清详情 + 3D 模型", hint: "192 正/背缩略图 + 1024/1536/2048 详情图 + OBJ/MTL 3D → exports/decor " },
  ];
  const catRows = CATS.map(
    (c) => `<label class="modal-check inline" style="display:flex;gap:8px;align-items:flex-start;margin:4px 0">
      <input type="checkbox" data-web-cat="${c.id}" checked style="width:auto;margin-top:3px">
      <span><b>${esc(c.label)}</b><span class="muted"> · ${esc(c.hint)}</span></span>
    </label>`
  ).join("");

  openModal(
    "导出 Web 资源 · exports/",
    `<p class="modal-hint">产物统一写入 <code>exports/</code>，Web 前端经 <code>/api/web-assets/*</code> 读取，不再走 layout 前端静态资源。首次全量导出可能较久（装饰 3000+ 件 × 正/背视图 + 1024/2K 详情图 + 3D 模型，单次导出约数百 MB 到 1GB+）。</p>
     <div class="m-section-title">导出类别</div>
     ${catRows}
     <label class="modal-check inline" style="display:flex;gap:8px;align-items:center;margin:10px 0">
       <input type="checkbox" id="web-assets-force" style="width:auto">
       <span>强制重导（装饰默认只补缺失的正/背视图；勾选后全部重渲染）</span>
     </label>
     <div class="m-section-title">当前 exports 状态</div>
     <div id="web-assets-current" class="modal-hint">正在读取…</div>
     <div id="web-assets-progress" class="modal-hint" style="margin-top:10px;white-space:pre-line"></div>`,
    `${mCancelBtnHtml()}${mPrimaryBtnHtml("开始导出")}`
  );

  const okBtn = document.querySelector("[data-ok]") as HTMLButtonElement | null;
  const currentEl = document.getElementById("web-assets-current");
  const progressEl = document.getElementById("web-assets-progress");
  const forceEl = document.getElementById("web-assets-force") as HTMLInputElement | null;
  let finished = false;
  let pollTimer = 0;
  let exportStartedAt = 0;

  const setProgress = (text: string, pct?: number): void => {
    if (!progressEl) return;
    const bar =
      typeof pct === "number"
        ? `<div class="m-progress" style="height:6px;background:#3a4050;border-radius:3px;overflow:hidden;margin-top:6px"><div style="height:100%;width:${Math.max(0, Math.min(100, Math.round(pct * 100)))}%;background:var(--accent,#e0a44a);transition:width .25s"></div></div>`
        : "";
    progressEl.innerHTML = `<div>${esc(text)}</div>${bar}`;
  };

  const refreshCurrent = async (): Promise<void> => {
    if (!currentEl) return;
    try {
      const m = await api.fetchWebAssetsManifest();
      const line = (label: string, cat: api.WebAssetCategoryInfo, extra = ""): string => {
        const state = cat.available ? "✅" : "❌";
        const count = cat.count > 0 ? ` · ${cat.count}${extra}` : "";
        return `<div>${state} ${esc(label)}${count}</div>`;
      };
      currentEl.innerHTML =
        line("音频", m.audio, " 个音频") +
        line("桌台 3D", m.counterSkins, " 个模型") +
        `<div class="muted" style="font-size:11px">桌台图标：${m.counterSkins.iconCount ?? 0} 个；装饰：${m.decor.count ?? 0} 正 / ${m.decor.backCount ?? 0} 背</div>`;
    } catch (e) {
      currentEl.textContent = `读取失败：${(e as Error).message}`;
    }
  };

  const stopPolling = (): void => {
    if (pollTimer) {
      window.clearInterval(pollTimer);
      pollTimer = 0;
    }
  };

  const poll = (): void => {
    stopPolling();
    pollTimer = window.setInterval(async () => {
      let st: api.WebAssetExportStatus;
      try {
        st = await api.fetchWebAssetsExportStatus();
      } catch {
        return; // 域重载/短暂断连：继续轮询
      }
      if (st.status === "running") {
        setProgress(st.message || "导出中…", st.progress);
        return;
      }
      if (st.status === "idle" && exportStartedAt && Date.now() - exportStartedAt > 15000) {
        stopPolling();
        setProgress("❌ 导出状态丢失（Unity 可能发生了域重载）。请查看 Unity Console，必要时重试。");
        if (okBtn) {
          okBtn.disabled = false;
          okBtn.textContent = "重试";
        }
        return;
      }
      if (exportStartedAt && Date.now() - exportStartedAt > 60 * 60 * 1000) {
        stopPolling();
        setProgress("❌ 导出超时（60 分钟）。请查看 Unity Console。");
        if (okBtn) {
          okBtn.disabled = false;
          okBtn.textContent = "重试";
        }
        return;
      }
      stopPolling();
      if (st.status === "error") {
        setProgress(`❌ ${st.message || "导出失败"}${st.error ? "：" + st.error : ""}`);
        if (okBtn) {
          okBtn.disabled = false;
          okBtn.textContent = "重试";
        }
        return;
      }
      if (st.status === "done") {
        finished = true;
        const s = st.summary;
        const secs = Math.round((st.durationMs || 0) / 1000);
        setProgress(
          `✅ 导出完成（${secs}s）\n` +
            `音频 ${s.audioClips}（失败 ${s.audioFailed}） · 桌台图标 ${s.counterIcons}（失败 ${s.counterIconsFailed}） · ` +
            `桌台 3D ${s.counter3d}（失败 ${s.counter3dFailed}） · ` +
            `装饰 ${s.decorFront} 正 / ${s.decorBack} 背 · 详情 ${s.decorDetailFront ?? 0} 正 / ${s.decorDetailBack ?? 0} 背 · 3D ${s.decorModels ?? 0}（缺失 ${s.decorMissing}，失败 ${s.decorFailed}）`,
          1
        );
        if (okBtn) {
          okBtn.disabled = false;
          okBtn.textContent = "关闭";
        }
        void refreshCurrent();
      }
    }, 1500);
  };

  void refreshCurrent();

  okBtn?.addEventListener("click", async () => {
    if (finished) {
      closeModal();
      return;
    }
    const cats = Array.from(document.querySelectorAll<HTMLInputElement>("[data-web-cat]:checked")).map(
      (el) => el.dataset.webCat as api.WebAssetCategory
    );
    if (cats.length === 0) {
      setStatus("请至少选择一个导出类别。", false);
      return;
    }
    okBtn.disabled = true;
    exportStartedAt = Date.now();
    setProgress("正在启动导出…", 0.01);
    try {
      await api.startWebAssetsExport(!!forceEl?.checked, cats);
      poll();
    } catch (e) {
      setProgress(`❌ ${(e as Error).message}`);
      okBtn.disabled = false;
    }
  });

  document.querySelector("[data-cancel]")?.addEventListener("click", () => {
    stopPolling();
    closeModal();
  });
}


