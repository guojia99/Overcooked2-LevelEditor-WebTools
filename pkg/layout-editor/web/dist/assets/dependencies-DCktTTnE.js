import{aM as v,W as C,g as k,aN as P,aO as O,aP as E,aQ as z,j as u,aR as y,$ as I,a1 as x,aS as Z,n as U,aT as q,w as N,V,aU as W}from"./version-Cez_XDJY.js";function _(e,t){if(e&&t){const n=t.replace(/\\/g,"/").split("/"),d=n.length>=2?n[n.length-2]:"";location.assign(v(e,d||void 0));return}if(e){location.assign(v(e));return}location.assign("/manage")}function A(e){location.pathname!==e&&history.pushState(null,"",e)}function D(e){const t=e.replace(/\\/g,"/").split("/");return t.length>=2?t[t.length-2]:""}async function X(e,t){let n=[];try{n=await P(e)}catch{return null}const d=n.find(s=>(s.dataDir.split("/").pop()??"")===t)??n.find(s=>D(s.assetPath)===t);return(d==null?void 0:d.assetPath)??null}let H=!1;async function B(e){const t=C();if(t.page==="dependencies"){if(!t.setId){k();return}if(t.setId&&t.levelId){const n=await X(t.setId,t.levelId);if(n){await h(e,t.setId,n);return}await f(e,t.setId),i(`未找到关卡「${t.levelId}」，已返回关卡列表。`,!1);return}await f(e,t.setId)}}function l(e){return String(e??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function i(e,t=!0){const n=document.getElementById("dep-status");n&&(n.textContent=e,n.classList.toggle("err",!t),n.classList.toggle("ok",t&&e.length>0))}function M(e){const t=document.getElementById("dep-content");t&&(t.innerHTML=`<p class="muted">${l(e)}</p>`),i(e)}function T(e){const t=e instanceof Error?e.message:String(e);i(t,!1);const n=document.getElementById("dep-content");n&&(n.innerHTML=`<div class="m-block"><h3>出错</h3><p>${l(t)}</p></div>`)}function Y(){N(e=>{e==="layout"?y():e==="manage"?k():V(e)})}function F(e){location.assign(W(e))}function R(e,t,n,d){var s,a;return document.body.classList.add("manage-bg"),e.innerHTML=`
    ${U("manage")}
    <div class="manage-bar">
      ${u(`← ${l(n)}`,"default",{id:"dep-back"})}
      <h1 class="m-title">${l(t)}</h1>
      <span class="status" id="dep-status"></span>
      <span style="flex:1"></span>
      ${u("↻ Reload","default",{id:"dep-reload",title:"触发 Unity Reload Pseudo Assets"})}
    </div>
    <div class="manage-content" id="dep-content"></div>
  `,Y(),(s=document.getElementById("dep-back"))==null||s.addEventListener("click",()=>d==null?void 0:d()),(a=document.getElementById("dep-reload"))==null||a.addEventListener("click",async()=>{try{await q(),i("已触发 Unity Reload")}catch(o){i(o.message,!1)}}),document.getElementById("dep-content")}function S(e,t){const n=e.missing.filter(r=>!t.has(r)),d=e.extras,s=n.length?`<div class="dep-warn dep-miss">🔴 缺失（场景/菜谱引用但 dependencies 未覆盖）：<b>${n.map(l).join(", ")}</b></div>`:'<div class="dep-ok">依赖资源包完整（相对当前关卡引用）。</div>',a=d.length?`<div class="dep-warn dep-extra">🟡 检测到未使用 bundle：${d.map(l).join(", ")} <label class="modal-check inline"><input type="checkbox" id="dep-clean"> 保存时清理未用 bundle</label></div>`:"";return`
    <div class="dep-box">
      <div class="muted">分析结果（当前 dependencies）：${e.current.length?e.current.map(l).join(", "):"<i class='muted'>（无）</i>"}</div>
      ${s}
      ${a}
    </div>`}async function G(e){H||(H=!0,window.addEventListener("popstate",()=>void B(e))),await B(e)}async function f(e,t){A(v(t));const n=R(e,`依赖管理 · ${t}`,"返回关卡列表",()=>F(t));M(`加载 ${t} 的关卡…`);let d=[];try{d=await P(t)}catch(a){T(a);return}i(`共 ${d.length} 个关卡`);const s=d.map((a,o)=>{const r=a.dataDir.split("/").pop()||`level${o}`,g=a.levelNameZH||a.levelName||r;return`
      <div class="m-card">
        <h3>${l(g)}</h3>
        <div class="m-meta muted">${l(a.sceneName)} · ${l(r)}</div>
        <div class="m-actions">
          ${u("管理依赖","primary",{"data-deps":l(a.assetPath)})}
          ${a.sceneAssetPath?u("打开布局","default",{"data-layout":l(a.sceneAssetPath)}):""}
        </div>
      </div>`}).join("");n.innerHTML=`
    <p class="modal-hint">管理 <code>LevelInfoSO.dependencies</code>：查看 bundle 分析、手动编辑依赖列表。场景写回与菜谱保存后会自动重建 dependencies。</p>
    <div class="m-grid">${s||'<p class="muted">暂无关卡</p>'}</div>`,n.querySelectorAll("[data-deps]").forEach(a=>a.addEventListener("click",()=>void h(e,t,a.dataset.deps))),n.querySelectorAll("[data-layout]").forEach(a=>a.addEventListener("click",()=>y(a.dataset.layout)))}async function h(e,t,n){var $,w,b;A(v(t,D(n)||void 0));const d=R(e,`依赖管理 · ${t}`,"返回关卡列表",()=>void f(e,t));M("加载关卡依赖…");let s,a=null,o;try{[s,a,o]=await Promise.all([O(n),E(n).catch(()=>null),z()])}catch(c){T(c);return}if(!s){d.innerHTML='<p class="muted">未找到该关卡。</p>';return}const r=new Set(o.alwaysLoadedBundles),g=(s.dependencies||[]).join(`
`),L=s.levelNameZH||s.levelName||s.sceneName;i(`已加载：${L}`),d.innerHTML=`
    <div class="m-actions-row">
      ${u("↻ 刷新分析","default",{id:"dep-refresh"})}
      ${u("打开关卡编辑器","default",s.sceneAssetPath?{id:"dep-open-layout"}:{id:"dep-open-layout",disabled:""})}
    </div>
    <div class="m-block">
      <h3>Bundle 分析 · ${l(L)}</h3>
      <p class="modal-hint">对比关卡实际引用与 <code>LevelInfoSO.dependencies</code>。场景写回后会<b>覆盖重建</b> dependencies，再合并音频所需 bundle。</p>
      <div id="dep-analysis">${a?S(a,r):'<p class="muted">分析不可用（请确认 Unity Bridge 已连接）。</p>'}</div>
    </div>
    <div class="m-block">
      <h3>依赖 bundles 列表</h3>
      <label class="m-field">每行一个 bundle 名（如 bundle47 / bundle11 / myset/custom_recipes）
        <textarea id="dep-textarea" rows="12">${l(g)}</textarea>
      </label>
      <div class="m-actions-row">
        ${u("保存依赖","primary",{id:"dep-save"})}
      </div>
    </div>
  `,($=document.getElementById("dep-open-layout"))==null||$.addEventListener("click",()=>{s.sceneAssetPath&&y(s.sceneAssetPath)}),(w=document.getElementById("dep-refresh"))==null||w.addEventListener("click",async()=>{try{I("刷新分析…"),a=await E(n);const c=document.getElementById("dep-analysis");c&&(c.innerHTML=a?S(a,r):'<p class="muted">分析不可用。</p>'),i("分析已刷新")}catch(c){i(c.message,!1)}finally{x()}}),(b=document.getElementById("dep-save"))==null||b.addEventListener("click",async()=>{var c;try{let p=document.getElementById("dep-textarea").value.split(/\r?\n/).map(m=>m.trim()).filter(Boolean);if((((c=document.getElementById("dep-clean"))==null?void 0:c.checked)??!1)&&a&&a.extras.length){const m=new Set(p);a.extras.forEach(j=>m.delete(j)),p=[...m],document.getElementById("dep-textarea").value=p.join(`
`)}I("保存依赖…"),await Z({assetPath:n,levelName:s.levelName,levelNameZH:s.levelNameZH,debugRecipeCount:s.debugRecipeCount,disableDynamicParenting:s.disableDynamicParenting,minOrderCount:s.minOrderCount,maxOrderCount:s.maxOrderCount,gridHalfSizeX:s.gridHalfSizeX??0,gridHalfSizeY:s.gridHalfSizeY??1,gridHalfSizeZ:s.gridHalfSizeZ??0,dependencies:p}),i("依赖已保存（已 reload）"),await h(e,t,n)}catch(p){i(p.message,!1)}finally{x()}})}export{_ as goDependencies,G as renderDependenciesView};
