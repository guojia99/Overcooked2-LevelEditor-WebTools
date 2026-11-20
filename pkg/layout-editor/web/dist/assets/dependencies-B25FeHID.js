import{b5 as f,$ as z,g as A,v as i,b6 as D,b7 as C,b8 as I,b9 as O,k as p,ba as y,ag as H,aj as x,bb as q,n as U,bc as F,w as W,Z as X,bd as Y}from"./dom-DuqOIrWu.js";function Q(e,t){if(e&&t){const a=t.replace(/\\/g,"/").split("/"),s=a.length>=2?a[a.length-2]:"";location.assign(f(e,s||void 0));return}if(e){location.assign(f(e));return}location.assign("/manage")}function P(e){location.pathname!==e&&history.pushState(null,"",e)}function _(e){return!/^bundle[0-9]+$/.test(e)}function M(e){const t=e.replace(/\\/g,"/").split("/");return t.length>=2?t[t.length-2]:""}async function K(e,t){let a=[];try{a=await D(e)}catch{return null}const s=a.find(d=>(d.dataDir.split("/").pop()??"")===t)??a.find(d=>M(d.assetPath)===t);return(s==null?void 0:s.assetPath)??null}let B=!1;async function S(e){const t=z();if(t.page==="dependencies"){if(!t.setId){A();return}if(t.setId&&t.levelId){const a=await K(t.setId,t.levelId);if(a){await h(e,t.setId,a);return}await g(e,t.setId),i(`未找到关卡「${t.levelId}」，已返回关卡列表。`,!1);return}await g(e,t.setId)}}function l(e){return String(e??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function T(e){const t=document.getElementById("dep-content");t&&(t.innerHTML=`<p class="muted">${l(e)}</p>`)}function j(e){const t=e instanceof Error?e.message:String(e);i(t,!1);const a=document.getElementById("dep-content");a&&(a.innerHTML=`<div class="m-block"><h3>出错</h3><p>${l(t)}</p></div>`)}function N(){W(e=>{e==="layout"?y():e==="manage"?A():X(e)})}function V(e){location.assign(Y(e))}function R(e,t,a,s){var d,n;return document.body.classList.add("manage-bg"),e.innerHTML=`
    ${U("manage")}
    <div class="manage-bar">
      ${p(`← ${l(a)}`,"default",{id:"dep-back"})}
      <h1 class="m-title">${l(t)}</h1>
      <span style="flex:1"></span>
      ${p("↻ Reload","default",{id:"dep-reload",title:"触发 Unity Reload Pseudo Assets"})}
    </div>
    <div class="manage-content" id="dep-content"></div>
  `,N(),(d=document.getElementById("dep-back"))==null||d.addEventListener("click",()=>s==null?void 0:s()),(n=document.getElementById("dep-reload"))==null||n.addEventListener("click",async()=>{try{await F(),i("已触发 Unity Reload")}catch(o){i(o.message,!1)}}),document.getElementById("dep-content")}function k(e,t){const a=e.missing.filter(r=>!t.has(r)),s=e.extras,d=a.length?`<div class="dep-warn dep-miss">🔴 缺失（场景/菜谱引用但 dependencies 未覆盖）：<b>${a.map(l).join(", ")}</b></div>`:'<div class="dep-ok">依赖资源包完整（相对当前关卡引用）。</div>',n=s.length?`<div class="dep-warn dep-extra">🟡 检测到未使用 bundle：${s.map(l).join(", ")} <label class="modal-check inline"><input type="checkbox" id="dep-clean"> 保存时清理未用 bundle</label></div>`:"";return`
    <div class="dep-box">
      <div class="muted">分析结果（当前 dependencies）：${e.current.length?e.current.map(l).join(", "):"<i class='muted'>（无）</i>"}</div>
      ${d}
      ${n}
    </div>`}async function ee(e){B||(B=!0,window.addEventListener("popstate",()=>void S(e))),await S(e)}async function g(e,t){P(f(t));const a=R(e,`依赖管理 · ${t}`,"返回关卡列表",()=>V(t));T(`加载 ${t} 的关卡…`);let s=[];try{s=await D(t)}catch(n){j(n);return}i(`共 ${s.length} 个关卡`);const d=s.map((n,o)=>{const r=n.dataDir.split("/").pop()||`level${o}`,v=n.levelNameZH||n.levelName||r;return`
      <div class="m-card">
        <h3>${l(v)}</h3>
        <div class="m-meta muted">${l(n.sceneName)} · ${l(r)}</div>
        <div class="m-actions">
          ${p("管理依赖","primary",{"data-deps":l(n.assetPath)})}
          ${n.sceneAssetPath?p("打开布局","default",{"data-layout":l(n.sceneAssetPath)}):""}
        </div>
      </div>`}).join("");a.innerHTML=`
    <p class="modal-hint">管理 <code>LevelInfoSO.dependencies</code>：查看 bundle 分析、手动编辑依赖列表。场景写回与菜谱保存后会自动重建 dependencies。</p>
    <div class="m-grid">${d||'<p class="muted">暂无关卡</p>'}</div>`,a.querySelectorAll("[data-deps]").forEach(n=>n.addEventListener("click",()=>void h(e,t,n.dataset.deps))),a.querySelectorAll("[data-layout]").forEach(n=>n.addEventListener("click",()=>y(n.dataset.layout)))}async function h(e,t,a){var $,w,L;P(f(t,M(a)||void 0));const s=R(e,`依赖管理 · ${t}`,"返回关卡列表",()=>void g(e,t));T("加载关卡依赖…");let d,n=null,o;try{[d,n,o]=await Promise.all([C(a),I(a).catch(()=>null),O()])}catch(c){j(c);return}if(!d){s.innerHTML='<p class="muted">未找到该关卡。</p>';return}const r=new Set(o.alwaysLoadedBundles),v=(d.dependencies||[]).join(`
`),b=d.levelNameZH||d.levelName||d.sceneName;i(`已加载：${b}`),s.innerHTML=`
    <div class="m-actions-row">
      ${p("↻ 刷新分析","default",{id:"dep-refresh"})}
      ${p("打开关卡编辑器","default",d.sceneAssetPath?{id:"dep-open-layout"}:{id:"dep-open-layout",disabled:""})}
    </div>
    <div class="m-block">
      <h3>Bundle 分析 · ${l(b)}</h3>
      <p class="modal-hint">对比关卡实际引用与 <code>LevelInfoSO.dependencies</code>。场景写回后会<b>覆盖重建</b> dependencies，再合并音频所需 bundle。</p>
      <div id="dep-analysis">${n?k(n,r):'<p class="muted">分析不可用（请确认 Unity Bridge 已连接）。</p>'}</div>
    </div>
    <div class="m-block">
      <h3>依赖 bundles 列表</h3>
      <label class="m-field">每行一个原生 bundle 名（如 bundle47 / bundle11，仅允许 ^bundle[0-9]+$；commonW、custom_recipes、custom_content 不属于此字段）
        <textarea id="dep-textarea" rows="12">${l(v)}</textarea>
      </label>
      <div class="m-actions-row">
        ${p("保存依赖","primary",{id:"dep-save"})}
      </div>
    </div>
  `,($=document.getElementById("dep-open-layout"))==null||$.addEventListener("click",()=>{d.sceneAssetPath&&y(d.sceneAssetPath)}),(w=document.getElementById("dep-refresh"))==null||w.addEventListener("click",async()=>{try{H("刷新分析…"),n=await I(a);const c=document.getElementById("dep-analysis");c&&(c.innerHTML=n?k(n,r):'<p class="muted">分析不可用。</p>'),i("分析已刷新")}catch(c){i(c.message,!1)}finally{x()}}),(L=document.getElementById("dep-save"))==null||L.addEventListener("click",async()=>{var c;try{let u=document.getElementById("dep-textarea").value.split(/\r?\n/).map(m=>m.trim()).filter(Boolean);const E=u.filter(_);if(E.length){i(`保存被阻止：dependencies 仅允许原生 bundle（^bundle[0-9]+$）：${E.join("、")}`,!1);return}if((((c=document.getElementById("dep-clean"))==null?void 0:c.checked)??!1)&&n&&n.extras.length){const m=new Set(u);n.extras.forEach(Z=>m.delete(Z)),u=[...m],document.getElementById("dep-textarea").value=u.join(`
`)}H("保存依赖…"),await q({assetPath:a,levelName:d.levelName,levelNameZH:d.levelNameZH,debugRecipeCount:d.debugRecipeCount,disableDynamicParenting:d.disableDynamicParenting,minOrderCount:d.minOrderCount,maxOrderCount:d.maxOrderCount,gridHalfSizeX:d.gridHalfSizeX??0,gridHalfSizeY:d.gridHalfSizeY??1,gridHalfSizeZ:d.gridHalfSizeZ??0,dependencies:u}),i("依赖已保存（已 reload）"),await h(e,t,a)}catch(u){i(u.message,!1)}finally{x()}})}export{Q as goDependencies,ee as renderDependenciesView};
