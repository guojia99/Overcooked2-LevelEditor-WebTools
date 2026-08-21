import {
  S,
  CELL,
  EditorFloor
} from "./state";
import {
  normalizeRot,
  escHtml
} from "./coords";
import { setStatus } from "./status";
import { draw } from "./render";
import { pushHistory } from "./historyOps";
import { setFloorSelection } from "./selection";
import {
  closeModal,
  openModal
} from "../modals";
import {
  showBusy,
  hideBusy
} from "../busy";
import {
  imageFloorUrl,
  uploadImageFloor
} from "../api";
import {
  themedFloorPrefabs,
  isThemedFloor,
  isAirFloor,
  finalizeFloor,
  snapRaftCenterToGrid,
  floorMatSummary
} from "./floors";
import {
  floorWalkY,
  floorVisualYForWalkHeight,
  floorLayerIndex
} from "./floorHeight";
import { floorLocalPoint } from "./renderFloors";
import { snapValue } from "../snap";
import {
  copyFloors,
  duplicateFloors
} from "./clipboard";
import { updateFloorBar, refreshFloorHeightPanel } from "./floorPalette";
import { clearFloorImageCache } from "./iconCaches";
import {
  FLOOR_MATERIAL_GROUPS,
  floorMaterialGroup,
  materialBilingual,
  surfaceKindLabelZh,
  deathLabelZh
} from "../floorColors";
import { tidyCatalogNameZh } from "../displayLabels";

export function openFloorEditorModal(f: EditorFloor) {
  const matchedMat = S.floorMaterials.find((m) => m.guid === f.materialGuid);
  const areaCells = f._wCells * f._dCells;
  const isRaft = f.surfaceKind === "raft";
  const isThemed = isThemedFloor(f);
  const isAir = isAirFloor(f);
  const isImage = !isAir && !isThemed && f.surfaceKind === "solid" && (!!f.imageTexturePath || !!f.imageMode);
  const isTinted = !isAir && !isThemed && f.surfaceKind === "solid" && !!f.tintEnabled && !isImage;
  const isPlainSolid = !isAir && !isThemed && f.surfaceKind === "solid" && !f.tintEnabled && !isImage;

  const matRows = FLOOR_MATERIAL_GROUPS.map((g) => {
    const groupMats = S.floorMaterials.filter((m) => floorMaterialGroup(m.id) === g.key);
    if (groupMats.length === 0) return "";
    const rows = groupMats
      .map((m) => {
        const bl = materialBilingual(m.id);
        return `<button type="button" class="mat-pick${m.guid === f.materialGuid ? " active" : ""}" data-guid="${m.guid}"><span class="mat-id">${bl.zh}</span><span class="mat-sub">${bl.en}</span></button>`;
      })
      .join("");
    return `<div class="mat-pick-group">${g.labelZh}</div><div class="mat-pick-grid">${rows}</div>`;
  }).join("");

  // 主题地板: pick a themed prefab — the whole rect is tiled with it on write-back.
  const themedRows = themedFloorPrefabs()
    .map(
      (it) =>
        `<button type="button" class="mat-pick${f.prefabGuid === it.guid ? " active" : ""}" data-pguid="${it.guid}"><span class="mat-id">${escHtml(tidyCatalogNameZh(it.nameZh, it.id))}</span><span class="mat-sub">${surfaceKindLabelZh(it.surfaceKind)} · ${escHtml(it.id)}</span></button>`
    )
    .join("");
  const themedBlock = `<div class="mat-pick-title">主题地板（整块区域 = 一个拉伸的 prefab 实例，写回后生效）</div>
     <div class="mat-pick-list"><div class="mat-pick-grid">${themedRows || '<div class="mat-pick-empty">catalog 中无主题地板 prefab</div>'}</div></div>`;

  // 染色地板: pick a tint color (replaces the material list).
  const colorBlock = `<div class="mat-pick-title">染色（染色地板 = 实心 Plane + 纯色）</div>
     <div class="floor-edit-row"><label>颜色 <input type="color" id="fe-tint" value="${f.tintColor ?? "#ffffff"}"></label>
     <button type="button" class="m-btn" id="fe-tint-clear" style="font-size:11px;padding:3px 8px;">清除颜色</button>
     <span class="muted" style="align-self:center;font-size:11px">实时生效</span></div>`;

  // 图片地板: upload an image + choose tile/stretch. Image data goes to the
  // current level set's data dir.
  const imgName = f.imageTexturePath ? f.imageTexturePath.split("/").pop() ?? "" : "";
  const mode = f.imageMode === "tile" ? "tile" : "stretch";
  const opacityPct = Math.round((f.imageOpacity != null ? f.imageOpacity : 1) * 100);
  const imgRot = normalizeRot(f.imageRotation ?? 0);
  const imgRotOpts = [0, 90, 180, 270]
    .map((r) => `<option value="${r}"${r === imgRot ? " selected" : ""}>${r}°</option>`)
    .join("");
  const previewHtml = f.imageTexturePath
    ? `<img id="fe-img-preview" src="${imageFloorUrl(f.imageTexturePath)}" alt="" style="max-width:120px;max-height:80px;border:1px solid #4a5060;border-radius:4px;background:#1a1d23;transform:rotate(${imgRot}deg)"/>`
    : `<span class="muted" style="font-size:11px">（未上传图片）</span>`;
  const imageBlock = `<div class="mat-pick-title">图片地板（实心 Plane + 贴图；图片存入当前关卡集 data 目录）</div>
     <div class="floor-edit-row" style="align-items:center;gap:10px">
       ${previewHtml}
       <span class="muted" style="font-size:11px">当前贴图：${imgName || "未设置"}</span>
     </div>
     <div class="floor-edit-row">
       <label class="modal-check"><input type="radio" name="fe-imgmode" value="stretch" ${mode === "stretch" ? "checked" : ""}/> 全部铺开（自动伸缩）</label>
       <label class="modal-check"><input type="radio" name="fe-imgmode" value="tile" ${mode === "tile" ? "checked" : ""}/> 一格重复平铺</label>
     </div>
     <div class="floor-edit-row">
       <label>图片旋转 <select id="fe-img-rot">${imgRotOpts}</select></label>
       <span class="muted" style="font-size:11px;align-self:center">以 90° 为单位（俯视顺时针）· 实时预览</span>
     </div>
     <div class="floor-edit-row">
       <label>不透明度 <input type="range" id="fe-img-opacity" min="0" max="100" step="1" value="${opacityPct}" style="width:160px"/></label>
       <span id="fe-img-opacity-val" class="muted" style="font-size:11px;align-self:center">${opacityPct}%</span>
     </div>
     <div class="floor-edit-row">
       <input type="file" id="fe-img-file" accept="image/png,image/jpeg,image/svg+xml,image/webp,image/gif" style="font-size:11px;flex:1"/>
       <button type="button" class="m-btn" id="fe-img-upload" style="font-size:11px;padding:3px 8px;">上传并应用</button>
     </div>
     <p class="modal-hint">修改宽高后铺开/平铺会自动重算 · 镜像已在 Unity 端修正 · 写回后生效</p>`;

  const materialBlock = isAir
    ? `<p class="modal-hint">空气地板没有可见地板，只生成可行走的 Col_AirFloor 碰撞盒（Ground 层），无需材质。</p>`
    : isThemed
    ? `<p class="modal-hint">当前为主题地板：写回时整块区域生成一个拉伸的 prefab 实例，可在下方改选；切回其它类型请用上方「地板类型」。</p>${themedBlock}`
    : isPlainSolid
      ? `<div class="mat-pick-title">切换材质（实心地板）</div>
         <div class="mat-pick-list">${matRows || '<div class="mat-pick-empty">当前关卡集无材质</div>'}</div>`
      : isImage
        ? imageBlock
        : isTinted
          ? colorBlock
          : `<p class="modal-hint">木筏地板由写回时按尺寸铺满真实木筏拼块生成，无需材质。</p>`;

  const meshTag = isAir ? "" : f.meshType === "plane" ? "（Plane）" : f.meshType === "quad" ? "（Quad）" : "";
  const typeLabel = isAir ? "空气地板" : isRaft ? "木筏地板" : isThemed ? "主题地板" : isImage ? "图片地板" : isTinted ? "染色地板" : "实心地板";
  const walkH = floorWalkY(f);
  const body = `
    <div class="fm-summary fm-inline">
      <span><b>类型</b> ${typeLabel}${meshTag}</span>
      <span><b>尺寸</b> <span id="fe-size">${f._wCells}×${f._dCells}格 (${(f._wCells * CELL).toFixed(1)}×${(f._dCells * CELL).toFixed(1)}m，${areaCells}格)</span></span>
      <span><b>材质</b> <span id="fe-mat">${floorMatSummary(f, matchedMat)}</span></span>
      <span><b>坐标</b> x${f._wx.toFixed(2)}, z${f._wz.toFixed(2)}</span>
      <span><b>高度</b> h=${walkH.toFixed(2)} · <span id="fe-h-layer">L${floorLayerIndex(walkH)}</span></span>
      <span><b>死亡</b> ${deathLabelZh(S.deathInfo)}</span>
    </div>
    <div class="floor-edit-row">
      <label>宽(格) <input type="number" min="1" id="fe-w" value="${f._wCells}" /></label>
      <label>高(格) <input type="number" min="1" id="fe-d" value="${f._dCells}" /></label>
      <label>旋转(°) <input type="number" step="90" id="fe-rot" value="${normalizeRot(f.localRotationY)}" /></label>
      <label title="行走面高度：0=地面层；抬高后站上地板的物品会一起抬升，写回时碰撞盒跟随">高度 <input type="number" min="0" step="0.05" id="fe-h" value="${walkH.toFixed(2)}" /></label>
      <span class="muted" style="align-self:center;font-size:11px">实时应用 · 也可 R / Shift+R</span>
    </div>
    <div class="mat-pick-title">地板类型</div>
    <div class="mat-pick-list type-row">
      <button type="button" class="mat-pick${isPlainSolid ? " active" : ""}" data-kind="solid"><span class="mat-id">⬛ 实心地板</span><span class="mat-sub">单块 Plane + 材质</span></button>
      <button type="button" class="mat-pick${isTinted ? " active" : ""}" data-kind="tinted"><span class="mat-id">🎨 染色地板</span><span class="mat-sub">实心 Plane + 纯色染色</span></button>
      <button type="button" class="mat-pick${isImage ? " active" : ""}" data-kind="image"><span class="mat-id">🖼️ 图片地板</span><span class="mat-sub">实心 Plane + 上传图片（平铺/铺开）</span></button>
      <button type="button" class="mat-pick${isRaft ? " active" : ""}" data-kind="raft"><span class="mat-id">🪵 木筏地板</span><span class="mat-sub">写回时按尺寸铺满真实木筏拼块</span></button>
      <button type="button" class="mat-pick${isThemed ? " active" : ""}" data-kind="themed"><span class="mat-id">🧩 主题地板</span><span class="mat-sub">整块区域 = 一个拉伸 prefab（地毯/冰/雪…）</span></button>
      <button type="button" class="mat-pick${isAir ? " active" : ""}" data-kind="air"><span class="mat-id">💨 空气地板</span><span class="mat-sub">仅可行走碰撞盒，无可见地板</span></button>
    </div>
    ${materialBlock}
    <p class="modal-hint">提示：拖四角缩放 · 宽高输入即时生效 · 左键拖动移动 · Esc 关闭</p>
  `;
  const footer = `<button type="button" class="modal-btn" data-fm-copy>复制</button><button type="button" class="modal-btn" data-fm-dup>克隆</button><button type="button" class="modal-btn" data-fm-delete>删除地板</button><button type="button" class="modal-btn primary" data-fm-close>关闭</button>`;

  openModal(`${typeLabel} · ${f.displayName}`, body, footer);
  document.querySelector(".modal-panel")?.classList.add("wide", "floor-edit");

  const feW = document.getElementById("fe-w") as HTMLInputElement;
  const feD = document.getElementById("fe-d") as HTMLInputElement;
  let sizePushed = false;
  const applyFloorSizeLive = () => {
    const wv = parseInt(feW.value, 10);
    const dv = parseInt(feD.value, 10);
    if (!(wv > 0) && !(dv > 0)) return;
    if (!sizePushed) {
      pushHistory();
      sizePushed = true;
    }
    if (wv > 0) f._wCells = wv;
    if (dv > 0) f._dCells = dv;
    finalizeFloor(f);
    draw();
    const sz = document.getElementById("fe-size");
    if (sz)
      sz.textContent = `${f._wCells}×${f._dCells}格 (${(f._wCells * CELL).toFixed(1)}×${(f._dCells * CELL).toFixed(1)}m，${f._wCells * f._dCells}格)`;
  };
  feW.addEventListener("input", applyFloorSizeLive);
  feD.addEventListener("input", applyFloorSizeLive);
  feW.addEventListener("change", applyFloorSizeLive);
  feD.addEventListener("change", applyFloorSizeLive);

  const feRot = document.getElementById("fe-rot") as HTMLInputElement | null;
  let rotPushed = false;
  const applyRotLive = () => {
    const v = parseInt(feRot?.value ?? "", 10);
    if (!Number.isFinite(v)) return;
    if (!rotPushed) {
      pushHistory();
      rotPushed = true;
    }
    f.localRotationY = normalizeRot(v);
    finalizeFloor(f);
    draw();
  };
  feRot?.addEventListener("input", applyRotLive);
  feRot?.addEventListener("change", applyRotLive);

  // 行走面高度：h=0 回落类型默认视觉 Y（实心 -0.05 / 主题 0.01 / 空气 0），h>0
  // 时视觉=行走面。站上地板的物品（Y≈h0）随动抬升，写回时碰撞盒由后端跟随。
  const feH = document.getElementById("fe-h") as HTMLInputElement | null;
  let hPushed = false;
  const applyHeightLive = () => {
    const v = parseFloat(feH?.value ?? "");
    if (!Number.isFinite(v) || v < 0) return;
    const h1 = Math.round(v * 100) / 100;
    const h0 = floorWalkY(f);
    if (Math.abs(h1 - h0) < 1e-6) return;
    if (!hPushed) {
      pushHistory();
      hPushed = true;
    }
    const kind = isAirFloor(f) ? "air" : isThemedFloor(f) ? "themed" : "plane";
    const y = floorVisualYForWalkHeight(h1, kind);
    f.localPosition.y = y;
    if (f.worldPosition) f.worldPosition.y = y;
    const dy = h1 - h0;
    const hw = (f._wCells * CELL) / 2;
    const hh = (f._dCells * CELL) / 2;
    let lifted = 0;
    for (const it of S.items) {
      const { lx, lz } = floorLocalPoint(f, it._wx, it._wz);
      if (Math.abs(lx) > hw + 0.01 || Math.abs(lz) > hh + 0.01) continue;
      const iy = it.localPosition?.y ?? 0;
      if (Math.abs(iy - h0) > 0.1) continue;
      it.localPosition.y = snapValue(iy + dy, S.freeSnapStep);
      if (it.worldPosition) it.worldPosition.y = it.localPosition.y;
      lifted++;
    }
    draw();
    updateFloorBar();
    refreshFloorHeightPanel();
    const layerEl = document.getElementById("fe-h-layer");
    if (layerEl) layerEl.textContent = `L${floorLayerIndex(h1)}`;
    if (lifted > 0) setStatus(`${lifted} 个物品已随地板抬升至 h=${h1.toFixed(2)}（可 Ctrl+Z 撤回）`);
  };
  feH?.addEventListener("input", applyHeightLive);
  feH?.addEventListener("change", applyHeightLive);

  document.querySelector<HTMLButtonElement>('.mat-pick[data-kind="solid"]')?.addEventListener("click", () => {
    if (isPlainSolid) return; // already solid
    pushHistory();
    f.surfaceKind = "solid";
    f.airFloor = false;
    if (f.parentPath === "Design/Collision") f.parentPath = "Art/Ground";
    f.tintEnabled = false;
    f.imageTexturePath = undefined;
    f.imageMode = undefined;
    f.imageRotation = undefined;
    f.prefabGuid = undefined;
    f.prefabAssetPath = undefined;
    finalizeFloor(f);
    draw();
    setStatus("已设为实心地板（可在下方切换材质）");
    openFloorEditorModal(f);
  });
  document.querySelector<HTMLButtonElement>('.mat-pick[data-kind="tinted"]')?.addEventListener("click", () => {
    if (isTinted) return; // already tinted
    pushHistory();
    f.surfaceKind = "solid";
    f.airFloor = false;
    if (f.parentPath === "Design/Collision") f.parentPath = "Art/Ground";
    f.tintEnabled = true;
    f.imageTexturePath = undefined;
    f.imageMode = undefined;
    f.imageRotation = undefined;
    f.prefabGuid = undefined;
    f.prefabAssetPath = undefined;
    if (!f.tintColor) f.tintColor = "#9aa0a6";
    finalizeFloor(f);
    draw();
    setStatus("已设为染色地板（实心 Plane + 纯色，写回后生效）");
    openFloorEditorModal(f);
  });
  document.querySelector<HTMLButtonElement>('.mat-pick[data-kind="image"]')?.addEventListener("click", () => {
    if (isImage) return; // already image
    pushHistory();
    f.surfaceKind = "solid";
    f.airFloor = false;
    if (f.parentPath === "Design/Collision") f.parentPath = "Art/Ground";
    f.tintEnabled = false;
    f.prefabGuid = undefined;
    f.prefabAssetPath = undefined;
    if (!f.imageMode) f.imageMode = "stretch";
    finalizeFloor(f);
    draw();
    setStatus("已设为图片地板，请在下方上传图片");
    openFloorEditorModal(f);
  });
  document.querySelector<HTMLButtonElement>('.mat-pick[data-kind="raft"]')?.addEventListener("click", () => {
    if (isRaft) return; // already raft
    pushHistory();
    f.surfaceKind = "raft";
    f.airFloor = false;
    f.tintEnabled = false;
    f.imageTexturePath = undefined;
    f.imageMode = undefined;
    f.imageRotation = undefined;
    f.prefabGuid = undefined;
    f.prefabAssetPath = undefined;
    snapRaftCenterToGrid(f);
    finalizeFloor(f);
    draw();
    setStatus(`已设为木筏地板 ${f._wCells}×${f._dCells}（写回时铺满拼块，可 Ctrl+Z 撤回）`);
    openFloorEditorModal(f);
  });
  document.querySelector<HTMLButtonElement>('.mat-pick[data-kind="themed"]')?.addEventListener("click", () => {
    if (isThemed) return; // already themed
    const cat = (f.prefabGuid ? S.catalogByGuid.get(f.prefabGuid) : undefined) ?? themedFloorPrefabs()[0];
    if (!cat) {
      setStatus("catalog 中无可用主题地板 prefab", false);
      return;
    }
    pushHistory();
    f.prefabGuid = cat.guid;
    f.prefabAssetPath = cat.assetPath;
    f.surfaceKind = cat.surfaceKind ?? "solid";
    f.displayName = cat.id;
    f.airFloor = false;
    f.tintEnabled = false;
    f.imageTexturePath = undefined;
    f.imageMode = undefined;
    f.imageRotation = undefined;
    finalizeFloor(f);
    draw();
    setStatus(`已设为主题地板：${tidyCatalogNameZh(cat.nameZh, cat.id)}（写回时生成单个缩放实例，可 Ctrl+Z 撤回）`);
    openFloorEditorModal(f);
  });
  document.querySelector<HTMLButtonElement>('.mat-pick[data-kind="air"]')?.addEventListener("click", () => {
    if (isAir) return; // already air
    pushHistory();
    f.airFloor = true;
    f.surfaceKind = "solid";
    f.tintEnabled = false;
    f.tintColor = undefined;
    f.imageTexturePath = undefined;
    f.imageMode = undefined;
    f.imageRotation = undefined;
    f.prefabGuid = undefined;
    f.prefabAssetPath = undefined;
    f.materialGuid = undefined;
    f.materialAssetPath = undefined;
    f.materialName = undefined;
    f.displayName = "AirFloor";
    f.meshType = "plane";
    f.meshFileId = 0;
    f.parentPath = "Design/Collision";
    finalizeFloor(f);
    draw();
    setStatus("已设为空气地板（仅可行走，无可见地板，写回后生效）");
    openFloorEditorModal(f);
  });
  // 主题地板: pick a themed prefab → whole rect tiles it on write-back.
  document.querySelectorAll<HTMLButtonElement>(".mat-pick[data-pguid]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const cat = S.catalogByGuid.get(btn.dataset.pguid ?? "");
      if (!cat) return;
      if (f.prefabGuid === cat.guid) return;
      pushHistory();
      f.prefabGuid = cat.guid;
      f.prefabAssetPath = cat.assetPath;
      f.surfaceKind = cat.surfaceKind ?? "solid";
      f.displayName = cat.id;
      f.airFloor = false;
      f.tintEnabled = false;
      f.imageTexturePath = undefined;
      f.imageMode = undefined;
      f.imageRotation = undefined;
      finalizeFloor(f);
      draw();
      setStatus(`已设为主题地板：${tidyCatalogNameZh(cat.nameZh, cat.id)}（写回时生成单个缩放实例，可 Ctrl+Z 撤回）`);
      openFloorEditorModal(f);
    });
  });
  // 染色地板 color picker (only present for the tinted type).
  const feTint = document.getElementById("fe-tint") as HTMLInputElement | null;
  let tintPushed = false;
  feTint?.addEventListener("input", () => {
    if (!tintPushed) {
      pushHistory();
      tintPushed = true;
    }
    f.tintColor = feTint.value;
    f.tintEnabled = true;
    draw();
  });
  document.getElementById("fe-tint-clear")?.addEventListener("click", () => {
    pushHistory();
    f.tintColor = "#ffffff";
    f.tintEnabled = true;
    draw();
    openFloorEditorModal(f);
  });

  // 图片地板: mode toggle + upload.
  document.querySelectorAll<HTMLInputElement>('input[name="fe-imgmode"]').forEach((radio) => {
    radio.addEventListener("change", () => {
      if (!radio.checked) return;
      pushHistory();
      f.imageMode = radio.value === "tile" ? "tile" : "stretch";
      finalizeFloor(f);
      draw();
    });
  });
  const feImgRot = document.getElementById("fe-img-rot") as HTMLSelectElement | null;
  feImgRot?.addEventListener("change", () => {
    const v = parseInt(feImgRot.value, 10);
    if (!Number.isFinite(v)) return;
    pushHistory();
    f.imageRotation = normalizeRot(v);
    const preview = document.getElementById("fe-img-preview") as HTMLImageElement | null;
    if (preview) preview.style.transform = `rotate(${f.imageRotation}deg)`;
    draw();
    setStatus(`图片旋转已设为 ${f.imageRotation}°（写回后生效）`);
  });
  const feImgOpacity = document.getElementById("fe-img-opacity") as HTMLInputElement | null;
  const feImgOpacityVal = document.getElementById("fe-img-opacity-val");
  feImgOpacity?.addEventListener("input", () => {
    const pct = parseInt(feImgOpacity.value, 10);
    if (!Number.isFinite(pct)) return;
    if (!tintPushed) {
      pushHistory();
      tintPushed = true;
    }
    f.imageOpacity = Math.max(0, Math.min(1, pct / 100));
    if (feImgOpacityVal) feImgOpacityVal.textContent = `${pct}%`;
    draw();
  });
  const feImgFile = document.getElementById("fe-img-file") as HTMLInputElement | null;
  document.getElementById("fe-img-upload")?.addEventListener("click", async () => {
    const file = feImgFile?.files?.[0];
    if (!file) {
      setStatus("请先选择一张图片", false);
      return;
    }
    if (!S.currentLevelSet) {
      setStatus("未识别当前关卡集，无法保存图片", false);
      return;
    }
    showBusy("上传图片…");
    try {
      const base64 = await new Promise<string>((resolve, reject) => {
        const r = new FileReader();
        r.onload = () => {
          const s = r.result as string;
          const comma = s.indexOf(",");
          resolve(comma >= 0 ? s.slice(comma + 1) : s);
        };
        r.onerror = () => reject(new Error("读取图片失败"));
        r.readAsDataURL(file);
      });
      const texturePath = await uploadImageFloor(S.currentLevelSet, file.name, base64);
      if (!texturePath) {
        setStatus("上传失败（请确认 Bridge 已连接且关卡集存在）", false);
        return;
      }
      pushHistory();
      f.surfaceKind = "solid";
      f.tintEnabled = false;
      f.imageTexturePath = texturePath;
      f.imageMode = f.imageMode ?? "stretch";
      if (f.imageOpacity == null) f.imageOpacity = 1;
      clearFloorImageCache(texturePath);
      finalizeFloor(f);
      draw();
      setStatus(`图片地板已应用：${texturePath.split("/").pop() ?? ""}（写回后生效）`);
      openFloorEditorModal(f);
    } catch (e) {
      setStatus((e as Error).message, false);
    } finally {
      hideBusy();
    }
  });

  if (isPlainSolid) {
    document.querySelectorAll<HTMLButtonElement>(".mat-pick[data-guid]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const m = S.floorMaterials.find((x) => x.guid === btn.dataset.guid);
        if (!m) return;
        pushHistory();
        f.materialGuid = m.guid;
        f.materialAssetPath = m.assetPath;
        f.materialName = m.id;
        f.surfaceKind = "solid";
        draw();
        setStatus(`已切换材质：${m.nameZh}（写回后生效）`);
        openFloorEditorModal(f);
      });
    });
  }

  document.querySelector("[data-fm-close]")?.addEventListener("click", closeModal);
  document.querySelector("[data-fm-copy]")?.addEventListener("click", () => {
    setFloorSelection([f._key]);
    copyFloors();
  });
  document.querySelector("[data-fm-dup]")?.addEventListener("click", () => {
    setFloorSelection([f._key]);
    duplicateFloors();
  });
  document.querySelector("[data-fm-delete]")?.addEventListener("click", () => {
    pushHistory();
    S.floors = S.floors.filter((x) => x._key !== f._key);
    S.selectedFloorKeys.delete(f._key);
    S.selectedFloorKey = null;
    closeModal();
    draw();
    updateFloorBar();
    setStatus("已删除地板");
  });
}
