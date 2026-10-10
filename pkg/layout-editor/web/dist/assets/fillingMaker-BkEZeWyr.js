import{X as S,Z as A,n as P,w as M,a5 as r,t as m,a0 as N,a6 as j,a2 as I,j as u,I as q,a7 as x,a8 as Z}from"./version-DTOSwNVA.js";import{openRecipeModelPreview as D}from"./recipeModelPreview-DQGt_1Ff.js";import{a as G}from"./foodIconChain-GPcILpbK.js";import{renderRecipeForm as O}from"./customRecipes-BuWAuvgH.js";const U="burger_filling";function l(n){return String(n??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function E(n){return n.assetPath.replace(/\\/g,"/").includes("/commonW2/")}function B(n){return(n.score??0)>0?!1:E(n)?(n.subcategory??"")==="filling":!0}async function Y(n){document.body.classList.add("manage-bg");const h=S(),g=h.page==="filling-maker"?h.setId??"":"";let d=[];try{d=await A()}catch{}if(d.length===0){n.innerHTML=`${P("custom-recipes")}
      <div class="manage-bar"><h1 class="m-title">🥩 夹心工作台</h1></div>
      <div class="manage-content"><div class="m-block"><h3>没有可用关卡集</h3>
        <p class="muted">夹心归属关卡集。请先在「关卡管理」创建关卡集，并打开过一次其自定义菜谱页（初始化配置）。
        若是桥接未启动，请在 Unity 中 Tools → Layout Editor → 启动服务。</p></div></div>`,M();return}const L=g!==""&&d.some(t=>t.setName===g);let i=L?g:d[0].setName;const H=L?h.fillingId??"":"",k=r(i,H||void 0);location.pathname!==k&&history.replaceState(null,"",k);function p(t){location.pathname!==t&&history.pushState(null,"",t)}function F(){const t=S();return t.page==="filling-maker"&&!!t.fillingId}let v=[];async function y(){n.innerHTML=`
      ${P("custom-recipes")}
      <div class="manage-bar">
        <h1 class="m-title">🥩 夹心工作台</h1>
        <span style="flex:1"></span>
        <a class="m-btn" href="/custom-recipes/burger-maker">🍔 汉堡组装工作台</a>
        <a class="m-btn" href="/custom-recipes">← 菜谱管理</a>
      </div>
      <div class="manage-content" id="fm-content"><p class="muted">加载中…</p></div>
    `,M();const t=document.getElementById("fm-content");let c=[];try{N("加载夹心…"),c=await j(i)}catch(e){t.innerHTML=`<div class="m-block"><h3>加载失败</h3><p class="muted">${l(e.message)}</p><p class="muted">请确认 Unity 编辑器已启动且桥接服务运行中。</p></div>`,I();return}finally{I()}const a=c.filter(e=>!E(e)&&B(e)),s=c.filter(e=>E(e)&&B(e));v=a;const o=(e,w)=>{let b;try{b=q(x(e),{iconSrc:()=>G({id:e.id,assetPath:e.assetPath,iconState:e.iconState,iconFallbackIds:e.ingredients??[],isCustom:!0})})}catch(T){b=`<div class="m-card"><h3>${l(e.nameZh)}</h3><p class="muted">卡片渲染失败：${l(T.message)}</p></div>`}const C=e.hasModel||!!e.hasModelSO||!!e.previewable?'<span class="bm-badge ok">有模型</span>':'<span class="bm-badge warn" title="没有模型的夹心在汉堡里那一层看不见，且不会出现在汉堡工作台候选中">⚠ 无模型</span>';return`
        <div class="cr-card-wrap">
          <div class="cr-card-inner">${b}</div>
          <div class="cr-card-foot">
            ${w?'<span class="cr-cat-tag">共享库（只读）</span>':`<span class="cr-cat-tag">${l(e.category)}</span>`}
            ${C}
            <span style="flex:1"></span>
            ${e.previewable?u("👁","small",{"data-fm-preview":l(e.assetPath),"data-fm-name":l(e.nameZh),title:"3D 模型在线预览"}):""}
            ${w?"":u("编辑","small",{"data-fm-edit":l(e.assetPath)})}
            ${w?"":u("删除","small",{"data-fm-del":l(e.assetPath)},"danger")}
          </div>
        </div>`};t.innerHTML=`
      <div class="m-actions-row">
        <span class="muted">关卡集</span>
        <select id="fm-set" class="rl-select">
          ${d.map(e=>`<option value="${l(e.setName)}" ${e.setName===i?"selected":""}>${l(e.levelSetNameZH||e.setName)}（${l(e.setName)}）</option>`).join("")}
        </select>
        <span style="flex:1"></span>
        ${u("＋ 新建夹心","primary",{id:"fm-new"})}
      </div>
      <p class="modal-hint">夹心 = <b>0 分的自定义菜谱</b>，不可单独点单，只作为汉堡的一层。
        新建后给它一个模型（可用「模板网格 + 自制贴图」零建模），就能在
        <b>🍔 汉堡组装工作台</b> 的候选里选用 —— 没有模型的夹心不会出现在候选中，因为游戏里那层看不见。</p>

      <section class="rl-section">
        <h2 class="rl-section-title">本关卡集的夹心<span class="rl-section-count">${a.length}</span></h2>
        ${a.length>0?`<div class="rl-grid">${a.map(e=>o(e,!1)).join("")}</div>`:'<p class="muted">还没有夹心。点右上角「＋ 新建夹心」创建第一个。</p>'}
      </section>

      <section class="rl-section">
        <h2 class="rl-section-title">🍔 Burger大全 共享夹心（只读参考）<span class="rl-section-count">${s.length}</span></h2>
        <p class="muted small">这些来自 commonW2 共享库，所有关卡集都能直接选用；要改请在本关卡集另建一个。</p>
        ${s.length>0?`<div class="rl-grid">${s.map(e=>o(e,!0)).join("")}</div>`:'<p class="muted">共享库中没有夹心条目。</p>'}
      </section>
    `,m(`本关卡集 ${a.length} 个夹心 · 共享库 ${s.length} 个`),R(),F()||p(r(i))}function f(t){O(n,i,t,{mode:"filling",score:0,category:U,onBack:()=>{p(r(i)),y()}})}async function $(){await y();const t=S();if(t.page==="filling-maker"&&t.setId===i&&t.fillingId){if(t.fillingId==="new"){f(null);return}const c=v.find(a=>a.id===t.fillingId);c?f(c.assetPath):m(`未找到夹心「${t.fillingId}」，请从列表重新进入。`,!1)}}function R(){var t,c;(t=document.getElementById("fm-set"))==null||t.addEventListener("change",a=>{i=a.target.value,history.replaceState(null,"",r(i)),$()}),(c=document.getElementById("fm-new"))==null||c.addEventListener("click",()=>{p(r(i,"new")),f(null)}),document.querySelectorAll("[data-fm-edit]").forEach(a=>a.addEventListener("click",()=>{const s=a.dataset.fmEdit,o=v.find(e=>e.assetPath===s);p(r(i,(o==null?void 0:o.id)??"new")),f(s)})),document.querySelectorAll("[data-fm-preview]").forEach(a=>a.addEventListener("click",()=>{D(a.dataset.fmPreview,a.dataset.fmName??"夹心模型",{fitTarget:"plate",onError:s=>m(s,!1)})})),document.querySelectorAll("[data-fm-del]").forEach(a=>a.addEventListener("click",()=>{const s=a.dataset.fmDel;confirm(`确定删除夹心「${s.split("/").pop()}」？
引用它的汉堡会丢失这一层。`)&&(async()=>{try{N("删除中…"),await Z(s),await y(),m("已删除。")}catch(o){m(o.message,!1)}finally{I()}})()}))}window.addEventListener("popstate",()=>void $()),await $()}export{Y as renderFillingMakerView};
