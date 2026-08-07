const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./modelPreview-CIfpfbKJ.js","./main-i-sB41AB.js","./recipeCard-D7GB0m9M.js","./recipeCard-V4z4NLxm.css"])))=>i.map(i=>d[i]);
import{h as z,w as ke,s as W,o as te,c as F,_ as Le}from"./main-i-sB41AB.js";import{a as pt,n as ft,w as gt,a0 as ye,a1 as Ce,a2 as Ke,j as et,a3 as bt,q as tt,i as yt,a4 as vt,a5 as ht,a6 as $t,a7 as Et,a8 as It,a9 as wt,aa as Bt,ab as nt,ac as kt,f as Lt}from"./recipeCard-D7GB0m9M.js";function c(s){return String(s??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}const he=/^[A-Za-z_][A-Za-z0-9_]*$/;function C(s,a=!0){const o=document.getElementById("cr-status");o&&(o.textContent=s,o.classList.toggle("err",!a),o.classList.toggle("ok",a&&s.length>0))}function Me(s,a){return document.body.classList.add("manage-bg"),s.innerHTML=`
    ${ft("custom-recipes")}
    <div class="manage-bar">
      <h1 class="m-title">${c(a)}</h1>
      <span class="status" id="cr-status"></span>
      <span style="flex:1"></span>
    </div>
    <div class="cr-warn-banner">⚠️ 自定义菜谱功能开发中，请勿在正式关卡中使用</div>
    <div class="manage-content" id="cr-content"></div>
  `,gt(o=>{o==="layout"?(location.hash="#/layout",location.reload()):o==="manage"?(location.hash="#/manage",location.reload()):o==="recipes"&&(location.href="/recipes")}),document.getElementById("cr-content")}async function Ct(s){const a=Me(s,"自定义菜谱管理");Re("加载关卡集…");let o=[];try{o=await pt()}catch(r){ve(r);return}C(`共 ${o.length} 个关卡集`),a.innerHTML=`
    <div class="m-section-title">选择关卡集</div>
    <p class="modal-hint">选择要管理自定义菜谱的关卡集。首次进入会自动初始化配置。</p>
    <div class="m-grid">${o.map(r=>`
      <div class="m-card">
        <h3>${c(r.levelSetNameZH||r.setName)} <span class="muted">(${c(r.levelSetName||r.setName)})</span></h3>
        <div class="m-meta">
          标识：${c(r.setName)} · 关卡数：${r.levelCount}<br>
          作者：${c(r.author||"—")} · 版本：${c(r.version||"—")}
        </div>
        <div class="m-actions">
          <button class="m-btn primary" data-open="${c(r.setName)}">管理菜谱</button>
        </div>
      </div>`).join("")||'<p class="muted">暂无关卡集</p>'}
    </div>
  `,a.querySelectorAll("[data-open]").forEach(r=>r.addEventListener("click",()=>void ue(s,r.dataset.open)))}function Re(s){const a=document.getElementById("cr-content");a&&(a.innerHTML=`<p class="muted">${c(s)}</p>`),C(s)}function ve(s){const a=s instanceof Error?s.message:String(s);C(a,!1);const o=document.getElementById("cr-content");o&&(o.innerHTML=`<div class="m-block"><h3>出错</h3><p>${c(a)}</p></div>`)}function be(s){return`/api/custom-recipes/icon?assetPath=${encodeURIComponent(s.assetPath)}`}function Qe(s,a){return`<img class="food-icon" loading="lazy" src="${a?`/icons/${s}/${encodeURIComponent(a)}.png`:"/icons/_placeholder.png"}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`}function Je(s){const a=s.replace(/\/[^/]+\.asset$/,"")+"/models";return`/api/custom-recipes/model-files/${btoa(unescape(encodeURIComponent(a))).replace(/\+/g,"-").replace(/\//g,"_").replace(/=+$/,"")}/`}const ee={url:"/api/custom-recipes/reference-model?path="+encodeURIComponent("Assets/common01/food/CustomRecipes/Pizza/models/plated_mushroom_01.prefab"),format:"obj",scale:.7};async function at(s,a,o){try{const r=await wt(s),p=r.find(g=>/\.(fbx|obj)$/i.test(g));if(!p){alert("该菜谱尚未上传模型文件。");return}const{openModelPreview:w}=await Le(async()=>{const{openModelPreview:g}=await import("./modelPreview-CIfpfbKJ.js");return{openModelPreview:g}},__vite__mapDeps([0,1,2,3]),import.meta.url),b=r.filter(g=>/\.(png|jpg|jpeg)$/i.test(g)).map(g=>Je(s)+encodeURIComponent(g));w({title:a,resourceBase:Je(s),modelFileName:p,scale:o==null?void 0:o.scale,rotationY:o==null?void 0:o.rotationY,onAdjust:o==null?void 0:o.onAdjust,remoteTextures:b,referenceUrl:ee.url,referenceFormat:ee.format,referenceScale:ee.scale})}catch(r){alert(r.message||"模型预览加载失败。")}}function ct(s){return{guid:s.guid,id:s.id,nameZh:s.nameZh,nameEn:s.nameEn||void 0,assetPath:s.assetPath,cookingStep:s.cookingStepId||void 0,ingredients:s.ingredients,compositionIds:s.compositionIds,score:s.score,isCustom:!0,intermediate:s.intermediate,group:s.group,type:"custom",cookingGroups:s.cookingGroups}}async function ue(s,a){var me,Q;const o=Me(s,`自定义菜谱 · ${c(a)}`);Re("加载菜谱配置…");let r,p,w=new Map,b=new Map;try{[r,p]=await Promise.all([ye(a),Ce(a)]);const m=await Ke(a).catch(()=>null);for(const B of(m==null?void 0:m.platingContainers)??[])b.set(B.id,B.nameZh||B.id);let v=!1;const d=await et().catch(()=>(v=!0,[]));for(const B of d)w.set(B.id,B.nameZh);v&&C("⚠️ 食材数据加载失败（/api/catalog/ingredients），卡片可能缺少食材图标")}catch(m){ve(m);return}C(`${p.length} 个菜谱 · UID前缀：${r.uidPrefix}`);const g=r.categories??[];function I(m){return m.zh||m.id}let i="",_="",R="all";function ne(m){return R===m?" active":""}function V(m){const v=ct(m);return!v.ingredients&&v.compositionIds&&(v.ingredients=v.compositionIds.filter(d=>w.has(d))),v}function H(){let m=i?p.filter(d=>d.category===i):p;R==="done"?m=m.filter(d=>!d.intermediate):R==="half"&&(m=m.filter(d=>d.intermediate));const v=_.trim().toLowerCase();return v&&(m=m.filter(d=>[d.nameZh,d.nameEn??"",d.id,...d.ingredients??[]].join(" ").toLowerCase().includes(v))),m}function x(){const m=H();return m.length===0?p.length===0&&_===""&&R==="all"&&i===""?((async()=>{const d=document.getElementById("cr-grid");if(!(!d||d.dataset.diag)){d.dataset.diag="1";try{const B=await bt(a).catch(()=>null);if(!B){d.innerHTML=`<div class="m-block">
                <h3>暂无菜谱</h3>
                <p class="muted">桥接不支持诊断接口（旧版本）。若磁盘 <code>Assets/LevelSets/${c(a)}/custom_recipes/</code> 下已有菜谱，请：
                <b>在 Unity 中 Tools → Layout Editor → 停止服务 → 启动服务</b>（或重启 Unity 重新编译），再刷新本页。</p>
              </div>`;return}const se=B.dirExists?`磁盘上检测到 <b>${B.fsAssets.length}</b> 个 .asset 文件，扫描命中 <b>${B.scannedCount}</b> 个自定义菜谱，成功加载 <b>${B.loadedCount}</b> 个。`:"目录不存在，尚未创建任何菜谱。",X=B.scannedCount>0&&B.loadedCount===0?'<p class="mp-status err">⚠️ 文件存在但资产加载失败（CustomRecipeSO 类型未加载）。请重启 Unity 并确认 Console 无编译错误。</p>':B.scannedCount===0&&B.fsAssets.length>0?'<p class="mp-status err">⚠️ 文件系统有资产但脚本 guid 不匹配（可能是旧版脚本/副本）。</p>':"";d.innerHTML=`<div class="m-block">
              <h3>暂无菜谱</h3>
              <p class="muted">${se}</p>
              ${X}
              <p class="muted small" style="margin-top:8px">点击右上角「+ 新建菜谱」开始创建；若已创建但未显示，请重启 Unity 桥（Tools → Layout Editor → 停止服务 → 启动服务）后刷新。</p>
            </div>`}catch{d.innerHTML='<div class="m-block"><h3>暂无菜谱</h3><p class="muted">点击右上角「+ 新建菜谱」开始创建。</p></div>'}}})(),'<p class="muted">加载中…</p>'):'<p class="muted">没有匹配的菜谱。点击右上角「+ 新建菜谱」开始创建。</p>':`<div class="rl-grid">${m.map(d=>{let B;try{B=tt(V(d),{allRecipes:p.map(V),ingredientName:j=>w.get(j)??j,iconSrc:()=>be(d)})}catch(j){B=`<div class="m-card"><h3>${c(d.nameZh)}</h3><p class="muted">卡片渲染失败：${c(j.message)}</p></div>`}const se=g.find(j=>j.id===d.category),X=d.platingStepId?b.get(d.platingStepId):"",J=(d.compositionIds??[]).length;return`
      <div class="cr-card-wrap">
        <div class="cr-card-inner">${B}</div>
        <div class="cr-card-foot">
          <span class="cr-cat-tag">${c(I(se??{id:d.category,zh:d.category,en:d.category}))}</span>
          ${X?`<span class="cr-cat-tag cr-plate-tag" title="装盘容器">🍽 ${c(X)}</span>`:""}
          ${d.intermediate?'<span class="cr-cat-tag cr-half-tag">中间产物</span>':""}
          <span class="muted small">UID ${d.uID} · 组成 ${J} 项</span>
          <span style="flex:1"></span>
          ${d.hasModel?`<button class="m-btn small" data-preview="${c(d.assetPath)}" title="3D 模型在线预览">👁</button>`:""}
          <button class="m-btn small" data-edit="${c(d.assetPath)}">编辑</button>
          <button class="m-btn small danger" data-del="${c(d.assetPath)}">删除</button>
        </div>
      </div>`}).join("")}</div>`}function ae(){return`
    <div class="cr-sidebar">
      <div class="m-section-title">分类</div>
      <div class="cr-cat-list">
        <button class="m-btn cr-cat-item${i===""?" primary":""}" data-cat="">全部 (${p.length})</button>
        ${g.map(m=>{const v=p.filter(d=>d.category===m.id).length;return`<button class="m-btn cr-cat-item${i===m.id?" primary":""}" data-cat="${c(m.id)}">${c(I(m))} (${v})</button>`}).join("")}
        <div class="cr-cat-actions">
          <button class="m-btn" id="cr-new-cat">+ 新建分类</button>
          ${g.length>0?'<button class="m-btn" id="cr-manage-cat">管理分类</button>':""}
        </div>
      </div>
    </div>`}o.innerHTML=`
    <div class="m-actions-row">
      <button class="m-btn" id="cr-back">← 返回关卡集列表</button>
      <span class="muted">当前关卡集：<b>${c(a)}</b></span>
      <span style="flex:1"></span>
      <button class="m-btn primary" id="cr-new-recipe">+ 新建菜谱</button>
    </div>
    <div class="cr-toolbar">
      <input type="search" id="cr-search" class="rl-search" placeholder="搜索菜名 / ID / 食材…" autocomplete="off">
      <div class="ing-groups">
        <button type="button" class="cr-comp-chip${ne("all")}" data-role="all">全部</button>
        <button type="button" class="cr-comp-chip${ne("done")}" data-role="done">成品</button>
        <button type="button" class="cr-comp-chip${ne("half")}" data-role="half">中间产物</button>
      </div>
    </div>
    <div class="cr-layout">
      <div id="cr-sidebar">${ae()}</div>
      <div id="cr-grid">${x()}</div>
    </div>
  `;function ce(){(async()=>{W("加载…");try{[r,p]=await Promise.all([ye(a),Ce(a)])}catch(m){ve(m);return}finally{z()}C(`${p.length} 个菜谱 · UID前缀：${r.uidPrefix}`),document.getElementById("cr-sidebar").innerHTML=ae(),oe(),document.getElementById("cr-grid").innerHTML=x(),D()})()}function $e(){var m;(m=document.getElementById("cr-search"))==null||m.addEventListener("input",v=>{_=v.target.value,document.getElementById("cr-grid").innerHTML=x(),D()}),document.querySelectorAll("[data-role]").forEach(v=>{v.addEventListener("click",()=>{R=v.dataset.role??"all",document.querySelectorAll("[data-role]").forEach(d=>d.classList.toggle("active",d===v)),document.getElementById("cr-grid").innerHTML=x(),D()})})}function oe(){var m,v;document.querySelectorAll(".cr-cat-item").forEach(d=>{d.addEventListener("click",()=>{i=d.dataset.cat??"",document.getElementById("cr-sidebar").innerHTML=ae(),oe(),document.getElementById("cr-grid").innerHTML=x(),D()})}),(m=document.getElementById("cr-new-cat"))==null||m.addEventListener("click",()=>Mt(a,d=>{i=d??"",ue(s,a)})),(v=document.getElementById("cr-manage-cat"))==null||v.addEventListener("click",()=>xt(a,r.categories,()=>void ue(s,a)))}function D(){document.querySelectorAll("[data-edit]").forEach(m=>m.addEventListener("click",()=>void Se(s,a,m.dataset.edit))),document.querySelectorAll("[data-preview]").forEach(m=>m.addEventListener("click",()=>{const v=m.dataset.preview;at(v,v.split("/").pop()??v)})),document.querySelectorAll("[data-del]").forEach(m=>m.addEventListener("click",()=>St(s,a,m.dataset.del,ce)))}$e(),oe(),D(),(me=document.getElementById("cr-back"))==null||me.addEventListener("click",()=>void Ct(s)),(Q=document.getElementById("cr-new-recipe"))==null||Q.addEventListener("click",()=>void Se(s,a,null))}async function Se(s,a,o,r){var Fe,He,qe,Ae,Ue,Ze,ze,je,Ne,Oe,Ge,Ve,Xe,Ye,We;const p=o!=null,w=Me(s,p?"编辑菜谱":"新建菜谱");Re("加载参考数据…");let b=[],g,I,i,_=[],R=[];const ne={cookingSteps:[],platingSteps:[],platingContainers:[],icons:[],reusableModels:[],ingredients:[]};try{[b,g,I]=await Promise.all([et().catch(()=>[]),Ke(a).catch(()=>ne),ye(a)]),[_,R]=await Promise.all([Ce(a).catch(()=>[]),yt(a).catch(()=>[])]),p&&(i=_.find(e=>e.assetPath===o))}catch(e){ve(e),z();return}z();const V=new Set;for(const e of _)V.add(e.id);for(const e of R)e.isCustom&&V.add(e.id);const H=new Map,x=new Map;for(const e of(i==null?void 0:i.compositionIds)??[]){const t=V.has(e)?x:H;t.set(e,(t.get(e)??0)+1)}const ae=!p,ce=(i==null?void 0:i.recipeName)??"",$e=(i==null?void 0:i.nameZh)??"",oe=(i==null?void 0:i.nameEn)??"",D=(i==null?void 0:i.category)??(r==null?void 0:r.category)??(((Fe=I.categories)==null?void 0:Fe.length)>0?I.categories[0].id:""),me=(i==null?void 0:i.score)??(r==null?void 0:r.score)??0,Q=(i==null?void 0:i.type)??((r==null?void 0:r.score)===0,"Cooked"),m=(i==null?void 0:i.cookingStepId)??"",v="",d=(i==null?void 0:i.platingStepId)??"",B="",se="",X=I.categories??[],J=new Map;for(const e of b)J.set(e.id,e);const j=b.length===0,xe=(()=>{const e=[];for(const n of _)p&&n.assetPath===o||e.push({id:n.id,nameZh:n.nameZh,nameEn:n.nameEn,score:n.score,cookingStepId:n.cookingStepId,hasIcon:n.hasIcon,assetPath:n.assetPath,official:!1,ingredients:n.ingredients});const t=new Set(e.map(n=>n.id));for(const n of R)!n.isCustom||(n.group??"")==="levelset"||t.has(n.id)||(e.push({id:n.id,nameZh:n.nameZh,nameEn:n.nameEn,score:n.score??0,cookingStepId:n.cookingStep??"",hasIcon:!!n.icon,assetPath:n.assetPath,official:!0,ingredients:n.ingredients??[]}),t.add(n.id));return e.sort((n,u)=>n.score-u.score||Number(n.official)-Number(u.official)||n.nameZh.localeCompare(u.nameZh,"zh")),e})(),pe=new Map;for(const e of xe)pe.set(e.id,e);const st=[..._.map(ct),...R.filter(e=>e.isCustom).map(e=>({...e,isCustom:!0}))];function it(e){return e.zh||e.id}function lt(){let e=X.map(t=>`<option value="${c(t.id)}" ${t.id===D?"selected":""}>${c(it(t))}</option>`).join("");return!X.some(t=>t.id===D)&&D&&(e+=`<option value="${c(D)}" selected>${c(D)}</option>`),`<select id="cr-type-cat" class="m-select">${e}</select>`}function ie(e,t,n){const u=new Map;for(const l of e)u.has(l.id)||u.set(l.id,l.nameZh||l.id);return`<select id="${n}" class="m-select">
      <option value="">— 不设置 —</option>
      ${[...u.entries()].map(([l,f])=>`<option value="${c(l)}" ${l===t?"selected":""}>${c(f)} (${c(l)})</option>`).join("")}
    </select>`}function Ee(){const e=[];for(const[t,n]of H)for(let u=0;u<n;u++)e.push(t);for(const[t,n]of x)for(let u=0;u<n;u++)e.push(t);return e}function Ie(e,t,n){const u=n>0,l=u?`<div class="cp-count">
          <button type="button" class="cp-step" data-cpdec data-cpid="${c(e)}" data-cpsub="${t?1:0}">−</button>
          <span class="cp-num">${n}</span>
          <button type="button" class="cp-step" data-cpinc data-cpid="${c(e)}" data-cpsub="${t?1:0}">＋</button>
        </div>`:"";if(t){const h=pe.get(e);if(!h)return"";const P=h.score<=0?'<span class="cr-badge-half">中间产物</span>':'<span class="cr-badge-done">成品菜 · 可作组成</span>',A=h.official?`/icons/recipes/${encodeURIComponent(h.id)}.png`:be(h);return`<div class="pick-card cp-card${u?" selected":""}" data-cpid="${c(e)}" data-cpsub="1" title="${c(h.id)}">
        <span class="pc-head"><img class="food-icon" loading="lazy" src="${c(A)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'" /><span class="pc-name">${c(h.nameZh)}${P}${h.nameEn?` <span class="muted pc-en">${c(h.nameEn)}</span>`:""}</span></span>
        <span class="muted small">${h.cookingStepId?c(h.cookingStepId):"无烹饪步骤"} · ${(h.ingredients??[]).length} 种食材${h.official?" · 官方":(h.score<=0," · 本关卡集")}</span>
        ${l}
      </div>`}const f=J.get(e);if(!f)return"";const E=f.group&&f.group!=="core"?` <span class="pc-badge">${Lt(f.group)}</span>`:"",S=f.nameEn&&f.nameEn.trim()||"";return`<div class="pick-card cp-card${u?" selected":""}" data-cpid="${c(e)}" data-cpsub="0" title="${c(f.id)}">
      <span class="pc-head">${Qe("ingredients",f.id)}<span class="pc-name">${c(f.nameZh)}${E}${S?` <span class="muted pc-en">${S}</span>`:""}</span></span>
      ${l}
    </div>`}function dt(){var P,A;const e=new Map;for(const[$,k]of H)e.set($,k);for(const[$,k]of x)e.set($,k);let t="all",n="";function u(){const $=n.trim().toLowerCase(),k=[];if(t==="all"||t==="ing")for(const y of b)$&&!y.nameZh.toLowerCase().includes($)&&!(y.nameEn??"").toLowerCase().includes($)&&!y.id.toLowerCase().includes($)||k.push({id:y.id,isSub:!1});const M=xe.filter(y=>t==="all"||(t==="sub"?y.score<=0:y.score>0));for(const y of M)$&&!y.nameZh.toLowerCase().includes($)&&!(y.nameEn??"").toLowerCase().includes($)&&!y.id.toLowerCase().includes($)||k.push({id:y.id,isSub:!0});return k}function l(){const $=u();return`
        <div class="cr-comp-toolbar">
          <input type="search" id="cp-search" class="ing-search" placeholder="搜索名称 / ID…" autocomplete="off">
          <div class="ing-groups">${[`<button type="button" class="cr-comp-chip${t==="all"?" active":""}" data-filter="all">全部</button>`,`<button type="button" class="cr-comp-chip${t==="ing"?" active":""}" data-filter="ing">食材</button>`,`<button type="button" class="cr-comp-chip${t==="sub"?" active":""}" data-filter="sub">中间产物</button>`,`<button type="button" class="cr-comp-chip${t==="done"?" active":""}" data-filter="done">成品菜</button>`].join("")}</div>
        </div>
        ${j&&t!=="sub"&&t!=="done"?'<p class="mp-status err">⚠️ 未加载到食材数据（桥接 /api/catalog/ingredients 异常），请刷新重试或检查 Unity 桥。</p>':""}
        <div class="modal-scroll" id="cp-scroll">
          <div class="pick-grid" id="cp-grid">${$.map(y=>Ie(y.id,y.isSub,e.get(y.id)??0)).join("")}</div>
          ${$.length?"":'<p class="muted">没有匹配的项</p>'}
        </div>`}te("添加食材 / 菜谱",`<p class="modal-hint">点击卡片加入（默认 1 份），再次点击 − / ＋ 调整数量；同一项可多次使用。<b>任何菜谱（成品菜或中间产物）都能作为本菜谱的组成</b>，如鸡蛋汉堡 = 煎蛋（成品菜）+ 面包。</p>
       <div id="cp-body">${l()}</div>`,`<button type="button" class="m-btn" data-cancel>取消</button>
       <button type="button" class="m-btn primary" data-ok>确定</button>`);const f=document.querySelector(".modal-panel");f&&f.classList.add("wide");const E=document.getElementById("cp-body");function S(){E.innerHTML=l(),h()}function h(){var $,k;($=document.getElementById("cp-search"))==null||$.addEventListener("input",M=>{n=M.target.value;const y=document.getElementById("cp-grid");if(y){y.innerHTML=u().map(L=>Ie(L.id,L.isSub,e.get(L.id)??0)).join("");const T=document.getElementById("cp-scroll");if(T){const L=T.querySelector("p.muted");L&&L.remove(),u().length===0&&T.insertAdjacentHTML("beforeend",'<p class="muted">没有匹配的项</p>')}}}),document.querySelectorAll("#cp-body .cr-comp-chip").forEach(M=>{M.addEventListener("click",()=>{t=M.dataset.filter??"all",S()})}),(k=document.getElementById("cp-grid"))==null||k.addEventListener("click",M=>{const y=M.target,T=y.closest(".cp-step"),L=y.closest(".cp-card");if(!L)return;const G=L.dataset.cpid,ge=L.dataset.cpsub==="1";let Y=e.get(G)??0;if(T){const mt=T.dataset.cpinc!==void 0?1:-1;Y=Math.max(0,Y+mt)}else Y=Y>0?0:1;Y<=0?e.delete(G):e.set(G,Y),L.outerHTML=Ie(G,ge,Y)})}h(),(P=document.querySelector("[data-cancel]"))==null||P.addEventListener("click",F),(A=document.querySelector("[data-ok]"))==null||A.addEventListener("click",()=>{H.clear(),x.clear();for(const[$,k]of e)(V.has($)?x:H).set($,k);F(),we(),q()})}function we(){const e=document.getElementById("cr-comp-list");if(!e)return;const t=[];for(const[l,f]of H){const E=J.get(l);t.push(`<div class="cr-comp-row" data-rowid="ing:${c(l)}">
        ${Qe("ingredients",l)}
        <span class="cr-row-name">${c((E==null?void 0:E.nameZh)??l)}</span>
        <span class="cr-row-stepper">
          <button type="button" class="cr-step" data-rowdec="ing:${c(l)}">−</button>
          <span class="cr-step-num">${f}</span>
          <button type="button" class="cr-step" data-rowinc="ing:${c(l)}">＋</button>
        </span>
        <button type="button" class="cr-step-del" data-rowdel="ing:${c(l)}" title="移除">×</button>
      </div>`)}for(const[l,f]of x){const E=pe.get(l),S=((E==null?void 0:E.score)??0)<=0?'<span class="cr-chip-tag">中间产物</span>':'<span class="cr-chip-tag">成品菜</span>';t.push(`<div class="cr-comp-row cr-comp-row-sub" data-rowid="sub:${c(l)}">
        ${E?`<img class="food-icon" loading="lazy" src="${c(E.official?`/icons/recipes/${encodeURIComponent(E.id)}.png`:be(E))}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'" />`:""}
        <span class="cr-row-name">${c((E==null?void 0:E.nameZh)??l)}${S}</span>
        <span class="cr-row-stepper">
          <button type="button" class="cr-step" data-rowdec="sub:${c(l)}">−</button>
          <span class="cr-step-num">${f}</span>
          <button type="button" class="cr-step" data-rowinc="sub:${c(l)}">＋</button>
        </span>
        <button type="button" class="cr-step-del" data-rowdel="sub:${c(l)}" title="移除">×</button>
      </div>`)}e.innerHTML=t.length?t.join(""):'<p class="muted small" style="margin:4px 0">尚未选择。点击下方「添加」选择食材或中间产物。</p>';const n=Ee().length,u=document.getElementById("cr-comp-hint");u&&(u.textContent=t.length?`共 ${n} 份组成（食材 ${[...H.values()].reduce((l,f)=>l+f,0)} · 中间产物 ${[...x.values()].reduce((l,f)=>l+f,0)}）`:""),e.querySelectorAll("[data-rowinc], [data-rowdec], [data-rowdel]").forEach(l=>{l.addEventListener("click",()=>{const f=l.dataset.rowinc??l.dataset.rowdec??l.dataset.rowdel??"",[E,S]=f.split(":"),h=E==="ing"?H:x,P=h.get(S)??0;l.dataset.rowdel!==void 0?h.delete(S):l.dataset.rowinc!==void 0?h.set(S,P+1):P<=1?h.delete(S):h.set(S,P-1),we(),q()})})}function rt(e){const t=[];for(const n of e){const u=pe.get(n);u&&(u.ingredients??[]).length>0?t.push(...u.ingredients):t.push(n)}return t}let K=null;function q(){var P,A,$,k,M,y;const e=document.getElementById("cr-preview");if(!e)return;const t=((P=document.getElementById("cr-rec-name"))==null?void 0:P.value.trim())??"",n=((A=document.getElementById("cr-zh"))==null?void 0:A.value.trim())??"",u=(($=document.getElementById("cr-en"))==null?void 0:$.value.trim())??"",l=Number((k=document.getElementById("cr-score"))==null?void 0:k.value)||0,E=(((M=document.getElementById("cr-type"))==null?void 0:M.value)??"Composite")==="Composite"?"":((y=document.getElementById("cr-cook-step"))==null?void 0:y.value)??"",S=Ee(),h={guid:"",id:t||"preview",nameZh:n||t||"未命名菜谱",nameEn:u||void 0,assetPath:"",isCustom:!0,group:"levelset",score:l,intermediate:l<=0,ingredients:rt(S),compositionIds:S,cookingStep:E||void 0,type:"custom"};e.innerHTML=tt(h,{allRecipes:st,ingredientName:T=>{var L;return((L=J.get(T))==null?void 0:L.nameZh)??T},iconSrc:K?()=>K:p&&(i!=null&&i.hasIcon)&&i.assetPath?()=>be(i):void 0})}w.innerHTML=`
    <div class="m-actions-row">
      <button class="m-btn" id="cr-form-back">← 返回菜谱列表</button>
      <span class="muted">关卡集：<b>${c(a)}</b> · ${p?`编辑 ${c(ce)}`:"新建菜谱"}</span>
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
          <input type="text" id="cr-rec-name" value="${c(ce)}" ${p?"disabled":""} placeholder="MyRecipe">
        </label>
        <div class="cr-form-grid">
          <label class="m-field">中文名<input type="text" id="cr-zh" value="${c($e)}" placeholder="我的菜谱"></label>
          <label class="m-field">英文名<input type="text" id="cr-en" value="${c(oe)}" placeholder="My Recipe"></label>
          <label class="m-field">分类 ${lt()}
            <button type="button" class="m-btn" id="cr-new-cat-inline" style="margin-top:6px">+ 新建分类</button>
          </label>
          <label class="m-field">类型
            <select id="cr-type" class="m-select">
              <option value="Composite" ${Q==="Composite"?"selected":""}>Composite（组合）</option>
              <option value="Cooked" ${Q==="Cooked"?"selected":""}>Cooked（烹饪）</option>
              <option value="Mixed" ${Q==="Mixed"?"selected":""}>Mixed（搅拌）</option>
            </select>
          </label>
          <label class="m-field">分数<input type="number" id="cr-score" value="${me}" min="0">
            <span class="muted small">0 = 中间产物（不直接上桌，可被其他菜谱引用）</span>
          </label>
          <label class="m-field">UID（自动生成）<input type="text" value="${(i==null?void 0:i.uID)??(ae?I.uidPrefix*1e3+I.nextSequence:"—")}" disabled></label>
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
          <label class="m-field" id="cr-cook-step-field">烹饪步骤 ${ie(g.cookingSteps,m,"cr-cook-step")}</label>
          <label class="m-field">烹饪图标 ${ie(g.icons,v,"cr-cook-icon")}</label>
          <label class="m-field" id="cr-cook-prog-field">烹饪程度
            <select id="cr-cook-prog" class="m-select">
              <option value="0">Raw（生）</option>
              <option value="1" selected>Cooked（熟）</option>
              <option value="2">Burnt（焦）</option>
            </select>
          </label>
          <label class="m-field">装盘容器 ${ie((He=g.platingContainers)!=null&&He.length?g.platingContainers:(g.platingSteps??[]).filter(e=>e.id==="Plate"||e.id==="Glass"),d,"cr-plate-step")}
            <span class="muted small">决定上桌容器（盘子/杯子），运行时映射为 PlatingStepData</span>
          </label>
          <label class="m-field" id="cr-mix-icon-field" style="display:none">搅拌图标 ${ie(g.icons,B,"cr-mix-icon")}</label>
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
        <p class="modal-hint">上传 <b>FBX 模型</b>（可不含材质，单独上传即可）；需要彩色时<b>补充一张 PNG 贴图</b>，保存时会将该 PNG 作为 FBX 的材质使用（内嵌进 FBX 并生成材质球）。选择文件后立即打开 3D 预览，调整方向/大小后保存提交。</p>
        <div class="cr-form-grid">
          <label class="m-field">上传 3D 模型（仅 FBX）<input type="file" id="cr-model-file" accept=".fbx">
            <span class="muted small">可单独上传；若预览显示为灰色，说明 FBX 未内嵌贴图，可补充 PNG 贴图</span></label>
          <label class="m-field">补充贴图（PNG，可选）<input type="file" id="cr-model-texture" accept=".png,image/png">
            <span class="muted small">作为 FBX 的材质贴图：选择后自动重新预览上色，保存时随 FBX 一起提交</span></label>
          <label class="m-field">复用已有模型 ${ie(g.reusableModels,se,"cr-model-ref")}</label>
          <label class="m-field">模型缩放<input type="number" id="cr-model-scale" value="${(i==null?void 0:i.modelScale)??1}" min="0.01" step="0.05">
            <span class="muted small">实际游戏中的大小（相对原模型尺寸）</span></label>
          <label class="m-field">Y 轴旋转（度）<input type="number" id="cr-model-rot" value="${(i==null?void 0:i.modelRotationY)??0}" step="5">
            <span class="muted small">修正模型朝向（如煎蛋面朝上）</span></label>
          <label class="m-field">在线预览<button type="button" class="m-btn" id="cr-preview-model">👁 预览并调整方向/大小</button>
            <span class="muted small">已保存的模型可随时预览调整；上传新文件则自动预览</span></label>
        </div>
      </div>
    </div>
  `,q(),we();function Pe(){var f;const e=((f=document.getElementById("cr-type"))==null?void 0:f.value)??"Composite",t=document.getElementById("cr-cook-step-field"),n=document.getElementById("cr-cook-prog-field"),u=document.getElementById("cr-mix-icon-field"),l=document.getElementById("cr-mix-prog-field");t&&(t.style.display=e==="Composite"?"none":""),n&&(n.style.display=e==="Composite"?"none":""),u&&(u.style.display=e==="Mixed"?"":"none"),l&&(l.style.display=e==="Mixed"?"":"none"),q()}(qe=document.getElementById("cr-add-comp"))==null||qe.addEventListener("click",()=>dt());const N=document.getElementById("cr-model-scale"),O=document.getElementById("cr-model-rot"),U=document.getElementById("cr-model-file"),fe=document.getElementById("cr-model-texture");let Z=null,le="",de=null,re=null;const Te=(e,t)=>{N&&(N.value=String(Math.round(e*100)/100)),O&&(O.value=String(Math.round(t)))},_e=async()=>{if(!Z)return null;if(!re)return Z;const{fuseTextureIntoFbx:e}=await Le(async()=>{const{fuseTextureIntoFbx:t}=await import("./fbxFuse-DsvmeW3k.js");return{fuseTextureIntoFbx:t}},[],import.meta.url);return e(Z,re)},Be=()=>{var t;const e=(t=U==null?void 0:U.files)==null?void 0:t[0];!e&&!Z||ke("正在加载 3D 预览…",async()=>{e&&(Z=new Uint8Array(await e.arrayBuffer()),le=e.name);const n=await _e();if(!n)return;const{openModelPreview:u}=await Le(async()=>{const{openModelPreview:l}=await import("./modelPreview-CIfpfbKJ.js");return{openModelPreview:l}},__vite__mapDeps([0,1,2,3]),import.meta.url);u({title:le,resourceBase:"",modelFileName:le,localBuffer:n.buffer.slice(n.byteOffset,n.byteOffset+n.byteLength),localTextures:de?[de]:void 0,scale:Number(N==null?void 0:N.value)||1,rotationY:Number(O==null?void 0:O.value)||0,onAdjust:Te,referenceUrl:ee.url,referenceFormat:ee.format,referenceScale:ee.scale})})};U==null||U.addEventListener("change",Be),fe==null||fe.addEventListener("change",()=>{ke("正在加载贴图…",async()=>{var t;const e=((t=fe.files)==null?void 0:t[0])??null;de=e,re=e?new Uint8Array(await e.arrayBuffer()):null,Z&&Be()})}),(Ae=document.getElementById("cr-preview-model"))==null||Ae.addEventListener("click",()=>{var e;if((e=U==null?void 0:U.files)!=null&&e[0]){Be();return}o&&ke("正在加载 3D 预览（首次加载较慢）…",async()=>{await at(o,ce||(o.split("/").pop()??o),{scale:Number(N==null?void 0:N.value)||1,rotationY:Number(O==null?void 0:O.value)||0,onAdjust:Te})})}),(Ue=document.getElementById("cr-new-sub"))==null||Ue.addEventListener("click",()=>{var e;return void Se(s,a,null,{score:0,category:((e=document.getElementById("cr-type-cat"))==null?void 0:e.value)||D})}),(Ze=document.getElementById("cr-type"))==null||Ze.addEventListener("change",Pe),(ze=document.getElementById("cr-score"))==null||ze.addEventListener("input",q),(je=document.getElementById("cr-zh"))==null||je.addEventListener("input",q),(Ne=document.getElementById("cr-en"))==null||Ne.addEventListener("input",q),(Oe=document.getElementById("cr-rec-name"))==null||Oe.addEventListener("input",q),(Ge=document.getElementById("cr-cook-step"))==null||Ge.addEventListener("change",q),(Ve=document.getElementById("cr-icon-upload"))==null||Ve.addEventListener("change",e=>{var n;K&&(URL.revokeObjectURL(K),K=null);const t=(n=e.target.files)==null?void 0:n[0];t&&(K=URL.createObjectURL(t)),q()}),Pe(),(Xe=document.getElementById("cr-new-cat-inline"))==null||Xe.addEventListener("click",()=>Rt(a,async e=>{I=await ye(a);const t=document.getElementById("cr-type-cat");t&&e&&(t.innerHTML=I.categories.map(n=>`<option value="${c(n.id)}">${c(n.zh||n.id)}</option>`).join(""),t.value=e)})),(Ye=document.getElementById("cr-form-back"))==null||Ye.addEventListener("click",()=>void ue(s,a)),(We=document.getElementById("cr-form-save"))==null||We.addEventListener("click",async()=>{var u,l,f,E,S,h,P,A,$,k,M;const e=document.getElementById("cr-rec-name").value.trim();if(!e)return alert("请填写标识符");if(!he.test(e))return alert("标识符仅允许英文字母/数字/下划线，且不能以数字开头");const t=document.getElementById("cr-type").value,n={setName:p?void 0:a,assetPath:p?o:void 0,recipeName:e,nameZh:document.getElementById("cr-zh").value.trim(),nameEn:document.getElementById("cr-en").value.trim(),category:document.getElementById("cr-type-cat").value,score:Number(document.getElementById("cr-score").value)||0,type:t,compositionIds:Ee(),cookingStepId:t==="Composite"?"":((u=document.getElementById("cr-cook-step"))==null?void 0:u.value)??"",cookingStepIconId:((l=document.getElementById("cr-cook-icon"))==null?void 0:l.value)??"",platingStepId:((f=document.getElementById("cr-plate-step"))==null?void 0:f.value)??"",mixingIconId:((E=document.getElementById("cr-mix-icon"))==null?void 0:E.value)??"",modelPrefabId:((S=document.getElementById("cr-model-ref"))==null?void 0:S.value)??"",cookingProgress:Number(((h=document.getElementById("cr-cook-prog"))==null?void 0:h.value)??"1")||1,mixingProgress:Number(((P=document.getElementById("cr-mix-prog"))==null?void 0:P.value)??"1")||1,modelScale:Number((A=document.getElementById("cr-model-scale"))==null?void 0:A.value)||1,modelRotationY:Number(($=document.getElementById("cr-model-rot"))==null?void 0:$.value)||0};W("保存中…");try{p?await vt(n):await ht(n);const y=o||`Assets/LevelSets/${a}/custom_recipes/${n.category}/${e}.asset`,T=(k=document.getElementById("cr-icon-upload").files)==null?void 0:k[0];if(T){const G=await ut(T);await $t(a,y,T.name,G)}const L=(M=document.getElementById("cr-model-file").files)==null?void 0:M[0];if(L){if(!/\.fbx$/i.test(L.name)){C("仅支持 FBX 模型文件。",!1);return}Z||(Z=new Uint8Array(await L.arrayBuffer()),le=L.name);const G=await _e()??Z,ge=[{fileName:le,base64:await De(G)}];de&&re&&ge.push({fileName:de.name,base64:await De(re)}),await Et(a,y,ge)}C(p?"已更新菜谱":"已创建菜谱"),ue(s,a)}catch(y){C(y.message,!1)}finally{z()}});function ut(e){return new Promise((t,n)=>{const u=new FileReader;u.onload=()=>{const l=u.result,f=l.indexOf(",");t(f>=0?l.substring(f+1):l)},u.onerror=n,u.readAsDataURL(e)})}function De(e){let t="";for(let u=0;u<e.length;u+=32768)t+=String.fromCharCode(...e.subarray(u,u+32768));return Promise.resolve(btoa(t))}}function St(s,a,o,r){var w,b;const p=o.split("/").pop()??o;te(`删除菜谱 · ${c(p)}`,"<p>将永久删除菜谱资源及其模型文件夹，且<b>不可恢复</b>。若其他菜谱引用了它作为子菜谱，组成将失效。</p>",'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn danger" data-ok>确认删除</button>'),(w=document.querySelector("[data-cancel]"))==null||w.addEventListener("click",F),(b=document.querySelector("[data-ok]"))==null||b.addEventListener("click",async()=>{W("删除中…");try{await Bt(o),F(),C("已删除菜谱"),r()}catch(g){C(g.message,!1)}finally{z()}})}function Mt(s,a){te("新建分类",`<label class="m-field">分类ID（仅字母/数字/下划线，用于目录名）<input type="text" id="cr-cat-id" placeholder="MyCategory"></label>
     <label class="m-field">中文名<input type="text" id="cr-cat-zh" placeholder="我的分类"></label>
     <label class="m-field">英文名<input type="text" id="cr-cat-en" placeholder="My Category"></label>`,'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>创建</button>'),ot(F,async()=>{const o=document.getElementById("cr-cat-id").value.trim();if(!o)return alert("请填写分类ID");if(!he.test(o))return alert("分类ID仅允许英文字母/数字/下划线");const r=document.getElementById("cr-cat-zh").value.trim(),p=document.getElementById("cr-cat-en").value.trim();W("创建分类…");try{await nt(s,o,r||o,p||o),F(),C("已创建分类"),a(o)}catch(w){C(w.message,!1)}finally{z()}})}function Rt(s,a){const o=prompt("分类ID（仅字母/数字/下划线）：");if(!o||!he.test(o)){alert("ID非法");return}const r=prompt("分类中文名：")||o,p=prompt("分类英文名：")||o;W("创建分类…"),nt(s,o,r,p).then(()=>{C("已创建分类"),a(o)}).catch(w=>C(w.message,!1)).finally(()=>z())}function xt(s,a,o){var w;function r(b){return`${b.zh||b.id}${b.en?` (${b.en})`:""}`}const p=a.map(b=>`
    <div class="m-row" style="margin-bottom:8px">
      <span style="flex:1">${c(r(b))} <span class="muted">[${c(b.id)}]</span></span>
      <button class="m-btn" data-rename="${c(b.id)}">重命名</button>
      <button class="m-btn danger" data-delcat="${c(b.id)}">删除</button>
    </div>`).join("");te("管理分类",`<div class="modal-scroll">${p||'<p class="muted">暂无分类</p>'}</div>`,'<button type="button" class="m-btn" data-cancel>关闭</button>'),(w=document.querySelector("[data-cancel]"))==null||w.addEventListener("click",F),document.querySelectorAll("[data-rename]").forEach(b=>{b.addEventListener("click",()=>{const g=b.dataset.rename,I=a.find(i=>i.id===g);Pt(s,g,(I==null?void 0:I.zh)??g,(I==null?void 0:I.en)??g,()=>{F(),o()})})}),document.querySelectorAll("[data-delcat]").forEach(b=>{b.addEventListener("click",()=>{var i,_;const g=b.dataset.delcat,I=a.find(R=>R.id===g);te(`删除分类 · ${c((I==null?void 0:I.zh)||g)}`,"<p>将检查关卡使用情况。如果有关卡正在使用该分类的菜谱，则不允许删除。</p>",'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn danger" data-ok>确认删除</button>'),(i=document.querySelector("[data-cancel]"))==null||i.addEventListener("click",F),(_=document.querySelector("[data-ok]"))==null||_.addEventListener("click",async()=>{W("检查中…");try{await It(s,g),F(),C("已删除分类"),o()}catch(R){C(R.message,!1),z()}})})})}function Pt(s,a,o,r,p){te(`重命名分类 · ${c(o||a)}`,`<label class="m-field">分类ID（仅字母/数字/下划线）<input type="text" id="cr-rename-id" value="${c(a)}"></label>
     <label class="m-field">中文名<input type="text" id="cr-rename-zh" value="${c(o)}"></label>
     <label class="m-field">英文名<input type="text" id="cr-rename-en" value="${c(r)}"></label>`,'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>确认</button>'),ot(F,async()=>{const w=document.getElementById("cr-rename-id").value.trim();if(!w)return alert("请填写分类ID");if(!he.test(w))return alert("分类ID仅允许英文字母/数字/下划线");const b=document.getElementById("cr-rename-zh").value.trim(),g=document.getElementById("cr-rename-en").value.trim();W("重命名…");try{await kt(s,a,w,b||w,g||w),F(),C("已重命名分类"),p()}catch(I){C(I.message,!1)}finally{z()}})}function ot(s,a){var o,r;(o=document.querySelector("[data-cancel]"))==null||o.addEventListener("click",s),(r=document.querySelector("[data-ok]"))==null||r.addEventListener("click",a)}export{Ct as renderCustomRecipesView};
