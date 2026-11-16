import{b5 as f,$ as O,g as A,v as l,b6 as D,b7 as R,b8 as I,b9 as Z,k as p,ba as y,ag as x,aj as H,bb as z,n as q,bc as U,w as F,Z as W,bd as X}from"./dom-CNJXYsUw.js";function N(e,t){if(e&&t){const n=t.replace(/\\/g,"/").split("/"),d=n.length>=2?n[n.length-2]:"";location.assign(f(e,d||void 0));return}if(e){location.assign(f(e));return}location.assign("/manage")}function P(e){location.pathname!==e&&history.pushState(null,"",e)}function Y(e,t){const n=e.trim();if(!n)return!1;const d=n.substring(n.lastIndexOf("/")+1).toLowerCase();return d!=="custom_recipes"&&d!=="custom_content"?!1:n!==`${t}/custom_recipes`&&n!==`${t}/custom_content`}function M(e){const t=e.replace(/\\/g,"/").split("/");return t.length>=2?t[t.length-2]:""}async function K(e,t){let n=[];try{n=await D(e)}catch{return null}const d=n.find(s=>(s.dataDir.split("/").pop()??"")===t)??n.find(s=>M(s.assetPath)===t);return(d==null?void 0:d.assetPath)??null}let B=!1;async function S(e){const t=O();if(t.page==="dependencies"){if(!t.setId){A();return}if(t.setId&&t.levelId){const n=await K(t.setId,t.levelId);if(n){await h(e,t.setId,n);return}await g(e,t.setId),l(`未找到关卡「${t.levelId}」，已返回关卡列表。`,!1);return}await g(e,t.setId)}}function i(e){return String(e??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function T(e){const t=document.getElementById("dep-content");t&&(t.innerHTML=`<p class="muted">${i(e)}</p>`)}function j(e){const t=e instanceof Error?e.message:String(e);l(t,!1);const n=document.getElementById("dep-content");n&&(n.innerHTML=`<div class="m-block"><h3>出错</h3><p>${i(t)}</p></div>`)}function V(){F(e=>{e==="layout"?y():e==="manage"?A():W(e)})}function G(e){location.assign(X(e))}function _(e,t,n,d){var s,a;return document.body.classList.add("manage-bg"),e.innerHTML=`
    ${q("manage")}
    <div class="manage-bar">
      ${p(`← ${i(n)}`,"default",{id:"dep-back"})}
      <h1 class="m-title">${i(t)}</h1>
      <span style="flex:1"></span>
      ${p("↻ Reload","default",{id:"dep-reload",title:"触发 Unity Reload Pseudo Assets"})}
    </div>
    <div class="manage-content" id="dep-content"></div>
  `,V(),(s=document.getElementById("dep-back"))==null||s.addEventListener("click",()=>d==null?void 0:d()),(a=document.getElementById("dep-reload"))==null||a.addEventListener("click",async()=>{try{await U(),l("已触发 Unity Reload")}catch(o){l(o.message,!1)}}),document.getElementById("dep-content")}function k(e,t){const n=e.missing.filter(r=>!t.has(r)),d=e.extras,s=n.length?`<div class="dep-warn dep-miss">🔴 缺失（场景/菜谱引用但 dependencies 未覆盖）：<b>${n.map(i).join(", ")}</b></div>`:'<div class="dep-ok">依赖资源包完整（相对当前关卡引用）。</div>',a=d.length?`<div class="dep-warn dep-extra">🟡 检测到未使用 bundle：${d.map(i).join(", ")} <label class="modal-check inline"><input type="checkbox" id="dep-clean"> 保存时清理未用 bundle</label></div>`:"";return`
    <div class="dep-box">
      <div class="muted">分析结果（当前 dependencies）：${e.current.length?e.current.map(i).join(", "):"<i class='muted'>（无）</i>"}</div>
      ${s}
      ${a}
    </div>`}async function ee(e){B||(B=!0,window.addEventListener("popstate",()=>void S(e))),await S(e)}async function g(e,t){P(f(t));const n=_(e,`依赖管理 · ${t}`,"返回关卡列表",()=>G(t));T(`加载 ${t} 的关卡…`);let d=[];try{d=await D(t)}catch(a){j(a);return}l(`共 ${d.length} 个关卡`);const s=d.map((a,o)=>{const r=a.dataDir.split("/").pop()||`level${o}`,v=a.levelNameZH||a.levelName||r;return`
      <div class="m-card">
        <h3>${i(v)}</h3>
        <div class="m-meta muted">${i(a.sceneName)} · ${i(r)}</div>
        <div class="m-actions">
          ${p("管理依赖","primary",{"data-deps":i(a.assetPath)})}
          ${a.sceneAssetPath?p("打开布局","default",{"data-layout":i(a.sceneAssetPath)}):""}
        </div>
      </div>`}).join("");n.innerHTML=`
    <p class="modal-hint">管理 <code>LevelInfoSO.dependencies</code>：查看 bundle 分析、手动编辑依赖列表。场景写回与菜谱保存后会自动重建 dependencies。</p>
    <div class="m-grid">${s||'<p class="muted">暂无关卡</p>'}</div>`,n.querySelectorAll("[data-deps]").forEach(a=>a.addEventListener("click",()=>void h(e,t,a.dataset.deps))),n.querySelectorAll("[data-layout]").forEach(a=>a.addEventListener("click",()=>y(a.dataset.layout)))}async function h(e,t,n){var $,w,L;P(f(t,M(n)||void 0));const d=_(e,`依赖管理 · ${t}`,"返回关卡列表",()=>void g(e,t));T("加载关卡依赖…");let s,a=null,o;try{[s,a,o]=await Promise.all([R(n),I(n).catch(()=>null),Z()])}catch(c){j(c);return}if(!s){d.innerHTML='<p class="muted">未找到该关卡。</p>';return}const r=new Set(o.alwaysLoadedBundles),v=(s.dependencies||[]).join(`
`),b=s.levelNameZH||s.levelName||s.sceneName;l(`已加载：${b}`),d.innerHTML=`
    <div class="m-actions-row">
      ${p("↻ 刷新分析","default",{id:"dep-refresh"})}
      ${p("打开关卡编辑器","default",s.sceneAssetPath?{id:"dep-open-layout"}:{id:"dep-open-layout",disabled:""})}
    </div>
    <div class="m-block">
      <h3>Bundle 分析 · ${i(b)}</h3>
      <p class="modal-hint">对比关卡实际引用与 <code>LevelInfoSO.dependencies</code>。场景写回后会<b>覆盖重建</b> dependencies，再合并音频所需 bundle。</p>
      <div id="dep-analysis">${a?k(a,r):'<p class="muted">分析不可用（请确认 Unity Bridge 已连接）。</p>'}</div>
    </div>
    <div class="m-block">
      <h3>依赖 bundles 列表</h3>
      <label class="m-field">每行一个 bundle 名（如 bundle47 / bundle11 / commonW3；custom_recipes / custom_content 属关卡集数据，由系统自动管理、不能手动添加）
        <textarea id="dep-textarea" rows="12">${i(v)}</textarea>
      </label>
      <div class="m-actions-row">
        ${p("保存依赖","primary",{id:"dep-save"})}
      </div>
    </div>
  `,($=document.getElementById("dep-open-layout"))==null||$.addEventListener("click",()=>{s.sceneAssetPath&&y(s.sceneAssetPath)}),(w=document.getElementById("dep-refresh"))==null||w.addEventListener("click",async()=>{try{x("刷新分析…"),a=await I(n);const c=document.getElementById("dep-analysis");c&&(c.innerHTML=a?k(a,r):'<p class="muted">分析不可用。</p>'),l("分析已刷新")}catch(c){l(c.message,!1)}finally{H()}}),(L=document.getElementById("dep-save"))==null||L.addEventListener("click",async()=>{var c;try{let u=document.getElementById("dep-textarea").value.split(/\r?\n/).map(m=>m.trim()).filter(Boolean);const E=u.filter(m=>Y(m,t));if(E.length){l(`保存被阻止：以下条目属关卡集数据（custom_recipes / custom_content），由系统自动管理、不能手动添加：${E.join("、")}`,!1);return}if((((c=document.getElementById("dep-clean"))==null?void 0:c.checked)??!1)&&a&&a.extras.length){const m=new Set(u);a.extras.forEach(C=>m.delete(C)),u=[...m],document.getElementById("dep-textarea").value=u.join(`
`)}x("保存依赖…"),await z({assetPath:n,levelName:s.levelName,levelNameZH:s.levelNameZH,debugRecipeCount:s.debugRecipeCount,disableDynamicParenting:s.disableDynamicParenting,minOrderCount:s.minOrderCount,maxOrderCount:s.maxOrderCount,gridHalfSizeX:s.gridHalfSizeX??0,gridHalfSizeY:s.gridHalfSizeY??1,gridHalfSizeZ:s.gridHalfSizeZ??0,dependencies:u}),l("依赖已保存（已 reload）"),await h(e,t,n)}catch(u){l(u.message,!1)}finally{H()}})}export{N as goDependencies,ee as renderDependenciesView};
