import{m as N,n as Z,w as _,h as A,j as U,k as P,l as I,o as M,p as F,q as G,t as O,v as V,x as b,S as k,y as z}from"./version-iccdJYUJ.js";N();const K=document.getElementById("app");document.body.classList.add("manage-bg");function c(e){return String(e??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function m(e,t=!0){const n=document.getElementById("rl-status");n&&(n.textContent=e,n.classList.toggle("err",!t),n.classList.toggle("ok",t&&e.length>0))}function W(e){const t=e instanceof Error?e.message:String(e);m(t,!1);const n=document.getElementById("rl-content");n&&(n.innerHTML=`<div class="rl-empty">加载失败：${c(t)}</div>`)}K.innerHTML=`
  ${Z("recipes")}
  <div class="manage-bar">
    <h1 class="m-title">📖 菜谱清单列表</h1>
    <span class="status" id="rl-status">加载中…</span>
    <span style="flex: 1"></span>
    <button type="button" class="m-btn" id="rl-export" title="把当前筛选出的菜谱合成一张 PNG 长图（重置筛选即导出全部）">🖼 导出图片</button>
    <label class="rl-tool-check" title="显示面糊、炸物部件、自选披萨部件等半成品">
      <input type="checkbox" id="rl-intermediates"> 含半成品
    </label>
    <select id="rl-group" class="rl-select" title="按来源筛选"></select>
  </div>
  <div class="rl-toolbar">
    <div class="rl-view-switch">
      <button type="button" class="m-btn rl-view-btn active" data-view="recipes">菜谱视图</button>
      <button type="button" class="m-btn rl-view-btn" data-view="ingredients">食材清单</button>
    </div>
    <input type="search" id="rl-search" class="rl-search" placeholder="搜索菜名 / 英文名 / ID / 食材…" autocomplete="off">
    <label class="rl-tool-check" title="同一道菜的多 DLC 换皮变体只保留最高 DLC 一版（如只显示「什锦火锅（DLC10）」）">
      <input type="checkbox" id="rl-web-reps" checked> 隐藏DLC重复
    </label>
    <select id="rl-score" class="rl-select" title="按分数过滤">
      <option value="all">全部分数</option>
      <option value="20">20 分</option>
      <option value="40">40 分</option>
      <option value="60">60 分</option>
      <option value="80">80 分</option>
      <option value="100">100 分</option>
      <option value="120">120 分</option>
      <option value="other">其他</option>
    </select>
    <div class="rl-chips" id="rl-types"></div>
  </div>
  <div class="manage-content rl-content" id="rl-content">
    <div class="rl-empty">加载中…</div>
  </div>
`;_(e=>{e==="layout"?location.href="/index.html#/layout":e==="manage"?location.href="/index.html#/manage":e==="dependencies"?location.href="/index.html#/dependencies":e==="custom-recipes"?location.href="/index.html#/custom-recipes":e==="guide"?location.href="/index.html#/guide":e==="changelog"&&(location.href="/index.html#/changelog")});let p=[],L=[];const B=new Map;let C="",y="all",f="all",T="all",H=!1,v="recipes",E=!0;const J=[20,40,60,80,100,120];function Q(e,t){if(t==="all")return!0;const n=e??0;return t==="other"?!J.includes(n):n===t}function X(e){return String(e??"").replace(/·?DLC\d+/g,"").replace(/[（）()· ]/g,"")}function R(e){const t=/^dlc(\d+)_/.exec(e??"");return t?parseInt(t[1],10):0}function $(e){const t=new Map;for(const n of e){const s=X(n.nameZh??""),i=t.get(s);(!i||R(n.id)>R(i.id))&&t.set(s,n)}return[...t.values()]}function Y(e){return G(e,{allRecipes:p,ingredientName:t=>{var n;return((n=B.get(t))==null?void 0:n.nameZh)??t},extraBadge:e.group==="levelset"?"本关":void 0})}function D(){const e=C.trim().toLowerCase();let t=p.filter(n=>!(!H&&n.intermediate||y!=="all"&&(n.type??"other")!==y||f!=="all"&&(n.group??"core")!==f||!Q(n.score,T)||e&&![n.nameZh,n.nameEn??"",n.id,...(n.ingredients??[]).map(i=>{var l;return((l=B.get(i))==null?void 0:l.nameZh)??i})].join(" ").toLowerCase().includes(e)));return E&&(t=$(t)),t}function ee(e){const t=e.group&&e.group!=="core"?` <span class="pc-badge">${c(b(e.group))}</span>`:"",n=e.nameEn&&e.nameEn.trim()?` <span class="muted pc-en">${c(e.nameEn)}</span>`:"";return`<div class="rl-ing-card" title="${c(e.id)}">
    <img class="food-icon" loading="lazy" src="/icons/ingredients/${encodeURIComponent(e.id)}.png" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">
    <span class="rl-ing-name">${c(e.nameZh)}${t}${n}</span>
    <span class="muted small">${c(e.id)}</span>
  </div>`}function te(){const e=C.trim().toLowerCase(),t=L.filter(l=>!(e&&!`${l.nameZh} ${l.nameEn??""} ${l.id}`.toLowerCase().includes(e)||f!=="all"&&(l.group??"core")!==f)),n=E?$(t):t;if(n.length===0)return'<div class="rl-empty">没有匹配的食材，试试调整搜索或筛选条件</div>';const s=new Map;for(const l of n){const a=l.group??"core";s.has(a)||s.set(a,[]),s.get(a).push(l)}return[...s.keys()].sort((l,a)=>{const o=r=>r==="core"?0:r==="levelset"?1:2;return o(l)-o(a)||l.localeCompare(a)}).map(l=>{const a=s.get(l);return`<section class="rl-section">
        <h2 class="rl-section-title">${c(b(l))}<span class="rl-section-count">${a.length}</span></h2>
        <div class="rl-ing-grid">${a.map(ee).join("")}</div>
      </section>`}).join("")}function u(){const e=document.getElementById("rl-content"),t=document.getElementById("rl-types");t&&(t.style.display=v==="recipes"?"":"none");const n=document.getElementById("rl-score");if(n&&(n.style.display=v==="recipes"?"":"none"),v==="ingredients"){e.innerHTML=te();return}const s=D();if(s.length===0){e.innerHTML='<div class="rl-empty">没有匹配的菜谱，试试调整搜索或筛选条件</div>';return}e.innerHTML=I(s).map(([i,l])=>F(i,l.map(Y).join(""),l.length)).join("")}function j(){const e=E?$(p.filter(o=>!o.intermediate)):p.filter(o=>!o.intermediate),t=I(e),n=document.getElementById("rl-types"),s=[{type:"all",label:"全部",count:e.length},...t.map(([o,r])=>({type:o,label:M(o),count:r.length}))];n.innerHTML=s.map(o=>`<button type="button" class="rl-chip-btn${o.type===y?" active":""}" data-type="${c(o.type)}">${c(o.label)}<span class="rl-cnt">${o.count}</span></button>`).join("");const i=document.getElementById("rl-group"),l=new Map;for(const o of e){const r=o.group??"core";l.set(r,(l.get(r)??0)+1)}const a=['<option value="all">全部来源</option>'];for(const[o,r]of l)a.push(`<option value="${c(o)}" ${o===f?"selected":""}>${c(b(o))} (${r})</option>`);i.innerHTML=a.join("")}function ne(){document.getElementById("rl-search").addEventListener("input",e=>{C=e.target.value,u()}),document.getElementById("rl-types").addEventListener("click",e=>{const t=e.target.closest(".rl-chip-btn");t&&(y=t.dataset.type??"all",document.querySelectorAll(".rl-chip-btn").forEach(n=>n.classList.toggle("active",n===t)),u())}),document.getElementById("rl-group").addEventListener("change",e=>{f=e.target.value,u()}),document.getElementById("rl-score").addEventListener("change",e=>{const t=e.target.value;T=t==="all"?"all":t==="other"?"other":Number(t),u()}),document.getElementById("rl-intermediates").addEventListener("change",e=>{H=e.target.checked,u()}),document.getElementById("rl-web-reps").addEventListener("change",e=>{E=e.target.checked,j(),u()}),document.querySelectorAll(".rl-view-btn").forEach(e=>{e.addEventListener("click",()=>{v=e.dataset.view??"recipes",document.querySelectorAll(".rl-view-btn").forEach(t=>t.classList.toggle("active",t===e)),u()})}),document.getElementById("rl-export").addEventListener("click",()=>void se())}async function se(){var n;const e=document.getElementById("rl-export"),t=D();if(t.length===0){m("没有可导出的菜谱",!1);return}e&&(e.disabled=!0),m("正在生成图片…");try{const s=I(t).map(([a,o])=>({typeLabel:M(a),count:o.length,cards:o.map(r=>{const q=O(r,{allRecipes:p}),S=V(r),g=[];return S&&g.push("半成品"),r.isCustom&&g.push("自定义"),r.group==="levelset"&&g.push("本关"),r.group&&r.group!=="core"&&r.group!=="levelset"&&g.push(b(r.group)),S||g.push(`⭐ ${r.score??0}`),{iconUrl:`/icons/recipes/${encodeURIComponent(r.id)}.png`,nameZh:r.nameZh,nameEn:r.nameEn||r.id,badges:g,groups:q.map(h=>({stepIcons:[h.step,...(h.extraSteps??[]).map(d=>d.step)].filter(Boolean).map(d=>k[d]).filter(d=>!!d),ingredientUrls:(h.ingredients??[]).map(d=>`/icons/ingredients/${encodeURIComponent(d)}.png`),ingredientStepIcons:(h.ingredients??[]).map(d=>{var x;return(((x=h.ingredientSteps)==null?void 0:x[d])??[]).map(w=>k[w]).filter(w=>!!w)})}))}})})),i=((n=document.getElementById("rl-content"))==null?void 0:n.getBoundingClientRect().width)||1200,l=new Date().toISOString().slice(0,10);await z({title:"菜谱清单列表",sub:`共 ${t.length} 个菜谱 · 导出于 ${l}`,sections:s},i,`菜谱清单_${t.length}个_${l}.png`),m(`已导出 PNG（${t.length} 个菜谱）`)}catch(s){m(s instanceof Error?s.message:String(s),!1)}finally{e&&(e.disabled=!1)}}async function le(){try{const[s,i]=await Promise.all([A(""),U()]);p=s,L=i}catch(s){W(s);return}for(const s of L)B.set(s.id,s);const e=await P().catch(()=>!1),t=p.filter(s=>!s.intermediate).length,n=$(p.filter(s=>!s.intermediate)).length;m(`共 ${p.length} 个菜谱（成品 ${t} · Web去重后 ${n}）${e?"":" · 静态数据"}`),j(),u(),ne()}le();
