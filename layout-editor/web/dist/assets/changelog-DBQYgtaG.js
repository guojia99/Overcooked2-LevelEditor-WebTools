import{n as v,w as b}from"./version-iccdJYUJ.js";function m(n){return n.replace(/\*\*/g,"").trim()}function y(n){const t=[];let s=null,e=null,i=null;const g=()=>{s&&(e||(e={title:"",items:[]},s.sections.push(e)))};for(const f of n.split(/\r?\n/)){const l=f.trimEnd(),a=l.match(/^##(?!#)\s+(.*)$/);if(a){s={version:a[1].trim(),date:"",sections:[]},t.push(s),e=null,i=null;continue}const r=l.match(/^###(?!#)\s+(.*)$/);if(r){s&&(s.date=r[1].trim());continue}const d=l.match(/^####\s+(.*)$/);if(d){s&&(e={title:m(d[1]),items:[]},s.sections.push(e)),i=null;continue}const u=l.trim().match(/^\*\*(.+?)\*\*:?\s*$/);if(u){s&&(e={title:m(u[1]),items:[]},s.sections.push(e)),i=null;continue}const o=l.match(/^(\s*)-\s+(.*)$/);if(o&&s){const $=o[1].length>=2,h=o[2].trim();$&&i?i.children.push(h):(g(),i={text:h,children:[]},e&&e.items.push(i))}}return t}function c(n){return n.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function p(n){return c(n).replace(/\*\*(.+?)\*\*/g,"<b>$1</b>")}function w(n){const t=n.toLowerCase();return t.includes("修复")||t.includes("bug")?"🛠️":t.includes("新增")||t.includes("新功能")||t.includes("功能")?"✨":t.includes("优化")?"⚡":(t.includes("关键")||t.includes("说明"),"📌")}function j(n){const t=n.items.map(e=>`<li>${p(e.text)}${e.children.length?`<ul class="clog-sublist">${e.children.map(i=>`<li>${p(i)}</li>`).join("")}</ul>`:""}</li>`).join("");return`<div class="clog-section">${n.title?`<h3 class="clog-section-title"><span class="clog-section-ico">${w(n.title)}</span>${c(n.title)}</h3>`:""}<ul class="clog-list">${t}</ul></div>`}function L(n,t){const s=t===0?'<span class="clog-latest">最新</span>':"";return`<section class="clog-entry${t===0?" clog-entry-latest":""}">
    <div class="clog-rail"><span class="clog-dot"></span></div>
    <div class="clog-card">
      <header class="clog-head">
        <span class="clog-version">${c(n.version)}</span>
        ${s}
        <span class="clog-date">🕒 ${c(n.date)}</span>
      </header>
      ${n.sections.map(j).join("")}
    </div>
  </section>`}async function T(n){document.body.classList.add("manage-bg");let t="";try{const e=await fetch("/UPDATE_LOG.md");e.ok&&(t=await e.text())}catch{}const s=y(t);n.innerHTML=`
    ${v("changelog")}
    <div class="manage-bar">
      <h1 class="m-title">📜 更新日志</h1>
      <span class="muted small">共 ${s.length} 个版本</span>
    </div>
    <div class="manage-content changelog-content">
      ${s.length?`<div class="clog-timeline">${s.map(L).join("")}</div>`:'<p class="modal-hint">未读取到 UPDATE_LOG.md：请先运行 <code>node layout-editor/scripts/build-catalog.mjs</code> 生成到 web/public。</p>'}
    </div>`,b(e=>{e==="layout"?(location.hash="#/layout",location.reload()):e==="manage"?(location.hash="#/manage",location.reload()):e==="dependencies"?(location.hash="#/dependencies",location.reload()):e==="custom-recipes"?(location.hash="#/custom-recipes",location.reload()):e==="recipes"?location.href="/recipes":e==="guide"&&(location.hash="#/guide",location.reload())})}export{y as parseChangelog,T as renderChangelogView};
