import{ah as k,n as H,J as M,H as R,q as S,T,m as D,y as $,z as x,X as q,Y as j,A,B as b,f as Z}from"./version-DlkaLSA5.js";k();const N=document.getElementById("app");document.body.classList.add("manage-bg");function i(e){return String(e??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function L(e,n=!0){const s=document.getElementById("rl-status");s&&(s.textContent=e,s.classList.toggle("err",!n),s.classList.toggle("ok",n&&e.length>0))}function F(e){const n=e instanceof Error?e.message:String(e);L(n,!1);const s=document.getElementById("rl-content");s&&(s.innerHTML=`<div class="rl-empty">加载失败：${i(n)}</div>`)}N.innerHTML=`
  ${H("recipes")}
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
`;M(e=>{e==="layout"?location.href="/index.html#/layout":e==="manage"?location.href="/index.html#/manage":e==="custom-recipes"&&(location.href="/index.html#/custom-recipes")});let d=[],v=[];const y=new Map;let w="",f="all",u="all",I="all",B=!1,m="recipes",g=!0;const W=[20,40,60,80,100,120];function _(e,n){if(n==="all")return!0;const s=e??0;return n==="other"?!W.includes(s):s===n}function z(e){return String(e??"").replace(/·?DLC\d+/g,"").replace(/[（）()· ]/g,"")}function E(e){const n=/^dlc(\d+)_/.exec(e??"");return n?parseInt(n[1],10):0}function h(e){const n=new Map,s=[];for(const t of e){if(t.group!=="web"){s.push(t);continue}const a=z(t.nameZh??""),l=n.get(a);(!l||E(t.id)>E(l.id))&&n.set(a,t)}return[...s,...n.values()]}function U(e){return j(e,{allRecipes:d,ingredientName:n=>{var s;return((s=y.get(n))==null?void 0:s.nameZh)??n},extraBadge:e.group==="levelset"?"本关":void 0,disabledReason:A(e)??void 0})}function V(){const e=w.trim().toLowerCase(),n=new Set(d.filter(t=>t.group==="levelset").map(t=>t.id));let s=d.filter(t=>!(t.group==="web"&&n.has(t.id)||!B&&t.intermediate||f!=="all"&&(t.type??"other")!==f||u!=="all"&&(t.group??"core")!==u||!_(t.score,I)||e&&![t.nameZh,t.nameEn??"",t.id,...(t.ingredients??[]).map(l=>{var o;return((o=y.get(l))==null?void 0:o.nameZh)??l})].join(" ").toLowerCase().includes(e)));return g&&(s=h(s)),s}function G(e){const n=e.group&&e.group!=="core"?` <span class="pc-badge">${i(b(e.group))}</span>`:"",s=e.nameEn&&e.nameEn.trim()?` <span class="muted pc-en">${i(e.nameEn)}</span>`:"",t=Z(e),a=t?' <span class="rl-badge rl-badge-disabled">⛔ 禁用</span>':"";return`<div class="rl-ing-card${t?" rl-ing-disabled":""}" title="${i(t?`${e.id}（${t}）`:e.id)}">
    <img class="food-icon" loading="lazy" src="/icons/ingredients/${encodeURIComponent(e.id)}.png" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">
    <span class="rl-ing-name">${i(e.nameZh)}${n}${a}${s}</span>
    <span class="muted small">${i(e.id)}</span>
  </div>`}function J(){const e=w.trim().toLowerCase(),n=v.filter(l=>g&&l.group==="web"?!0:!(e&&!`${l.nameZh} ${l.nameEn??""} ${l.id}`.toLowerCase().includes(e)||u!=="all"&&(l.group??"core")!==u)),s=g?h(n):n;if(s.length===0)return'<div class="rl-empty">没有匹配的食材，试试调整搜索或筛选条件</div>';const t=new Map;for(const l of s){const o=l.group??"core";t.has(o)||t.set(o,[]),t.get(o).push(l)}return[...t.keys()].sort((l,o)=>{const r=c=>c==="core"?0:c==="levelset"?1:c==="web"?2:3;return r(l)-r(o)||l.localeCompare(o)}).map(l=>{const o=t.get(l);return`<section class="rl-section">
        <h2 class="rl-section-title">${i(b(l))}<span class="rl-section-count">${o.length}</span></h2>
        <div class="rl-ing-grid">${o.map(G).join("")}</div>
      </section>`}).join("")}function p(){const e=document.getElementById("rl-content"),n=document.getElementById("rl-types");n&&(n.style.display=m==="recipes"?"":"none");const s=document.getElementById("rl-score");if(s&&(s.style.display=m==="recipes"?"":"none"),m==="ingredients"){e.innerHTML=J();return}const t=V();if(t.length===0){e.innerHTML='<div class="rl-empty">没有匹配的菜谱，试试调整搜索或筛选条件</div>';return}e.innerHTML=$(t).map(([a,l])=>q(a,l.map(U).join(""),l.length)).join("")}function C(){const e=g?h(d.filter(r=>!r.intermediate)):d.filter(r=>!r.intermediate),n=$(e),s=document.getElementById("rl-types"),t=[{type:"all",label:"全部",count:e.length},...n.map(([r,c])=>({type:r,label:x(r),count:c.length}))];s.innerHTML=t.map(r=>`<button type="button" class="rl-chip-btn${r.type===f?" active":""}" data-type="${i(r.type)}">${i(r.label)}<span class="rl-cnt">${r.count}</span></button>`).join("");const a=document.getElementById("rl-group"),l=new Map;for(const r of e){const c=r.group??"core";l.set(c,(l.get(c)??0)+1)}const o=['<option value="all">全部来源</option>'];for(const[r,c]of l)o.push(`<option value="${i(r)}" ${r===u?"selected":""}>${i(b(r))} (${c})</option>`);a.innerHTML=o.join("")}function K(){document.getElementById("rl-search").addEventListener("input",e=>{w=e.target.value,p()}),document.getElementById("rl-types").addEventListener("click",e=>{const n=e.target.closest(".rl-chip-btn");n&&(f=n.dataset.type??"all",document.querySelectorAll(".rl-chip-btn").forEach(s=>s.classList.toggle("active",s===n)),p())}),document.getElementById("rl-group").addEventListener("change",e=>{u=e.target.value,p()}),document.getElementById("rl-score").addEventListener("change",e=>{const n=e.target.value;I=n==="all"?"all":n==="other"?"other":Number(n),p()}),document.getElementById("rl-intermediates").addEventListener("change",e=>{B=e.target.checked,p()}),document.getElementById("rl-web-reps").addEventListener("change",e=>{g=e.target.checked,C(),p()}),document.querySelectorAll(".rl-view-btn").forEach(e=>{e.addEventListener("click",()=>{m=e.dataset.view??"recipes",document.querySelectorAll(".rl-view-btn").forEach(n=>n.classList.toggle("active",n===e)),p()})})}async function O(){try{await R();const[t,a]=await Promise.all([S(""),T()]);d=t,v=a}catch(t){F(t);return}for(const t of v)y.set(t.id,t);const e=await D().catch(()=>!1),n=d.filter(t=>!t.intermediate).length,s=h(d.filter(t=>!t.intermediate)).length;L(`共 ${d.length} 个菜谱（成品 ${n} · Web去重后 ${s}）${e?"":" · 静态数据"}`),C(),p(),K()}O();
