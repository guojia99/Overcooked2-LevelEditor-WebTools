import{aA as m,P as O,g as S,aB as k,aC as R,aD as $,aE as j,aF as f,V as E,X as I,aG as z,n as Z,aH as q,w as U,O as X,aI as F}from"./version-2E74eDyL.js";function W(e,t){if(e&&t){const n=t.replace(/\\/g,"/").split("/"),d=n.length>=2?n[n.length-2]:"";location.assign(m(e,d||void 0));return}if(e){location.assign(m(e));return}location.assign("/manage")}function A(e){location.pathname!==e&&history.pushState(null,"",e)}function D(e){const t=e.replace(/\\/g,"/").split("/");return t.length>=2?t[t.length-2]:""}async function V(e,t){let n=[];try{n=await k(e)}catch{return null}const d=n.find(s=>(s.dataDir.split("/").pop()??"")===t)??n.find(s=>D(s.assetPath)===t);return(d==null?void 0:d.assetPath)??null}let x=!1;async function H(e){const t=O();if(t.page==="dependencies"){if(!t.setId){S();return}if(t.setId&&t.levelId){const n=await V(t.setId,t.levelId);if(n){await y(e,t.setId,n);return}await g(e,t.setId),i(`未找到关卡「${t.levelId}」，已返回关卡列表。`,!1);return}await g(e,t.setId)}}function l(e){return String(e??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function i(e,t=!0){const n=document.getElementById("dep-status");n&&(n.textContent=e,n.classList.toggle("err",!t),n.classList.toggle("ok",t&&e.length>0))}function P(e){const t=document.getElementById("dep-content");t&&(t.innerHTML=`<p class="muted">${l(e)}</p>`),i(e)}function M(e){const t=e instanceof Error?e.message:String(e);i(t,!1);const n=document.getElementById("dep-content");n&&(n.innerHTML=`<div class="m-block"><h3>出错</h3><p>${l(t)}</p></div>`)}function Y(){U(e=>{e==="layout"?f():e==="manage"?S():X(e)})}function G(e){location.assign(F(e))}function T(e,t,n,d){var s,a;return document.body.classList.add("manage-bg"),e.innerHTML=`
    ${Z("manage")}
    <div class="manage-bar">
      ${`<button class="m-btn" id="dep-back">← ${l(n)}</button>`}
      <h1 class="m-title">${l(t)}</h1>
      <span class="status" id="dep-status"></span>
      <span style="flex:1"></span>
      <button class="m-btn" id="dep-reload" title="触发 Unity Reload Pseudo Assets">↻ Reload</button>
    </div>
    <div class="manage-content" id="dep-content"></div>
  `,Y(),(s=document.getElementById("dep-back"))==null||s.addEventListener("click",()=>d==null?void 0:d()),(a=document.getElementById("dep-reload"))==null||a.addEventListener("click",async()=>{try{await q(),i("已触发 Unity Reload")}catch(o){i(o.message,!1)}}),document.getElementById("dep-content")}function B(e,t){const n=e.missing.filter(r=>!t.has(r)),d=e.extras,s=n.length?`<div class="dep-warn dep-miss">🔴 缺失（场景/菜谱引用但 dependencies 未覆盖）：<b>${n.map(l).join(", ")}</b></div>`:'<div class="dep-ok">依赖资源包完整（相对当前关卡引用）。</div>',a=d.length?`<div class="dep-warn dep-extra">🟡 检测到未使用 bundle：${d.map(l).join(", ")} <label class="modal-check inline"><input type="checkbox" id="dep-clean"> 保存时清理未用 bundle</label></div>`:"";return`
    <div class="dep-box">
      <div class="muted">分析结果（当前 dependencies）：${e.current.length?e.current.map(l).join(", "):"<i class='muted'>（无）</i>"}</div>
      ${s}
      ${a}
    </div>`}async function _(e){x||(x=!0,window.addEventListener("popstate",()=>void H(e))),await H(e)}async function g(e,t){A(m(t));const n=T(e,`依赖管理 · ${t}`,"返回关卡列表",()=>G(t));P(`加载 ${t} 的关卡…`);let d=[];try{d=await k(t)}catch(a){M(a);return}i(`共 ${d.length} 个关卡`);const s=d.map((a,o)=>{const r=a.dataDir.split("/").pop()||`level${o}`,v=a.levelNameZH||a.levelName||r;return`
      <div class="m-card">
        <h3>${l(v)}</h3>
        <div class="m-meta muted">${l(a.sceneName)} · ${l(r)}</div>
        <div class="m-actions">
          <button class="m-btn primary" data-deps="${l(a.assetPath)}">管理依赖</button>
          ${a.sceneAssetPath?`<button class="m-btn" data-layout="${l(a.sceneAssetPath)}">打开布局</button>`:""}
        </div>
      </div>`}).join("");n.innerHTML=`
    <p class="modal-hint">管理 <code>LevelInfoSO.dependencies</code>：查看 bundle 分析、手动编辑依赖列表。场景写回与菜谱保存后会自动重建 dependencies。</p>
    <div class="m-grid">${s||'<p class="muted">暂无关卡</p>'}</div>`,n.querySelectorAll("[data-deps]").forEach(a=>a.addEventListener("click",()=>void y(e,t,a.dataset.deps))),n.querySelectorAll("[data-layout]").forEach(a=>a.addEventListener("click",()=>f(a.dataset.layout)))}async function y(e,t,n){var b,L,w;A(m(t,D(n)||void 0));const d=T(e,`依赖管理 · ${t}`,"返回关卡列表",()=>void g(e,t));P("加载关卡依赖…");let s,a=null,o;try{[s,a,o]=await Promise.all([R(n),$(n).catch(()=>null),j()])}catch(c){M(c);return}if(!s){d.innerHTML='<p class="muted">未找到该关卡。</p>';return}const r=new Set(o.alwaysLoadedBundles),v=(s.dependencies||[]).join(`
`),h=s.levelNameZH||s.levelName||s.sceneName;i(`已加载：${h}`),d.innerHTML=`
    <div class="m-actions-row">
      <button class="m-btn" id="dep-refresh">↻ 刷新分析</button>
      <button class="m-btn" id="dep-open-layout" ${s.sceneAssetPath?"":"disabled"}>打开关卡编辑器</button>
    </div>
    <div class="m-block">
      <h3>Bundle 分析 · ${l(h)}</h3>
      <p class="modal-hint">对比关卡实际引用与 <code>LevelInfoSO.dependencies</code>。场景写回后会<b>覆盖重建</b> dependencies，再合并音频所需 bundle。</p>
      <div id="dep-analysis">${a?B(a,r):'<p class="muted">分析不可用（请确认 Unity Bridge 已连接）。</p>'}</div>
    </div>
    <div class="m-block">
      <h3>依赖 bundles 列表</h3>
      <label class="m-field">每行一个 bundle 名（如 bundle47 / bundle11 / myset/custom_recipes）
        <textarea id="dep-textarea" rows="12">${l(v)}</textarea>
      </label>
      <div class="m-actions-row">
        <button class="m-btn primary" id="dep-save">保存依赖</button>
      </div>
    </div>
  `,(b=document.getElementById("dep-open-layout"))==null||b.addEventListener("click",()=>{s.sceneAssetPath&&f(s.sceneAssetPath)}),(L=document.getElementById("dep-refresh"))==null||L.addEventListener("click",async()=>{try{E("刷新分析…"),a=await $(n);const c=document.getElementById("dep-analysis");c&&(c.innerHTML=a?B(a,r):'<p class="muted">分析不可用。</p>'),i("分析已刷新")}catch(c){i(c.message,!1)}finally{I()}}),(w=document.getElementById("dep-save"))==null||w.addEventListener("click",async()=>{var c;try{let u=document.getElementById("dep-textarea").value.split(/\r?\n/).map(p=>p.trim()).filter(Boolean);if((((c=document.getElementById("dep-clean"))==null?void 0:c.checked)??!1)&&a&&a.extras.length){const p=new Set(u);a.extras.forEach(C=>p.delete(C)),u=[...p],document.getElementById("dep-textarea").value=u.join(`
`)}E("保存依赖…"),await z({assetPath:n,levelName:s.levelName,levelNameZH:s.levelNameZH,debugRecipeCount:s.debugRecipeCount,disableDynamicParenting:s.disableDynamicParenting,minOrderCount:s.minOrderCount,maxOrderCount:s.maxOrderCount,gridHalfSizeX:s.gridHalfSizeX??0,gridHalfSizeY:s.gridHalfSizeY??1,gridHalfSizeZ:s.gridHalfSizeZ??0,dependencies:u}),i("依赖已保存（已 reload）"),await y(e,t,n)}catch(u){i(u.message,!1)}finally{I()}})}export{W as goDependencies,_ as renderDependenciesView};
