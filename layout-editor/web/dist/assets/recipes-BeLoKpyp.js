import{m as T,n as q,w as x,j as A,k as D,l as N,o as B,p as P,q as L,t as I,v as Z,x as F,y as O,z as U,A as E}from"./version-DbhnSXba.js";T();const _=document.getElementById("app");document.body.classList.add("manage-bg");function c(e){return String(e??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function p(e,t=!0){const n=document.getElementById("rl-status");n&&(n.textContent=e,n.classList.toggle("err",!t),n.classList.toggle("ok",t&&e.length>0))}function z(e){const t=e instanceof Error?e.message:String(e);p(t,!1);const n=document.getElementById("rl-content");n&&(n.innerHTML=`<div class="rl-empty">加载失败：${c(t)}</div>`)}_.innerHTML=`
  ${q("recipes")}
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
`;x();let u=[],y=[];const $=new Map;let w="",f="all",g="all",C="all",k=!1,m="recipes",h=!0;const G=[20,40,60,80,100,120];function K(e,t){if(t==="all")return!0;const n=e??0;return t==="other"?!G.includes(n):n===t}function V(e){return String(e??"").replace(/·?DLC\d+/gi,"").replace(/[（）()· ]/g,"")}function W(e){return[V(e.nameZh??""),e.isCustom?"custom":"official",e.score??0,e.cookingStep??""].join("|")}function S(e){const t=/^dlc(\d+)_/i.exec(e??"");return t?parseInt(t[1],10):0}function J(e,t){const n=S(e.id),r=S(t.id);if(n!==r)return n>r;const o=e.group==="burger"?1:0,s=t.group==="burger"?1:0;return o!==s?o>s:(e.id??"")<(t.id??"")}function v(e){const t=new Map;for(const n of e){const r=W(n),o=t.get(r);(!o||J(n,o))&&t.set(r,n)}return[...t.values()]}function Q(e){return e.isCustom&&e.assetPath?`/api/custom-recipes/icon?assetPath=${encodeURIComponent(e.assetPath)}`:`/icons/recipes/${encodeURIComponent(e.id)}.png`}function b(e){return F(e,{allRecipes:u,ingredientName:t=>{var n;return((n=$.get(t))==null?void 0:n.nameZh)??t},extraBadge:e.group==="levelset"?"本关":e.group==="burger"?"🍔":void 0,iconSrc:Q})}function M(){const e=w.trim().toLowerCase();let t=u.filter(n=>!(!k&&n.intermediate||f!=="all"&&(n.type??"other")!==f||g!=="all"&&(n.group??"core")!==g||!K(n.score,C)||e&&![n.nameZh,n.nameEn??"",n.id,...(n.ingredients??[]).map(o=>{var s;return((s=$.get(o))==null?void 0:s.nameZh)??o})].join(" ").toLowerCase().includes(e)));return h&&(t=v(t)),t}function X(e){const t=e.group&&e.group!=="core"?` <span class="pc-badge">${c(E(e.group))}</span>`:"",n=e.nameEn&&e.nameEn.trim()?` <span class="muted pc-en">${c(e.nameEn)}</span>`:"";return`<div class="rl-ing-card" title="${c(e.id)}">
    <img class="food-icon" loading="lazy" src="/icons/ingredients/${encodeURIComponent(e.id)}.png" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">
    <span class="rl-ing-name">${c(e.nameZh)}${t}${n}</span>
    <span class="muted small">${c(e.id)}</span>
  </div>`}function Y(){const e=w.trim().toLowerCase(),t=y.filter(s=>!(e&&!`${s.nameZh} ${s.nameEn??""} ${s.id}`.toLowerCase().includes(e)||g!=="all"&&(s.group??"core")!==g)),n=h?v(t):t;if(n.length===0)return'<div class="rl-empty">没有匹配的食材，试试调整搜索或筛选条件</div>';const r=new Map;for(const s of n){const i=s.group??"core";r.has(i)||r.set(i,[]),r.get(i).push(s)}return[...r.keys()].sort((s,i)=>{const l=a=>a==="core"?0:a==="levelset"?1:2;return l(s)-l(i)||s.localeCompare(i)}).map(s=>{const i=r.get(s);return`<section class="rl-section">
        <h2 class="rl-section-title">${c(E(s))}<span class="rl-section-count">${i.length}</span></h2>
        <div class="rl-ing-grid">${i.map(X).join("")}</div>
      </section>`}).join("")}function d(){const e=document.getElementById("rl-content"),t=document.getElementById("rl-types");t&&(t.style.display=m==="recipes"?"":"none");const n=document.getElementById("rl-score");if(n&&(n.style.display=m==="recipes"?"":"none"),m==="ingredients"){e.innerHTML=Y();return}const r=M();if(r.length===0){e.innerHTML='<div class="rl-empty">没有匹配的菜谱，试试调整搜索或筛选条件</div>';return}e.innerHTML=B(r).map(([o,s])=>o==="burger"?L(o,ee(s),s.length):L(o,s.map(b).join(""),s.length)).join("")}function ee(e){const t=new Map;for(const n of e){const r=n.subtype??"";t.has(r)||t.set(r,[]),t.get(r).push(n)}return t.size<=1&&t.has("")?e.map(b).join(""):[...t.entries()].sort((n,r)=>I(n[0])-I(r[0])).map(([n,r])=>`<div class="rl-subsection">
          <h3 class="rl-subsection-title">${c(n?Z(n):"其他汉堡")}<span class="rl-section-count">${r.length}</span></h3>
          <div class="rl-grid">${r.map(b).join("")}</div>
        </div>`).join("")}function H(){const e=h?v(u.filter(l=>!l.intermediate)):u.filter(l=>!l.intermediate),t=B(e),n=document.getElementById("rl-types"),r=[{type:"all",label:"全部",count:e.length},...t.map(([l,a])=>({type:l,label:P(l),count:a.length}))];n.innerHTML=r.map(l=>`<button type="button" class="rl-chip-btn${l.type===f?" active":""}" data-type="${c(l.type)}">${c(l.label)}<span class="rl-cnt">${l.count}</span></button>`).join("");const o=document.getElementById("rl-group"),s=new Map;for(const l of e){const a=l.group??"core";s.set(a,(s.get(a)??0)+1)}const i=['<option value="all">全部来源</option>'];for(const[l,a]of s)i.push(`<option value="${c(l)}" ${l===g?"selected":""}>${c(E(l))} (${a})</option>`);o.innerHTML=i.join("")}function te(){document.getElementById("rl-search").addEventListener("input",e=>{w=e.target.value,d()}),document.getElementById("rl-types").addEventListener("click",e=>{const t=e.target.closest(".rl-chip-btn");t&&(f=t.dataset.type??"all",document.querySelectorAll(".rl-chip-btn").forEach(n=>n.classList.toggle("active",n===t)),d())}),document.getElementById("rl-group").addEventListener("change",e=>{g=e.target.value,d()}),document.getElementById("rl-score").addEventListener("change",e=>{const t=e.target.value;C=t==="all"?"all":t==="other"?"other":Number(t),d()}),document.getElementById("rl-intermediates").addEventListener("change",e=>{k=e.target.checked,d()}),document.getElementById("rl-web-reps").addEventListener("change",e=>{h=e.target.checked,H(),d()}),document.querySelectorAll(".rl-view-btn").forEach(e=>{e.addEventListener("click",()=>{m=e.dataset.view??"recipes",document.querySelectorAll(".rl-view-btn").forEach(t=>t.classList.toggle("active",t===e)),d()})}),document.getElementById("rl-export").addEventListener("click",()=>void ne())}async function ne(){const e=document.getElementById("rl-export"),t=document.getElementById("rl-content");if(!t){p("页面内容尚未就绪",!1);return}const n=m==="ingredients",r=n?t.querySelectorAll(".rl-ing-card").length:M().length;if(r===0){p(n?"没有可导出的食材":"没有可导出的菜谱",!1);return}e&&(e.disabled=!0),p("正在生成图片…");let o=null;try{const s=new Date().toISOString().slice(0,10),i=n?"食材清单":"菜谱清单列表",l=n?"个食材":"个菜谱",a=t.getBoundingClientRect().width||1200;o=O(a+48,"sum-page"),o.innerHTML=`
      <header class="sum-head">
        <h1 class="sum-title">${c(i)}</h1>
        <div class="sum-sub">共 ${r} ${c(l)} · 导出于 ${c(s)}</div>
      </header>
      <div data-export-host></div>
    `;const j=o.querySelector("[data-export-host]");for(const R of Array.from(t.children))j.appendChild(R.cloneNode(!0));await re(o),await U(o,`${i}_${r}个_${s}.png`),p(`已导出 PNG（${r} ${l}）`)}catch(s){p(s instanceof Error?s.message:String(s),!1)}finally{o&&o.remove(),e&&(e.disabled=!1)}}async function re(e){const t=Array.from(e.querySelectorAll("img"));await Promise.all(t.map(n=>new Promise(r=>{if(n.complete)return r();n.addEventListener("load",()=>r(),{once:!0}),n.addEventListener("error",()=>r(),{once:!0})})))}async function se(){try{const[r,o]=await Promise.all([A(""),D()]);u=r,y=o}catch(r){z(r);return}for(const r of y)$.set(r.id,r);const e=await N().catch(()=>!1),t=u.filter(r=>!r.intermediate).length,n=v(u.filter(r=>!r.intermediate)).length;p(`共 ${u.length} 个菜谱（成品 ${t} · Web去重后 ${n}）${e?"":" · 静态数据"}`),H(),d(),te()}se();
