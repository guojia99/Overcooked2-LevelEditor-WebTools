const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./modelPreview--kYUNj62.js","./version-A-zPmgDU.js","./version-CV97QipF.css"])))=>i.map(i=>d[i]);
import{h as F,w as pe,s as J,_ as ze}from"./main-BsPYVMoC.js";import{z as Bt,n as Lt,A as Ct,a5 as fe,a6 as Pe,a7 as ut,I as mt,a8 as Rt,w as pt,p as Mt,a9 as xt,aa as Zt,ab as zt,ac as Pt,o as Q,c as P,ad as Tt,ae as Xt,af as Yt,ag as bt,ah as _t,x as Dt,ai as Ut}from"./version-A-zPmgDU.js";function l(r){return String(r??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}const ye=/^[A-Za-z_][A-Za-z0-9_]*$/;function B(r,d=!0){const o=document.getElementById("cr-status");o&&(o.textContent=r,o.classList.toggle("err",!d),o.classList.toggle("ok",d&&r.length>0))}function Xe(r,d){return document.body.classList.add("manage-bg"),r.innerHTML=`
    ${Lt("custom-recipes")}
    <div class="manage-bar">
      <h1 class="m-title">${l(d)}</h1>
      <span class="status" id="cr-status"></span>
      <span style="flex:1"></span>
    </div>
    <div class="manage-content" id="cr-content"></div>
  `,Ct(o=>{o==="layout"?(location.hash="#/layout",location.reload()):o==="manage"?(location.hash="#/manage",location.reload()):o==="recipes"&&(location.href="/recipes")}),document.getElementById("cr-content")}async function jt(r){const d=Xe(r,"自定义菜谱管理");Ye("加载关卡集…");let o=[];try{o=await Bt()}catch(u){ge(u);return}B(`共 ${o.length} 个关卡集`),d.innerHTML=`
    <div class="m-section-title">选择关卡集</div>
    <p class="modal-hint">选择要管理自定义菜谱的关卡集。首次进入会自动初始化配置。</p>
    <div class="m-grid">${o.map(u=>`
      <div class="m-card">
        <h3>${l(u.levelSetNameZH||u.setName)} <span class="muted">(${l(u.levelSetName||u.setName)})</span></h3>
        <div class="m-meta">
          标识：${l(u.setName)} · 关卡数：${u.levelCount}<br>
          作者：${l(u.author||"—")} · 版本：${l(u.version||"—")}
        </div>
        <div class="m-actions">
          <button class="m-btn primary" data-open="${l(u.setName)}">管理菜谱</button>
        </div>
      </div>`).join("")||'<p class="muted">暂无关卡集</p>'}
    </div>
  `,d.querySelectorAll("[data-open]").forEach(u=>u.addEventListener("click",()=>void de(r,u.dataset.open)))}function Ye(r){const d=document.getElementById("cr-content");d&&(d.innerHTML=`<p class="muted">${l(r)}</p>`),B(r)}function ge(r){const d=r instanceof Error?r.message:String(r);B(d,!1);const o=document.getElementById("cr-content");o&&(o.innerHTML=`<div class="m-block"><h3>出错</h3><p>${l(d)}</p></div>`)}function be(r){return`/api/custom-recipes/icon?assetPath=${encodeURIComponent(r.assetPath)}`}function dt(r,d){return`<img class="food-icon" loading="lazy" src="${d?`/icons/${r}/${encodeURIComponent(d)}.png`:"/icons/_placeholder.png"}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`}function rt(r){var b;const d=((b=r.split("/").pop())==null?void 0:b.replace(/\.asset$/,""))??"model",o=r.replace(/\/[^/]+\.asset$/,"")+"/models/"+encodeURIComponent(d);return`/api/custom-recipes/model-files/${btoa(unescape(encodeURIComponent(o))).replace(/\+/g,"-").replace(/\//g,"_").replace(/=+$/,"")}/`}const ne={url:"/api/custom-recipes/reference-model?path="+encodeURIComponent("Assets/common01/food/CustomRecipes/Pizza/models/plated_mushroom_01.prefab"),format:"obj",scale:.7};async function ft(r,d,o){try{const u=await Xt(r),b=u.find(f=>/\.(fbx|obj)$/i.test(f));if(!b){alert("该菜谱尚未上传模型文件。");return}const{openModelPreview:S}=await ze(async()=>{const{openModelPreview:f}=await import("./modelPreview--kYUNj62.js");return{openModelPreview:f}},__vite__mapDeps([0,1,2]),import.meta.url),y=u.filter(f=>/\.(png|jpg|jpeg)$/i.test(f)).map(f=>rt(r)+encodeURIComponent(f));S({title:d,resourceBase:rt(r),modelFileName:b,scale:o==null?void 0:o.scale,rotationX:o==null?void 0:o.rotationX,rotationY:o==null?void 0:o.rotationY,rotationZ:o==null?void 0:o.rotationZ,positionX:o==null?void 0:o.positionX,positionY:o==null?void 0:o.positionY,positionZ:o==null?void 0:o.positionZ,unitySize:o==null?void 0:o.unitySize,onAdjust:o==null?void 0:o.onAdjust,remoteTextures:y,referenceUrl:ne.url,referenceFormat:ne.format,referenceScale:ne.scale})}catch(u){alert(u.message||"模型预览加载失败。")}}function gt(r){return{guid:r.guid,id:r.id,nameZh:r.nameZh,nameEn:r.nameEn||void 0,assetPath:r.assetPath,cookingStep:r.cookingStepId||void 0,ingredients:r.ingredients,compositionIds:r.compositionIds,score:r.score,isCustom:!0,intermediate:r.intermediate,group:r.group,type:"custom",cookingGroups:r.cookingGroups}}async function de(r,d){var re,K;const o=Xe(r,`自定义菜谱 · ${l(d)}`);Ye("加载菜谱配置…");let u,b,S=new Map,y=new Map;try{[u,b]=await Promise.all([fe(d),Pe(d)]);const m=await ut(d).catch(()=>null);for(const $ of(m==null?void 0:m.platingContainers)??[])y.set($.id,$.nameZh||$.id);let v=!1;const s=await mt().catch(()=>(v=!0,[]));for(const $ of s)S.set($.id,$.nameZh);v&&B("⚠️ 食材数据加载失败（/api/catalog/ingredients），卡片可能缺少食材图标")}catch(m){ge(m);return}B(`${b.length} 个菜谱 · UID前缀：${u.uidPrefix}`);const f=u.categories??[];function w(m){return m.zh||m.id}let a="",T="",M="all";function oe(m){return M===m?" active":""}function O(m){const v=gt(m);return!v.ingredients&&v.compositionIds&&(v.ingredients=v.compositionIds.filter(s=>S.has(s))),v}function Y(){let m=a?b.filter(s=>s.category===a):b;M==="done"?m=m.filter(s=>!s.intermediate):M==="half"&&(m=m.filter(s=>s.intermediate));const v=T.trim().toLowerCase();return v&&(m=m.filter(s=>[s.nameZh,s.nameEn??"",s.id,...s.ingredients??[]].join(" ").toLowerCase().includes(v))),m}function x(){const m=Y();return m.length===0?b.length===0&&T===""&&M==="all"&&a===""?((async()=>{const s=document.getElementById("cr-grid");if(!(!s||s.dataset.diag)){s.dataset.diag="1";try{const $=await Rt(d).catch(()=>null);if(!$){s.innerHTML=`<div class="m-block">
                <h3>暂无菜谱</h3>
                <p class="muted">桥接不支持诊断接口（旧版本）。若磁盘 <code>Assets/LevelSets/${l(d)}/custom_recipes/</code> 下已有菜谱，请：
                <b>在 Unity 中 Tools → Layout Editor → 停止服务 → 启动服务</b>（或重启 Unity 重新编译），再刷新本页。</p>
              </div>`;return}const N=$.dirExists?`磁盘上检测到 <b>${$.fsAssets.length}</b> 个 .asset 文件，扫描命中 <b>${$.scannedCount}</b> 个自定义菜谱，成功加载 <b>${$.loadedCount}</b> 个。`:"目录不存在，尚未创建任何菜谱。",V=$.scannedCount>0&&$.loadedCount===0?'<p class="mp-status err">⚠️ 文件存在但资产加载失败（CustomRecipeSO 类型未加载）。请重启 Unity 并确认 Console 无编译错误。</p>':$.scannedCount===0&&$.fsAssets.length>0?'<p class="mp-status err">⚠️ 文件系统有资产但脚本 guid 不匹配（可能是旧版脚本/副本）。</p>':"";s.innerHTML=`<div class="m-block">
              <h3>暂无菜谱</h3>
              <p class="muted">${N}</p>
              ${V}
              <p class="muted small" style="margin-top:8px">点击右上角「+ 新建菜谱」开始创建；若已创建但未显示，请重启 Unity 桥（Tools → Layout Editor → 停止服务 → 启动服务）后刷新。</p>
            </div>`}catch{s.innerHTML='<div class="m-block"><h3>暂无菜谱</h3><p class="muted">点击右上角「+ 新建菜谱」开始创建。</p></div>'}}})(),'<p class="muted">加载中…</p>'):'<p class="muted">没有匹配的菜谱。点击右上角「+ 新建菜谱」开始创建。</p>':`<div class="rl-grid">${m.map(s=>{let $;try{$=pt(O(s),{allRecipes:b.map(O),ingredientName:H=>S.get(H)??H,iconSrc:()=>be(s)})}catch(H){$=`<div class="m-card"><h3>${l(s.nameZh)}</h3><p class="muted">卡片渲染失败：${l(H.message)}</p></div>`}const N=f.find(H=>H.id===s.category),V=s.platingStepId?y.get(s.platingStepId):"",ee=(s.compositionIds??[]).length;return`
      <div class="cr-card-wrap">
        <div class="cr-card-inner">${$}</div>
        <div class="cr-card-foot">
          <span class="cr-cat-tag">${l(w(N??{id:s.category,zh:s.category,en:s.category}))}</span>
          ${V?`<span class="cr-cat-tag cr-plate-tag" title="装盘容器">🍽 ${l(V)}</span>`:""}
          ${s.intermediate?'<span class="cr-cat-tag cr-half-tag">中间产物</span>':""}
          <span class="muted small">UID ${s.uID} · 组成 ${ee} 项</span>
          <span style="flex:1"></span>
          ${s.hasModel?`<button class="m-btn small" data-preview="${l(s.assetPath)}" title="3D 模型在线预览">👁</button>`:""}
          <button class="m-btn small" data-edit="${l(s.assetPath)}">编辑</button>
          <button class="m-btn small danger" data-del="${l(s.assetPath)}">删除</button>
        </div>
      </div>`}).join("")}</div>`}function se(){return`
    <div class="cr-sidebar">
      <div class="m-section-title">分类</div>
      <div class="cr-cat-list">
        <button class="m-btn cr-cat-item${a===""?" primary":""}" data-cat="">全部 (${b.length})</button>
        ${f.map(m=>{const v=b.filter(s=>s.category===m.id).length;return`<button class="m-btn cr-cat-item${a===m.id?" primary":""}" data-cat="${l(m.id)}">${l(w(m))} (${v})</button>`}).join("")}
        <div class="cr-cat-actions">
          <button class="m-btn" id="cr-new-cat">+ 新建分类</button>
          ${f.length>0?'<button class="m-btn" id="cr-manage-cat">管理分类</button>':""}
        </div>
      </div>
    </div>`}o.innerHTML=`
    <div class="m-actions-row">
      <button class="m-btn" id="cr-back">← 返回关卡集列表</button>
      <span class="muted">当前关卡集：<b>${l(d)}</b></span>
      <span style="flex:1"></span>
      <button class="m-btn primary" id="cr-new-recipe">+ 新建菜谱</button>
    </div>
    <div class="cr-toolbar">
      <input type="search" id="cr-search" class="rl-search" placeholder="搜索菜名 / ID / 食材…" autocomplete="off">
      <div class="ing-groups">
        <button type="button" class="cr-comp-chip${oe("all")}" data-role="all">全部</button>
        <button type="button" class="cr-comp-chip${oe("done")}" data-role="done">成品</button>
        <button type="button" class="cr-comp-chip${oe("half")}" data-role="half">中间产物</button>
      </div>
    </div>
    <div class="cr-layout">
      <div id="cr-sidebar">${se()}</div>
      <div id="cr-grid">${x()}</div>
    </div>
  `;function ae(){(async()=>{J("加载…");try{[u,b]=await Promise.all([fe(d),Pe(d)])}catch(m){ge(m);return}finally{F()}B(`${b.length} 个菜谱 · UID前缀：${u.uidPrefix}`),document.getElementById("cr-sidebar").innerHTML=se(),ce(),document.getElementById("cr-grid").innerHTML=x(),X()})()}function ve(){var m;(m=document.getElementById("cr-search"))==null||m.addEventListener("input",v=>{T=v.target.value,document.getElementById("cr-grid").innerHTML=x(),X()}),document.querySelectorAll("[data-role]").forEach(v=>{v.addEventListener("click",()=>{M=v.dataset.role??"all",document.querySelectorAll("[data-role]").forEach(s=>s.classList.toggle("active",s===v)),document.getElementById("cr-grid").innerHTML=x(),X()})})}function ce(){var m,v;document.querySelectorAll(".cr-cat-item").forEach(s=>{s.addEventListener("click",()=>{a=s.dataset.cat??"",document.getElementById("cr-sidebar").innerHTML=se(),ce(),document.getElementById("cr-grid").innerHTML=x(),X()})}),(m=document.getElementById("cr-new-cat"))==null||m.addEventListener("click",()=>Ht(d,s=>{a=s??"",de(r,d)})),(v=document.getElementById("cr-manage-cat"))==null||v.addEventListener("click",()=>At(d,u.categories,()=>void de(r,d)))}function X(){document.querySelectorAll("[data-edit]").forEach(m=>m.addEventListener("click",()=>void Te(r,d,m.dataset.edit))),document.querySelectorAll("[data-preview]").forEach(m=>m.addEventListener("click",()=>{const v=m.dataset.preview,s=b.find(N=>N.assetPath===v),$=s&&s.modelScale>0?s.modelScale:1;ft(v,v.split("/").pop()??v,{scale:(s==null?void 0:s.modelScale)??1,rotationX:(s==null?void 0:s.modelRotationX)??0,rotationY:(s==null?void 0:s.modelRotationY)??0,rotationZ:(s==null?void 0:s.modelRotationZ)??0,positionX:(s==null?void 0:s.modelPositionX)??0,positionY:(s==null?void 0:s.modelPositionY)??0,positionZ:(s==null?void 0:s.modelPositionZ)??0,unitySize:s&&s.boundsSizeX!=null&&s.boundsSizeY!=null&&s.boundsSizeZ!=null?{x:s.boundsSizeX/$,y:s.boundsSizeY/$,z:s.boundsSizeZ/$,minY:((s.boundsMinY??0)-(s.modelPositionY??0))/$}:void 0})})),document.querySelectorAll("[data-del]").forEach(m=>m.addEventListener("click",()=>Ft(r,d,m.dataset.del,ae)))}ve(),ce(),X(),(re=document.getElementById("cr-back"))==null||re.addEventListener("click",()=>void jt(r)),(K=document.getElementById("cr-new-recipe"))==null||K.addEventListener("click",()=>void Te(r,d,null))}async function Te(r,d,o,u){var Oe,Ne,Ve,Ge,We,Qe,Je,Ke,et,tt,nt,ot,st,at,ct,it;const b=o!=null,S=Xe(r,b?"编辑菜谱":"新建菜谱");Ye("加载参考数据…");let y=[],f,w,a,T=[],M=[];const oe={cookingSteps:[],platingSteps:[],platingContainers:[],icons:[],reusableModels:[],ingredients:[]};try{[y,f,w]=await Promise.all([mt().catch(()=>[]),ut(d).catch(()=>oe),fe(d)]),[T,M]=await Promise.all([Pe(d).catch(()=>[]),Mt(d).catch(()=>[])]),b&&(a=T.find(e=>e.assetPath===o))}catch(e){ge(e),F();return}F();const O=new Set;for(const e of T)O.add(e.id);for(const e of M)e.isCustom&&O.add(e.id);const Y=new Map,x=new Map;for(const e of(a==null?void 0:a.compositionIds)??[]){const t=O.has(e)?x:Y;t.set(e,(t.get(e)??0)+1)}const se=!b,ae=(a==null?void 0:a.recipeName)??"",ve=(a==null?void 0:a.nameZh)??"",ce=(a==null?void 0:a.nameEn)??"",X=(a==null?void 0:a.category)??(u==null?void 0:u.category)??(((Oe=w.categories)==null?void 0:Oe.length)>0?w.categories[0].id:""),re=(a==null?void 0:a.score)??(u==null?void 0:u.score)??0,K=(a==null?void 0:a.type)??((u==null?void 0:u.score)===0?"Cooked":"Composite"),m=(a==null?void 0:a.cookingStepId)??"",v="",s=(a==null?void 0:a.platingStepId)??"",$="",N="",V=w.categories??[],ee=new Map;for(const e of y)ee.set(e.id,e);const H=y.length===0,_e=(()=>{const e=[];for(const n of T)b&&n.assetPath===o||e.push({id:n.id,nameZh:n.nameZh,nameEn:n.nameEn,score:n.score,cookingStepId:n.cookingStepId,hasIcon:n.hasIcon,assetPath:n.assetPath,official:!1,ingredients:n.ingredients});const t=new Set(e.map(n=>n.id));for(const n of M)!n.isCustom||(n.group??"")==="levelset"||t.has(n.id)||(e.push({id:n.id,nameZh:n.nameZh,nameEn:n.nameEn,score:n.score??0,cookingStepId:n.cookingStep??"",hasIcon:!!n.icon,assetPath:n.assetPath,official:!0,ingredients:n.ingredients??[]}),t.add(n.id));return e.sort((n,i)=>n.score-i.score||Number(n.official)-Number(i.official)||n.nameZh.localeCompare(i.nameZh,"zh")),e})(),ue=new Map;for(const e of _e)ue.set(e.id,e);const vt=[...T.map(gt),...M.filter(e=>e.isCustom).map(e=>({...e,isCustom:!0}))];function ht(e){return e.zh||e.id}function $t(){let e=V.map(t=>`<option value="${l(t.id)}" ${t.id===X?"selected":""}>${l(ht(t))}</option>`).join("");return!V.some(t=>t.id===X)&&X&&(e+=`<option value="${l(X)}" selected>${l(X)}</option>`),`<select id="cr-type-cat" class="m-select">${e}</select>`}function ie(e,t,n){const i=new Map;for(const c of e)i.has(c.id)||i.set(c.id,c.nameZh||c.id);return`<select id="${n}" class="m-select">
      <option value="">— 不设置 —</option>
      ${[...i.entries()].map(([c,p])=>`<option value="${l(c)}" ${c===t?"selected":""}>${l(p)} (${l(c)})</option>`).join("")}
    </select>`}function he(){const e=[];for(const[t,n]of Y)for(let i=0;i<n;i++)e.push(t);for(const[t,n]of x)for(let i=0;i<n;i++)e.push(t);return e}function $e(e,t,n){const i=n>0,c=i?`<div class="cp-count">
          <button type="button" class="cp-step" data-cpdec data-cpid="${l(e)}" data-cpsub="${t?1:0}">−</button>
          <span class="cp-num">${n}</span>
          <button type="button" class="cp-step" data-cpinc data-cpid="${l(e)}" data-cpsub="${t?1:0}">＋</button>
        </div>`:"";if(t){const g=ue.get(e);if(!g)return"";const Z=g.score<=0?'<span class="cr-badge-half">中间产物</span>':'<span class="cr-badge-done">成品菜 · 可作组成</span>',D=g.official?`/icons/recipes/${encodeURIComponent(g.id)}.png`:be(g);return`<div class="pick-card cp-card${i?" selected":""}" data-cpid="${l(e)}" data-cpsub="1" title="${l(g.id)}">
        <span class="pc-head"><img class="food-icon" loading="lazy" src="${l(D)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'" /><span class="pc-name">${l(g.nameZh)}${Z}${g.nameEn?` <span class="muted pc-en">${l(g.nameEn)}</span>`:""}</span></span>
        <span class="muted small">${g.cookingStepId?l(g.cookingStepId):"无烹饪步骤"} · ${(g.ingredients??[]).length} 种食材${g.official?" · 官方":(g.score<=0," · 本关卡集")}</span>
        ${c}
      </div>`}const p=ee.get(e);if(!p)return"";const E=p.group&&p.group!=="core"?` <span class="pc-badge">${Dt(p.group)}</span>`:"",k=p.nameEn&&p.nameEn.trim()||"";return`<div class="pick-card cp-card${i?" selected":""}" data-cpid="${l(e)}" data-cpsub="0" title="${l(p.id)}">
      <span class="pc-head">${dt("ingredients",p.id)}<span class="pc-name">${l(p.nameZh)}${E}${k?` <span class="muted pc-en">${k}</span>`:""}</span></span>
      ${c}
    </div>`}function Et(){var Z,D;const e=new Map;for(const[I,L]of Y)e.set(I,L);for(const[I,L]of x)e.set(I,L);let t="all",n="";function i(){const I=n.trim().toLowerCase(),L=[];if(t==="all"||t==="ing")for(const h of y)I&&!h.nameZh.toLowerCase().includes(I)&&!(h.nameEn??"").toLowerCase().includes(I)&&!h.id.toLowerCase().includes(I)||L.push({id:h.id,isSub:!1});const R=_e.filter(h=>t==="all"||(t==="sub"?h.score<=0:h.score>0));for(const h of R)I&&!h.nameZh.toLowerCase().includes(I)&&!(h.nameEn??"").toLowerCase().includes(I)&&!h.id.toLowerCase().includes(I)||L.push({id:h.id,isSub:!0});return L}function c(){const I=i();return`
        <div class="cr-comp-toolbar">
          <input type="search" id="cp-search" class="ing-search" placeholder="搜索名称 / ID…" autocomplete="off">
          <div class="ing-groups">${[`<button type="button" class="cr-comp-chip${t==="all"?" active":""}" data-filter="all">全部</button>`,`<button type="button" class="cr-comp-chip${t==="ing"?" active":""}" data-filter="ing">食材</button>`,`<button type="button" class="cr-comp-chip${t==="sub"?" active":""}" data-filter="sub">中间产物</button>`,`<button type="button" class="cr-comp-chip${t==="done"?" active":""}" data-filter="done">成品菜</button>`].join("")}</div>
        </div>
        ${H&&t!=="sub"&&t!=="done"?'<p class="mp-status err">⚠️ 未加载到食材数据（桥接 /api/catalog/ingredients 异常），请刷新重试或检查 Unity 桥。</p>':""}
        <div class="modal-scroll" id="cp-scroll">
          <div class="pick-grid" id="cp-grid">${I.map(h=>$e(h.id,h.isSub,e.get(h.id)??0)).join("")}</div>
          ${I.length?"":'<p class="muted">没有匹配的项</p>'}
        </div>`}Q("添加食材 / 菜谱",`<p class="modal-hint">点击卡片加入（默认 1 份），再次点击 − / ＋ 调整数量；同一项可多次使用。<b>任何菜谱（成品菜或中间产物）都能作为本菜谱的组成</b>，如鸡蛋汉堡 = 煎蛋（成品菜）+ 面包。</p>
       <div id="cp-body">${c()}</div>`,`<button type="button" class="m-btn" data-cancel>取消</button>
       <button type="button" class="m-btn primary" data-ok>确定</button>`);const p=document.querySelector(".modal-panel");p&&p.classList.add("wide");const E=document.getElementById("cp-body");function k(){E.innerHTML=c(),g()}function g(){var I,L;(I=document.getElementById("cp-search"))==null||I.addEventListener("input",R=>{n=R.target.value;const h=document.getElementById("cp-grid");if(h){h.innerHTML=i().map(C=>$e(C.id,C.isSub,e.get(C.id)??0)).join("");const z=document.getElementById("cp-scroll");if(z){const C=z.querySelector("p.muted");C&&C.remove(),i().length===0&&z.insertAdjacentHTML("beforeend",'<p class="muted">没有匹配的项</p>')}}}),document.querySelectorAll("#cp-body .cr-comp-chip").forEach(R=>{R.addEventListener("click",()=>{t=R.dataset.filter??"all",k()})}),(L=document.getElementById("cp-grid"))==null||L.addEventListener("click",R=>{const h=R.target,z=h.closest(".cp-step"),C=h.closest(".cp-card");if(!C)return;const W=C.dataset.cpid,Ze=C.dataset.cpsub==="1";let U=e.get(W)??0;if(z){const me=z.dataset.cpinc!==void 0?1:-1;U=Math.max(0,U+me)}else U=U>0?0:1;U<=0?e.delete(W):e.set(W,U),C.outerHTML=$e(W,Ze,U)})}g(),(Z=document.querySelector("[data-cancel]"))==null||Z.addEventListener("click",P),(D=document.querySelector("[data-ok]"))==null||D.addEventListener("click",()=>{Y.clear(),x.clear();for(const[I,L]of e)(O.has(I)?x:Y).set(I,L);P(),Ee(),_()})}function Ee(){const e=document.getElementById("cr-comp-list");if(!e)return;const t=[];for(const[c,p]of Y){const E=ee.get(c);t.push(`<div class="cr-comp-row" data-rowid="ing:${l(c)}">
        ${dt("ingredients",c)}
        <span class="cr-row-name">${l((E==null?void 0:E.nameZh)??c)}</span>
        <span class="cr-row-stepper">
          <button type="button" class="cr-step" data-rowdec="ing:${l(c)}">−</button>
          <span class="cr-step-num">${p}</span>
          <button type="button" class="cr-step" data-rowinc="ing:${l(c)}">＋</button>
        </span>
        <button type="button" class="cr-step-del" data-rowdel="ing:${l(c)}" title="移除">×</button>
      </div>`)}for(const[c,p]of x){const E=ue.get(c),k=((E==null?void 0:E.score)??0)<=0?'<span class="cr-chip-tag">中间产物</span>':'<span class="cr-chip-tag">成品菜</span>';t.push(`<div class="cr-comp-row cr-comp-row-sub" data-rowid="sub:${l(c)}">
        ${E?`<img class="food-icon" loading="lazy" src="${l(E.official?`/icons/recipes/${encodeURIComponent(E.id)}.png`:be(E))}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'" />`:""}
        <span class="cr-row-name">${l((E==null?void 0:E.nameZh)??c)}${k}</span>
        <span class="cr-row-stepper">
          <button type="button" class="cr-step" data-rowdec="sub:${l(c)}">−</button>
          <span class="cr-step-num">${p}</span>
          <button type="button" class="cr-step" data-rowinc="sub:${l(c)}">＋</button>
        </span>
        <button type="button" class="cr-step-del" data-rowdel="sub:${l(c)}" title="移除">×</button>
      </div>`)}e.innerHTML=t.length?t.join(""):'<p class="muted small" style="margin:4px 0">尚未选择。点击下方「添加」选择食材或中间产物。</p>';const n=he().length,i=document.getElementById("cr-comp-hint");i&&(i.textContent=t.length?`共 ${n} 份组成（食材 ${[...Y.values()].reduce((c,p)=>c+p,0)} · 中间产物 ${[...x.values()].reduce((c,p)=>c+p,0)}）`:""),e.querySelectorAll("[data-rowinc], [data-rowdec], [data-rowdel]").forEach(c=>{c.addEventListener("click",()=>{const p=c.dataset.rowinc??c.dataset.rowdec??c.dataset.rowdel??"",[E,k]=p.split(":"),g=E==="ing"?Y:x,Z=g.get(k)??0;c.dataset.rowdel!==void 0?g.delete(k):c.dataset.rowinc!==void 0?g.set(k,Z+1):Z<=1?g.delete(k):g.set(k,Z-1),Ee(),_()})})}function It(e){const t=[];for(const n of e){const i=ue.get(n);i&&(i.ingredients??[]).length>0?t.push(...i.ingredients):t.push(n)}return t}let te=null;function _(){var Z,D,I,L,R,h;const e=document.getElementById("cr-preview");if(!e)return;const t=((Z=document.getElementById("cr-rec-name"))==null?void 0:Z.value.trim())??"",n=((D=document.getElementById("cr-zh"))==null?void 0:D.value.trim())??"",i=((I=document.getElementById("cr-en"))==null?void 0:I.value.trim())??"",c=Number((L=document.getElementById("cr-score"))==null?void 0:L.value)||0,E=(((R=document.getElementById("cr-type"))==null?void 0:R.value)??"Composite")==="Composite"?"":((h=document.getElementById("cr-cook-step"))==null?void 0:h.value)??"",k=he(),g={guid:"",id:t||"preview",nameZh:n||t||"未命名菜谱",nameEn:i||void 0,assetPath:"",isCustom:!0,group:"levelset",score:c,intermediate:c<=0,ingredients:It(k),compositionIds:k,cookingStep:E||void 0,type:"custom"};e.innerHTML=pt(g,{allRecipes:vt,ingredientName:z=>{var C;return((C=ee.get(z))==null?void 0:C.nameZh)??z},iconSrc:te?()=>te:b&&(a!=null&&a.hasIcon)&&a.assetPath?()=>be(a):void 0})}S.innerHTML=`
    <div class="m-actions-row">
      <button class="m-btn" id="cr-form-back">← 返回菜谱列表</button>
      <span class="muted">关卡集：<b>${l(d)}</b> · ${b?`编辑 ${l(ae)}`:"新建菜谱"}</span>
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
          <input type="text" id="cr-rec-name" value="${l(ae)}" ${b?"disabled":""} placeholder="MyRecipe">
        </label>
        <div class="cr-form-grid">
          <label class="m-field">中文名<input type="text" id="cr-zh" value="${l(ve)}" placeholder="我的菜谱"></label>
          <label class="m-field">英文名<input type="text" id="cr-en" value="${l(ce)}" placeholder="My Recipe"></label>
          <label class="m-field">分类 ${$t()}
            <button type="button" class="m-btn" id="cr-new-cat-inline" style="margin-top:6px">+ 新建分类</button>
          </label>
          <label class="m-field">类型
            <select id="cr-type" class="m-select">
              <option value="Composite" ${K==="Composite"?"selected":""}>Composite（组合）</option>
              <option value="Cooked" ${K==="Cooked"?"selected":""}>Cooked（烹饪）</option>
              <option value="Mixed" ${K==="Mixed"?"selected":""}>Mixed（搅拌）</option>
            </select>
          </label>
          <label class="m-field">分数<input type="number" id="cr-score" value="${re}" min="0">
            <span class="muted small">0 = 中间产物（不直接上桌，可被其他菜谱引用）</span>
          </label>
          <label class="m-field">UID（自动生成）<input type="text" value="${(a==null?void 0:a.uID)??(se?w.uidPrefix*1e3+w.nextSequence:"—")}" disabled></label>
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
        <p class="modal-hint" id="cr-step-hint">按类型配置：<b>Cooked（烹饪）</b>需烹饪步骤与装盘容器；<b>Mixed（搅拌）</b>只需搅拌与装盘容器；<b>Composite（组合）</b>纯组装，无需烹饪与装盘。</p>
        <div class="cr-form-grid">
          <label class="m-field" id="cr-cook-step-field">烹饪步骤 ${ie(f.cookingSteps,m,"cr-cook-step")}</label>
          <label class="m-field" id="cr-cook-icon-field">烹饪图标 ${ie(f.icons,v,"cr-cook-icon")}</label>
          <label class="m-field" id="cr-cook-prog-field">烹饪程度
            <select id="cr-cook-prog" class="m-select">
              <option value="0">Raw（生）</option>
              <option value="1" selected>Cooked（熟）</option>
              <option value="2">Burnt（焦）</option>
            </select>
          </label>
          <label class="m-field" id="cr-plate-field">装盘容器 ${ie((Ne=f.platingContainers)!=null&&Ne.length?f.platingContainers:(f.platingSteps??[]).filter(e=>e.id==="Plate"||e.id==="Glass"),s,"cr-plate-step")}
            <span class="muted small">决定上桌容器（盘子/杯子），运行时映射为 PlatingStepData</span>
          </label>
          <label class="m-field" id="cr-mix-icon-field" style="display:none">搅拌图标 ${ie(f.icons,$,"cr-mix-icon")}</label>
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
          <label class="m-field">复用已有模型 ${ie(f.reusableModels,N,"cr-model-ref")}</label>
          <label class="m-field">模型缩放<input type="number" id="cr-model-scale" value="${(a==null?void 0:a.modelScale)??1}" min="0.0001" step="0.001">
            <span class="muted small">实际游戏中的大小（相对原模型尺寸；单位制异常的模型可为极小值如 0.0037）</span></label>
          <label class="m-field">旋转 X（度）<input type="number" id="cr-model-rot-x" value="${(a==null?void 0:a.modelRotationX)??0}" step="5">
            <span class="muted small">前后翻转</span></label>
          <label class="m-field">旋转 Y（度）<input type="number" id="cr-model-rot-y" value="${(a==null?void 0:a.modelRotationY)??0}" step="5">
            <span class="muted small">俯视转向</span></label>
          <label class="m-field">旋转 Z（度）<input type="number" id="cr-model-rot-z" value="${(a==null?void 0:a.modelRotationZ)??0}" step="5">
            <span class="muted small">侧倒修正；模型竖立时（如煎蛋）用 X/Z 旋转摆平</span></label>
          <label class="m-field">位置 X<input type="number" id="cr-model-pos-x" value="${(a==null?void 0:a.modelPositionX)??0}" step="0.05">
            <span class="muted small">左右偏移</span></label>
          <label class="m-field">位置 Y（底面高度）<input type="number" id="cr-model-pos-y" value="${(a==null?void 0:a.modelPositionY)??0}" step="0.05">
            <span class="muted small">模型底面相对盘面的高度，向上为正</span></label>
          <label class="m-field">位置 Z<input type="number" id="cr-model-pos-z" value="${(a==null?void 0:a.modelPositionZ)??0}" step="0.05">
            <span class="muted small">前后偏移</span></label>
          <label class="m-field">在线预览<button type="button" class="m-btn" id="cr-preview-model">👁 预览并调整方向/大小</button>
            <span class="muted small">已保存的模型可随时预览调整；上传新文件则自动预览</span></label>
          <label class="m-field">模型诊断<button type="button" class="m-btn" id="cr-diagnose">🔍 检查装盘链路</button>
            <span class="muted small">模型在游戏中不显示时，检查引用/网格/材质与匹配链路</span></label>
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
  `,_(),Ee();function De(){var p;const e=((p=document.getElementById("cr-type"))==null?void 0:p.value)??"Composite",t=e==="Cooked",n=e!=="Composite",i=e==="Mixed",c=(E,k)=>{const g=document.getElementById(E);g&&(g.style.display=k?"":"none")};c("cr-cook-step-field",t),c("cr-cook-icon-field",t),c("cr-cook-prog-field",t),c("cr-plate-field",n),c("cr-mix-icon-field",i),c("cr-mix-prog-field",i),_()}(Ve=document.getElementById("cr-add-comp"))==null||Ve.addEventListener("click",()=>Et());const Ie=document.getElementById("cr-model-scale"),we=document.getElementById("cr-model-rot-y"),Se=document.getElementById("cr-model-rot-x"),ke=document.getElementById("cr-model-rot-z"),Be=document.getElementById("cr-model-pos-x"),Le=document.getElementById("cr-model-pos-y"),Ce=document.getElementById("cr-model-pos-z"),j=document.getElementById("cr-model-file"),G=document.getElementById("cr-model-texture"),Re=()=>{const e=(t,n)=>{const i=Number(t==null?void 0:t.value);return Number.isFinite(i)?i:n};return{scale:e(Ie,1),rotationX:e(Se,0),rotationY:e(we,0),rotationZ:e(ke,0),positionX:e(Be,0),positionY:e(Le,0),positionZ:e(Ce,0)}},wt=e=>{Ie&&(Ie.value=String(Math.round(e.scale*1e4)/1e4)),Se&&(Se.value=String(Math.round(e.rotationX))),we&&(we.value=String(Math.round(e.rotationY))),ke&&(ke.value=String(Math.round(e.rotationZ))),Be&&(Be.value=String(Math.round(e.positionX*100)/100)),Le&&(Le.value=String(Math.round(e.positionY*100)/100)),Ce&&(Ce.value=String(Math.round(e.positionZ*100)/100))},Ue=()=>{if(!a||a.boundsSizeX==null||a.boundsSizeY==null||a.boundsSizeZ==null)return;const e=a.modelScale>0?a.modelScale:1;return{x:a.boundsSizeX/e,y:a.boundsSizeY/e,z:a.boundsSizeZ/e,minY:((a.boundsMinY??0)-(a.modelPositionY??0))/e}};let q=null,le="";const A={};let Me=null;const je=e=>{wt(e)},St=(e,t,n)=>{const i=/\.([^.]+)$/.exec(n);let c=i?"."+i[1].toLowerCase():".png";return c!==".png"&&c!==".jpg"&&c!==".jpeg"&&(c=".png"),`${e}_${t}${c}`},Fe=()=>{var n;const e=((n=document.getElementById("cr-rec-name"))==null?void 0:n.value.trim())||"texture",t={};for(const i of Object.keys(A)){const c=A[i];c&&(t[i]=St(e,i,c.file.name))}return t},He=async()=>{if(!q)return null;const{renameFbxTextureRefs:e}=await ze(async()=>{const{renameFbxTextureRefs:t}=await import("./fbxTextureRename-O8eilyGQ.js");return{renameFbxTextureRefs:t}},[],import.meta.url);return e(q,Fe())},xe=()=>{var t;const e=(t=j==null?void 0:j.files)==null?void 0:t[0];!e&&!q||pe("正在加载 3D 预览…",async()=>{e&&(q=new Uint8Array(await e.arrayBuffer()),le=e.name);const n=await He();if(!n)return;const i=A.base_color,{openModelPreview:c}=await ze(async()=>{const{openModelPreview:p}=await import("./modelPreview--kYUNj62.js");return{openModelPreview:p}},__vite__mapDeps([0,1,2]),import.meta.url);c({title:le,resourceBase:"",modelFileName:le,localBuffer:n.bytes.buffer.slice(n.bytes.byteOffset,n.bytes.byteOffset+n.bytes.byteLength),localTextures:i?[i.file]:void 0,...Re(),unitySize:Ue(),onAdjust:je,referenceUrl:ne.url,referenceFormat:ne.format,referenceScale:ne.scale})})};j==null||j.addEventListener("change",xe),document.querySelectorAll(".cr-tex-slot").forEach(e=>{e.addEventListener("click",()=>{Me=e.dataset.tex??null,!(!Me||!G)&&(G.value="",G.click())})}),G==null||G.addEventListener("change",()=>{var n;const e=(n=G.files)==null?void 0:n[0],t=Me;!e||!t||pe("正在加载贴图…",async()=>{const i=A[t];i&&URL.revokeObjectURL(i.url);const c=URL.createObjectURL(e);A[t]={file:e,bytes:new Uint8Array(await e.arrayBuffer()),url:c};const p=document.getElementById("cr-tex-thumb-"+t);p&&(p.innerHTML=`<img src="${c}" alt="${t}">`),q&&xe()})}),(Ge=document.getElementById("cr-preview-model"))==null||Ge.addEventListener("click",()=>{var e;if((e=j==null?void 0:j.files)!=null&&e[0]){xe();return}o&&pe("正在加载 3D 预览（首次加载较慢）…",async()=>{await ft(o,ae||(o.split("/").pop()??o),{...Re(),unitySize:Ue(),onAdjust:je})})}),(We=document.getElementById("cr-diagnose"))==null||We.addEventListener("click",()=>{if(!o){alert("保存菜谱后才能诊断。");return}pe("正在诊断…",async()=>{try{const e=await Ut(o),t=[];if(e.error&&t.push(`错误：${e.error}`),t.push(`模型引用：${e.modelDirect?"model 直引 ✓":"无直引"}${e.modelSOBased?"（另有 modelSO）":""}`),t.push(`模型路径：${e.modelPath||"—"}${e.modelPath?`（${e.modelType}）`:""}`),t.push(`模型结构：${e.modelStructure||"—"}`),t.push(`渲染器 ${e.rendererCount} 个 · 含网格 ${e.meshCount} 个 · 含材质 ${e.materialCount} 个（网格/材质齐全才可见）`),e.boundsSizeX||e.boundsSizeY||e.boundsSizeZ){const i=[...[["X",e.boundsSizeX],["Y",e.boundsSizeY],["Z",e.boundsSizeZ]]].sort((c,p)=>c[1]-p[1])[0];t.push(`Unity 内模型包围盒（含变换）：X ${e.boundsSizeX.toFixed(3)} · Y ${e.boundsSizeY.toFixed(3)} · Z ${e.boundsSizeZ.toFixed(3)}`),t.push(`薄轴 = ${i[0]}（${i[1].toFixed(3)}）：${i[0]==="Y"?"模型平躺 ✓":"模型竖立 ✗，需在预览中用「旋转 90°」或手动设 X/Z 旋转摆平"}；底面 Y = ${e.boundsMinY.toFixed(3)}`)}t.push(`组成：${e.compositionCount} 项（≥1 才可装盘匹配）`),t.push(`烹饪步骤：${e.cookingStepSet?"已配置 ✓":"未配置"} · 装盘步骤：${e.platingStepSet?"已配置 ✓":"未配置"}`),t.push(`装盘模型 GetModel：${e.platingPrefabSet?"非空 ✓":"为空 ✗（游戏中会显示空盘子）"}`),t.push(`变换：缩放 ${e.modelScale} · 旋转 X/Y/Z ${e.modelRotationX}°/${e.modelRotationY}°/${e.modelRotationZ}° · 位置 ${e.modelPositionX}/${e.modelPositionY}/${e.modelPositionZ}`),qe(t)}catch(e){qe([`诊断失败：${e.message||String(e)}`])}})});function qe(e){var c;const t=e.join(`
`),n=`<div class="diag-scroll"><pre class="diag-text">${l(t)}</pre></div>`;Q("装盘链路诊断",n,`<button type="button" class="m-btn" id="diag-copy">📋 复制链路数据</button>
       <button type="button" class="m-btn primary" data-cancel>关闭</button>`),(c=document.querySelector("[data-cancel]"))==null||c.addEventListener("click",P);const i=document.getElementById("diag-copy");i==null||i.addEventListener("click",async()=>{try{await navigator.clipboard.writeText(t),i.textContent="已复制 ✓"}catch{i.textContent="复制失败"}setTimeout(()=>{i&&(i.textContent="📋 复制链路数据")},2e3)})}(Qe=document.getElementById("cr-new-sub"))==null||Qe.addEventListener("click",()=>{var e;return void Te(r,d,null,{score:0,category:((e=document.getElementById("cr-type-cat"))==null?void 0:e.value)||X})}),(Je=document.getElementById("cr-type"))==null||Je.addEventListener("change",De),(Ke=document.getElementById("cr-score"))==null||Ke.addEventListener("input",_),(et=document.getElementById("cr-zh"))==null||et.addEventListener("input",_),(tt=document.getElementById("cr-en"))==null||tt.addEventListener("input",_),(nt=document.getElementById("cr-rec-name"))==null||nt.addEventListener("input",_),(ot=document.getElementById("cr-cook-step"))==null||ot.addEventListener("change",_),(st=document.getElementById("cr-icon-upload"))==null||st.addEventListener("change",e=>{var n;te&&(URL.revokeObjectURL(te),te=null);const t=(n=e.target.files)==null?void 0:n[0];t&&(te=URL.createObjectURL(t)),_()}),De(),(at=document.getElementById("cr-new-cat-inline"))==null||at.addEventListener("click",()=>qt(d,async e=>{w=await fe(d);const t=document.getElementById("cr-type-cat");t&&e&&(t.innerHTML=w.categories.map(n=>`<option value="${l(n.id)}">${l(n.zh||n.id)}</option>`).join(""),t.value=e)})),(ct=document.getElementById("cr-form-back"))==null||ct.addEventListener("click",()=>void de(r,d)),(it=document.getElementById("cr-form-save"))==null||it.addEventListener("click",async()=>{var c,p,E,k,g,Z,D,I,L;const e=document.getElementById("cr-rec-name").value.trim();if(!e)return alert("请填写标识符");if(!ye.test(e))return alert("标识符仅允许英文字母/数字/下划线，且不能以数字开头");const t=document.getElementById("cr-type").value,n=Re(),i={setName:b?void 0:d,assetPath:b?o:void 0,recipeName:e,nameZh:document.getElementById("cr-zh").value.trim(),nameEn:document.getElementById("cr-en").value.trim(),category:document.getElementById("cr-type-cat").value,score:Number(document.getElementById("cr-score").value)||0,type:t,compositionIds:he(),cookingStepId:t==="Composite"?"":((c=document.getElementById("cr-cook-step"))==null?void 0:c.value)??"",cookingStepIconId:((p=document.getElementById("cr-cook-icon"))==null?void 0:p.value)??"",platingStepId:((E=document.getElementById("cr-plate-step"))==null?void 0:E.value)??"",mixingIconId:((k=document.getElementById("cr-mix-icon"))==null?void 0:k.value)??"",modelPrefabId:((g=document.getElementById("cr-model-ref"))==null?void 0:g.value)??"",cookingProgress:Number(((Z=document.getElementById("cr-cook-prog"))==null?void 0:Z.value)??"1")||1,mixingProgress:Number(((D=document.getElementById("cr-mix-prog"))==null?void 0:D.value)??"1")||1,modelScale:n.scale,modelRotationX:n.rotationX,modelRotationY:n.rotationY,modelRotationZ:n.rotationZ,modelPositionX:n.positionX,modelPositionY:n.positionY,modelPositionZ:n.positionZ};J("保存中…");try{b?await xt(i):await Zt(i);const R=o||`Assets/LevelSets/${d}/custom_recipes/${i.category}/${e}.asset`,h=(I=document.getElementById("cr-icon-upload").files)==null?void 0:I[0];if(h){const C=await kt(h);await zt(d,R,h.name,C)}const z=(L=document.getElementById("cr-model-file").files)==null?void 0:L[0];if(z){if(!/\.fbx$/i.test(z.name)){B("仅支持 FBX 模型文件。",!1);return}if(q||(q=new Uint8Array(await z.arrayBuffer()),le=z.name),!A.base_color){B("请先在 base_color 格子中选择彩色贴图。",!1);return}const C=await He()??{bytes:q,renamed:0};C.renamed===0&&alert("警告：FBX 中未找到可改写的贴图引用（可能不是二进制 FBX 或不含贴图引用），贴图引用名未修改。");const W=[{fileName:le,base64:await Ae(C.bytes)}],Ze=Fe();for(const U of Object.keys(A)){const me=A[U],lt=Ze[U];me&&lt&&W.push({fileName:lt,base64:await Ae(me.bytes)})}await Pt(d,R,W)}B(`${b?"已更新菜谱":"已创建菜谱"} · 模型变换已保存：缩放 ${n.scale} · 旋转 ${n.rotationX}°/${n.rotationY}°/${n.rotationZ}° · 位置 ${n.positionX}/${n.positionY}/${n.positionZ}（重新打开可回显，游戏内直接生效）`),de(r,d)}catch(R){B(R.message,!1)}finally{F()}});function kt(e){return new Promise((t,n)=>{const i=new FileReader;i.onload=()=>{const c=i.result,p=c.indexOf(",");t(p>=0?c.substring(p+1):c)},i.onerror=n,i.readAsDataURL(e)})}function Ae(e){let t="";for(let i=0;i<e.length;i+=32768)t+=String.fromCharCode(...e.subarray(i,i+32768));return Promise.resolve(btoa(t))}}function Ft(r,d,o,u){var S,y;const b=o.split("/").pop()??o;Q(`删除菜谱 · ${l(b)}`,"<p>将永久删除菜谱资源及其模型文件夹，且<b>不可恢复</b>。若其他菜谱引用了它作为子菜谱，组成将失效。</p>",'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn danger" data-ok>确认删除</button>'),(S=document.querySelector("[data-cancel]"))==null||S.addEventListener("click",P),(y=document.querySelector("[data-ok]"))==null||y.addEventListener("click",async()=>{J("删除中…");try{await Yt(o),P(),B("已删除菜谱"),u()}catch(f){B(f.message,!1)}finally{F()}})}function Ht(r,d){Q("新建分类",`<label class="m-field">分类ID（仅字母/数字/下划线，用于目录名）<input type="text" id="cr-cat-id" placeholder="MyCategory"></label>
     <label class="m-field">中文名<input type="text" id="cr-cat-zh" placeholder="我的分类"></label>
     <label class="m-field">英文名<input type="text" id="cr-cat-en" placeholder="My Category"></label>`,'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>创建</button>'),yt(P,async()=>{const o=document.getElementById("cr-cat-id").value.trim();if(!o)return alert("请填写分类ID");if(!ye.test(o))return alert("分类ID仅允许英文字母/数字/下划线");const u=document.getElementById("cr-cat-zh").value.trim(),b=document.getElementById("cr-cat-en").value.trim();J("创建分类…");try{await bt(r,o,u||o,b||o),P(),B("已创建分类"),d(o)}catch(S){B(S.message,!1)}finally{F()}})}function qt(r,d){const o=prompt("分类ID（仅字母/数字/下划线）：");if(!o||!ye.test(o)){alert("ID非法");return}const u=prompt("分类中文名：")||o,b=prompt("分类英文名：")||o;J("创建分类…"),bt(r,o,u,b).then(()=>{B("已创建分类"),d(o)}).catch(S=>B(S.message,!1)).finally(()=>F())}function At(r,d,o){var S;function u(y){return`${y.zh||y.id}${y.en?` (${y.en})`:""}`}const b=d.map(y=>`
    <div class="m-row" style="margin-bottom:8px">
      <span style="flex:1">${l(u(y))} <span class="muted">[${l(y.id)}]</span></span>
      <button class="m-btn" data-rename="${l(y.id)}">重命名</button>
      <button class="m-btn danger" data-delcat="${l(y.id)}">删除</button>
    </div>`).join("");Q("管理分类",`<div class="modal-scroll">${b||'<p class="muted">暂无分类</p>'}</div>`,'<button type="button" class="m-btn" data-cancel>关闭</button>'),(S=document.querySelector("[data-cancel]"))==null||S.addEventListener("click",P),document.querySelectorAll("[data-rename]").forEach(y=>{y.addEventListener("click",()=>{const f=y.dataset.rename,w=d.find(a=>a.id===f);Ot(r,f,(w==null?void 0:w.zh)??f,(w==null?void 0:w.en)??f,()=>{P(),o()})})}),document.querySelectorAll("[data-delcat]").forEach(y=>{y.addEventListener("click",()=>{var a,T;const f=y.dataset.delcat,w=d.find(M=>M.id===f);Q(`删除分类 · ${l((w==null?void 0:w.zh)||f)}`,"<p>将检查关卡使用情况。如果有关卡正在使用该分类的菜谱，则不允许删除。</p>",'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn danger" data-ok>确认删除</button>'),(a=document.querySelector("[data-cancel]"))==null||a.addEventListener("click",P),(T=document.querySelector("[data-ok]"))==null||T.addEventListener("click",async()=>{J("检查中…");try{await Tt(r,f),P(),B("已删除分类"),o()}catch(M){B(M.message,!1),F()}})})})}function Ot(r,d,o,u,b){Q(`重命名分类 · ${l(o||d)}`,`<label class="m-field">分类ID（仅字母/数字/下划线）<input type="text" id="cr-rename-id" value="${l(d)}"></label>
     <label class="m-field">中文名<input type="text" id="cr-rename-zh" value="${l(o)}"></label>
     <label class="m-field">英文名<input type="text" id="cr-rename-en" value="${l(u)}"></label>`,'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>确认</button>'),yt(P,async()=>{const S=document.getElementById("cr-rename-id").value.trim();if(!S)return alert("请填写分类ID");if(!ye.test(S))return alert("分类ID仅允许英文字母/数字/下划线");const y=document.getElementById("cr-rename-zh").value.trim(),f=document.getElementById("cr-rename-en").value.trim();J("重命名…");try{await _t(r,d,S,y||S,f||S),P(),B("已重命名分类"),b()}catch(w){B(w.message,!1)}finally{F()}})}function yt(r,d){var o,u;(o=document.querySelector("[data-cancel]"))==null||o.addEventListener("click",r),(u=document.querySelector("[data-ok]"))==null||u.addEventListener("click",d)}export{jt as renderCustomRecipesView};
