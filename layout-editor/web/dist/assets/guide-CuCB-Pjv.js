import{S as T,y as A,A as k,E as B,w as F,n as N,F as x,H as S}from"./version-CPbf5vRQ.js";function v(e){return String(e??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function P(e,i,t=!0){return!i||t===!1?"/icons/_placeholder.png":`/icons/${e}/${encodeURIComponent(i)}.png`}function I(e,i=""){return`<img class="food-icon" loading="lazy" src="${v(e)}" alt="${v(i)}" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`}const U={core:["TomatoSO","MeatSO","CheeseSO","LettuceSO","FlourSO","EggSO","PotatoSO","MushroomSO","PastaSO","FishSO"],dlc02:["DLC02_ChoppedBun","KebobChicken","KebobTomato","Melon","SmoothieStrawberry","Banana"],dlc03:["dlc03_chocolate","marshmallow","orange","whippedcream","driedfruit","milk"],dlc04:["dlc04_orange","dlc04_peach","corn","grapes"],dlc05:["DLC05_Dough","DLC05_Egg","DLC05_Marshmallow","DLC05_Strawberry","DLC05_Banana","DLC05_Crackers"],dlc07:["dlc07_potato","broccoli","CarrotSO"],dlc08:["dlc08_bun","dlc08_chicken"],dlc09:["dlc09_flour","dlc09_egg","dlc09_potato","dlc09_orange"],dlc10:["dlc10_orange","dlc10_grapes","dlc10_peach"],dlc11:["dlc11_tomato","dlc11_lettuce","dlc11_hotdogbun","dlc11_frankfurter","dlc11_ketchup"],dlc13:["dlc13_flour","dlc13_egg","dlc13_strawberry","dlc13_orange"]},K=["Burger_Plain_SO","Pizza_Mushroom_80_SO","MixedFlourEggBlueberry","Soup_Tomato_SO","Kebob_ChickenTomato"],V=Object.keys(T);function z(){return`
    <table class="guide-table">
      <thead><tr><th>路径</th><th>用途</th></tr></thead>
      <tbody>
        <tr><td><code>/icons/ingredients/&lt;id&gt;.png</code></td><td>食材图标</td></tr>
        <tr><td><code>/icons/recipes/&lt;id&gt;.png</code></td><td>官方菜谱成品图</td></tr>
        <tr><td><code>/icons/catalog/&lt;id&gt;.png</code></td><td>锅具 / prefab 调色板缩略图</td></tr>
        <tr><td><code>/icons/_placeholder.png</code></td><td>缺失时的回退图</td></tr>
      </tbody>
    </table>
    <p class="guide-note">目录数据中 <code>icon: true</code> 表示已解包对应 PNG；否则前端显示占位图。</p>`}function Z(e){const i=new Map(e.map(n=>[n.id,n])),t=[];for(const[n,s]of Object.entries(U)){const r=A(n),o=s.map(d=>{const l=i.get(d),h=(l==null?void 0:l.nameZh)??d,u=P("ingredients",d,(l==null?void 0:l.icon)!==!1);return`<div class="guide-icon-cell" title="${v(d)}">${I(u,h)}<span>${v(h)}</span></div>`}).join("");t.push(`
      <div class="guide-icon-group">
        <h4>${v(r)}</h4>
        <div class="guide-icon-grid">${o}</div>
      </div>`)}return t.join("")}function J(e){const i=new Map(e.map(n=>[n.id,n]));return`<div class="guide-recipe-grid">${K.map(n=>{const s=i.get(n);if(!s)return"";const r=P("recipes",s.id,s.icon!==!1),d=(s.ingredients??[]).slice(0,6).map(h=>{const u=Q(h);return I(P("ingredients",h),u)}).join(""),l=s.score!=null?`<span class="guide-score">${s.score} 分</span>`:'<span class="guide-score muted">中间产物</span>';return`
      <div class="guide-recipe-card">
        <div class="guide-recipe-head">${I(r,s.nameZh??n)}<div>
          <b>${v(s.nameZh??n)}</b>
          ${s.nameEn?`<span class="muted">${v(s.nameEn)}</span>`:""}
          ${l}
        </div></div>
        <div class="guide-recipe-ings">${d||'<span class="muted">—</span>'}</div>
      </div>`}).filter(Boolean).join("")}</div>`}function Q(e){return e.replace(/SO$/,"").replace(/^dlc\d+_/,"")}function W(){const e={Pot:"煮锅",FryingPan:"煎锅",DeepFatFryer:"炸篮",OvenTray:"烤箱",Steamer:"蒸笼",Mixer:"搅拌碗",Blender:"搅拌杯",MixingBowl:"搅拌碗",GriddlePan:"煎烤盘",KebabSkewer:"烤串",ToastingFork:"烤棉花糖叉",HotPot:"大火锅",RoastingTray:"烤托盘"};return`<div class="guide-icon-grid guide-icon-grid-wide">${V.map(t=>{const n=T[t]??"/icons/_placeholder.png";return`<div class="guide-icon-cell">${I(n,e[t]??t)}<span>${v(e[t]??t)}</span></div>`}).join("")}</div>`}function a(e){return String(e??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function X(e,i){switch(e.type){case"paragraph":return`<p>${e.text}</p>`;case"steps":return`<ol class="guide-steps">${e.items.map(t=>`<li>${a(t)}</li>`).join("")}</ol>`;case"bullets":return`<ul class="guide-bullets">${e.items.map(t=>`<li>${t}</li>`).join("")}</ul>`;case"callout":return`<p class="guide-callout">${a(e.text)}</p>`;case"note":return`<p class="guide-note">${a(e.text)}</p>`;case"table":return`<table class="guide-table guide-table-grid"><thead><tr>${e.header.map(t=>`<th>${a(t)}</th>`).join("")}</tr></thead><tbody>${e.rows.map(t=>`<tr>${t.map(n=>`<td>${n}</td>`).join("")}</tr>`).join("")}</tbody></table>`;case"kbdTable":return`<table class="guide-table"><tbody>${e.rows.map(([t,n])=>`<tr><th>${a(t)}</th><td>${n}</td></tr>`).join("")}</tbody></table>`;case"link":return`<p><a class="guide-link" href="${a(e.href)}"${e.external?' target="_blank" rel="noopener"':""}>${a(e.label)}</a></p>`;case"dynamic":switch(e.kind){case"icon-paths":return z();case"ingredient-samples":return Z(i.ingredients);case"recipe-samples":return J(i.recipes);case"utensil-icons":return W();default:return""}}}function D(e,i){var t;return(t=e.blocks)!=null&&t.length?`<div class="guide-card-body">${e.blocks.map(n=>X(n,i)).join("")}</div>`:""}function M(e){var i,t;return(i=e.children)!=null&&i.length?e.children.reduce((n,s)=>n+M(s),0):(t=e.blocks)!=null&&t.length?1:0}function q(e,i,t){var o;const n=D(e,i),s=((o=e.children)==null?void 0:o.map(d=>q(d,i,t+1)).join(""))??"";if(!n&&!s)return"";const r=t>=3?"h5":"h4";return`
    <article class="guide-card guide-anchor" id="guide-${a(e.id)}" data-guide-id="${a(e.id)}">
      <div class="guide-card-accent"></div>
      <${r} class="guide-card-title">${a(e.title)}</${r}>
      ${n}
      ${s}
    </article>`}function H(e,i,t){const n=D(e,i),s=(e.children??[]).map(d=>{var l;return(l=d.children)!=null&&l.length?H(d,i,t+1):q(d,i,t)}).join("");if(!n&&!s)return"";const r=t===1?"guide-section":"guide-subsection",o=t===1?"h2":"h3";return`
    <section class="${r} guide-anchor" id="guide-${a(e.id)}" data-guide-id="${a(e.id)}">
      <${o} class="${t===1?"guide-section-title":"guide-subsection-title"}">${a(e.title)}</${o}>
      ${n}
      ${s}
    </section>`}function Y(e,i,t,n){const s=(e.children??[]).map(d=>{var l;return(l=d.children)!=null&&l.length?H(d,i,1):q(d,i,1)}).join(""),r=M(e);return`
    <header class="guide-hero">
      <div class="guide-hero-badge">${String(t+1).padStart(2,"0")}<span class="guide-hero-badge-total">/${n}</span></div>
      <div class="guide-hero-main">
        <h1 class="guide-hero-title">${e.icon?`<span class="guide-hero-icon">${e.icon}</span>`:""}${a(e.title)}</h1>
        ${e.desc?`<p class="guide-hero-desc">${a(e.desc)}</p>`:""}
      </div>
      <div class="guide-hero-meta">${r} 张功能卡片</div>
    </header>
    <div class="guide-page-body">${s}</div>`}function G(e,i,t){var n;t.push({node:e,pageId:i}),(n=e.children)==null||n.forEach(s=>G(s,i,t))}function ee(e,i){const t=e.map((r,o)=>`<button type="button" class="guide-page-btn${r.id===i?" active":""}" data-page="${a(r.id)}"><span class="guide-page-num">${String(o+1).padStart(2,"0")}</span><span class="guide-page-label">${r.icon?`${r.icon} `:""}${a(r.title)}</span></button>`).join(""),n=e.find(r=>r.id===i)??e[0],s=((n==null?void 0:n.children)??[]).map(r=>R(r,0)).join("");return`
    <div class="guide-sidebar-title">章节</div>
    <nav class="guide-pages">${t}</nav>
    <div class="guide-sidebar-title guide-subtree-title">本页小节</div>
    <ul class="guide-tree-root">${s}</ul>
    <ul class="guide-search-results" hidden></ul>`}function R(e,i){var s;const t=`<a class="guide-tree-link" href="#guide-${a(e.id)}" data-guide-id="${a(e.id)}">${a(e.title)}</a>`;if(!((s=e.children)!=null&&s.length))return`<li class="guide-tree-leaf" data-guide-title="${a(e.title.toLowerCase())}" data-guide-id="${a(e.id)}">${t}</li>`;const n=e.children.map(r=>R(r,i+1)).join("");return`
    <li class="guide-tree-branch" data-guide-title="${a(e.title.toLowerCase())}" data-guide-id="${a(e.id)}">
      <details class="guide-tree-details"${i<1?" open":""}>
        <summary>${t}</summary>
        <ul class="guide-tree-nested">${n}</ul>
      </details>
    </li>`}function te(e){const i=[];return e.forEach(t=>G(t,t.id,i)),i}function C(e,i,t){e.querySelectorAll(".guide-tree-link").forEach(r=>{r.classList.toggle("active",r.dataset.guideId===t)});const n=i.querySelector(".guide-anchor.active-card");n==null||n.classList.remove("active-card");const s=i.querySelector(`#guide-${CSS.escape(t)}`);s==null||s.classList.add("active-card")}function ie(e,i){const t=e.querySelector(".guide-sidebar"),n=e.querySelector(".guide-body"),s=e.querySelector("#guide-search");if(!t||!n)return;const r=t,o=n,d=te(i.chapters);t.querySelectorAll(".guide-page-btn").forEach(c=>{c.addEventListener("click",()=>{const p=c.dataset.page;p&&p!==i.pageId&&i.onNavigatePage(p)})}),t.querySelectorAll(".guide-tree-link").forEach(c=>{c.addEventListener("click",p=>{p.preventDefault();const f=c.dataset.guideId,$=f?n.querySelector(`#guide-${CSS.escape(f)}`):null;$==null||$.scrollIntoView({behavior:"smooth",block:"start"}),f&&C(t,n,f)})});const l=new IntersectionObserver(c=>{var $;const f=($=c.filter(y=>y.isIntersecting).sort((y,_)=>y.boundingClientRect.top-_.boundingClientRect.top)[0])==null?void 0:$.target;f!=null&&f.dataset.guideId&&C(t,n,f.dataset.guideId)},{root:null,rootMargin:"-20% 0px -60% 0px",threshold:0});n.querySelectorAll(".guide-anchor").forEach(c=>l.observe(c));function h(c){const p=r.querySelector(".guide-search-results"),f=r.querySelectorAll(".guide-tree-root"),$=r.querySelector(".guide-subtree-title");if(!p)return;if(!c){p.hidden=!0,p.innerHTML="",f.forEach(g=>g.style.display=""),$&&($.style.display="");return}f.forEach(g=>g.style.display="none"),$&&($.style.display="none");const y=d.filter(({node:g})=>g.title.toLowerCase().includes(c)),_=new Map;y.forEach(({node:g,pageId:b})=>{const m=_.get(b)??[];m.push(g),_.set(b,m)});const w=[];i.chapters.forEach(g=>{const b=_.get(g.id);b!=null&&b.length&&(w.push(`<li class="guide-search-group">${g.icon??"📄"} ${a(g.title)}</li>`),b.forEach(m=>{w.push(`<li class="guide-tree-leaf"><a class="guide-tree-link" href="${k(g.id)}" data-page="${a(g.id)}" data-guide-id="${a(m.id)}">${a(m.title)}</a></li>`)}))}),p.innerHTML=w.length?w.join(""):'<li class="guide-search-empty">没有匹配的小节</li>',p.hidden=!1,p.querySelectorAll(".guide-tree-link").forEach(g=>{g.addEventListener("click",b=>{b.preventDefault();const m=g.dataset.page??i.pageId,E=g.dataset.guideId;if(m!==i.pageId)i.onNavigatePage(m,E);else{const L=E?o.querySelector(`#guide-${CSS.escape(E)}`):null;L==null||L.scrollIntoView({behavior:"smooth",block:"start"}),E&&C(r,o,E)}})})}const u=s;if(u){u._guideSearchHandler&&u.removeEventListener("input",u._guideSearchHandler);const c=()=>h(u.value.trim().toLowerCase());u._guideSearchHandler=c,u.addEventListener("input",c)}}function ae(){B("guide")}function O(){var i;const e=x();return e.page==="guide"&&e.guidePageId?e.guidePageId:((i=S[0])==null?void 0:i.id)??"overview"}async function ne(){try{const e=await fetch("/ingredients.json"),t=(e.ok?await e.json():{ingredients:[]}).ingredients??[];let n=[];const s=await fetch("/recipes.json");if(s.ok){const r=await s.json(),o=Object.values(r.groupFiles??{});o.length&&(n=(await Promise.all(o.map(async l=>{const h=await fetch(`/${l}`);return h.ok?(await h.json()).recipes??[]:[]}))).flat())}return{ingredients:t,recipes:n}}catch{return{ingredients:[],recipes:[]}}}const se=()=>`
  ${N("guide")}
  <div class="manage-bar guide-bar">
    <h1 class="m-title">📘 功能说明</h1>
    <input type="search" id="guide-search" class="guide-search" placeholder="搜索全部章节…" autocomplete="off">
  </div>
  <div class="guide-layout">
    <aside class="guide-sidebar"></aside>
    <div class="guide-body manage-content"></div>
  </div>
`;function j(e,i,t,n){const s=e.querySelector(".guide-sidebar"),r=e.querySelector(".guide-body");if(!s||!r)return;const o=e.querySelector("#guide-search"),d=(o==null?void 0:o.value.trim().toLowerCase())??"",l=S.findIndex(c=>c.id===t),h=S[l]??S[0];s.innerHTML=ee(S,h.id),r.innerHTML=Y(h,i,l,S.length),r.scrollTop=0,ie(e,{chapters:S,pageId:h.id,onNavigatePage:(c,p)=>{history.pushState({guidePage:c},"",k(c)),j(e,i,c,p)}}),n&&requestAnimationFrame(()=>{var c;(c=r.querySelector(`#guide-${CSS.escape(n)}`))==null||c.scrollIntoView({block:"start"})});const u=e.querySelector("#guide-search");u&&d&&(u.value=d,u.dispatchEvent(new Event("input",{bubbles:!0})),n||u.focus())}async function ce(e,i){document.body.classList.add("manage-bg");const t=await ne(),n={ingredients:t.ingredients,recipes:t.recipes};e.innerHTML=se(),F();const s=i??O(),r=k(s);location.pathname!==r&&history.replaceState({guidePage:s},"",r),j(e,n,s),window.addEventListener("popstate",()=>{j(e,n,O())})}export{ae as goGuide,ce as renderGuideView};
