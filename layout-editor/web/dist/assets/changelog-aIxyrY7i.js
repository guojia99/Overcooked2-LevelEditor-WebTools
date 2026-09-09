import{n as v,w as b}from"./version-CPbf5vRQ.js";function m(t){return t.replace(/\*\*/g,"").trim()}function w(t){const n=[];let e=null,s=null,i=null;const p=()=>{e&&(s||(s={title:"",items:[]},e.sections.push(s)))};for(const f of t.split(/\r?\n/)){const l=f.trimEnd(),o=l.match(/^##(?!#)\s+(.*)$/);if(o){e={version:o[1].trim(),date:"",sections:[]},n.push(e),s=null,i=null;continue}const r=l.match(/^###(?!#)\s+(.*)$/);if(r){e&&(e.date=r[1].trim());continue}const u=l.match(/^####\s+(.*)$/);if(u){e&&(s={title:m(u[1]),items:[]},e.sections.push(s)),i=null;continue}const d=l.trim().match(/^\*\*(.+?)\*\*:?\s*$/);if(d){e&&(s={title:m(d[1]),items:[]},e.sections.push(s)),i=null;continue}const a=l.match(/^(\s*)-\s+(.*)$/);if(a&&e){const $=a[1].length>=2,g=a[2].trim();$&&i?i.children.push(g):(p(),i={text:g,children:[]},s&&s.items.push(i))}}return n}function c(t){return t.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function h(t){return c(t).replace(/\*\*(.+?)\*\*/g,"<b>$1</b>")}function y(t){const n=t.toLowerCase();return n.includes("修复")||n.includes("bug")?"🛠️":n.includes("新增")||n.includes("新功能")||n.includes("功能")?"✨":n.includes("优化")?"⚡":(n.includes("关键")||n.includes("说明"),"📌")}function j(t){const n=t.items.map(s=>`<li>${h(s.text)}${s.children.length?`<ul class="clog-sublist">${s.children.map(i=>`<li>${h(i)}</li>`).join("")}</ul>`:""}</li>`).join("");return`<div class="clog-section">${t.title?`<h3 class="clog-section-title"><span class="clog-section-ico">${y(t.title)}</span>${c(t.title)}</h3>`:""}<ul class="clog-list">${n}</ul></div>`}function L(t,n){const e=n===0?'<span class="clog-latest">最新</span>':"";return`<section class="clog-entry${n===0?" clog-entry-latest":""}">
    <div class="clog-rail"><span class="clog-dot"></span></div>
    <div class="clog-card">
      <header class="clog-head">
        <span class="clog-version">${c(t.version)}</span>
        ${e}
        <span class="clog-date">🕒 ${c(t.date)}</span>
      </header>
      ${t.sections.map(j).join("")}
    </div>
  </section>`}async function T(t){document.body.classList.add("manage-bg");let n="";try{const s=await fetch("/UPDATE_LOG.md");s.ok&&(n=await s.text())}catch{}const e=w(n);t.innerHTML=`
    ${v("changelog")}
    <div class="manage-bar">
      <h1 class="m-title">📜 更新日志</h1>
      <span class="muted small">共 ${e.length} 个版本</span>
    </div>
    <div class="manage-content changelog-content">
      ${e.length?`<div class="clog-timeline">${e.map(L).join("")}</div>`:'<p class="modal-hint">未读取到 UPDATE_LOG.md：请先运行 <code>node layout-editor/scripts/build-catalog.mjs</code> 生成到 web/public。</p>'}
    </div>`,b()}export{w as parseChangelog,T as renderChangelogView};
