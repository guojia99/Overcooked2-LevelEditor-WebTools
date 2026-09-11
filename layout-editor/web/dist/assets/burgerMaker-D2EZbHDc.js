import{n as te,w as ae,I as se,W as x,a6 as Z,R as P,a7 as ne,a1 as oe,U as ce,V as de}from"./version-DLCzTXp2.js";function i(t){return String(t??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function q(t){v(!1,t.message??String(t))}const D="burgerMakerSetName",H="burgerMakerLoadAssetPath";function v(t,n){const o=document.getElementById("bm-status");o&&(o.textContent=n,o.classList.toggle("err",!t),o.classList.toggle("ok",t&&n.length>0))}function L(t,n){return n.has(t)||t==="ChoppedBunSO"||t.toLowerCase()==="dlc08_bun"?!0:/ChoppedBun/i.test(t)}function z(t){return[...t.buns??[],...t.candidates??[]]}function B(t,n){return t.find(o=>o.id===n)}function N(t,n){const o=B(t,n);return o?o.nameZh:n}function ie(t,n){var e;const o=B(t,n);return((e=o==null?void 0:o.nameEn)==null?void 0:e.trim())||n}function V(t){return t.kind==="custom"?`/api/custom-recipes/icon?assetPath=${encodeURIComponent(t.assetPath)}`:t.kind==="official-recipe"?`/icons/recipes/${encodeURIComponent(t.id)}.png`:`/icons/ingredients/${encodeURIComponent(t.id)}.png`}function le(t){if(!t)return'<img class="food-icon bm-layer-icon" src="/icons/_placeholder.png" alt="">';const n=V(t);return`<img class="food-icon bm-layer-icon" loading="lazy" src="${i(n)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`}function re(t){const n=V(t);return`<img class="food-icon" loading="lazy" src="${i(n)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`}function me(t){var e;const n=(e=t.nameEn)==null?void 0:e.trim(),o=(t.nameZh+" "+(n??"")+" "+t.id).toLowerCase();return`<button type="button" class="pick-card bm-pick-card" data-id="${i(t.id)}" data-name="${i(o)}" title="${i(t.id)}">
    <span class="pc-head">${re(t)}<span class="pc-name">${i(t.nameZh)}${n?` <span class="muted pc-en">${i(n)}</span>`:""}</span></span>
    <span class="muted small">${i(t.id)}</span>
  </button>`}const ue=[{test:/meat|patty|beef|steak|pork|chicken|sausage|bacon|fried|grill|肉|排|鸡|猪|肠|培根|虾|fish|鱼|蟹|crab|shrimp/i,palette:{bg1:"rgba(160,82,45,0.62)",bg2:"rgba(101,50,14,0.5)",border:"rgba(210,120,70,0.75)"}},{test:/cheese|芝士|奶酪/i,palette:{bg1:"rgba(244,208,63,0.62)",bg2:"rgba(212,172,13,0.48)",border:"rgba(255,220,90,0.8)"}},{test:/lettuce|生菜|菜叶|spinach|菠菜|cabbage|卷心菜/i,palette:{bg1:"rgba(88,214,141,0.58)",bg2:"rgba(39,174,96,0.45)",border:"rgba(130,235,170,0.75)"}},{test:/tomato|番茄|tomatoes/i,palette:{bg1:"rgba(231,76,60,0.58)",bg2:"rgba(192,57,43,0.48)",border:"rgba(255,120,100,0.78)"}},{test:/onion|洋葱|shallot/i,palette:{bg1:"rgba(195,155,211,0.55)",bg2:"rgba(142,68,173,0.42)",border:"rgba(210,170,230,0.72)"}},{test:/pickle|cucumber|黄瓜|gherkin/i,palette:{bg1:"rgba(130,224,170,0.55)",bg2:"rgba(30,132,73,0.45)",border:"rgba(150,240,190,0.72)"}},{test:/pineapple|菠萝|banana|香蕉|melon|瓜|fruit|果/i,palette:{bg1:"rgba(249,231,159,0.58)",bg2:"rgba(241,196,15,0.45)",border:"rgba(255,230,120,0.78)"}},{test:/mushroom|蘑菇|fung/i,palette:{bg1:"rgba(210,215,211,0.58)",bg2:"rgba(149,165,166,0.45)",border:"rgba(230,235,230,0.72)"}},{test:/egg|蛋|omelet/i,palette:{bg1:"rgba(255,248,220,0.62)",bg2:"rgba(245,230,180,0.5)",border:"rgba(255,250,210,0.78)"}},{test:/potato|土豆|chip|薯|fries/i,palette:{bg1:"rgba(245,203,167,0.58)",bg2:"rgba(211,152,90,0.48)",border:"rgba(255,210,150,0.75)"}},{test:/pepper|椒|chili|jalapeno/i,palette:{bg1:"rgba(255,120,80,0.55)",bg2:"rgba(192,57,43,0.42)",border:"rgba(255,150,110,0.72)"}},{test:/mixed|搅拌|blend/i,palette:{bg1:"rgba(187,143,206,0.52)",bg2:"rgba(125,90,140,0.42)",border:"rgba(200,160,215,0.7)"}}];function pe(t,n){const o=`${t} ${(n==null?void 0:n.nameZh)??""} ${(n==null?void 0:n.nameEn)??""}`;for(const g of ue)if(g.test.test(o))return g.palette;let e=0;for(let g=0;g<t.length;g++)e=e*31+t.charCodeAt(g)>>>0;const l=e%360;return{bg1:`hsla(${l}, 48%, 42%, 0.55)`,bg2:`hsla(${l}, 48%, 32%, 0.45)`,border:`hsla(${l}, 55%, 58%, 0.68)`}}function be(t,n){const o=pe(t,n);return`--bm-fill-bg1:${o.bg1};--bm-fill-bg2:${o.bg2};--bm-fill-border:${o.border}`}function S(t,n){return t.some(o=>L(o,n))}function ge(t,n){const o=[];let e=!1;for(const g of t)L(g,n)?e||(o.push({id:g,part:"bottom"}),e=!0):o.push({id:g});const l=t.find(g=>L(g,n));return l&&o.push({id:l,part:"top"}),o}async function he(t){var O;document.body.classList.add("manage-bg"),t.innerHTML=`
    ${te("custom-recipes")}
    <div class="manage-bar">
      <h1 class="m-title">🍔 汉堡组装工作台 <span class="muted" style="font-size:13px;font-weight:normal">产出归属当前关卡集</span></h1>
      <span class="status" id="bm-status"></span>
      <span style="flex:1"></span>
      <a class="m-btn" href="/custom-recipes">← 返回菜谱管理</a>
    </div>
    <div class="manage-content" id="bm-content"><p class="muted">加载中…</p></div>
  `,ae();const n=document.getElementById("bm-content");let o=[];try{o=await se()}catch{}if(o.length===0){n.innerHTML=`<div class="m-block"><h3>没有可用关卡集</h3>
      <p class="muted">汉堡产出的菜谱归属关卡集。请先在「关卡管理」创建关卡集，并打开过一次其自定义菜谱页（初始化配置）。</p></div>`;return}const e={setName:sessionStorage.getItem(D)??o[0].setName,category:"burger_local",defPath:"",stack:[],loadedProductId:"",loadedProductAssetPath:"",loadedRecipeName:"",loadedNameZh:"",loadedNameEn:"",loadedScore:0};o.some(a=>a.setName===e.setName)||(e.setName=o[0].setName);let l;try{x("加载汉堡数据…"),l=await Z(e.setName)}catch(a){P(),n.innerHTML=`<div class="m-block"><h3>加载失败</h3><p class="muted">${i(a.message)}</p><p class="muted">请确认 Unity 编辑器已启动且桥接服务运行中（Tools → Layout Editor → 启动服务）。</p></div>`;return}finally{P()}if(l.definitions.length===0){n.innerHTML=`<div class="m-block"><h3>未找到汉堡组装定义</h3>
      <p class="muted">commonW2 共享库中没有 CustomRecipeOptionalBurgerSO 资产。请先在 Unity 中完成 Burger大全 迁移。</p></div>`;return}const g=()=>l.definitions.find(a=>a.id==="OptionalBurger")??l.definitions.find(a=>a.id==="ChickenBurgerAssembly")??l.definitions[0];e.defPath=((O=g())==null?void 0:O.assetPath)??"";function w(){return new Set((l.buns??[]).map(a=>a.id))}function W(){const a=[["bun",l.buns??[]],["custom",l.candidates.filter(c=>c.kind==="custom")],["ingredient",l.candidates.filter(c=>c.kind==="ingredient")]],s={bun:"🍞 汉堡皮",custom:"🥩 中间产物（commonW2）",ingredient:"🥬 官方食材（commonW2 组装定义允许）"};return a.map(([c,u])=>u.length===0?"":`<div class="bm-pick-section" data-bm-group="${i(c)}">
          <div class="bm-pick-section-title">${s[c]} <span class="rl-cnt">${u.length}</span></div>
          <div class="pick-grid bm-pick-grid">${u.map(me).join("")}</div>
        </div>`).join("")}function F(){const a=z(l),s=w(),c=e.stack.map((d,m)=>{const h=B(a,d),r=L(d,s)?' <span class="muted small">（上下两片）</span>':"";return`<div class="bm-layer">
          ${le(h)}
          <span class="muted small">${m+1}</span>
          <span class="bm-layer-name">${i(N(a,d))}${r}
            <span class="muted small">${i(ie(a,d))} · ${i(d)}</span></span>
          <button class="m-btn small bm-up" data-i="${m}" ${m===0?"disabled":""} title="下移">↓</button>
          <button class="m-btn small bm-down" data-i="${m}" ${m===e.stack.length-1?"disabled":""} title="上移">↑</button>
          <button class="m-btn small danger bm-remove" data-i="${m}">×</button>
        </div>`}).join(""),u=ge(e.stack,s);return`
      <div class="bm-layer-list">
        ${c||'<p class="muted">点击下方「+ 添加层」开始堆叠（需至少一层汉堡面包）…</p>'}
      </div>
      <div class="bm-preview-wrap">
        <div class="bm-stack-preview">
          ${u.map(d=>{const m=d.part==="bottom"?"（底）":d.part==="top"?"（顶）":"",h=d.part==="bottom"?" bun-bottom":d.part==="top"?" bun-top":"";if(d.part)return`<div class="bm-stack-slice bun${h}"><span class="bm-stack-slice-label">${i(N(a,d.id))}${m}</span></div>`;const r=B(a,d.id);return`<div class="bm-stack-slice bm-fill" style="${be(d.id,r)}" title="${i(d.id)}"><span class="bm-stack-slice-label">${i(N(a,d.id))}</span></div>`}).join("")}
        </div>
      </div>
      <p class="muted small bm-stack-meta">共 ${e.stack.length} 层${S(e.stack,s)?"":" · ⚠️ 缺少汉堡面包"}</p>
    `}function K(){const a=Math.min(600,20+e.stack.length*20),s=!!e.loadedProductAssetPath,c=e.loadedRecipeName,u=e.loadedNameZh,d=e.loadedNameEn,m=e.loadedScore>0?e.loadedScore:a,h=s&&e.loadedProductAssetPath?`/api/custom-recipes/icon?assetPath=${encodeURIComponent(e.loadedProductAssetPath)}`:"";return`
      <details class="bm-props-details" open>
        <summary class="bm-props-summary">📝 成品信息${s?' <span class="muted small">（编辑模式）</span>':""}</summary>
        <div class="bm-props-body">
          <div class="bm-def-row"><span class="muted">关卡集</span>
            <select id="bm-set" class="rl-select" style="flex:1">
              ${o.map(r=>`<option value="${i(r.setName)}" ${r.setName===e.setName?"selected":""}>${i(r.levelSetNameZH||r.setName)}（${i(r.setName)}）</option>`).join("")}
            </select>
          </div>
          <div class="bm-def-row"><span class="muted">分类</span><input id="bm-category" class="rl-select" style="flex:1" value="${i(e.category)}" placeholder="关卡集内分类 id，如 burger_local"></div>
          <div class="bm-def-row"><span class="muted">标识</span><input id="bm-name" class="rl-select" style="flex:1" value="${i(c)}" placeholder="如 CheeseChickenBurger（字母数字下划线）"></div>
          <div class="bm-def-row"><span class="muted">中文名</span><input id="bm-name-zh" class="rl-select" style="flex:1" value="${i(u)}" placeholder="如 芝士鸡肉汉堡"></div>
          <div class="bm-def-row"><span class="muted">英文名</span><input id="bm-name-en" class="rl-select" style="flex:1" value="${i(d)}" placeholder="默认同标识"></div>
          <div class="bm-def-row"><span class="muted">分数</span><input id="bm-score" type="number" class="rl-select" style="width:90px" value="${m}"><span class="muted small">建议 ${a}</span></div>
          <div class="bm-def-row bm-icon-row"><span class="muted">图标</span>
            <div style="flex:1;display:flex;align-items:center;gap:8px;flex-wrap:wrap">
              <input type="file" id="bm-icon" accept="image/png,image/jpeg" class="rl-select" style="flex:1;min-width:160px">
              ${h?`<img id="bm-icon-existing" class="food-icon" loading="lazy" src="${i(h)}" alt="" onerror="this.hidden=true">`:""}
              <img id="bm-icon-preview" class="food-icon" hidden alt="">
            </div>
          </div>
          <div class="bm-def-row"><span style="flex:1"></span><button class="m-btn primary" id="bm-save">${s?"💾 保存修改":"🍔 生成汉堡菜谱"}</button></div>
        </div>
      </details>
    `}function Y(a){return new Promise((s,c)=>{const u=new FileReader;u.onload=()=>{const d=u.result,m=d.indexOf(",");s(m>=0?d.substring(m+1):d)},u.onerror=()=>c(new Error("图标读取失败")),u.readAsDataURL(a)})}function G(){return`
      <div class="bm-page">
        <div class="bm-dev-banner mp-status">
          ⚠️ <b>功能开发中</b>：汉堡组装工作台仍在完善，部分交互与生成结果可能变动，请以 Unity 写回后的资产为准。
        </div>
        <div class="bm-main-card">
          <div class="bm-toolbar">
            <label class="bm-toolbar-label bm-toolbar-grow">
              <span class="muted small">载入已有汉堡</span>
              <select id="bm-load-product" class="rl-select">
                <option value="">选择已有汉堡改层另存…</option>${l.products.filter(s=>S(s.compositionIds??[],w())).map(s=>`<option value="${i(s.id)}">${i(s.nameZh)}（${s.compositionIds.length} 层 · ${s.score} 分）</option>`).join("")}
              </select>
            </label>
            ${e.loadedProductId?'<button type="button" class="m-btn small" id="bm-clear-loaded">清除载入</button>':""}
          </div>
          <h3 class="bm-section-title">🍔 堆叠编辑</h3>
          ${F()}
          <div class="bm-actions-row">
            <button type="button" class="m-btn primary" id="bm-open-cand">+ 添加层</button>
          </div>
          <hr class="bm-divider">
          ${K()}
        </div>
      </div>
    `}function J(a=document){a.querySelectorAll(".bm-remove").forEach(s=>s.addEventListener("click",()=>{e.stack.splice(parseInt(s.dataset.i,10),1),y()})),a.querySelectorAll(".bm-up").forEach(s=>s.addEventListener("click",()=>{const c=parseInt(s.dataset.i,10);c>0&&([e.stack[c-1],e.stack[c]]=[e.stack[c],e.stack[c-1]],y())})),a.querySelectorAll(".bm-down").forEach(s=>s.addEventListener("click",()=>{const c=parseInt(s.dataset.i,10);c<e.stack.length-1&&([e.stack[c+1],e.stack[c]]=[e.stack[c],e.stack[c+1]],y())}))}function Q(){var d,m;ce("添加堆叠层",`<p class="modal-hint">点击卡片加入堆叠，可连续添加多个；汉堡皮（ChoppedBun）在预览中显示为上下两片。</p>
       <input type="search" id="bm-cand-search" class="rl-search" placeholder="搜索名称 / 英文名 / ID…" autocomplete="off" style="width:100%;margin-bottom:8px">
       <div class="modal-scroll bm-pick-scroll">
         <div id="bm-cand-list">${W()}</div>
       </div>`,`<span class="muted" id="bm-pick-count">当前堆叠 ${e.stack.length} 层</span>
       <button type="button" class="m-btn primary" data-cancel>完成</button>`),(d=document.querySelector("[data-cancel]"))==null||d.addEventListener("click",()=>{de(),y()});const a=document.querySelector(".modal-panel");a&&(a.classList.add("wide"),a.classList.add("bm-pick-panel"));const s=document.getElementById("bm-cand-list"),c=document.getElementById("bm-pick-count"),u=()=>{c&&(c.textContent=`当前堆叠 ${e.stack.length} 层`)};s.addEventListener("click",h=>{const r=h.target.closest(".bm-pick-card");if(!(r!=null&&r.dataset.id))return;e.stack.push(r.dataset.id),u();const p=z(l);v(!0,`已添加「${N(p,r.dataset.id)}」`),r.classList.add("selected"),window.setTimeout(()=>r.classList.remove("selected"),500)}),(m=document.getElementById("bm-cand-search"))==null||m.addEventListener("input",h=>{const r=h.target.value.trim().toLowerCase();s.querySelectorAll(".bm-pick-section").forEach(p=>{let f=0;p.querySelectorAll(".bm-pick-card").forEach(b=>{const $=!r||(b.dataset.name??"").includes(r);b.hidden=!$,$&&f++}),p.hidden=f===0})})}function U(a){const s=a.compositionIds??[];return S(s,w())?(e.stack=s.slice(),e.loadedProductId=a.id,e.loadedProductAssetPath=a.assetPath??"",e.loadedRecipeName=a.recipeName||a.id,e.loadedNameZh=a.nameZh,e.loadedNameEn=a.recipeName||a.id,e.loadedScore=a.score,!0):(v(!1,`「${a.nameZh}」的组成中没有汉堡面包，无法载入。`),!1)}function X(){var s,c,u,d,m,h,r;(s=document.getElementById("bm-set"))==null||s.addEventListener("change",p=>{(async()=>{var f;e.setName=p.target.value,sessionStorage.setItem(D,e.setName),x("切换关卡集…");try{l=await Z(e.setName),e.defPath=((f=g())==null?void 0:f.assetPath)??"",e.stack=[],e.loadedProductId="",e.loadedProductAssetPath="",e.loadedRecipeName="",e.loadedNameZh="",e.loadedNameEn="",e.loadedScore=0,y()}catch(b){q(b)}finally{P()}})()}),(c=document.getElementById("bm-category"))==null||c.addEventListener("change",p=>{e.category=p.target.value.trim()||"burger_local"});let a="";(u=document.getElementById("bm-icon"))==null||u.addEventListener("change",p=>{var $;const f=($=p.target.files)==null?void 0:$[0],b=document.getElementById("bm-icon-preview");if(a&&(URL.revokeObjectURL(a),a=""),!!b){if(!f){b.hidden=!0,b.removeAttribute("src");return}a=URL.createObjectURL(f),b.src=a,b.hidden=!1}}),(d=document.getElementById("bm-open-cand"))==null||d.addEventListener("click",()=>Q()),(m=document.getElementById("bm-load-product"))==null||m.addEventListener("change",p=>{const f=p.target.value;if(!f)return;const b=l.products.find($=>$.id===f);b&&U(b)&&(v(!0,`已载入「${b.nameZh}」的 ${e.stack.length} 层堆叠，修改后点「保存修改」更新成品菜谱。`),y())}),(h=document.getElementById("bm-clear-loaded"))==null||h.addEventListener("click",()=>{e.loadedProductId="",e.loadedProductAssetPath="",e.loadedRecipeName="",e.loadedNameZh="",e.loadedNameEn="",e.loadedScore=0,e.stack=[],v(!0,"已清除载入，可重新堆叠新汉堡。"),y()}),J(),(r=document.getElementById("bm-save"))==null||r.addEventListener("click",()=>{(async()=>{var M;const p=document.getElementById("bm-name").value.trim(),f=document.getElementById("bm-name-zh").value.trim(),b=document.getElementById("bm-name-en").value.trim(),$=parseInt(document.getElementById("bm-score").value,10)||0;if(!/^[A-Za-z0-9_]+$/.test(p)){v(!1,"标识只能包含字母、数字和下划线。");return}if(e.stack.length===0){v(!1,"至少添加一层。");return}if(!S(e.stack,w())){v(!1,"堆叠中至少包含一层汉堡面包。");return}let _="";const E=(M=document.getElementById("bm-icon").files)==null?void 0:M[0];E&&(_=await Y(E));try{const I=!!e.loadedProductAssetPath;x(I?"保存汉堡修改…":"生成汉堡菜谱…");const k=await ne({setName:e.setName,category:document.getElementById("bm-category").value.trim()||"burger_local",definitionAssetPath:e.defPath,recipeName:p,nameZh:f,nameEn:b,score:$,layerIds:e.stack,updateAssetPath:e.loadedProductAssetPath||void 0});let A=k.iconError??"";const R=k.assetPath;if(E&&R)try{await oe(e.setName,R,E.name,_)}catch(T){A=T.message??String(T)}l=await Z(e.setName);const j=g();j&&(e.defPath=j.assetPath),I?(e.loadedRecipeName=p,e.loadedNameZh=f,e.loadedNameEn=b,e.loadedScore=$):(e.stack=[],e.loadedProductId="",e.loadedProductAssetPath="",e.loadedRecipeName="",e.loadedNameZh="",e.loadedNameEn="",e.loadedScore=0);const ee=A?`；⚠️ 图标未写入：${A}`:E&&R?"；订单图标已保存":"";v(!0,(k.updated?"✅ 已保存「":"✅ 汉堡「")+(f||p)+(k.updated?"」的修改":"」已生成到关卡集「"+e.setName+"」")+(k.updated?"":`（uID ${k.uID}）`)+ee),y()}catch(I){q(I)}finally{P()}})()})}function y(){n.innerHTML=G(),X()}y();const C=sessionStorage.getItem(H);if(C){sessionStorage.removeItem(H);const a=l.products.find(s=>s.assetPath===C)??l.products.find(s=>C.endsWith("/"+s.id+".asset"));a&&U(a)?(v(!0,`已从自定义菜谱打开「${a.nameZh}」，修改后点「保存修改」。`),y()):v(!1,"无法在汉堡工作台载入该菜谱（请确认属于当前关卡集且为成品汉堡）。")}}export{he as renderBurgerMakerView};
