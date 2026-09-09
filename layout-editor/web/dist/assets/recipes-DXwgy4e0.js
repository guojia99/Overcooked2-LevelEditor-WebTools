import{m as P,n as U,w as Z,j as _,k as A,l as F,o as L,p as T,q as G,t as O,v as z,x as V,y as b,S as R,z as K}from"./version-CPbf5vRQ.js";P();const W=document.getElementById("app");document.body.classList.add("manage-bg");function c(e){return String(e??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function m(e,t=!0){const n=document.getElementById("rl-status");n&&(n.textContent=e,n.classList.toggle("err",!t),n.classList.toggle("ok",t&&e.length>0))}function J(e){const t=e instanceof Error?e.message:String(e);m(t,!1);const n=document.getElementById("rl-content");n&&(n.innerHTML=`<div class="rl-empty">加载失败：${c(t)}</div>`)}W.innerHTML=`
  ${U("recipes")}
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
`;Z();let p=[],w=[];const C=new Map;let B="",y="all",f="all",H="all",x=!1,v="recipes",E=!0;const Q=[20,40,60,80,100,120];function X(e,t){if(t==="all")return!0;const n=e??0;return t==="other"?!Q.includes(n):n===t}function Y(e){return String(e??"").replace(/·?DLC\d+/g,"").replace(/[（）()· ]/g,"")}function M(e){const t=/^dlc(\d+)_/.exec(e??"");return t?parseInt(t[1],10):0}function $(e){const t=new Map;for(const n of e){const s=Y(n.nameZh??""),i=t.get(s);(!i||M(n.id)>M(i.id))&&t.set(s,n)}return[...t.values()]}function D(e){return e.isCustom&&e.assetPath?`/api/custom-recipes/icon?assetPath=${encodeURIComponent(e.assetPath)}`:`/icons/recipes/${encodeURIComponent(e.id)}.png`}function ee(e){return O(e,{allRecipes:p,ingredientName:t=>{var n;return((n=C.get(t))==null?void 0:n.nameZh)??t},extraBadge:e.group==="levelset"?"本关":e.group==="burger"?"🍔":void 0,iconSrc:D})}function j(){const e=B.trim().toLowerCase();let t=p.filter(n=>!(!x&&n.intermediate||y!=="all"&&(n.type??"other")!==y||f!=="all"&&(n.group??"core")!==f||!X(n.score,H)||e&&![n.nameZh,n.nameEn??"",n.id,...(n.ingredients??[]).map(i=>{var o;return((o=C.get(i))==null?void 0:o.nameZh)??i})].join(" ").toLowerCase().includes(e)));return E&&(t=$(t)),t}function te(e){const t=e.group&&e.group!=="core"?` <span class="pc-badge">${c(b(e.group))}</span>`:"",n=e.nameEn&&e.nameEn.trim()?` <span class="muted pc-en">${c(e.nameEn)}</span>`:"";return`<div class="rl-ing-card" title="${c(e.id)}">
    <img class="food-icon" loading="lazy" src="/icons/ingredients/${encodeURIComponent(e.id)}.png" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">
    <span class="rl-ing-name">${c(e.nameZh)}${t}${n}</span>
    <span class="muted small">${c(e.id)}</span>
  </div>`}function ne(){const e=B.trim().toLowerCase(),t=w.filter(o=>!(e&&!`${o.nameZh} ${o.nameEn??""} ${o.id}`.toLowerCase().includes(e)||f!=="all"&&(o.group??"core")!==f)),n=E?$(t):t;if(n.length===0)return'<div class="rl-empty">没有匹配的食材，试试调整搜索或筛选条件</div>';const s=new Map;for(const o of n){const a=o.group??"core";s.has(a)||s.set(a,[]),s.get(a).push(o)}return[...s.keys()].sort((o,a)=>{const r=l=>l==="core"?0:l==="levelset"?1:2;return r(o)-r(a)||o.localeCompare(a)}).map(o=>{const a=s.get(o);return`<section class="rl-section">
        <h2 class="rl-section-title">${c(b(o))}<span class="rl-section-count">${a.length}</span></h2>
        <div class="rl-ing-grid">${a.map(te).join("")}</div>
      </section>`}).join("")}function u(){const e=document.getElementById("rl-content"),t=document.getElementById("rl-types");t&&(t.style.display=v==="recipes"?"":"none");const n=document.getElementById("rl-score");if(n&&(n.style.display=v==="recipes"?"":"none"),v==="ingredients"){e.innerHTML=ne();return}const s=j();if(s.length===0){e.innerHTML='<div class="rl-empty">没有匹配的菜谱，试试调整搜索或筛选条件</div>';return}e.innerHTML=L(s).map(([i,o])=>G(i,o.map(ee).join(""),o.length)).join("")}function q(){const e=E?$(p.filter(r=>!r.intermediate)):p.filter(r=>!r.intermediate),t=L(e),n=document.getElementById("rl-types"),s=[{type:"all",label:"全部",count:e.length},...t.map(([r,l])=>({type:r,label:T(r),count:l.length}))];n.innerHTML=s.map(r=>`<button type="button" class="rl-chip-btn${r.type===y?" active":""}" data-type="${c(r.type)}">${c(r.label)}<span class="rl-cnt">${r.count}</span></button>`).join("");const i=document.getElementById("rl-group"),o=new Map;for(const r of e){const l=r.group??"core";o.set(l,(o.get(l)??0)+1)}const a=['<option value="all">全部来源</option>'];for(const[r,l]of o)a.push(`<option value="${c(r)}" ${r===f?"selected":""}>${c(b(r))} (${l})</option>`);i.innerHTML=a.join("")}function se(){document.getElementById("rl-search").addEventListener("input",e=>{B=e.target.value,u()}),document.getElementById("rl-types").addEventListener("click",e=>{const t=e.target.closest(".rl-chip-btn");t&&(y=t.dataset.type??"all",document.querySelectorAll(".rl-chip-btn").forEach(n=>n.classList.toggle("active",n===t)),u())}),document.getElementById("rl-group").addEventListener("change",e=>{f=e.target.value,u()}),document.getElementById("rl-score").addEventListener("change",e=>{const t=e.target.value;H=t==="all"?"all":t==="other"?"other":Number(t),u()}),document.getElementById("rl-intermediates").addEventListener("change",e=>{x=e.target.checked,u()}),document.getElementById("rl-web-reps").addEventListener("change",e=>{E=e.target.checked,q(),u()}),document.querySelectorAll(".rl-view-btn").forEach(e=>{e.addEventListener("click",()=>{v=e.dataset.view??"recipes",document.querySelectorAll(".rl-view-btn").forEach(t=>t.classList.toggle("active",t===e)),u()})}),document.getElementById("rl-export").addEventListener("click",()=>void oe())}async function oe(){var n;const e=document.getElementById("rl-export"),t=j();if(t.length===0){m("没有可导出的菜谱",!1);return}e&&(e.disabled=!0),m("正在生成图片…");try{const s=L(t).map(([a,r])=>({typeLabel:T(a),count:r.length,cards:r.map(l=>{const N=z(l,{allRecipes:p}),S=V(l),g=[];return S&&g.push("半成品"),l.isCustom&&g.push("自定义"),l.group==="levelset"&&g.push("本关"),l.group&&l.group!=="core"&&l.group!=="levelset"&&g.push(b(l.group)),S||g.push(`⭐ ${l.score??0}`),{iconUrl:D(l),nameZh:l.nameZh,nameEn:l.nameEn||l.id,badges:g,groups:N.map(h=>({stepIcons:[h.step,...(h.extraSteps??[]).map(d=>d.step)].filter(Boolean).map(d=>R[d]).filter(d=>!!d),ingredientUrls:(h.ingredients??[]).map(d=>`/icons/ingredients/${encodeURIComponent(d)}.png`),ingredientStepIcons:(h.ingredients??[]).map(d=>{var k;return(((k=h.ingredientSteps)==null?void 0:k[d])??[]).map(I=>R[I]).filter(I=>!!I)})}))}})})),i=((n=document.getElementById("rl-content"))==null?void 0:n.getBoundingClientRect().width)||1200,o=new Date().toISOString().slice(0,10);await K({title:"菜谱清单列表",sub:`共 ${t.length} 个菜谱 · 导出于 ${o}`,sections:s},i,`菜谱清单_${t.length}个_${o}.png`),m(`已导出 PNG（${t.length} 个菜谱）`)}catch(s){m(s instanceof Error?s.message:String(s),!1)}finally{e&&(e.disabled=!1)}}async function re(){try{const[s,i]=await Promise.all([_(""),A()]);p=s,w=i}catch(s){J(s);return}for(const s of w)C.set(s.id,s);const e=await F().catch(()=>!1),t=p.filter(s=>!s.intermediate).length,n=$(p.filter(s=>!s.intermediate)).length;m(`共 ${p.length} 个菜谱（成品 ${t} · Web去重后 ${n}）${e?"":" · 静态数据"}`),q(),u(),se()}re();
