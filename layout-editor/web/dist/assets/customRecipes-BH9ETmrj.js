const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./modelPreview-TaBeyhEE.js","./main-DxdCj_ag.js","./recipeCard-CQ0LDvU6.js","./recipeCard-BUt9hWWn.css"])))=>i.map(i=>d[i]);
import{h as z,w as Le,s as J,o as ce,c as j,_ as Ce}from"./main-DxdCj_ag.js";import{a as yt,n as vt,w as ht,a0 as ye,a1 as xe,a2 as ct,j as at,a3 as $t,q as st,i as Et,a4 as It,a5 as wt,a6 as kt,a7 as Bt,a8 as Lt,a9 as Ct,aa as xt,ab as ot,ac as St,f as Mt}from"./recipeCard-CQ0LDvU6.js";function s(i){return String(i??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}const he=/^[A-Za-z_][A-Za-z0-9_]*$/;function B(i,c=!0){const o=document.getElementById("cr-status");o&&(o.textContent=i,o.classList.toggle("err",!c),o.classList.toggle("ok",c&&i.length>0))}function Me(i,c){return document.body.classList.add("manage-bg"),i.innerHTML=`
    ${vt("custom-recipes")}
    <div class="manage-bar">
      <h1 class="m-title">${s(c)}</h1>
      <span class="status" id="cr-status"></span>
      <span style="flex:1"></span>
    </div>
    <div class="cr-warn-banner">⚠️ 自定义菜谱功能开发中，请勿在正式关卡中使用</div>
    <div class="manage-content" id="cr-content"></div>
  `,ht(o=>{o==="layout"?(location.hash="#/layout",location.reload()):o==="manage"?(location.hash="#/manage",location.reload()):o==="recipes"&&(location.href="/recipes")}),document.getElementById("cr-content")}async function Rt(i){const c=Me(i,"自定义菜谱管理");Re("加载关卡集…");let o=[];try{o=await yt()}catch(u){ve(u);return}B(`共 ${o.length} 个关卡集`),c.innerHTML=`
    <div class="m-section-title">选择关卡集</div>
    <p class="modal-hint">选择要管理自定义菜谱的关卡集。首次进入会自动初始化配置。</p>
    <div class="m-grid">${o.map(u=>`
      <div class="m-card">
        <h3>${s(u.levelSetNameZH||u.setName)} <span class="muted">(${s(u.levelSetName||u.setName)})</span></h3>
        <div class="m-meta">
          标识：${s(u.setName)} · 关卡数：${u.levelCount}<br>
          作者：${s(u.author||"—")} · 版本：${s(u.version||"—")}
        </div>
        <div class="m-actions">
          <button class="m-btn primary" data-open="${s(u.setName)}">管理菜谱</button>
        </div>
      </div>`).join("")||'<p class="muted">暂无关卡集</p>'}
    </div>
  `,c.querySelectorAll("[data-open]").forEach(u=>u.addEventListener("click",()=>void ue(i,u.dataset.open)))}function Re(i){const c=document.getElementById("cr-content");c&&(c.innerHTML=`<p class="muted">${s(i)}</p>`),B(i)}function ve(i){const c=i instanceof Error?i.message:String(i);B(c,!1);const o=document.getElementById("cr-content");o&&(o.innerHTML=`<div class="m-block"><h3>出错</h3><p>${s(c)}</p></div>`)}function be(i){return`/api/custom-recipes/icon?assetPath=${encodeURIComponent(i.assetPath)}`}function tt(i,c){return`<img class="food-icon" loading="lazy" src="${c?`/icons/${i}/${encodeURIComponent(c)}.png`:"/icons/_placeholder.png"}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`}function nt(i){const c=i.replace(/\/[^/]+\.asset$/,"")+"/models";return`/api/custom-recipes/model-files/${btoa(unescape(encodeURIComponent(c))).replace(/\+/g,"-").replace(/\//g,"_").replace(/=+$/,"")}/`}const ne={url:"/api/custom-recipes/reference-model?path="+encodeURIComponent("Assets/common01/food/CustomRecipes/Pizza/models/plated_mushroom_01.prefab"),format:"obj",scale:.7};async function it(i,c,o){try{const u=await Ct(i),f=u.find(g=>/\.(fbx|obj)$/i.test(g));if(!f){alert("该菜谱尚未上传模型文件。");return}const{openModelPreview:w}=await Ce(async()=>{const{openModelPreview:g}=await import("./modelPreview-TaBeyhEE.js");return{openModelPreview:g}},__vite__mapDeps([0,1,2,3]),import.meta.url),b=u.filter(g=>/\.(png|jpg|jpeg)$/i.test(g)).map(g=>nt(i)+encodeURIComponent(g));w({title:c,resourceBase:nt(i),modelFileName:f,scale:o==null?void 0:o.scale,rotationY:o==null?void 0:o.rotationY,onAdjust:o==null?void 0:o.onAdjust,remoteTextures:b,referenceUrl:ne.url,referenceFormat:ne.format,referenceScale:ne.scale})}catch(u){alert(u.message||"模型预览加载失败。")}}function lt(i){return{guid:i.guid,id:i.id,nameZh:i.nameZh,nameEn:i.nameEn||void 0,assetPath:i.assetPath,cookingStep:i.cookingStepId||void 0,ingredients:i.ingredients,compositionIds:i.compositionIds,score:i.score,isCustom:!0,intermediate:i.intermediate,group:i.group,type:"custom",cookingGroups:i.cookingGroups}}async function ue(i,c){var me,K;const o=Me(i,`自定义菜谱 · ${s(c)}`);Re("加载菜谱配置…");let u,f,w=new Map,b=new Map;try{[u,f]=await Promise.all([ye(c),xe(c)]);const m=await ct(c).catch(()=>null);for(const k of(m==null?void 0:m.platingContainers)??[])b.set(k.id,k.nameZh||k.id);let v=!1;const d=await at().catch(()=>(v=!0,[]));for(const k of d)w.set(k.id,k.nameZh);v&&B("⚠️ 食材数据加载失败（/api/catalog/ingredients），卡片可能缺少食材图标")}catch(m){ve(m);return}B(`${f.length} 个菜谱 · UID前缀：${u.uidPrefix}`);const g=u.categories??[];function I(m){return m.zh||m.id}let r="",P="",M="all";function ae(m){return M===m?" active":""}function G(m){const v=lt(m);return!v.ingredients&&v.compositionIds&&(v.ingredients=v.compositionIds.filter(d=>w.has(d))),v}function U(){let m=r?f.filter(d=>d.category===r):f;M==="done"?m=m.filter(d=>!d.intermediate):M==="half"&&(m=m.filter(d=>d.intermediate));const v=P.trim().toLowerCase();return v&&(m=m.filter(d=>[d.nameZh,d.nameEn??"",d.id,...d.ingredients??[]].join(" ").toLowerCase().includes(v))),m}function R(){const m=U();return m.length===0?f.length===0&&P===""&&M==="all"&&r===""?((async()=>{const d=document.getElementById("cr-grid");if(!(!d||d.dataset.diag)){d.dataset.diag="1";try{const k=await $t(c).catch(()=>null);if(!k){d.innerHTML=`<div class="m-block">
                <h3>暂无菜谱</h3>
                <p class="muted">桥接不支持诊断接口（旧版本）。若磁盘 <code>Assets/LevelSets/${s(c)}/custom_recipes/</code> 下已有菜谱，请：
                <b>在 Unity 中 Tools → Layout Editor → 停止服务 → 启动服务</b>（或重启 Unity 重新编译），再刷新本页。</p>
              </div>`;return}const le=k.dirExists?`磁盘上检测到 <b>${k.fsAssets.length}</b> 个 .asset 文件，扫描命中 <b>${k.scannedCount}</b> 个自定义菜谱，成功加载 <b>${k.loadedCount}</b> 个。`:"目录不存在，尚未创建任何菜谱。",W=k.scannedCount>0&&k.loadedCount===0?'<p class="mp-status err">⚠️ 文件存在但资产加载失败（CustomRecipeSO 类型未加载）。请重启 Unity 并确认 Console 无编译错误。</p>':k.scannedCount===0&&k.fsAssets.length>0?'<p class="mp-status err">⚠️ 文件系统有资产但脚本 guid 不匹配（可能是旧版脚本/副本）。</p>':"";d.innerHTML=`<div class="m-block">
              <h3>暂无菜谱</h3>
              <p class="muted">${le}</p>
              ${W}
              <p class="muted small" style="margin-top:8px">点击右上角「+ 新建菜谱」开始创建；若已创建但未显示，请重启 Unity 桥（Tools → Layout Editor → 停止服务 → 启动服务）后刷新。</p>
            </div>`}catch{d.innerHTML='<div class="m-block"><h3>暂无菜谱</h3><p class="muted">点击右上角「+ 新建菜谱」开始创建。</p></div>'}}})(),'<p class="muted">加载中…</p>'):'<p class="muted">没有匹配的菜谱。点击右上角「+ 新建菜谱」开始创建。</p>':`<div class="rl-grid">${m.map(d=>{let k;try{k=st(G(d),{allRecipes:f.map(G),ingredientName:N=>w.get(N)??N,iconSrc:()=>be(d)})}catch(N){k=`<div class="m-card"><h3>${s(d.nameZh)}</h3><p class="muted">卡片渲染失败：${s(N.message)}</p></div>`}const le=g.find(N=>N.id===d.category),W=d.platingStepId?b.get(d.platingStepId):"",ee=(d.compositionIds??[]).length;return`
      <div class="cr-card-wrap">
        <div class="cr-card-inner">${k}</div>
        <div class="cr-card-foot">
          <span class="cr-cat-tag">${s(I(le??{id:d.category,zh:d.category,en:d.category}))}</span>
          ${W?`<span class="cr-cat-tag cr-plate-tag" title="装盘容器">🍽 ${s(W)}</span>`:""}
          ${d.intermediate?'<span class="cr-cat-tag cr-half-tag">中间产物</span>':""}
          <span class="muted small">UID ${d.uID} · 组成 ${ee} 项</span>
          <span style="flex:1"></span>
          ${d.hasModel?`<button class="m-btn small" data-preview="${s(d.assetPath)}" title="3D 模型在线预览">👁</button>`:""}
          <button class="m-btn small" data-edit="${s(d.assetPath)}">编辑</button>
          <button class="m-btn small danger" data-del="${s(d.assetPath)}">删除</button>
        </div>
      </div>`}).join("")}</div>`}function se(){return`
    <div class="cr-sidebar">
      <div class="m-section-title">分类</div>
      <div class="cr-cat-list">
        <button class="m-btn cr-cat-item${r===""?" primary":""}" data-cat="">全部 (${f.length})</button>
        ${g.map(m=>{const v=f.filter(d=>d.category===m.id).length;return`<button class="m-btn cr-cat-item${r===m.id?" primary":""}" data-cat="${s(m.id)}">${s(I(m))} (${v})</button>`}).join("")}
        <div class="cr-cat-actions">
          <button class="m-btn" id="cr-new-cat">+ 新建分类</button>
          ${g.length>0?'<button class="m-btn" id="cr-manage-cat">管理分类</button>':""}
        </div>
      </div>
    </div>`}o.innerHTML=`
    <div class="m-actions-row">
      <button class="m-btn" id="cr-back">← 返回关卡集列表</button>
      <span class="muted">当前关卡集：<b>${s(c)}</b></span>
      <span style="flex:1"></span>
      <button class="m-btn primary" id="cr-new-recipe">+ 新建菜谱</button>
    </div>
    <div class="cr-toolbar">
      <input type="search" id="cr-search" class="rl-search" placeholder="搜索菜名 / ID / 食材…" autocomplete="off">
      <div class="ing-groups">
        <button type="button" class="cr-comp-chip${ae("all")}" data-role="all">全部</button>
        <button type="button" class="cr-comp-chip${ae("done")}" data-role="done">成品</button>
        <button type="button" class="cr-comp-chip${ae("half")}" data-role="half">中间产物</button>
      </div>
    </div>
    <div class="cr-layout">
      <div id="cr-sidebar">${se()}</div>
      <div id="cr-grid">${R()}</div>
    </div>
  `;function oe(){(async()=>{J("加载…");try{[u,f]=await Promise.all([ye(c),xe(c)])}catch(m){ve(m);return}finally{z()}B(`${f.length} 个菜谱 · UID前缀：${u.uidPrefix}`),document.getElementById("cr-sidebar").innerHTML=se(),ie(),document.getElementById("cr-grid").innerHTML=R(),D()})()}function $e(){var m;(m=document.getElementById("cr-search"))==null||m.addEventListener("input",v=>{P=v.target.value,document.getElementById("cr-grid").innerHTML=R(),D()}),document.querySelectorAll("[data-role]").forEach(v=>{v.addEventListener("click",()=>{M=v.dataset.role??"all",document.querySelectorAll("[data-role]").forEach(d=>d.classList.toggle("active",d===v)),document.getElementById("cr-grid").innerHTML=R(),D()})})}function ie(){var m,v;document.querySelectorAll(".cr-cat-item").forEach(d=>{d.addEventListener("click",()=>{r=d.dataset.cat??"",document.getElementById("cr-sidebar").innerHTML=se(),ie(),document.getElementById("cr-grid").innerHTML=R(),D()})}),(m=document.getElementById("cr-new-cat"))==null||m.addEventListener("click",()=>Tt(c,d=>{r=d??"",ue(i,c)})),(v=document.getElementById("cr-manage-cat"))==null||v.addEventListener("click",()=>Dt(c,u.categories,()=>void ue(i,c)))}function D(){document.querySelectorAll("[data-edit]").forEach(m=>m.addEventListener("click",()=>void Se(i,c,m.dataset.edit))),document.querySelectorAll("[data-preview]").forEach(m=>m.addEventListener("click",()=>{const v=m.dataset.preview;it(v,v.split("/").pop()??v)})),document.querySelectorAll("[data-del]").forEach(m=>m.addEventListener("click",()=>_t(i,c,m.dataset.del,oe)))}$e(),ie(),D(),(me=document.getElementById("cr-back"))==null||me.addEventListener("click",()=>void Rt(i)),(K=document.getElementById("cr-new-recipe"))==null||K.addEventListener("click",()=>void Se(i,c,null))}async function Se(i,c,o,u){var He,qe,Ae,Fe,Ze,ze,Ne,Oe,Ve,Xe,Ye,Ge,We,Qe,Je;const f=o!=null,w=Me(i,f?"编辑菜谱":"新建菜谱");Re("加载参考数据…");let b=[],g,I,r,P=[],M=[];const ae={cookingSteps:[],platingSteps:[],platingContainers:[],icons:[],reusableModels:[],ingredients:[]};try{[b,g,I]=await Promise.all([at().catch(()=>[]),ct(c).catch(()=>ae),ye(c)]),[P,M]=await Promise.all([xe(c).catch(()=>[]),Et(c).catch(()=>[])]),f&&(r=P.find(e=>e.assetPath===o))}catch(e){ve(e),z();return}z();const G=new Set;for(const e of P)G.add(e.id);for(const e of M)e.isCustom&&G.add(e.id);const U=new Map,R=new Map;for(const e of(r==null?void 0:r.compositionIds)??[]){const t=G.has(e)?R:U;t.set(e,(t.get(e)??0)+1)}const se=!f,oe=(r==null?void 0:r.recipeName)??"",$e=(r==null?void 0:r.nameZh)??"",ie=(r==null?void 0:r.nameEn)??"",D=(r==null?void 0:r.category)??(u==null?void 0:u.category)??(((He=I.categories)==null?void 0:He.length)>0?I.categories[0].id:""),me=(r==null?void 0:r.score)??(u==null?void 0:u.score)??0,K=(r==null?void 0:r.type)??((u==null?void 0:u.score)===0,"Cooked"),m=(r==null?void 0:r.cookingStepId)??"",v="",d=(r==null?void 0:r.platingStepId)??"",k="",le="",W=I.categories??[],ee=new Map;for(const e of b)ee.set(e.id,e);const N=b.length===0,_e=(()=>{const e=[];for(const n of P)f&&n.assetPath===o||e.push({id:n.id,nameZh:n.nameZh,nameEn:n.nameEn,score:n.score,cookingStepId:n.cookingStepId,hasIcon:n.hasIcon,assetPath:n.assetPath,official:!1,ingredients:n.ingredients});const t=new Set(e.map(n=>n.id));for(const n of M)!n.isCustom||(n.group??"")==="levelset"||t.has(n.id)||(e.push({id:n.id,nameZh:n.nameZh,nameEn:n.nameEn,score:n.score??0,cookingStepId:n.cookingStep??"",hasIcon:!!n.icon,assetPath:n.assetPath,official:!0,ingredients:n.ingredients??[]}),t.add(n.id));return e.sort((n,l)=>n.score-l.score||Number(n.official)-Number(l.official)||n.nameZh.localeCompare(l.nameZh,"zh")),e})(),pe=new Map;for(const e of _e)pe.set(e.id,e);const dt=[...P.map(lt),...M.filter(e=>e.isCustom).map(e=>({...e,isCustom:!0}))];function ut(e){return e.zh||e.id}function mt(){let e=W.map(t=>`<option value="${s(t.id)}" ${t.id===D?"selected":""}>${s(ut(t))}</option>`).join("");return!W.some(t=>t.id===D)&&D&&(e+=`<option value="${s(D)}" selected>${s(D)}</option>`),`<select id="cr-type-cat" class="m-select">${e}</select>`}function re(e,t,n){const l=new Map;for(const a of e)l.has(a.id)||l.set(a.id,a.nameZh||a.id);return`<select id="${n}" class="m-select">
      <option value="">— 不设置 —</option>
      ${[...l.entries()].map(([a,p])=>`<option value="${s(a)}" ${a===t?"selected":""}>${s(p)} (${s(a)})</option>`).join("")}
    </select>`}function Ee(){const e=[];for(const[t,n]of U)for(let l=0;l<n;l++)e.push(t);for(const[t,n]of R)for(let l=0;l<n;l++)e.push(t);return e}function Ie(e,t,n){const l=n>0,a=l?`<div class="cp-count">
          <button type="button" class="cp-step" data-cpdec data-cpid="${s(e)}" data-cpsub="${t?1:0}">−</button>
          <span class="cp-num">${n}</span>
          <button type="button" class="cp-step" data-cpinc data-cpid="${s(e)}" data-cpsub="${t?1:0}">＋</button>
        </div>`:"";if(t){const h=pe.get(e);if(!h)return"";const _=h.score<=0?'<span class="cr-badge-half">中间产物</span>':'<span class="cr-badge-done">成品菜 · 可作组成</span>',q=h.official?`/icons/recipes/${encodeURIComponent(h.id)}.png`:be(h);return`<div class="pick-card cp-card${l?" selected":""}" data-cpid="${s(e)}" data-cpsub="1" title="${s(h.id)}">
        <span class="pc-head"><img class="food-icon" loading="lazy" src="${s(q)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'" /><span class="pc-name">${s(h.nameZh)}${_}${h.nameEn?` <span class="muted pc-en">${s(h.nameEn)}</span>`:""}</span></span>
        <span class="muted small">${h.cookingStepId?s(h.cookingStepId):"无烹饪步骤"} · ${(h.ingredients??[]).length} 种食材${h.official?" · 官方":(h.score<=0," · 本关卡集")}</span>
        ${a}
      </div>`}const p=ee.get(e);if(!p)return"";const E=p.group&&p.group!=="core"?` <span class="pc-badge">${Mt(p.group)}</span>`:"",x=p.nameEn&&p.nameEn.trim()||"";return`<div class="pick-card cp-card${l?" selected":""}" data-cpid="${s(e)}" data-cpsub="0" title="${s(p.id)}">
      <span class="pc-head">${tt("ingredients",p.id)}<span class="pc-name">${s(p.nameZh)}${E}${x?` <span class="muted pc-en">${x}</span>`:""}</span></span>
      ${a}
    </div>`}function pt(){var _,q;const e=new Map;for(const[$,L]of U)e.set($,L);for(const[$,L]of R)e.set($,L);let t="all",n="";function l(){const $=n.trim().toLowerCase(),L=[];if(t==="all"||t==="ing")for(const y of b)$&&!y.nameZh.toLowerCase().includes($)&&!(y.nameEn??"").toLowerCase().includes($)&&!y.id.toLowerCase().includes($)||L.push({id:y.id,isSub:!1});const S=_e.filter(y=>t==="all"||(t==="sub"?y.score<=0:y.score>0));for(const y of S)$&&!y.nameZh.toLowerCase().includes($)&&!(y.nameEn??"").toLowerCase().includes($)&&!y.id.toLowerCase().includes($)||L.push({id:y.id,isSub:!0});return L}function a(){const $=l();return`
        <div class="cr-comp-toolbar">
          <input type="search" id="cp-search" class="ing-search" placeholder="搜索名称 / ID…" autocomplete="off">
          <div class="ing-groups">${[`<button type="button" class="cr-comp-chip${t==="all"?" active":""}" data-filter="all">全部</button>`,`<button type="button" class="cr-comp-chip${t==="ing"?" active":""}" data-filter="ing">食材</button>`,`<button type="button" class="cr-comp-chip${t==="sub"?" active":""}" data-filter="sub">中间产物</button>`,`<button type="button" class="cr-comp-chip${t==="done"?" active":""}" data-filter="done">成品菜</button>`].join("")}</div>
        </div>
        ${N&&t!=="sub"&&t!=="done"?'<p class="mp-status err">⚠️ 未加载到食材数据（桥接 /api/catalog/ingredients 异常），请刷新重试或检查 Unity 桥。</p>':""}
        <div class="modal-scroll" id="cp-scroll">
          <div class="pick-grid" id="cp-grid">${$.map(y=>Ie(y.id,y.isSub,e.get(y.id)??0)).join("")}</div>
          ${$.length?"":'<p class="muted">没有匹配的项</p>'}
        </div>`}ce("添加食材 / 菜谱",`<p class="modal-hint">点击卡片加入（默认 1 份），再次点击 − / ＋ 调整数量；同一项可多次使用。<b>任何菜谱（成品菜或中间产物）都能作为本菜谱的组成</b>，如鸡蛋汉堡 = 煎蛋（成品菜）+ 面包。</p>
       <div id="cp-body">${a()}</div>`,`<button type="button" class="m-btn" data-cancel>取消</button>
       <button type="button" class="m-btn primary" data-ok>确定</button>`);const p=document.querySelector(".modal-panel");p&&p.classList.add("wide");const E=document.getElementById("cp-body");function x(){E.innerHTML=a(),h()}function h(){var $,L;($=document.getElementById("cp-search"))==null||$.addEventListener("input",S=>{n=S.target.value;const y=document.getElementById("cp-grid");if(y){y.innerHTML=l().map(C=>Ie(C.id,C.isSub,e.get(C.id)??0)).join("");const T=document.getElementById("cp-scroll");if(T){const C=T.querySelector("p.muted");C&&C.remove(),l().length===0&&T.insertAdjacentHTML("beforeend",'<p class="muted">没有匹配的项</p>')}}}),document.querySelectorAll("#cp-body .cr-comp-chip").forEach(S=>{S.addEventListener("click",()=>{t=S.dataset.filter??"all",x()})}),(L=document.getElementById("cp-grid"))==null||L.addEventListener("click",S=>{const y=S.target,T=y.closest(".cp-step"),C=y.closest(".cp-card");if(!C)return;const F=C.dataset.cpid,fe=C.dataset.cpsub==="1";let Z=e.get(F)??0;if(T){const ge=T.dataset.cpinc!==void 0?1:-1;Z=Math.max(0,Z+ge)}else Z=Z>0?0:1;Z<=0?e.delete(F):e.set(F,Z),C.outerHTML=Ie(F,fe,Z)})}h(),(_=document.querySelector("[data-cancel]"))==null||_.addEventListener("click",j),(q=document.querySelector("[data-ok]"))==null||q.addEventListener("click",()=>{U.clear(),R.clear();for(const[$,L]of e)(G.has($)?R:U).set($,L);j(),we(),H()})}function we(){const e=document.getElementById("cr-comp-list");if(!e)return;const t=[];for(const[a,p]of U){const E=ee.get(a);t.push(`<div class="cr-comp-row" data-rowid="ing:${s(a)}">
        ${tt("ingredients",a)}
        <span class="cr-row-name">${s((E==null?void 0:E.nameZh)??a)}</span>
        <span class="cr-row-stepper">
          <button type="button" class="cr-step" data-rowdec="ing:${s(a)}">−</button>
          <span class="cr-step-num">${p}</span>
          <button type="button" class="cr-step" data-rowinc="ing:${s(a)}">＋</button>
        </span>
        <button type="button" class="cr-step-del" data-rowdel="ing:${s(a)}" title="移除">×</button>
      </div>`)}for(const[a,p]of R){const E=pe.get(a),x=((E==null?void 0:E.score)??0)<=0?'<span class="cr-chip-tag">中间产物</span>':'<span class="cr-chip-tag">成品菜</span>';t.push(`<div class="cr-comp-row cr-comp-row-sub" data-rowid="sub:${s(a)}">
        ${E?`<img class="food-icon" loading="lazy" src="${s(E.official?`/icons/recipes/${encodeURIComponent(E.id)}.png`:be(E))}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'" />`:""}
        <span class="cr-row-name">${s((E==null?void 0:E.nameZh)??a)}${x}</span>
        <span class="cr-row-stepper">
          <button type="button" class="cr-step" data-rowdec="sub:${s(a)}">−</button>
          <span class="cr-step-num">${p}</span>
          <button type="button" class="cr-step" data-rowinc="sub:${s(a)}">＋</button>
        </span>
        <button type="button" class="cr-step-del" data-rowdel="sub:${s(a)}" title="移除">×</button>
      </div>`)}e.innerHTML=t.length?t.join(""):'<p class="muted small" style="margin:4px 0">尚未选择。点击下方「添加」选择食材或中间产物。</p>';const n=Ee().length,l=document.getElementById("cr-comp-hint");l&&(l.textContent=t.length?`共 ${n} 份组成（食材 ${[...U.values()].reduce((a,p)=>a+p,0)} · 中间产物 ${[...R.values()].reduce((a,p)=>a+p,0)}）`:""),e.querySelectorAll("[data-rowinc], [data-rowdec], [data-rowdel]").forEach(a=>{a.addEventListener("click",()=>{const p=a.dataset.rowinc??a.dataset.rowdec??a.dataset.rowdel??"",[E,x]=p.split(":"),h=E==="ing"?U:R,_=h.get(x)??0;a.dataset.rowdel!==void 0?h.delete(x):a.dataset.rowinc!==void 0?h.set(x,_+1):_<=1?h.delete(x):h.set(x,_-1),we(),H()})})}function ft(e){const t=[];for(const n of e){const l=pe.get(n);l&&(l.ingredients??[]).length>0?t.push(...l.ingredients):t.push(n)}return t}let te=null;function H(){var _,q,$,L,S,y;const e=document.getElementById("cr-preview");if(!e)return;const t=((_=document.getElementById("cr-rec-name"))==null?void 0:_.value.trim())??"",n=((q=document.getElementById("cr-zh"))==null?void 0:q.value.trim())??"",l=(($=document.getElementById("cr-en"))==null?void 0:$.value.trim())??"",a=Number((L=document.getElementById("cr-score"))==null?void 0:L.value)||0,E=(((S=document.getElementById("cr-type"))==null?void 0:S.value)??"Composite")==="Composite"?"":((y=document.getElementById("cr-cook-step"))==null?void 0:y.value)??"",x=Ee(),h={guid:"",id:t||"preview",nameZh:n||t||"未命名菜谱",nameEn:l||void 0,assetPath:"",isCustom:!0,group:"levelset",score:a,intermediate:a<=0,ingredients:ft(x),compositionIds:x,cookingStep:E||void 0,type:"custom"};e.innerHTML=st(h,{allRecipes:dt,ingredientName:T=>{var C;return((C=ee.get(T))==null?void 0:C.nameZh)??T},iconSrc:te?()=>te:f&&(r!=null&&r.hasIcon)&&r.assetPath?()=>be(r):void 0})}w.innerHTML=`
    <div class="m-actions-row">
      <button class="m-btn" id="cr-form-back">← 返回菜谱列表</button>
      <span class="muted">关卡集：<b>${s(c)}</b> · ${f?`编辑 ${s(oe)}`:"新建菜谱"}</span>
      <span style="flex:1"></span>
      <button class="m-btn primary" id="cr-form-save">💾 保存</button>
    </div>
    <div class="cr-form">
      <div class="cr-section">
        <div class="m-section-title">组装效果（实时预览）</div>
        <div id="cr-preview" class="cr-preview"></div>
      </div>
      <div class="cr-section">
        <div class="m-section-title">基本信息</div>
        <label class="m-field">标识符 recipeName（仅字母/数字/下划线，创建后不可修改）
          <input type="text" id="cr-rec-name" value="${s(oe)}" ${f?"disabled":""} placeholder="MyRecipe">
        </label>
        <div class="cr-form-grid">
          <label class="m-field">中文名<input type="text" id="cr-zh" value="${s($e)}" placeholder="我的菜谱"></label>
          <label class="m-field">英文名<input type="text" id="cr-en" value="${s(ie)}" placeholder="My Recipe"></label>
          <label class="m-field">分类 ${mt()}
            <button type="button" class="m-btn" id="cr-new-cat-inline" style="margin-top:6px">+ 新建分类</button>
          </label>
          <label class="m-field">类型
            <select id="cr-type" class="m-select">
              <option value="Composite" ${K==="Composite"?"selected":""}>Composite（组合）</option>
              <option value="Cooked" ${K==="Cooked"?"selected":""}>Cooked（烹饪）</option>
              <option value="Mixed" ${K==="Mixed"?"selected":""}>Mixed（搅拌）</option>
            </select>
          </label>
          <label class="m-field">分数<input type="number" id="cr-score" value="${me}" min="0">
            <span class="muted small">0 = 中间产物（不直接上桌，可被其他菜谱引用）</span>
          </label>
          <label class="m-field">UID（自动生成）<input type="text" value="${(r==null?void 0:r.uID)??(se?I.uidPrefix*1e3+I.nextSequence:"—")}" disabled></label>
          <label class="m-field">菜谱图标（PNG，卡片图）<input type="file" id="cr-icon-upload" accept="image/png">
            <span class="muted small">上传后立即在组装预览中显示</span></label>
        </div>
      </div>
      <div class="cr-section">
        <div class="m-section-title">组成（食材 / 菜谱）</div>
        <p class="modal-hint">食材直接选用；<b>其他菜谱（成品菜或中间产物）也可作为本菜谱的组成工序</b>（如鸡蛋汉堡 = 煎蛋 + 面包 + 生菜）。点击「添加」弹出选择器。</p>
        <div class="cr-comp-list" id="cr-comp-list"></div>
        <div class="cr-comp-toolbar" style="margin-top:10px">
          <button type="button" class="m-btn primary" id="cr-add-comp">＋ 添加食材 / 菜谱</button>
          <button type="button" class="m-btn" id="cr-new-sub">＋ 新建中间产物</button>
          <span class="muted small" id="cr-comp-hint" style="margin-left:auto"></span>
        </div>
      </div>
      <div class="cr-section">
        <div class="m-section-title">烹饪与装盘</div>
        <div class="cr-form-grid">
          <label class="m-field" id="cr-cook-step-field">烹饪步骤 ${re(g.cookingSteps,m,"cr-cook-step")}</label>
          <label class="m-field">烹饪图标 ${re(g.icons,v,"cr-cook-icon")}</label>
          <label class="m-field" id="cr-cook-prog-field">烹饪程度
            <select id="cr-cook-prog" class="m-select">
              <option value="0">Raw（生）</option>
              <option value="1" selected>Cooked（熟）</option>
              <option value="2">Burnt（焦）</option>
            </select>
          </label>
          <label class="m-field">装盘容器 ${re((qe=g.platingContainers)!=null&&qe.length?g.platingContainers:(g.platingSteps??[]).filter(e=>e.id==="Plate"||e.id==="Glass"),d,"cr-plate-step")}
            <span class="muted small">决定上桌容器（盘子/杯子），运行时映射为 PlatingStepData</span>
          </label>
          <label class="m-field" id="cr-mix-icon-field" style="display:none">搅拌图标 ${re(g.icons,k,"cr-mix-icon")}</label>
          <label class="m-field" id="cr-mix-prog-field" style="display:none">搅拌程度
            <select id="cr-mix-prog" class="m-select">
              <option value="0">Unmixed（未搅拌）</option>
              <option value="1" selected>Mixed（已搅拌）</option>
              <option value="2">OverMixed（过度搅拌）</option>
            </select>
          </label>
        </div>
      </div>
      <div class="cr-section">
        <div class="m-section-title">模型（3D）</div>
        <p class="modal-hint">上传 <b>FBX 模型</b>（可不含材质，单独上传即可）；需要彩色时<b>补充贴图</b>：<b>base_color 彩色贴图必传</b>，roughness/metallic/normal 可选。保存时贴图会重命名为 <code>{菜名}_base_color.png</code> 等格式，并把 FBX 内部对应的贴图引用名一并改写，Unity 导入即可自动链接贴图（与美术直接拖 FBX + 贴图使用一致）。选择文件后立即打开 3D 预览，调整方向/大小后保存提交。</p>
        <div class="cr-form-grid">
          <label class="m-field">上传 3D 模型（仅 FBX）<input type="file" id="cr-model-file" accept=".fbx">
            <span class="muted small">可单独上传；若预览显示为灰色，说明 FBX 未内嵌贴图，可补充贴图</span></label>
          <label class="m-field">复用已有模型 ${re(g.reusableModels,le,"cr-model-ref")}</label>
          <label class="m-field">模型缩放<input type="number" id="cr-model-scale" value="${(r==null?void 0:r.modelScale)??1}" min="0.01" step="0.05">
            <span class="muted small">实际游戏中的大小（相对原模型尺寸）</span></label>
          <label class="m-field">Y 轴旋转（度）<input type="number" id="cr-model-rot" value="${(r==null?void 0:r.modelRotationY)??0}" step="5">
            <span class="muted small">修正模型朝向（如煎蛋面朝上）</span></label>
          <label class="m-field">在线预览<button type="button" class="m-btn" id="cr-preview-model">👁 预览并调整方向/大小</button>
            <span class="muted small">已保存的模型可随时预览调整；上传新文件则自动预览</span></label>
        </div>
        <div class="m-field cr-tex-field">补充贴图（点击格子选择/替换；base_color 必传）
          <div class="cr-tex-slots">
            <button type="button" class="cr-tex-slot" data-tex="base_color">
              <span class="cr-tex-thumb" id="cr-tex-thumb-base_color">＋</span>
              <span class="cr-tex-name">base_color <b>必传</b></span>
            </button>
            <button type="button" class="cr-tex-slot" data-tex="roughness">
              <span class="cr-tex-thumb" id="cr-tex-thumb-roughness">＋</span>
              <span class="cr-tex-name">roughness</span>
            </button>
            <button type="button" class="cr-tex-slot" data-tex="metallic">
              <span class="cr-tex-thumb" id="cr-tex-thumb-metallic">＋</span>
              <span class="cr-tex-name">metallic</span>
            </button>
            <button type="button" class="cr-tex-slot" data-tex="normal">
              <span class="cr-tex-thumb" id="cr-tex-thumb-normal">＋</span>
              <span class="cr-tex-name">normal</span>
            </button>
          </div>
          <input type="file" id="cr-model-texture" accept=".png,.jpg,.jpeg,image/png,image/jpeg" hidden>
        </div>
      </div>
    </div>
  `,H(),we();function Te(){var p;const e=((p=document.getElementById("cr-type"))==null?void 0:p.value)??"Composite",t=document.getElementById("cr-cook-step-field"),n=document.getElementById("cr-cook-prog-field"),l=document.getElementById("cr-mix-icon-field"),a=document.getElementById("cr-mix-prog-field");t&&(t.style.display=e==="Composite"?"none":""),n&&(n.style.display=e==="Composite"?"none":""),l&&(l.style.display=e==="Mixed"?"":"none"),a&&(a.style.display=e==="Mixed"?"":"none"),H()}(Ae=document.getElementById("cr-add-comp"))==null||Ae.addEventListener("click",()=>pt());const O=document.getElementById("cr-model-scale"),V=document.getElementById("cr-model-rot"),A=document.getElementById("cr-model-file"),Q=document.getElementById("cr-model-texture");let X=null,de="";const Y={};let ke=null;const Pe=(e,t)=>{O&&(O.value=String(Math.round(e*100)/100)),V&&(V.value=String(Math.round(t)))},gt=(e,t,n)=>{const l=/\.([^.]+)$/.exec(n);let a=l?"."+l[1].toLowerCase():".png";return a!==".png"&&a!==".jpg"&&a!==".jpeg"&&(a=".png"),`${e}_${t}${a}`},De=()=>{var n;const e=((n=document.getElementById("cr-rec-name"))==null?void 0:n.value.trim())||"texture",t={};for(const l of Object.keys(Y)){const a=Y[l];a&&(t[l]=gt(e,l,a.file.name))}return t},je=async()=>{if(!X)return null;const{renameFbxTextureRefs:e}=await Ce(async()=>{const{renameFbxTextureRefs:t}=await import("./fbxTextureRename-O8eilyGQ.js");return{renameFbxTextureRefs:t}},[],import.meta.url);return e(X,De())},Be=()=>{var t;const e=(t=A==null?void 0:A.files)==null?void 0:t[0];!e&&!X||Le("正在加载 3D 预览…",async()=>{e&&(X=new Uint8Array(await e.arrayBuffer()),de=e.name);const n=await je();if(!n)return;const l=Y.base_color,{openModelPreview:a}=await Ce(async()=>{const{openModelPreview:p}=await import("./modelPreview-TaBeyhEE.js");return{openModelPreview:p}},__vite__mapDeps([0,1,2,3]),import.meta.url);a({title:de,resourceBase:"",modelFileName:de,localBuffer:n.bytes.buffer.slice(n.bytes.byteOffset,n.bytes.byteOffset+n.bytes.byteLength),localTextures:l?[l.file]:void 0,scale:Number(O==null?void 0:O.value)||1,rotationY:Number(V==null?void 0:V.value)||0,onAdjust:Pe,referenceUrl:ne.url,referenceFormat:ne.format,referenceScale:ne.scale})})};A==null||A.addEventListener("change",Be),document.querySelectorAll(".cr-tex-slot").forEach(e=>{e.addEventListener("click",()=>{ke=e.dataset.tex??null,!(!ke||!Q)&&(Q.value="",Q.click())})}),Q==null||Q.addEventListener("change",()=>{var n;const e=(n=Q.files)==null?void 0:n[0],t=ke;!e||!t||Le("正在加载贴图…",async()=>{const l=Y[t];l&&URL.revokeObjectURL(l.url);const a=URL.createObjectURL(e);Y[t]={file:e,bytes:new Uint8Array(await e.arrayBuffer()),url:a};const p=document.getElementById("cr-tex-thumb-"+t);p&&(p.innerHTML=`<img src="${a}" alt="${t}">`),X&&Be()})}),(Fe=document.getElementById("cr-preview-model"))==null||Fe.addEventListener("click",()=>{var e;if((e=A==null?void 0:A.files)!=null&&e[0]){Be();return}o&&Le("正在加载 3D 预览（首次加载较慢）…",async()=>{await it(o,oe||(o.split("/").pop()??o),{scale:Number(O==null?void 0:O.value)||1,rotationY:Number(V==null?void 0:V.value)||0,onAdjust:Pe})})}),(Ze=document.getElementById("cr-new-sub"))==null||Ze.addEventListener("click",()=>{var e;return void Se(i,c,null,{score:0,category:((e=document.getElementById("cr-type-cat"))==null?void 0:e.value)||D})}),(ze=document.getElementById("cr-type"))==null||ze.addEventListener("change",Te),(Ne=document.getElementById("cr-score"))==null||Ne.addEventListener("input",H),(Oe=document.getElementById("cr-zh"))==null||Oe.addEventListener("input",H),(Ve=document.getElementById("cr-en"))==null||Ve.addEventListener("input",H),(Xe=document.getElementById("cr-rec-name"))==null||Xe.addEventListener("input",H),(Ye=document.getElementById("cr-cook-step"))==null||Ye.addEventListener("change",H),(Ge=document.getElementById("cr-icon-upload"))==null||Ge.addEventListener("change",e=>{var n;te&&(URL.revokeObjectURL(te),te=null);const t=(n=e.target.files)==null?void 0:n[0];t&&(te=URL.createObjectURL(t)),H()}),Te(),(We=document.getElementById("cr-new-cat-inline"))==null||We.addEventListener("click",()=>Pt(c,async e=>{I=await ye(c);const t=document.getElementById("cr-type-cat");t&&e&&(t.innerHTML=I.categories.map(n=>`<option value="${s(n.id)}">${s(n.zh||n.id)}</option>`).join(""),t.value=e)})),(Qe=document.getElementById("cr-form-back"))==null||Qe.addEventListener("click",()=>void ue(i,c)),(Je=document.getElementById("cr-form-save"))==null||Je.addEventListener("click",async()=>{var l,a,p,E,x,h,_,q,$,L,S;const e=document.getElementById("cr-rec-name").value.trim();if(!e)return alert("请填写标识符");if(!he.test(e))return alert("标识符仅允许英文字母/数字/下划线，且不能以数字开头");const t=document.getElementById("cr-type").value,n={setName:f?void 0:c,assetPath:f?o:void 0,recipeName:e,nameZh:document.getElementById("cr-zh").value.trim(),nameEn:document.getElementById("cr-en").value.trim(),category:document.getElementById("cr-type-cat").value,score:Number(document.getElementById("cr-score").value)||0,type:t,compositionIds:Ee(),cookingStepId:t==="Composite"?"":((l=document.getElementById("cr-cook-step"))==null?void 0:l.value)??"",cookingStepIconId:((a=document.getElementById("cr-cook-icon"))==null?void 0:a.value)??"",platingStepId:((p=document.getElementById("cr-plate-step"))==null?void 0:p.value)??"",mixingIconId:((E=document.getElementById("cr-mix-icon"))==null?void 0:E.value)??"",modelPrefabId:((x=document.getElementById("cr-model-ref"))==null?void 0:x.value)??"",cookingProgress:Number(((h=document.getElementById("cr-cook-prog"))==null?void 0:h.value)??"1")||1,mixingProgress:Number(((_=document.getElementById("cr-mix-prog"))==null?void 0:_.value)??"1")||1,modelScale:Number((q=document.getElementById("cr-model-scale"))==null?void 0:q.value)||1,modelRotationY:Number(($=document.getElementById("cr-model-rot"))==null?void 0:$.value)||0};J("保存中…");try{f?await It(n):await wt(n);const y=o||`Assets/LevelSets/${c}/custom_recipes/${n.category}/${e}.asset`,T=(L=document.getElementById("cr-icon-upload").files)==null?void 0:L[0];if(T){const F=await bt(T);await kt(c,y,T.name,F)}const C=(S=document.getElementById("cr-model-file").files)==null?void 0:S[0];if(C){if(!/\.fbx$/i.test(C.name)){B("仅支持 FBX 模型文件。",!1);return}if(X||(X=new Uint8Array(await C.arrayBuffer()),de=C.name),!Y.base_color){B("请先在 base_color 格子中选择彩色贴图。",!1);return}const F=await je()??{bytes:X,renamed:0};F.renamed===0&&alert("警告：FBX 中未找到可改写的贴图引用（可能不是二进制 FBX 或不含贴图引用），贴图引用名未修改。");const fe=[{fileName:de,base64:await Ue(F.bytes)}],Z=De();for(const ge of Object.keys(Y)){const Ke=Y[ge],et=Z[ge];Ke&&et&&fe.push({fileName:et,base64:await Ue(Ke.bytes)})}await Bt(c,y,fe)}B(f?"已更新菜谱":"已创建菜谱"),ue(i,c)}catch(y){B(y.message,!1)}finally{z()}});function bt(e){return new Promise((t,n)=>{const l=new FileReader;l.onload=()=>{const a=l.result,p=a.indexOf(",");t(p>=0?a.substring(p+1):a)},l.onerror=n,l.readAsDataURL(e)})}function Ue(e){let t="";for(let l=0;l<e.length;l+=32768)t+=String.fromCharCode(...e.subarray(l,l+32768));return Promise.resolve(btoa(t))}}function _t(i,c,o,u){var w,b;const f=o.split("/").pop()??o;ce(`删除菜谱 · ${s(f)}`,"<p>将永久删除菜谱资源及其模型文件夹，且<b>不可恢复</b>。若其他菜谱引用了它作为子菜谱，组成将失效。</p>",'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn danger" data-ok>确认删除</button>'),(w=document.querySelector("[data-cancel]"))==null||w.addEventListener("click",j),(b=document.querySelector("[data-ok]"))==null||b.addEventListener("click",async()=>{J("删除中…");try{await xt(o),j(),B("已删除菜谱"),u()}catch(g){B(g.message,!1)}finally{z()}})}function Tt(i,c){ce("新建分类",`<label class="m-field">分类ID（仅字母/数字/下划线，用于目录名）<input type="text" id="cr-cat-id" placeholder="MyCategory"></label>
     <label class="m-field">中文名<input type="text" id="cr-cat-zh" placeholder="我的分类"></label>
     <label class="m-field">英文名<input type="text" id="cr-cat-en" placeholder="My Category"></label>`,'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>创建</button>'),rt(j,async()=>{const o=document.getElementById("cr-cat-id").value.trim();if(!o)return alert("请填写分类ID");if(!he.test(o))return alert("分类ID仅允许英文字母/数字/下划线");const u=document.getElementById("cr-cat-zh").value.trim(),f=document.getElementById("cr-cat-en").value.trim();J("创建分类…");try{await ot(i,o,u||o,f||o),j(),B("已创建分类"),c(o)}catch(w){B(w.message,!1)}finally{z()}})}function Pt(i,c){const o=prompt("分类ID（仅字母/数字/下划线）：");if(!o||!he.test(o)){alert("ID非法");return}const u=prompt("分类中文名：")||o,f=prompt("分类英文名：")||o;J("创建分类…"),ot(i,o,u,f).then(()=>{B("已创建分类"),c(o)}).catch(w=>B(w.message,!1)).finally(()=>z())}function Dt(i,c,o){var w;function u(b){return`${b.zh||b.id}${b.en?` (${b.en})`:""}`}const f=c.map(b=>`
    <div class="m-row" style="margin-bottom:8px">
      <span style="flex:1">${s(u(b))} <span class="muted">[${s(b.id)}]</span></span>
      <button class="m-btn" data-rename="${s(b.id)}">重命名</button>
      <button class="m-btn danger" data-delcat="${s(b.id)}">删除</button>
    </div>`).join("");ce("管理分类",`<div class="modal-scroll">${f||'<p class="muted">暂无分类</p>'}</div>`,'<button type="button" class="m-btn" data-cancel>关闭</button>'),(w=document.querySelector("[data-cancel]"))==null||w.addEventListener("click",j),document.querySelectorAll("[data-rename]").forEach(b=>{b.addEventListener("click",()=>{const g=b.dataset.rename,I=c.find(r=>r.id===g);jt(i,g,(I==null?void 0:I.zh)??g,(I==null?void 0:I.en)??g,()=>{j(),o()})})}),document.querySelectorAll("[data-delcat]").forEach(b=>{b.addEventListener("click",()=>{var r,P;const g=b.dataset.delcat,I=c.find(M=>M.id===g);ce(`删除分类 · ${s((I==null?void 0:I.zh)||g)}`,"<p>将检查关卡使用情况。如果有关卡正在使用该分类的菜谱，则不允许删除。</p>",'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn danger" data-ok>确认删除</button>'),(r=document.querySelector("[data-cancel]"))==null||r.addEventListener("click",j),(P=document.querySelector("[data-ok]"))==null||P.addEventListener("click",async()=>{J("检查中…");try{await Lt(i,g),j(),B("已删除分类"),o()}catch(M){B(M.message,!1),z()}})})})}function jt(i,c,o,u,f){ce(`重命名分类 · ${s(o||c)}`,`<label class="m-field">分类ID（仅字母/数字/下划线）<input type="text" id="cr-rename-id" value="${s(c)}"></label>
     <label class="m-field">中文名<input type="text" id="cr-rename-zh" value="${s(o)}"></label>
     <label class="m-field">英文名<input type="text" id="cr-rename-en" value="${s(u)}"></label>`,'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>确认</button>'),rt(j,async()=>{const w=document.getElementById("cr-rename-id").value.trim();if(!w)return alert("请填写分类ID");if(!he.test(w))return alert("分类ID仅允许英文字母/数字/下划线");const b=document.getElementById("cr-rename-zh").value.trim(),g=document.getElementById("cr-rename-en").value.trim();J("重命名…");try{await St(i,c,w,b||w,g||w),j(),B("已重命名分类"),f()}catch(I){B(I.message,!1)}finally{z()}})}function rt(i,c){var o,u;(o=document.querySelector("[data-cancel]"))==null||o.addEventListener("click",i),(u=document.querySelector("[data-ok]"))==null||u.addEventListener("click",c)}export{Rt as renderCustomRecipesView};
