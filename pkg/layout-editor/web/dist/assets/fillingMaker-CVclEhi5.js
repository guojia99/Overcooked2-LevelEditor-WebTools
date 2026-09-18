import{L as w,O as T,n as k,w as P,X as m,Q as M,Y as x,U as L,x as A,Z as q,$ as Z}from"./version-aGNr2fLU.js";import{openRecipeModelPreview as j}from"./recipeModelPreview-DicRs3vr.js";import{a as O}from"./foodIconChain-GPcILpbK.js";import{renderRecipeForm as U}from"./customRecipes-D6gQ1CQq.js";import"./modelUnits-BbgM9XXv.js";const D="burger_filling";function l(s){return String(s??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function p(s,d=!0){const o=document.getElementById("fm-status");o&&(o.textContent=s,o.classList.toggle("err",!d),o.classList.toggle("ok",d&&s.length>0))}function S(s){return s.assetPath.replace(/\\/g,"/").includes("/commonW2/")}function N(s){return(s.score??0)>0?!1:S(s)?(s.subcategory??"")==="filling":!0}async function Q(s){document.body.classList.add("manage-bg");const d=w(),o=d.page==="filling-maker"?d.setId??"":"";let u=[];try{u=await T()}catch{}if(u.length===0){s.innerHTML=`${k("custom-recipes")}
      <div class="manage-bar"><h1 class="m-title">🥩 夹心工作台</h1></div>
      <div class="manage-content"><div class="m-block"><h3>没有可用关卡集</h3>
        <p class="muted">夹心归属关卡集。请先在「关卡管理」创建关卡集，并打开过一次其自定义菜谱页（初始化配置）。
        若是桥接未启动，请在 Unity 中 Tools → Layout Editor → 启动服务。</p></div></div>`,P();return}const E=o!==""&&u.some(t=>t.setName===o);let i=E?o:u[0].setName;const B=E?d.fillingId??"":"",I=m(i,B||void 0);location.pathname!==I&&history.replaceState(null,"",I);function f(t){location.pathname!==t&&history.pushState(null,"",t)}function C(){const t=w();return t.page==="filling-maker"&&!!t.fillingId}let h=[];async function v(){s.innerHTML=`
      ${k("custom-recipes")}
      <div class="manage-bar">
        <h1 class="m-title">🥩 夹心工作台</h1>
        <span class="status" id="fm-status"></span>
        <span style="flex:1"></span>
        <a class="m-btn" href="/custom-recipes/burger-maker">🍔 汉堡组装工作台</a>
        <a class="m-btn" href="/custom-recipes">← 菜谱管理</a>
      </div>
      <div class="manage-content" id="fm-content"><p class="muted">加载中…</p></div>
    `,P();const t=document.getElementById("fm-content");let c=[];try{M("加载夹心…"),c=await x(i)}catch(e){t.innerHTML=`<div class="m-block"><h3>加载失败</h3><p class="muted">${l(e.message)}</p><p class="muted">请确认 Unity 编辑器已启动且桥接服务运行中。</p></div>`,L();return}finally{L()}const a=c.filter(e=>!S(e)&&N(e)),n=c.filter(e=>S(e)&&N(e));h=a;const r=(e,$)=>{let b;try{b=A(q(e),{iconSrc:()=>O({id:e.id,assetPath:e.assetPath,iconState:e.iconState,iconFallbackIds:e.ingredients??[],isCustom:!0})})}catch(R){b=`<div class="m-card"><h3>${l(e.nameZh)}</h3><p class="muted">卡片渲染失败：${l(R.message)}</p></div>`}const H=e.hasModel||!!e.hasModelSO||!!e.previewable?'<span class="bm-badge ok">有模型</span>':'<span class="bm-badge warn" title="没有模型的夹心在汉堡里那一层看不见，且不会出现在汉堡工作台候选中">⚠ 无模型</span>';return`
        <div class="cr-card-wrap">
          <div class="cr-card-inner">${b}</div>
          <div class="cr-card-foot">
            ${$?'<span class="cr-cat-tag">共享库（只读）</span>':`<span class="cr-cat-tag">${l(e.category)}</span>`}
            ${H}
            <span style="flex:1"></span>
            ${e.previewable?`<button class="m-btn small" data-fm-preview="${l(e.assetPath)}" data-fm-name="${l(e.nameZh)}" title="3D 模型在线预览">👁</button>`:""}
            ${$?"":`<button class="m-btn small" data-fm-edit="${l(e.assetPath)}">编辑</button>`}
            ${$?"":`<button class="m-btn small danger" data-fm-del="${l(e.assetPath)}">删除</button>`}
          </div>
        </div>`};t.innerHTML=`
      <div class="m-actions-row">
        <span class="muted">关卡集</span>
        <select id="fm-set" class="rl-select">
          ${u.map(e=>`<option value="${l(e.setName)}" ${e.setName===i?"selected":""}>${l(e.levelSetNameZH||e.setName)}（${l(e.setName)}）</option>`).join("")}
        </select>
        <span style="flex:1"></span>
        <button class="m-btn primary" id="fm-new">＋ 新建夹心</button>
      </div>
      <p class="modal-hint">夹心 = <b>0 分的自定义菜谱</b>，不可单独点单，只作为汉堡的一层。
        新建后给它一个模型（可用「模板网格 + 自制贴图」零建模），就能在
        <b>🍔 汉堡组装工作台</b> 的候选里选用 —— 没有模型的夹心不会出现在候选中，因为游戏里那层看不见。</p>

      <section class="rl-section">
        <h2 class="rl-section-title">本关卡集的夹心<span class="rl-section-count">${a.length}</span></h2>
        ${a.length>0?`<div class="rl-grid">${a.map(e=>r(e,!1)).join("")}</div>`:'<p class="muted">还没有夹心。点右上角「＋ 新建夹心」创建第一个。</p>'}
      </section>

      <section class="rl-section">
        <h2 class="rl-section-title">🍔 Burger大全 共享夹心（只读参考）<span class="rl-section-count">${n.length}</span></h2>
        <p class="muted small">这些来自 commonW2 共享库，所有关卡集都能直接选用；要改请在本关卡集另建一个。</p>
        ${n.length>0?`<div class="rl-grid">${n.map(e=>r(e,!0)).join("")}</div>`:'<p class="muted">共享库中没有夹心条目。</p>'}
      </section>
    `,p(`本关卡集 ${a.length} 个夹心 · 共享库 ${n.length} 个`),F(),C()||f(m(i))}function g(t){U(s,i,t,{mode:"filling",score:0,category:D,onBack:()=>{f(m(i)),v()}})}async function y(){await v();const t=w();if(t.page==="filling-maker"&&t.setId===i&&t.fillingId){if(t.fillingId==="new"){g(null);return}const c=h.find(a=>a.id===t.fillingId);c?g(c.assetPath):p(`未找到夹心「${t.fillingId}」，请从列表重新进入。`,!1)}}function F(){var t,c;(t=document.getElementById("fm-set"))==null||t.addEventListener("change",a=>{i=a.target.value,history.replaceState(null,"",m(i)),y()}),(c=document.getElementById("fm-new"))==null||c.addEventListener("click",()=>{f(m(i,"new")),g(null)}),document.querySelectorAll("[data-fm-edit]").forEach(a=>a.addEventListener("click",()=>{const n=a.dataset.fmEdit,r=h.find(e=>e.assetPath===n);f(m(i,(r==null?void 0:r.id)??"new")),g(n)})),document.querySelectorAll("[data-fm-preview]").forEach(a=>a.addEventListener("click",()=>{j(a.dataset.fmPreview,a.dataset.fmName??"夹心模型",{fitTarget:"plate",onError:n=>p(n,!1)})})),document.querySelectorAll("[data-fm-del]").forEach(a=>a.addEventListener("click",()=>{const n=a.dataset.fmDel;confirm(`确定删除夹心「${n.split("/").pop()}」？
引用它的汉堡会丢失这一层。`)&&(async()=>{try{M("删除中…"),await Z(n),await v(),p("已删除。")}catch(r){p(r.message,!1)}finally{L()}})()}))}window.addEventListener("popstate",()=>void y()),await y()}export{Q as renderFillingMakerView};
