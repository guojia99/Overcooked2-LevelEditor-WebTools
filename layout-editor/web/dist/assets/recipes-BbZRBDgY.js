import{n as E,w as L,i as I,j as $,K as B,k as h,l as w,p as H,q as T,f as M}from"./recipeCard-D7GB0m9M.js";const j=document.getElementById("app");document.body.classList.add("manage-bg");function i(e){return String(e??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function y(e,t=!0){const n=document.getElementById("rl-status");n&&(n.textContent=e,n.classList.toggle("err",!t),n.classList.toggle("ok",t&&e.length>0))}function x(e){const t=e instanceof Error?e.message:String(e);y(t,!1);const n=document.getElementById("rl-content");n&&(n.innerHTML=`<div class="rl-empty">加载失败：${i(t)}</div>`)}j.innerHTML=`
  ${E("recipes")}
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
    <input type="search" id="rl-search" class="rl-search" placeholder="搜索菜名 / 英文名 / ID / 食材…" autocomplete="off">
    <div class="rl-chips" id="rl-types"></div>
  </div>
  <div class="manage-content rl-content" id="rl-content">
    <div class="rl-empty">加载中…</div>
  </div>
`;L(e=>{e==="layout"?location.href="/index.html#/layout":e==="manage"?location.href="/index.html#/manage":e==="custom-recipes"&&(location.href="/index.html#/custom-recipes")});let r=[],f=[];const g=new Map;let v="",d="all",p="all",b=!1;function k(e){return T(e,{allRecipes:r,ingredientName:t=>{var n;return((n=g.get(t))==null?void 0:n.nameZh)??t}})}function q(){const e=v.trim().toLowerCase();return r.filter(t=>!(!b&&t.intermediate||d!=="all"&&(t.type??"other")!==d||p!=="all"&&(t.group??"core")!==p||e&&![t.nameZh,t.nameEn??"",t.id,...(t.ingredients??[]).map(s=>{var c;return((c=g.get(s))==null?void 0:c.nameZh)??s})].join(" ").toLowerCase().includes(e)))}function o(){const e=document.getElementById("rl-content"),t=q();if(t.length===0){e.innerHTML='<div class="rl-empty">没有匹配的菜谱，试试调整搜索或筛选条件</div>';return}e.innerHTML=h(t).map(([n,s])=>H(n,s.map(k).join(""),s.length)).join("")}function C(){const e=r.filter(l=>!l.intermediate),t=h(e),n=document.getElementById("rl-types"),s=[{type:"all",label:"全部",count:e.length},...t.map(([l,a])=>({type:l,label:w(l),count:a.length}))];n.innerHTML=s.map(l=>`<button type="button" class="rl-chip-btn${l.type===d?" active":""}" data-type="${i(l.type)}">${i(l.label)}<span class="rl-cnt">${l.count}</span></button>`).join("");const c=document.getElementById("rl-group"),u=new Map;for(const l of e){const a=l.group??"core";u.set(a,(u.get(a)??0)+1)}const m=['<option value="all">全部来源</option>'];for(const[l,a]of u)m.push(`<option value="${i(l)}" ${l===p?"selected":""}>${i(M(l))} (${a})</option>`);c.innerHTML=m.join("")}function S(){document.getElementById("rl-search").addEventListener("input",e=>{v=e.target.value,o()}),document.getElementById("rl-types").addEventListener("click",e=>{const t=e.target.closest(".rl-chip-btn");t&&(d=t.dataset.type??"all",document.querySelectorAll(".rl-chip-btn").forEach(n=>n.classList.toggle("active",n===t)),o())}),document.getElementById("rl-group").addEventListener("change",e=>{p=e.target.value,o()}),document.getElementById("rl-intermediates").addEventListener("change",e=>{b=e.target.checked,o()})}async function F(){try{const[n,s]=await Promise.all([I(""),$()]);r=n,f=s}catch(n){x(n);return}for(const n of f)g.set(n.id,n);const e=await B().catch(()=>!1),t=r.filter(n=>!n.intermediate).length;y(`共 ${r.length} 个菜谱（成品 ${t}）${e?"":" · 静态数据"}`),C(),o(),S()}F();
