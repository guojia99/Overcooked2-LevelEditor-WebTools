import{a8 as M,n as S,B as k,q as H,J as T,l as x,w as L,x as D,M as q,N as j,y}from"./version-u_f4rper.js";M();const R=document.getElementById("app");document.body.classList.add("manage-bg");function a(e){return String(e??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function $(e,t=!0){const n=document.getElementById("rl-status");n&&(n.textContent=e,n.classList.toggle("err",!t),n.classList.toggle("ok",t&&e.length>0))}function N(e){const t=e instanceof Error?e.message:String(e);$(t,!1);const n=document.getElementById("rl-content");n&&(n.innerHTML=`<div class="rl-empty">加载失败：${a(t)}</div>`)}R.innerHTML=`
  ${S("recipes")}
  <div class="manage-bar">
    <h1 class="m-title">📖 菜谱清单列表</h1>
    <span class="status" id="rl-status">加载中…</span>
    <span style="flex: 1"></span>
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
`;k(e=>{e==="layout"?location.href="/index.html#/layout":e==="manage"?location.href="/index.html#/manage":e==="custom-recipes"&&(location.href="/index.html#/custom-recipes")});let d=[],v=[];const b=new Map;let w="",f="all",p="all",I="all",B=!1,m="recipes",g=!0;const Z=[20,40,60,80,100,120];function A(e,t){if(t==="all")return!0;const n=e??0;return t==="other"?!Z.includes(n):n===t}function _(e){return String(e??"").replace(/·?DLC\d+/g,"").replace(/[（）()· ]/g,"")}function E(e){const t=/^dlc(\d+)_/.exec(e??"");return t?parseInt(t[1],10):0}function h(e){const t=new Map,n=[];for(const l of e){if(l.group!=="web"){n.push(l);continue}const s=_(l.nameZh??""),r=t.get(s);(!r||E(l.id)>E(r.id))&&t.set(s,l)}return[...n,...t.values()]}function F(e){return j(e,{allRecipes:d,ingredientName:t=>{var n;return((n=b.get(t))==null?void 0:n.nameZh)??t},extraBadge:e.group==="levelset"?"本关":void 0})}function P(){const e=w.trim().toLowerCase(),t=new Set(d.filter(s=>s.group==="levelset").map(s=>s.id)),n=new Set(d.filter(s=>s.group==="web"&&(s.assetPath??"").includes("/custom_web/")).map(s=>s.id));let l=d.filter(s=>!(s.group==="web"&&t.has(s.id)||s.group==="web"&&n.has(s.id)&&!(s.assetPath??"").includes("/custom_web/")||!B&&s.intermediate||f!=="all"&&(s.type??"other")!==f||p!=="all"&&(s.group??"core")!==p||!A(s.score,I)||e&&![s.nameZh,s.nameEn??"",s.id,...(s.ingredients??[]).map(i=>{var o;return((o=b.get(i))==null?void 0:o.nameZh)??i})].join(" ").toLowerCase().includes(e)));return g&&(l=h(l)),l}function W(e){const t=e.group&&e.group!=="core"?` <span class="pc-badge">${a(y(e.group))}</span>`:"",n=e.nameEn&&e.nameEn.trim()?` <span class="muted pc-en">${a(e.nameEn)}</span>`:"";return`<div class="rl-ing-card" title="${a(e.id)}">
    <img class="food-icon" loading="lazy" src="/icons/ingredients/${encodeURIComponent(e.id)}.png" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">
    <span class="rl-ing-name">${a(e.nameZh)}${t}${n}</span>
    <span class="muted small">${a(e.id)}</span>
  </div>`}function U(){const e=w.trim().toLowerCase(),t=v.filter(r=>g&&r.group==="web"?!0:!(e&&!`${r.nameZh} ${r.nameEn??""} ${r.id}`.toLowerCase().includes(e)||p!=="all"&&(r.group??"core")!==p)),n=g?h(t):t;if(n.length===0)return'<div class="rl-empty">没有匹配的食材，试试调整搜索或筛选条件</div>';const l=new Map;for(const r of n){const i=r.group??"core";l.has(i)||l.set(i,[]),l.get(i).push(r)}return[...l.keys()].sort((r,i)=>{const o=c=>c==="core"?0:c==="levelset"?1:c==="web"?2:3;return o(r)-o(i)||r.localeCompare(i)}).map(r=>{const i=l.get(r);return`<section class="rl-section">
        <h2 class="rl-section-title">${a(y(r))}<span class="rl-section-count">${i.length}</span></h2>
        <div class="rl-ing-grid">${i.map(W).join("")}</div>
      </section>`}).join("")}function u(){const e=document.getElementById("rl-content"),t=document.getElementById("rl-types");t&&(t.style.display=m==="recipes"?"":"none");const n=document.getElementById("rl-score");if(n&&(n.style.display=m==="recipes"?"":"none"),m==="ingredients"){e.innerHTML=U();return}const l=P();if(l.length===0){e.innerHTML='<div class="rl-empty">没有匹配的菜谱，试试调整搜索或筛选条件</div>';return}e.innerHTML=L(l).map(([s,r])=>q(s,r.map(F).join(""),r.length)).join("")}function C(){const e=g?h(d.filter(o=>!o.intermediate)):d.filter(o=>!o.intermediate),t=L(e),n=document.getElementById("rl-types"),l=[{type:"all",label:"全部",count:e.length},...t.map(([o,c])=>({type:o,label:D(o),count:c.length}))];n.innerHTML=l.map(o=>`<button type="button" class="rl-chip-btn${o.type===f?" active":""}" data-type="${a(o.type)}">${a(o.label)}<span class="rl-cnt">${o.count}</span></button>`).join("");const s=document.getElementById("rl-group"),r=new Map;for(const o of e){const c=o.group??"core";r.set(c,(r.get(c)??0)+1)}const i=['<option value="all">全部来源</option>'];for(const[o,c]of r)i.push(`<option value="${a(o)}" ${o===p?"selected":""}>${a(y(o))} (${c})</option>`);s.innerHTML=i.join("")}function V(){document.getElementById("rl-search").addEventListener("input",e=>{w=e.target.value,u()}),document.getElementById("rl-types").addEventListener("click",e=>{const t=e.target.closest(".rl-chip-btn");t&&(f=t.dataset.type??"all",document.querySelectorAll(".rl-chip-btn").forEach(n=>n.classList.toggle("active",n===t)),u())}),document.getElementById("rl-group").addEventListener("change",e=>{p=e.target.value,u()}),document.getElementById("rl-score").addEventListener("change",e=>{const t=e.target.value;I=t==="all"?"all":t==="other"?"other":Number(t),u()}),document.getElementById("rl-intermediates").addEventListener("change",e=>{B=e.target.checked,u()}),document.getElementById("rl-web-reps").addEventListener("change",e=>{g=e.target.checked,C(),u()}),document.querySelectorAll(".rl-view-btn").forEach(e=>{e.addEventListener("click",()=>{m=e.dataset.view??"recipes",document.querySelectorAll(".rl-view-btn").forEach(t=>t.classList.toggle("active",t===e)),u()})})}async function z(){try{const[l,s]=await Promise.all([H(""),T()]);d=l,v=s}catch(l){N(l);return}for(const l of v)b.set(l.id,l);const e=await x().catch(()=>!1),t=d.filter(l=>!l.intermediate).length,n=h(d.filter(l=>!l.intermediate)).length;$(`共 ${d.length} 个菜谱（成品 ${t} · Web去重后 ${n}）${e?"":" · 静态数据"}`),C(),u(),V()}z();
