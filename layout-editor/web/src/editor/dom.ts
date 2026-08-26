import { navHtml } from "../nav";
import { S } from "./state";

/** 布局视图路由标记（由 URL hash 决定，模块加载时计算）。 */
export const MANAGE_ACTIVE = /^#\/manage/.test(location.hash);
export const CUSTOM_RECIPES_ACTIVE = /^#\/custom-recipes/.test(location.hash);
export const GUIDE_ACTIVE = /^#\/guide/.test(location.hash);

/** 全部 DOM 引用（buildLayoutDom 在布局视图填充；各模块禁止顶层访问 DOM，只在函数体内使用）。
 *  与拆分前 `document.getElementById(...) as HTMLCanvasElement` 等价：视为非空，直接使用。 */
export const dom = {
  app: null as unknown as HTMLElement,
  sceneSelect: null as unknown as HTMLSelectElement,
  statusEl: null as unknown as HTMLElement,
  paletteCats: null as unknown as HTMLElement,
  canvas: null as unknown as HTMLCanvasElement,
  ctx: null as unknown as CanvasRenderingContext2D,
  detailEl: null as unknown as HTMLElement,
  ctxMenuEl: null as unknown as HTMLElement,
  pickTipEl: null as unknown as HTMLElement,
  movePickBar: null as unknown as HTMLElement,
  floorBar: null as unknown as HTMLElement,
};

/** 布局视图的完整 DOM 模板 + 元素引用填充（仅 layout 视图调用；manage/custom-recipes 返回空）。 */
export function buildLayoutDom(): void {
  dom.app = document.getElementById("app")!;
  if (MANAGE_ACTIVE || CUSTOM_RECIPES_ACTIVE || GUIDE_ACTIVE) return;
  document.body.classList.remove("manage-bg");
  dom.app.innerHTML = `
  ${navHtml("layout")}
  <div class="toolbar">
    <div class="toolbar-row">
      <button id="btn-reload" title="重新加载当前场景">🔄 重新加载</button>
      <button id="btn-save" class="primary" title="将布局写回 Unity">💾 写回 Unity</button>
      <button id="btn-save-items" class="primary" title="仅写回核心物品（不修改地板、背景、装饰）">🎯 仅核心物品</button>
      <button id="btn-repair-broken" type="button" title="移除当前场景中源预制件缺失的损坏实例（解决 pseudoPrefabSO 空引用导致的 NullReferenceException）">🔧 修复损坏</button>
      <button id="btn-deps-check" type="button" title="检查后端服务、音频、bundle 等依赖是否就绪">🩺 依赖检查</button>
      <button id="btn-test-layout" type="button" title="一键生成测试布局：30×16 地板 + 相机 FOV 56 + 全部食材箱 + 全部核心层道具（开关组合默认用组合）">🧪 测试布局</button>
      <span class="toolbar-sep"></span>
      <button id="btn-recipes" type="button" title="查看所有可用菜谱">📖 菜谱</button>
      <button id="btn-utensils" type="button" title="查看所有锅具参数，一键同步给相同锅具">🍳 锅具管理</button>
      <button id="btn-level-config" type="button" title="配置各玩家分数与关卡截图">📊 关卡配置</button>
      <button id="btn-camera-light" type="button" title="修改游戏相机背景色 / FOV 与 Art/Lights 灯光颜色、强度">🎥 相机/灯光</button>
      <button id="btn-level-audio" type="button" title="配置关卡音频">🔊 音频</button>
      <button id="btn-summary" type="button" title="查看关卡菜谱汇总并一键导出图片">📋 汇总</button>
      <button id="btn-sync" type="button" title="从其他关卡复制道具、地板与背景主题（仅前端数据，写回后生效）">📥 同步布局…</button>
      <span id="status" class="status">连接中…</span>
    </div>
    <div class="toolbar-row">
      <div class="layer-tabs" id="layer-tabs">
        <button type="button" data-layer="decor" class="layer-tab">🎀 装饰层</button>
        <button type="button" data-layer="items" class="layer-tab active">📦 核心层</button>
        <button type="button" data-layer="floor" class="layer-tab">🗺️ 地板层</button>
        <button type="button" data-layer="background" class="layer-tab">🌊 背景层</button>
        <button type="button" data-layer="move" class="layer-tab">🎬 移动层</button>
      </div>
      <span class="toolbar-sep"></span>
      <div class="vis-wrap">
        <button type="button" id="btn-visibility" title="控制当前层显示哪些类别的内容">👁 图层显示</button>
        <div id="vis-popover" class="vis-popover hidden">
          <div class="vis-title">当前层显示：</div>
          <label><input type="checkbox" data-vcat="items" checked /> 🎯 核心物品</label>
          <label><input type="checkbox" data-vcat="decor" checked /> 🎀 装饰</label>
          <label><input type="checkbox" data-vcat="floors" checked /> 🗺️ 地板</label>
          <label><input type="checkbox" data-vcat="background" checked /> 🌊 背景 / 水面</label>
          <div class="vis-note">关闭的类别将不显示、也不可点选（仅影响当前层）</div>
        </div>
      </div>
      <label class="toolbar-check" title="距半格网格 0.1 内自动吸附到网格，其余位置按所选精度自由摆放"><input type="checkbox" id="snap-grid" checked /> 🧲 吸附格子</label>
      <label class="toolbar-check">🎯 精度
        <select id="snap-free-step" title="自由摆放 / 微移的精度">
          <option value="0.1">0.1</option>
          <option value="0.01" selected>0.01</option>
          <option value="0.001">0.001</option>
        </select>
      </label>
      <label class="toolbar-check"><input type="checkbox" id="show-grid" checked /> 👁 显示网格</label>
      <label class="toolbar-check" title="在画布上显示游戏相机视野的大致范围（FOV 视锥与地面的交线，16:9 估算）"><input type="checkbox" id="show-camera-fov" ${S.showCameraFov ? "checked" : ""} /> 🎥 相机视野</label>
      <label class="toolbar-check"><input type="checkbox" id="show-coords" checked /> 📏 坐标系</label>
      <label class="toolbar-check" title="勾选后允许工作台重叠时仍然写回"><input type="checkbox" id="allow-ws-overlap" /> ⚠ 允许工作台重叠</label>
      <label class="toolbar-check" title="补齐锅具时自动分配中间产物（煎肉排/面糊/炸物等）到对应锅具的 allowedIngredientSOs"><input type="checkbox" id="chk-auto-intermediates" ${S.autoIntermediates ? "checked" : ""} /> 🧩 自动中间产物</label>
    </div>
  </div>
  <div class="main">
    <aside class="palette" id="palette-panel">
      <div class="palette-header">
        <input type="search" id="palette-search" placeholder="搜索 prefab…" />
        <select id="decor-size-filter" class="decor-size-filter hidden" title="按装饰物实测尺寸筛选（小 / 中 / 大 / 特大）">
          <option value="all">尺寸：全部</option>
          <option value="small">尺寸：小</option>
          <option value="medium">尺寸：中</option>
          <option value="large">尺寸：大</option>
          <option value="xl">尺寸：特大</option>
        </select>
      </div>
      <div class="palette-cats" id="palette-cats"></div>
    </aside>
    <div class="panel-resizer" id="palette-resizer" title="拖动调整宽度"></div>
    <button type="button" class="panel-collapse" id="btn-collapse-palette" title="收起 / 展开物品栏">◀</button>
    <div class="canvas-wrap">
      <canvas id="canvas"></canvas>
      <div id="item-detail" class="item-detail hidden" role="dialog"></div>
      <div id="ctx-menu" class="ctx-menu hidden" role="dialog"></div>
      <div id="pick-tip" class="pick-tip hidden" role="dialog"></div>
      <div id="move-pick-bar" class="move-pick-bar hidden" role="dialog"></div>
      <div id="floor-bar" class="floor-bar hidden"></div>
      <button type="button" id="fhf-toggle" class="fhf-toggle hidden" title="高度层过滤：按行走面高度分层显示（地板/核心/装饰层可用）">📐</button>
      <div id="floor-height-filter" class="floor-height-filter floating hidden">
        <div class="fhf-row">
          <span class="fhf-title">📐 高度层</span>
          <label class="fhf-thickness" title="每层的高度带宽（如 0.2 = 0~0.2 一层、0.2~0.4 一层）">层厚 <input type="number" id="fhf-thickness" min="0.05" max="2" step="0.05" value="0.2" /></label>
          <button type="button" id="fhf-reset" class="fhf-reset" title="显示全部高度">全部</button>
        </div>
        <div id="fhf-layers" class="fhf-layers"></div>
        <div class="fhf-sliders" title="自由高度区间（与上方层列表联动：点层=设为该层区间，拖滑块=自定义区间）">
          <div class="fhf-dual">
            <input type="range" id="fhf-min" min="0" max="2" step="0.05" value="0" />
            <input type="range" id="fhf-max" min="0" max="2" step="0.05" value="2" />
          </div>
          <div class="fhf-range-vals"><span class="fhf-val" id="fhf-min-val">0.00</span> ~ <span class="fhf-val" id="fhf-max-val">2.00</span></div>
        </div>
      </div>
      <div class="hint">拖拽空白框选 · Shift 加选 · Ctrl+C/V/X 复制/粘贴/裁切 · Ctrl+Z 撤回 · Ctrl+Shift+Z 重做 · 重叠点击弹出选择 · 空格+拖动平移 · 右键微移/旋转/改参数 · Del 删除 · R/Shift+R 旋转90° · 滚轮缩放</div>
    </div>
    <button type="button" class="panel-collapse" id="btn-collapse-items" title="收起 / 展开物品清单">▶</button>
    <div class="panel-resizer" id="panel-resizer" title="拖动调整宽度（最长占一半）"></div>
    <aside class="scene-items" id="items-panel">
      <div class="scene-items-header">
        <div class="panel-tabs" id="panel-tabs">
          <button type="button" data-tab="items" class="panel-tab active">📋 物品清单</button>
          <button type="button" data-tab="move" class="panel-tab">🎯 移动控制 <span id="move-control-count" class="move-control-count"></span></button>
          <button type="button" data-tab="bevents" class="panel-tab">🔘 按钮事件组 <span id="bevents-count" class="move-control-count"></span></button>
        </div>
        <span id="scene-items-count" class="scene-items-count"></span>
      </div>
      <div class="scene-items-body" id="scene-items-body"></div>
    </aside>
  </div>
`;
  dom.sceneSelect = document.getElementById("scene-select") as HTMLSelectElement;
  dom.statusEl = document.getElementById("status")!;
  dom.paletteCats = document.getElementById("palette-cats")!;
  dom.canvas = document.getElementById("canvas") as HTMLCanvasElement;
  dom.ctx = (dom.canvas && dom.canvas.getContext("2d")) as CanvasRenderingContext2D;
  dom.detailEl = document.getElementById("item-detail")!;
  dom.ctxMenuEl = document.getElementById("ctx-menu")!;
  dom.pickTipEl = document.getElementById("pick-tip")!;
  dom.movePickBar = document.getElementById("move-pick-bar")!;
  dom.floorBar = document.getElementById("floor-bar")!;
}
