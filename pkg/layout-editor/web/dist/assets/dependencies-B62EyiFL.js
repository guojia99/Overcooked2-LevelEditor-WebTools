import{a$ as v,Y as z,g as k,v as i,b0 as A,b1 as C,b2 as E,b3 as O,k as u,b4 as y,af as I,ai as H,b5 as Z,n as q,b6 as U,w as X,X as Y,b7 as F}from"./dom-C1TKhhzS.js";function G(e,t){if(e&&t){const a=t.replace(/\\/g,"/").split("/"),d=a.length>=2?a[a.length-2]:"";location.assign(v(e,d||void 0));return}if(e){location.assign(v(e));return}location.assign("/manage")}function D(e){location.pathname!==e&&history.pushState(null,"",e)}function P(e){const t=e.replace(/\\/g,"/").split("/");return t.length>=2?t[t.length-2]:""}async function K(e,t){let a=[];try{a=await A(e)}catch{return null}const d=a.find(s=>(s.dataDir.split("/").pop()??"")===t)??a.find(s=>P(s.assetPath)===t);return(d==null?void 0:d.assetPath)??null}let x=!1;async function B(e){const t=z();if(t.page==="dependencies"){if(!t.setId){k();return}if(t.setId&&t.levelId){const a=await K(t.setId,t.levelId);if(a){await h(e,t.setId,a);return}await g(e,t.setId),i(`未找到关卡「${t.levelId}」，已返回关卡列表。`,!1);return}await g(e,t.setId)}}function l(e){return String(e??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function M(e){const t=document.getElementById("dep-content");t&&(t.innerHTML=`<p class="muted">${l(e)}</p>`)}function T(e){const t=e instanceof Error?e.message:String(e);i(t,!1);const a=document.getElementById("dep-content");a&&(a.innerHTML=`<div class="m-block"><h3>出错</h3><p>${l(t)}</p></div>`)}function N(){X(e=>{e==="layout"?y():e==="manage"?k():Y(e)})}function V(e){location.assign(F(e))}function R(e,t,a,d){var s,n;return document.body.classList.add("manage-bg"),e.innerHTML=`
    ${q("manage")}
    <div class="manage-bar">
      ${u(`← ${l(a)}`,"default",{id:"dep-back"})}
      <h1 class="m-title">${l(t)}</h1>
      <span style="flex:1"></span>
      ${u("↻ Reload","default",{id:"dep-reload",title:"触发 Unity Reload Pseudo Assets"})}
    </div>
    <div class="manage-content" id="dep-content"></div>
  `,N(),(s=document.getElementById("dep-back"))==null||s.addEventListener("click",()=>d==null?void 0:d()),(n=document.getElementById("dep-reload"))==null||n.addEventListener("click",async()=>{try{await U(),i("已触发 Unity Reload")}catch(o){i(o.message,!1)}}),document.getElementById("dep-content")}function S(e,t){const a=e.missing.filter(r=>!t.has(r)),d=e.extras,s=a.length?`<div class="dep-warn dep-miss">🔴 缺失（场景/菜谱引用但 dependencies 未覆盖）：<b>${a.map(l).join(", ")}</b></div>`:'<div class="dep-ok">依赖资源包完整（相对当前关卡引用）。</div>',n=d.length?`<div class="dep-warn dep-extra">🟡 检测到未使用 bundle：${d.map(l).join(", ")} <label class="modal-check inline"><input type="checkbox" id="dep-clean"> 保存时清理未用 bundle</label></div>`:"";return`
    <div class="dep-box">
      <div class="muted">分析结果（当前 dependencies）：${e.current.length?e.current.map(l).join(", "):"<i class='muted'>（无）</i>"}</div>
      ${s}
      ${n}
    </div>`}async function J(e){x||(x=!0,window.addEventListener("popstate",()=>void B(e))),await B(e)}async function g(e,t){D(v(t));const a=R(e,`依赖管理 · ${t}`,"返回关卡列表",()=>V(t));M(`加载 ${t} 的关卡…`);let d=[];try{d=await A(t)}catch(n){T(n);return}i(`共 ${d.length} 个关卡`);const s=d.map((n,o)=>{const r=n.dataDir.split("/").pop()||`level${o}`,f=n.levelNameZH||n.levelName||r;return`
      <div class="m-card">
        <h3>${l(f)}</h3>
        <div class="m-meta muted">${l(n.sceneName)} · ${l(r)}</div>
        <div class="m-actions">
          ${u("管理依赖","primary",{"data-deps":l(n.assetPath)})}
          ${n.sceneAssetPath?u("打开布局","default",{"data-layout":l(n.sceneAssetPath)}):""}
        </div>
      </div>`}).join("");a.innerHTML=`
    <p class="modal-hint">管理 <code>LevelInfoSO.dependencies</code>：查看 bundle 分析、手动编辑依赖列表。场景写回与菜谱保存后会自动重建 dependencies。</p>
    <div class="m-grid">${s||'<p class="muted">暂无关卡</p>'}</div>`,a.querySelectorAll("[data-deps]").forEach(n=>n.addEventListener("click",()=>void h(e,t,n.dataset.deps))),a.querySelectorAll("[data-layout]").forEach(n=>n.addEventListener("click",()=>y(n.dataset.layout)))}async function h(e,t,a){var $,w,L;D(v(t,P(a)||void 0));const d=R(e,`依赖管理 · ${t}`,"返回关卡列表",()=>void g(e,t));M("加载关卡依赖…");let s,n=null,o;try{[s,n,o]=await Promise.all([C(a),E(a).catch(()=>null),O()])}catch(c){T(c);return}if(!s){d.innerHTML='<p class="muted">未找到该关卡。</p>';return}const r=new Set(o.alwaysLoadedBundles),f=(s.dependencies||[]).join(`
`),b=s.levelNameZH||s.levelName||s.sceneName;i(`已加载：${b}`),d.innerHTML=`
    <div class="m-actions-row">
      ${u("↻ 刷新分析","default",{id:"dep-refresh"})}
      ${u("打开关卡编辑器","default",s.sceneAssetPath?{id:"dep-open-layout"}:{id:"dep-open-layout",disabled:""})}
    </div>
    <div class="m-block">
      <h3>Bundle 分析 · ${l(b)}</h3>
      <p class="modal-hint">对比关卡实际引用与 <code>LevelInfoSO.dependencies</code>。场景写回后会<b>覆盖重建</b> dependencies，再合并音频所需 bundle。</p>
      <div id="dep-analysis">${n?S(n,r):'<p class="muted">分析不可用（请确认 Unity Bridge 已连接）。</p>'}</div>
    </div>
    <div class="m-block">
      <h3>依赖 bundles 列表</h3>
      <label class="m-field">每行一个 bundle 名（如 bundle47 / bundle11 / myset/custom_recipes）
        <textarea id="dep-textarea" rows="12">${l(f)}</textarea>
      </label>
      <div class="m-actions-row">
        ${u("保存依赖","primary",{id:"dep-save"})}
      </div>
    </div>
  `,($=document.getElementById("dep-open-layout"))==null||$.addEventListener("click",()=>{s.sceneAssetPath&&y(s.sceneAssetPath)}),(w=document.getElementById("dep-refresh"))==null||w.addEventListener("click",async()=>{try{I("刷新分析…"),n=await E(a);const c=document.getElementById("dep-analysis");c&&(c.innerHTML=n?S(n,r):'<p class="muted">分析不可用。</p>'),i("分析已刷新")}catch(c){i(c.message,!1)}finally{H()}}),(L=document.getElementById("dep-save"))==null||L.addEventListener("click",async()=>{var c;try{let p=document.getElementById("dep-textarea").value.split(/\r?\n/).map(m=>m.trim()).filter(Boolean);if((((c=document.getElementById("dep-clean"))==null?void 0:c.checked)??!1)&&n&&n.extras.length){const m=new Set(p);n.extras.forEach(j=>m.delete(j)),p=[...m],document.getElementById("dep-textarea").value=p.join(`
`)}I("保存依赖…"),await Z({assetPath:a,levelName:s.levelName,levelNameZH:s.levelNameZH,debugRecipeCount:s.debugRecipeCount,disableDynamicParenting:s.disableDynamicParenting,minOrderCount:s.minOrderCount,maxOrderCount:s.maxOrderCount,gridHalfSizeX:s.gridHalfSizeX??0,gridHalfSizeY:s.gridHalfSizeY??1,gridHalfSizeZ:s.gridHalfSizeZ??0,dependencies:p}),i("依赖已保存（已 reload）"),await h(e,t,a)}catch(p){i(p.message,!1)}finally{H()}})}export{G as goDependencies,J as renderDependenciesView};
