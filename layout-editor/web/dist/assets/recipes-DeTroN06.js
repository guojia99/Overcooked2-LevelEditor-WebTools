import{a9 as k,n as M,B as H,p as S,J as T,l as x,u as w,w as D,O as R,P as j,x as y}from"./version-mvxhqx11.js";k();const q=document.getElementById("app");document.body.classList.add("manage-bg");function a(e){return String(e??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function $(e,t=!0){const n=document.getElementById("rl-status");n&&(n.textContent=e,n.classList.toggle("err",!t),n.classList.toggle("ok",t&&e.length>0))}function Z(e){const t=e instanceof Error?e.message:String(e);$(t,!1);const n=document.getElementById("rl-content");n&&(n.innerHTML=`<div class="rl-empty">加载失败：${a(t)}</div>`)}q.innerHTML=`
  ${M("recipes")}
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
`;H(e=>{e==="layout"?location.href="/index.html#/layout":e==="manage"?location.href="/index.html#/manage":e==="custom-recipes"&&(location.href="/index.html#/custom-recipes")});let p=[],v=[];const b=new Map;let E="",m="all",u="all",I="all",B=!1,g="recipes",f=!0;const A=[20,40,60,80,100,120];function N(e,t){if(t==="all")return!0;const n=e??0;return t==="other"?!A.includes(n):n===t}function F(e){return String(e??"").replace(/·?DLC\d+/g,"").replace(/[（）()· ]/g,"")}function L(e){const t=/^dlc(\d+)_/.exec(e??"");return t?parseInt(t[1],10):0}function h(e){const t=new Map;for(const n of e){const l=F(n.nameZh??""),o=t.get(l);(!o||L(n.id)>L(o.id))&&t.set(l,n)}return[...t.values()]}function _(e){return j(e,{allRecipes:p,ingredientName:t=>{var n;return((n=b.get(t))==null?void 0:n.nameZh)??t},extraBadge:e.group==="levelset"?"本关":void 0})}function O(){const e=E.trim().toLowerCase();let t=p.filter(n=>!(!B&&n.intermediate||m!=="all"&&(n.type??"other")!==m||u!=="all"&&(n.group??"core")!==u||!N(n.score,I)||e&&![n.nameZh,n.nameEn??"",n.id,...(n.ingredients??[]).map(o=>{var s;return((s=b.get(o))==null?void 0:s.nameZh)??o})].join(" ").toLowerCase().includes(e)));return f&&(t=h(t)),t}function P(e){const t=e.group&&e.group!=="core"?` <span class="pc-badge">${a(y(e.group))}</span>`:"",n=e.nameEn&&e.nameEn.trim()?` <span class="muted pc-en">${a(e.nameEn)}</span>`:"";return`<div class="rl-ing-card" title="${a(e.id)}">
    <img class="food-icon" loading="lazy" src="/icons/ingredients/${encodeURIComponent(e.id)}.png" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">
    <span class="rl-ing-name">${a(e.nameZh)}${t}${n}</span>
    <span class="muted small">${a(e.id)}</span>
  </div>`}function U(){const e=E.trim().toLowerCase(),t=v.filter(s=>!(e&&!`${s.nameZh} ${s.nameEn??""} ${s.id}`.toLowerCase().includes(e)||u!=="all"&&(s.group??"core")!==u)),n=f?h(t):t;if(n.length===0)return'<div class="rl-empty">没有匹配的食材，试试调整搜索或筛选条件</div>';const l=new Map;for(const s of n){const i=s.group??"core";l.has(i)||l.set(i,[]),l.get(i).push(s)}return[...l.keys()].sort((s,i)=>{const r=c=>c==="core"?0:c==="levelset"?1:2;return r(s)-r(i)||s.localeCompare(i)}).map(s=>{const i=l.get(s);return`<section class="rl-section">
        <h2 class="rl-section-title">${a(y(s))}<span class="rl-section-count">${i.length}</span></h2>
        <div class="rl-ing-grid">${i.map(P).join("")}</div>
      </section>`}).join("")}function d(){const e=document.getElementById("rl-content"),t=document.getElementById("rl-types");t&&(t.style.display=g==="recipes"?"":"none");const n=document.getElementById("rl-score");if(n&&(n.style.display=g==="recipes"?"":"none"),g==="ingredients"){e.innerHTML=U();return}const l=O();if(l.length===0){e.innerHTML='<div class="rl-empty">没有匹配的菜谱，试试调整搜索或筛选条件</div>';return}e.innerHTML=w(l).map(([o,s])=>R(o,s.map(_).join(""),s.length)).join("")}function C(){const e=f?h(p.filter(r=>!r.intermediate)):p.filter(r=>!r.intermediate),t=w(e),n=document.getElementById("rl-types"),l=[{type:"all",label:"全部",count:e.length},...t.map(([r,c])=>({type:r,label:D(r),count:c.length}))];n.innerHTML=l.map(r=>`<button type="button" class="rl-chip-btn${r.type===m?" active":""}" data-type="${a(r.type)}">${a(r.label)}<span class="rl-cnt">${r.count}</span></button>`).join("");const o=document.getElementById("rl-group"),s=new Map;for(const r of e){const c=r.group??"core";s.set(c,(s.get(c)??0)+1)}const i=['<option value="all">全部来源</option>'];for(const[r,c]of s)i.push(`<option value="${a(r)}" ${r===u?"selected":""}>${a(y(r))} (${c})</option>`);o.innerHTML=i.join("")}function V(){document.getElementById("rl-search").addEventListener("input",e=>{E=e.target.value,d()}),document.getElementById("rl-types").addEventListener("click",e=>{const t=e.target.closest(".rl-chip-btn");t&&(m=t.dataset.type??"all",document.querySelectorAll(".rl-chip-btn").forEach(n=>n.classList.toggle("active",n===t)),d())}),document.getElementById("rl-group").addEventListener("change",e=>{u=e.target.value,d()}),document.getElementById("rl-score").addEventListener("change",e=>{const t=e.target.value;I=t==="all"?"all":t==="other"?"other":Number(t),d()}),document.getElementById("rl-intermediates").addEventListener("change",e=>{B=e.target.checked,d()}),document.getElementById("rl-web-reps").addEventListener("change",e=>{f=e.target.checked,C(),d()}),document.querySelectorAll(".rl-view-btn").forEach(e=>{e.addEventListener("click",()=>{g=e.dataset.view??"recipes",document.querySelectorAll(".rl-view-btn").forEach(t=>t.classList.toggle("active",t===e)),d()})})}async function z(){try{const[l,o]=await Promise.all([S(""),T()]);p=l,v=o}catch(l){Z(l);return}for(const l of v)b.set(l.id,l);const e=await x().catch(()=>!1),t=p.filter(l=>!l.intermediate).length,n=h(p.filter(l=>!l.intermediate)).length;$(`共 ${p.length} 个菜谱（成品 ${t} · Web去重后 ${n}）${e?"":" · 静态数据"}`),C(),d(),V()}z();
