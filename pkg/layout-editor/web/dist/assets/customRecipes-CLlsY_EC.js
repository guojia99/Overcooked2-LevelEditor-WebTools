const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["assets/textureEditor-gumOTPY-.js","assets/dom-fmO7ZFgN.js","assets/dom-BPCLAQNn.css","assets/recipeModelPreview-CEsLkBiP.js"])))=>i.map(i=>d[i]);
import{v as _,q as un,aA as pn,aB as fn,aC as yt,aD as Je,aE as Zn,p as gn,k as B,aF as _n,_ as st,ae as Ge,$ as Dn,z as je,a5 as tn,a6 as Nn,n as Yn,w as jn,ax as Hn,aG as Xn,aH as An,a4 as On,aI as Fn,aJ as Un,ay as bn,aK as qn,aL as Ke,K as vn,V as re,ap as Ae,a2 as Et,W,ag as Ce,aM as yn,aj as pe,aN as vt,aO as Wn,aw as Gn,aP as Vn,aQ as Kn,an as te,ao as ne,am as nn,aR as Jn,a1 as Qn,aS as ea,aT as an,aU as ta,aV as hn,aW as na,aX as aa,aY as sa,ak as $n,aZ as oa,az as ia,Q as ca,a_ as la,a$ as da,b0 as ra,b1 as ma,y as sn,b2 as ua}from"./dom-fmO7ZFgN.js";import{sanitizeUploadFileName as pa}from"./fbxTextureRename-CZchYSrG.js";import{renameMtlTextureRefs as fa,ensureObjMtllib as ga}from"./mtlTextureRename-vGs99CsZ.js";import{openRecipeModelPreview as In}from"./recipeModelPreview-CEsLkBiP.js";const En="crDeveloperMode";function wn(){return localStorage.getItem(En)==="1"}function ba(s){localStorage.setItem(En,s?"1":"0")}const kn=[{id:"commonW2",title:"commonW2 · Burger大全 / 炸物 / 意面",desc:"全关卡集共用的汉堡、炸物与意面扩展菜谱库。修改会影响所有引用该库的关卡集。"},{id:"commonW3",title:"commonW3 · 沙拉 / 果汁 / 炒饭 / 汤粥 / 冰淇淋 / 布丁 / 牛奶冰沙",desc:"Web 扩展共享菜谱库（沙拉、果汁、炒饭、汤粥、冰淇淋、布丁、牛奶冰沙）。修改会影响所有引用该库的关卡集。"},{id:"commonW5",title:"commonW5 · 卷饼 / 奶茶",desc:"Web 共享菜谱库（卷饼及中间备料、奶茶四件套与热水中间产物）。修改会影响所有引用该库的关卡集。"}];function ht(s){var n;return((n=kn.find(o=>o.id===s))==null?void 0:n.title)??s}function xn(s,n){return s.replace(/\\/g,"/").includes(`/${n}/custom_recipes/`)}function va(s){return s.kind==="set"?s.setName:ht(s.libraryId)}function i(s){return String(s??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}const mt=/^[A-Za-z_][A-Za-z0-9_]*$/;let Ve="";function wt(s,n){return document.body.classList.add("manage-bg"),s.innerHTML=`
    ${Yn("custom-recipes")}
    <div class="manage-bar">
      <h1 class="m-title">${i(n)}</h1>
      <span style="flex:1"></span>
    </div>
    <div class="manage-content" id="cr-content"></div>
  `,jn(),document.getElementById("cr-content")}function $t(s){location.pathname!==s&&history.pushState(null,"",s)}let on=!1;function ct(s){return{kind:"set",setName:s}}function Sn(s){return{kind:"library",libraryId:s}}function ot(s,n){if(s.kind==="set"){$t(n?An(s.setName,n):On(s.setName));return}$t(n?Fn(s.libraryId,n):Un(s.libraryId))}async function cn(s){const n=Dn();if(n.page==="custom-recipes"&&n.libraryId){if(!wn()){await dt(s),_("请先开启开发者模式以编辑共享菜谱库。",!1);return}const o=Sn(n.libraryId);if(n.recipeId){await ln(s,o,n.recipeId);return}await Ee(s,o);return}if(n.page==="custom-recipes"&&n.setId&&n.recipeId){await ln(s,ct(n.setId),n.recipeId);return}if(n.page==="custom-recipes"&&n.setId){await Ee(s,ct(n.setId));return}await dt(s)}async function lt(s){return s.kind==="set"?Hn(s.setName):Xn(s.libraryId)}async function ln(s,n,o){if(o==="new"){if(n.kind==="library"){await Ee(s,n),_("共享菜谱库不支持新建，仅可编辑已有菜谱。",!1);return}await rt(s,n,null);return}let r=[];try{r=await lt(n)}catch(p){Qe(p);return}const E=n.kind==="set"?r.find(p=>p.id===o&&!xt(p))??r.find(p=>p.id===o)??r.find(p=>p.assetPath.replace(/\\/g,"/").endsWith("/"+o+".asset")):r.find(p=>p.id===o)??r.find(p=>p.assetPath.replace(/\\/g,"/").endsWith("/"+o+".asset"));if(!E){await Ee(s,n),_(`未找到菜谱「${o}」，已返回列表。`,!1);return}await rt(s,n,E.assetPath)}async function Pa(s){on||(on=!0,window.addEventListener("popstate",()=>void cn(s))),await cn(s)}async function dt(s){var p;$t("/custom-recipes");const n=wt(s,"自定义菜谱管理");kt("加载关卡集…");let o=[];try{o=await Qn()}catch(f){Qe(f);return}_(`共 ${o.length} 个关卡集`),n.innerHTML=`
    <div class="m-section-title">选择关卡集</div>
    <p class="modal-hint">选择要管理自定义菜谱的关卡集。首次进入会自动初始化配置。</p>
    <div class="m-grid">${o.map(f=>`
      <div class="m-card">
        <h3>${i(f.levelSetNameZH||f.setName)} <span class="muted">(${i(f.levelSetName||f.setName)})</span></h3>
        <div class="m-meta">
          标识：${i(f.setName)} · 关卡数：${f.levelCount}<br>
          作者：${i(f.author||"—")} · 版本：${i(f.version||"—")}
        </div>
        <div class="m-actions">
          ${B("管理菜谱","primary",{"data-open":i(f.setName)})}
        </div>
      </div>`).join("")||'<p class="muted">暂无关卡集</p>'}
    </div>
  `,n.querySelectorAll("[data-open]").forEach(f=>f.addEventListener("click",()=>void Ee(s,ct(f.dataset.open))));const r=wn(),E=`
    <details class="cc-advanced" ${r?"open":""}>
    <summary>高级：开发者模式与共享菜谱库</summary>
    <p class="modal-hint">
      <label class="cr-dev-toggle">
        <input type="checkbox" id="cr-dev-mode" ${r?"checked":""}>
        开启开发者模式（直接修改 commonW2 / commonW3 / commonW5 已有菜谱；<b>影响全关卡集</b>，不支持新建/删除）
      </label>
    </p>
    ${r?`<div class="m-grid">${kn.map(f=>`
      <div class="m-card cr-dev-card">
        <h3>${i(f.title)}</h3>
        <div class="m-meta">${i(f.desc)}</div>
        <div class="m-actions">
          ${B("编辑共享库","primary",{"data-open-lib":i(f.id)})}
        </div>
      </div>`).join("")}</div>`:""}
    </details>
  `;n.insertAdjacentHTML("beforeend",E),(p=document.getElementById("cr-dev-mode"))==null||p.addEventListener("change",f=>{ba(f.target.checked),dt(s)}),n.querySelectorAll("[data-open-lib]").forEach(f=>f.addEventListener("click",()=>void Ee(s,Sn(f.dataset.openLib))))}function kt(s){const n=document.getElementById("cr-content");n&&(n.innerHTML=`<p class="muted">${i(s)}</p>`),_(s)}function Qe(s){const n=s instanceof Error?s.message:String(s);_(n,!1);const o=document.getElementById("cr-content");o&&(o.innerHTML=`<div class="m-block"><h3>出错</h3><p>${i(n)}</p></div>`)}function it(s){return`/api/custom-recipes/icon?assetPath=${encodeURIComponent(s.assetPath)}`}function xt(s){const n=s.assetPath.replace(/\\/g,"/");return n.includes("/commonW2/")||n.includes("/commonW3/")||n.includes("/commonW5/")}function It(s,n){return`<img class="food-icon" loading="lazy" src="${n?`/icons/${s}/${encodeURIComponent(n)}.png`:"/icons/_placeholder.png"}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'">`}function ya(s){return bn(s)}async function Ee(s,n){var ye,et,ie,Oe,Fe,tt;const o=n.kind==="library",r=n.kind==="set"?n.setName:"";ot(n);const E=wt(s,o?`开发者 · ${i(ht(n.libraryId))}`:`自定义菜谱 · ${i(r)}`);kt("加载菜谱配置…");let p,f,I=[],C=new Map,Y=new Map,A=[];try{[p,f]=await Promise.all([o?yt(n.libraryId):Je(r),lt(n)]);const h=o?await pn(n.libraryId).catch(()=>null):await fn(r).catch(()=>null);for(const R of(h==null?void 0:h.platingContainers)??[])Y.set(R.id,R.nameZh||R.id);let b=!1;const[m,c]=await Promise.all([un(n.kind==="set"?n.setName:void 0).catch(()=>(b=!0,[])),o?Promise.resolve([]):gn(r).catch(()=>[])]);I=c;for(const R of m)C.set(R.id,R.nameZh);b&&_("⚠️ 食材数据加载失败（/api/catalog/ingredients），卡片可能缺少食材图标")}catch(h){Qe(h);return}function N(){return o?f:f.filter(h=>!xt(h))}_(`${N().length} 个菜谱 · UID前缀：${p.uidPrefix}`);const j=p.categories??[],t=o?j:j.filter(h=>h.id!=="burger");function F(h){return h.zh||Ke(h.id)}let X=t.some(h=>h.id===Ve)?Ve:"",G="";const fe=p.subcategories??[];function we(h,b){const m=fe.find(c=>c.parent===h&&c.id===b);return(m==null?void 0:m.zh)||b}function x(h,b){const m=fe.find(c=>c.parent===h&&c.id===b);return(m==null?void 0:m.order)??999}let D="",ee="all",oe=!1,M=!1,U=!1;function Me(h){const b=ya(h);return!b.ingredients&&b.compositionIds&&(b.ingredients=b.compositionIds.filter(m=>C.has(m))),b}A=Ln(f,I,null,o?void 0:r,o?n.libraryId:void 0).allRecipeLikes;function me(){const h=N();let b=X?h.filter(c=>c.category===X):h;if(X&&G&&(b=b.filter(c=>(c.subcategory??"")===G)),oe&&(b=b.filter(c=>c.score<=0)),M&&(b=b.filter(c=>c.type==="Mixed")),U&&(b=b.filter(c=>c.score>0)),ee!=="all"){const c=[0,20,40,60,80,100,120];b=b.filter(R=>ee==="other"?!c.includes(R.score??0):(R.score??0)===ee)}const m=D.trim().toLowerCase();return m&&(b=b.filter(c=>[c.nameZh,c.nameEn??"",c.id,...c.ingredients??[]].join(" ").toLowerCase().includes(m))),b}function V(){const h=me();if(h.length===0)return o&&N().length===0?`<div class="m-block"><h3>共享库为空</h3><p class="muted">${i(n.libraryId)} 下未扫描到任何菜谱资产。</p></div>`:N().length===0&&D===""&&X===""?((async()=>{const m=document.getElementById("cr-grid");if(!(!m||m.dataset.diag)){m.dataset.diag="1";try{const c=await ea(r).catch(()=>null);if(!c){m.innerHTML=`<div class="m-block">
                <h3>暂无菜谱</h3>
                <p class="muted">桥接不支持诊断接口（旧版本）。若磁盘 <code>Assets/LevelSets/${i(r)}/custom_recipes/</code> 下已有菜谱，请：
                <b>在 Unity 中 Tools → Layout Editor → 停止服务 → 启动服务</b>（或重启 Unity 重新编译），再刷新本页。</p>
              </div>`;return}const R=c.dirExists?`磁盘上检测到 <b>${c.fsAssets.length}</b> 个 .asset 文件，扫描命中 <b>${c.scannedCount}</b> 个自定义菜谱，成功加载 <b>${c.loadedCount}</b> 个。`:"目录不存在，尚未创建任何菜谱。",q=c.scannedCount>0&&c.loadedCount===0?'<p class="mp-status err">⚠️ 文件存在但资产加载失败（CustomRecipeSO 类型未加载）。请重启 Unity 并确认 Console 无编译错误。</p>':c.scannedCount===0&&c.fsAssets.length>0?'<p class="mp-status err">⚠️ 文件系统有资产但脚本 guid 不匹配（可能是旧版脚本/副本）。</p>':"";m.innerHTML=`<div class="m-block">
              <h3>暂无菜谱</h3>
              <p class="muted">${R}</p>
              ${q}
              <p class="muted small" style="margin-top:8px">点击右上角「+ 新建菜谱」开始创建；若已创建但未显示，请重启 Unity 桥（Tools → Layout Editor → 停止服务 → 启动服务）后刷新。</p>
            </div>`}catch{m.innerHTML='<div class="m-block"><h3>暂无菜谱</h3><p class="muted">点击右上角「+ 新建菜谱」开始创建。</p></div>'}}})(),'<p class="muted">加载中…</p>'):o?'<p class="muted">没有匹配的菜谱。</p>':'<p class="muted">没有匹配的菜谱。点击右上角「+ 新建菜谱」开始创建。</p>';const b=m=>{let c;try{c=vn(Me(m),{allRecipes:A,ingredientName:he=>C.get(he)??he,iconSrc:()=>it(m)})}catch(he){c=`<div class="m-card"><h3>${i(m.nameZh)}</h3><p class="muted">卡片渲染失败：${i(he.message)}</p></div>`}const R=j.find(he=>he.id===m.category),q=m.platingStepId?Y.get(m.platingStepId):"",Re=(m.compositionIds??[]).length,ae=m.optionalKind==="burger"||m.optionalKind==="pizza"||m.optionalKind==="sandwich",ce=!!m.isFinishedBurger,xe=ae||ce;return`
      <div class="cr-card-wrap">
        <div class="cr-card-inner">${c}</div>
        <div class="cr-card-foot">
          <span class="cr-cat-tag">${i(F(R??{id:m.category,zh:Ke(m.category),en:m.category}))}</span>
          ${q?`<span class="cr-cat-tag cr-plate-tag" title="装盘容器">🍽 ${i(q)}</span>`:""}
          <span class="muted small">${m.optionalKind==="sandwich"?"三明治组装":ae?"组装定义":ce?"成品汉堡":"菜谱"} · 组成 ${Re} 项</span>
          <details class="cc-advanced"><summary>高级：编号</summary><span class="muted small">UID ${m.uID}</span></details>
          <span style="flex:1"></span>
          ${m.previewable??m.hasModel?B("👁","small",{"data-preview":i(m.assetPath),title:"3D 模型在线预览"}):""}
          ${o?B("编辑","small",{"data-edit":i(m.assetPath)}):m.optionalKind==="sandwich"?B("🥪 工作台","small",{"data-goto-sandwich":i(m.id),title:"在三明治工作台编辑 toast/夹心/酱料"}):xe?B("🍔 工作台","small",{"data-goto-burger":"",...ce?{"data-burger-product":i(m.id)}:{},title:ce?"在汉堡工作台载入并编辑此成品汉堡":"组装定义的可选夹心与堆叠模型在汉堡工作台中管理"}):B("编辑","small",{"data-edit":i(m.assetPath)})}
          ${o?"":B("删除","small",{"data-del":i(m.assetPath)},"danger")}
        </div>
      </div>`};return`<div class="rl-grid">${h.map(b).join("")}</div>`}function ge(){const h=N();return`
      ${je("全部",X==="",{"data-cat":""},"cr-cat-chip",` <span class="rl-cnt">${h.length}</span>`)}
      ${t.map(b=>{const m=h.filter(c=>c.category===b.id).length;return je(`${i(F(b))} `,X===b.id,{"data-cat":i(b.id)},"cr-cat-chip",`<span class="rl-cnt">${m}</span>`)}).join("")}
      ${o?"":`<span class="cr-cat-tools">
        ${B("+ 新建分类","default",{id:"cr-new-cat"})}
        ${t.length>0?B("管理分类","default",{id:"cr-manage-cat"}):""}
      </span>`}`}function J(){if(!X)return"";const h=N().filter(R=>R.category===X),b=new Map;for(const R of h){const q=R.subcategory??"";q&&b.set(q,(b.get(q)??0)+1)}if(b.size===0)return"";const m=h.filter(R=>!(R.subcategory??"")).length,c=[...b.entries()].sort((R,q)=>x(X,R[0])-x(X,q[0])||R[0].localeCompare(q[0]));return`
      ${je("全部",G==="",{"data-sub":""},"cr-sub-chip",` <span class="rl-cnt">${h.length}</span>`)}
      ${c.map(([R,q])=>je(`${i(we(X,R))} `,G===R,{"data-sub":i(R)},"cr-sub-chip",`<span class="rl-cnt">${q}</span>`)).join("")}
      ${m>0?`<span class="muted small">未分子类 ${m}</span>`:""}`}const He=o?`<div class="m-block cr-dev-banner"><b>⚠️ 开发者模式</b>：正在编辑 <b>${i(ht(n.libraryId))}</b>。保存会立即影响所有引用该库的关卡集；仅支持修改已有菜谱，不支持新建或删除。</div>`:"",be=o?"":`
      ${B("🍞 统一面包皮","default",{id:"cr-unify-bun",title:"把本关卡集自定义菜谱里的汉堡面包皮统一换成同一种"})}
      ${B("📦 资产瘦身","default",{id:"cr-optimize-assets",title:"只调 .meta 导入参数（贴图分辨率/网格精度）缩小导出包；不改源 FBX/PNG"})}
      ${B("🥩 夹心工作台","default",{id:"cr-new-filling",title:"夹心 = 0 分自定义菜谱，做好后可在汉堡工作台选用"})}
      ${B("🥪 三明治工作台","default",{id:"cr-new-sandwich",title:"CustomRecipeOptionalSandwichSO：toast + 夹心 + 酱料"})}
      ${B("🍔 新增汉堡菜谱","default",{id:"cr-new-burger"})}
      ${B("+ 新建菜谱","primary",{id:"cr-new-recipe"})}`;E.innerHTML=`
    ${He}
    <div class="m-actions-row">
      ${B(`← ${o?"返回":"返回关卡集列表"}`,"default",{id:"cr-back"})}
      <span class="muted">${o?`共享库：<b>${i(n.libraryId)}</b>`:`当前关卡集：<b>${i(r)}</b>`}</span>
      <span style="flex:1"></span>
      ${be}
    </div>
    <div class="cr-toolbar">
      <input type="search" id="cr-search" class="rl-search" placeholder="搜索菜名 / ID / 食材…" autocomplete="off">
      <select id="cr-score-filter" class="rl-select" title="按分数过滤">
        <option value="all">全部分数</option>
        <option value="0">0 分</option>
        <option value="20">20 分</option>
        <option value="40">40 分</option>
        <option value="60">60 分</option>
        <option value="80">80 分</option>
        <option value="100">100 分</option>
        <option value="120">120 分</option>
        <option value="other">其他分数</option>
      </select>
      ${je("🧩 中间产物",oe,{"data-state":"intermediate",title:"只看 0 分半成品（可与其他状态叠加 = 交集）"},"cr-state-chip")}
      ${je("🥣 搅拌物",M,{"data-state":"mixed",title:"只看 Mixed 搅拌类菜谱（可与其他状态叠加 = 交集）"},"cr-state-chip")}
      ${je("🍽 成品",U,{"data-state":"finished",title:"只看可点单的成品菜（score>0，可与其他状态叠加 = 交集）"},"cr-state-chip")}
    </div>
    <div class="cr-toolbar cr-cat-bar" id="cr-cat-chips">${ge()}</div>
    <div class="cr-toolbar cr-sub-bar" id="cr-sub-chips">${J()}</div>
    <div id="cr-grid">${V()}</div>
  `;function Ze(){(async()=>{Ce("加载…");try{[p,f]=await Promise.all([o?yt(n.libraryId):Je(r),lt(n)])}catch(h){Qe(h);return}finally{pe()}_(`${N().length} 个菜谱 · UID前缀：${p.uidPrefix}`),document.getElementById("cr-cat-chips").innerHTML=ge(),ke(),P(),document.getElementById("cr-grid").innerHTML=V(),K()})()}function g(){var h,b;(h=document.getElementById("cr-search"))==null||h.addEventListener("input",m=>{D=m.target.value,document.getElementById("cr-grid").innerHTML=V(),K()}),(b=document.getElementById("cr-score-filter"))==null||b.addEventListener("change",m=>{const c=m.target.value;ee=c==="all"?"all":c==="other"?"other":Number(c),document.getElementById("cr-grid").innerHTML=V(),K()}),document.querySelectorAll(".cr-state-chip").forEach(m=>{m.addEventListener("click",()=>{m.dataset.state==="intermediate"?oe=!oe:m.dataset.state==="mixed"?M=!M:m.dataset.state==="finished"&&(U=!U),m.classList.toggle("active"),document.getElementById("cr-grid").innerHTML=V(),K()})})}function P(){const h=document.getElementById("cr-sub-chips");h&&(h.innerHTML=J(),h.style.display=h.innerHTML.trim()?"":"none",ve())}function ve(){document.querySelectorAll(".cr-sub-chip").forEach(h=>{h.addEventListener("click",()=>{G=h.dataset.sub??"",P(),document.getElementById("cr-grid").innerHTML=V(),K()})})}function ke(){var h,b;document.querySelectorAll(".cr-cat-chip").forEach(m=>{m.addEventListener("click",()=>{X=m.dataset.cat??"",Ve=X,G="",document.getElementById("cr-cat-chips").innerHTML=ge(),ke(),P(),document.getElementById("cr-grid").innerHTML=V(),K()})}),o||((h=document.getElementById("cr-new-cat"))==null||h.addEventListener("click",()=>xa(r,m=>{Ve=m??"",Ee(s,n)})),(b=document.getElementById("cr-manage-cat"))==null||b.addEventListener("click",()=>Cn(r,p.categories,()=>void Ee(s,n))))}function K(){var h;(h=document.getElementById("cr-new-sandwich"))==null||h.addEventListener("click",()=>{location.assign(an(r))}),document.querySelectorAll("[data-goto-sandwich]").forEach(b=>b.addEventListener("click",()=>location.assign(an(r,b.dataset.gotoSandwich||void 0)))),document.querySelectorAll("[data-goto-burger]").forEach(b=>b.addEventListener("click",()=>{const m=b.dataset.burgerProduct;location.assign(tn(r,m||void 0))})),document.querySelectorAll("[data-edit]").forEach(b=>b.addEventListener("click",()=>{const m=b.dataset.edit,c=f.find(q=>q.assetPath===m),R=(c==null?void 0:c.id)??(m.split("/").pop()??"").replace(/\.asset$/,"");ot(n,R||"new"),rt(s,n,m)})),document.querySelectorAll("[data-preview]").forEach(b=>b.addEventListener("click",()=>{const m=b.dataset.preview,c=f.find(q=>q.assetPath===m),R=c&&c.modelScale>0?c.modelScale:1;In(m,m.split("/").pop()??m,{readonly:o?!1:void 0,fitTarget:(c==null?void 0:c.platingStepId)==="Glass"?"cup":"plate",scale:(c==null?void 0:c.modelScale)??1,rotationX:(c==null?void 0:c.modelRotationX)??0,rotationY:(c==null?void 0:c.modelRotationY)??0,rotationZ:(c==null?void 0:c.modelRotationZ)??0,positionX:(c==null?void 0:c.modelPositionX)??0,positionY:(c==null?void 0:c.modelPositionY)??0,positionZ:(c==null?void 0:c.modelPositionZ)??0,pivotX:(c==null?void 0:c.modelPivotX)??0,pivotY:(c==null?void 0:c.modelPivotY)??0,pivotZ:(c==null?void 0:c.modelPivotZ)??0,unitySize:c&&c.boundsSizeX!=null&&c.boundsSizeY!=null&&c.boundsSizeZ!=null?{x:c.boundsSizeX/R,y:c.boundsSizeY/R,z:c.boundsSizeZ/R,minY:((c.boundsMinY??0)-(c.modelPositionY??0))/R}:void 0,boundsMinY:c==null?void 0:c.boundsMinY})})),o||document.querySelectorAll("[data-del]").forEach(b=>b.addEventListener("click",()=>ka(s,n,b.dataset.del,Ze)))}g(),ke(),P(),K(),(ye=document.getElementById("cr-back"))==null||ye.addEventListener("click",()=>void dt(s)),o||((et=document.getElementById("cr-unify-bun"))==null||et.addEventListener("click",()=>$a(r,Ze)),(ie=document.getElementById("cr-optimize-assets"))==null||ie.addEventListener("click",()=>void wa(r)),(Oe=document.getElementById("cr-new-burger"))==null||Oe.addEventListener("click",()=>{location.assign(tn(r))}),(Fe=document.getElementById("cr-new-filling"))==null||Fe.addEventListener("click",()=>{location.assign(Nn(r))}),(tt=document.getElementById("cr-new-recipe"))==null||tt.addEventListener("click",()=>{ot(n,"new"),rt(s,n,null,X?{category:X}:void 0)}))}function ha(s){return s===hn?"Burger大全规范面皮；与核心面包 IngredientOrderNode uID 同为 16088，订单匹配等价。":s===da?"主线核心面包，不引入额外 DLC 依赖；与 DLC8 面皮 uID 16088 等价。":s===ra?"⚠️ 未经 uID 实测，不在核心面包/DLC8 面皮的等价组内 —— 已有食材箱可能接不上订单。":""}function dn(s){const n=new Map;for(const o of s){let r=n.get(o.assetPath);r||(r={assetPath:o.assetPath,id:o.id,nameZh:o.nameZh,isAssembly:o.isAssembly,isFinishedBurger:o.isFinishedBurger,bunIds:[],layers:0},n.set(o.assetPath,r)),r.bunIds.includes(o.bunId)||r.bunIds.push(o.bunId),r.layers++}return[...n.values()].sort((o,r)=>o.nameZh.localeCompare(r.nameZh,"zh"))}async function $a(s,n){var U,Me,me,V,ge,J,He,be,Ze;Ce("扫描面包皮…");let o;try{o=await ta(s)}catch(g){_(g instanceof Error?g.message:String(g),!1);return}finally{pe()}const r=dn(o.usages),E=dn(o.sharedUsages),p=new Map(o.buns.map(g=>[g.id,g])),f=g=>{var P;return((P=p.get(g))==null?void 0:P.nameZh)??g};if(r.length===0){re("🍞 统一面包皮",`<p class="modal-hint">关卡集 <b>${i(s)}</b> 的 <code>custom_recipes/</code> 里没有引用汉堡面包皮的自定义菜谱，无需统一。</p>
       ${E.length>0?`<p class="muted small">（commonW2 共享库里有 ${E.length} 道含面包的汉堡，属全关卡集共用资产，本工具不会修改。）</p>`:""}`,B("关闭","default",{"data-close":""})),(U=document.querySelector("[data-close]"))==null||U.addEventListener("click",W);return}const I=o.buns.filter(g=>g.bundleAvailable!==!1);let C=((Me=I.find(g=>g.id===hn))==null?void 0:Me.id)??((me=I[0])==null?void 0:me.id)??((V=o.buns[0])==null?void 0:V.id)??"";const Y=new Set,A=g=>g.bunIds.length===1&&g.bunIds[0]===C,N=()=>r.filter(g=>!A(g)),j=()=>N().filter(g=>!Y.has(g.assetPath)),t=()=>o.buns.map(g=>{const P=g.bundleAvailable===!1,ve=ha(g.id);return`<label class="bs-target${P?" disabled":""}${g.id===C?" active":""}">
          <input type="radio" name="bs-target" value="${i(g.id)}" ${g.id===C?"checked":""} ${P?"disabled":""}>
          ${It("ingredients",g.id)}
          <span class="bs-target-text">
            <b>${i(g.nameZh)}</b> <span class="muted small">${i(g.id)}${g.bundleName?` · ${i(g.bundleName)}`:""}</span>
            ${P?'<span class="mp-status err">bundle 未就绪，不可选</span>':""}
            ${ve?`<span class="muted small bs-note">${i(ve)}</span>`:""}
          </span>
        </label>`}).join(""),F=()=>r.map(g=>{const P=A(g),ve=!P&&!Y.has(g.assetPath),ke=g.isAssembly?"组装定义":g.isFinishedBurger?"成品汉堡":"菜谱",K=g.bunIds.map(ye=>i(f(ye))).join(" / ");return`<label class="bs-row${P?" done":""}">
          <input type="checkbox" class="bs-cb" value="${i(g.assetPath)}" ${ve?"checked":""} ${P?"disabled":""}>
          <span class="bs-row-main">
            <b>${i(g.nameZh)}</b>
            <span class="muted small">${i(g.id)} · ${ke} · ${g.layers} 层面包</span>
          </span>
          <span class="bs-row-swap">${P?`<span class="muted">已是 ${i(f(C))}</span>`:`${K} <b>→</b> ${i(f(C))}`}</span>
        </label>`}).join(""),X=()=>E.length===0?"":`<details class="bs-shared">
      <summary>不可修改的 ${E.length} 项（commonW2 共享库）</summary>
      <p class="muted small">commonW2「🍔 Burger大全」是<b>全关卡集共用</b>的共享库，改它会污染所有关卡集，后端会硬拒。
      官方汉堡（Burger_Plain_SO 等）的面包锁死在游戏 bundle 内是 DLC2 面皮，同样无法更改。</p>
      <div class="bs-shared-list">${E.map(g=>`<div class="muted small">${i(g.nameZh)} <span>${i(g.bunIds.map(f).join(" / "))}</span></div>`).join("")}</div>
    </details>`,G=`
    <p class="modal-hint">把关卡集 <b>${i(s)}</b> 的 <code>custom_recipes/</code> 内自定义菜谱的汉堡面包皮统一换成同一种（成品汉堡的组成层 + 本地组装定义的 bunSO）。</p>
    <div class="m-section-title">目标面包皮</div>
    <div class="bs-targets" id="bs-targets">${t()}</div>
    <div class="m-section-title bs-head">受影响的菜谱 <span class="muted small" id="bs-count"></span>
      <span style="flex:1"></span>
      ${B("全选","small",{id:"bs-all"})}
      ${B("全不选","small",{id:"bs-none"})}
    </div>
    <div class="bs-rows" id="bs-rows">${F()}</div>
    ${X()}
    <div class="bs-remind">
      <b>替换后需要你自己做的两件事（本工具不联动）：</b>
      <ol>
        <li>对<b>引用了这些菜谱的关卡</b>各重存一次菜谱 —— 才会重算 <code>BurgerOptional.bunSO</code>、补全 DLC 匹配表、重建 bundle 依赖。</li>
        <li>检查这些关卡场景里的<b>食材箱</b>是否还给着旧面包皮，需要的话自行替换。</li>
      </ol>
    </div>
    <p class="mp-status" id="bs-status"></p>
  `;re("🍞 统一面包皮",G,`${B("取消","default",{id:"bs-cancel"})}${B("执行替换","primary",{id:"bs-apply"})}`,{closeOnBackdrop:!1}),(ge=document.querySelector(".modal-panel"))==null||ge.classList.add("wide");const fe=document.getElementById("bs-rows"),we=document.getElementById("bs-count"),x=document.getElementById("bs-apply"),D=document.getElementById("bs-status");function ee(){const g=j().length,P=r.length-N().length;we.textContent=`已选 ${g} / 待替换 ${N().length}${P>0?` · ${P} 项已是目标`:""}`,x.disabled=g===0,x.textContent=g>0?`执行替换（${g} 道）`:"执行替换"}function oe(){fe.querySelectorAll(".bs-cb").forEach(g=>g.addEventListener("change",()=>{g.checked?Y.delete(g.value):Y.add(g.value),ee()}))}function M(){fe.innerHTML=F(),oe(),ee()}(J=document.getElementById("bs-targets"))==null||J.addEventListener("change",g=>{const P=g.target;P.name==="bs-target"&&(C=P.value,document.getElementById("bs-targets").innerHTML=t(),M())}),(He=document.getElementById("bs-all"))==null||He.addEventListener("click",()=>{Y.clear(),M()}),(be=document.getElementById("bs-none"))==null||be.addEventListener("click",()=>{for(const g of N())Y.add(g.assetPath);M()}),(Ze=document.getElementById("bs-cancel"))==null||Ze.addEventListener("click",W),x.addEventListener("click",()=>{(async()=>{const g=j();if(g.length!==0){x.disabled=!0,D.textContent="替换中…",D.className="mp-status";try{const P=await na({setName:s,targetBunId:C,assetPaths:g.map(K=>K.assetPath)});W();const ve=P.warnings.length>0?` · ${P.warnings.join(" ")}`:"",ke=P.skipped.length>0?` · 跳过 ${P.skipped.length} 项`:"";_(`已把 ${P.changed} 道菜谱的 ${P.layers} 个面包层换成「${f(C)}」${ke}${ve}`,!0),n()}catch(P){x.disabled=!1,D.textContent=P instanceof Error?P.message:String(P),D.className="mp-status err"}}})()}),M()}const Ia=[128,256,512,1024],Ea=[{id:"off",label:"关闭（体积最大）"},{id:"low",label:"Low（轻度量化）"},{id:"medium",label:"Medium（推荐）"},{id:"high",label:"High（体积最小）"}];function rn(s){return s.map(n=>`${n.key}×${n.count}`).join("、")}async function wa(s){var fe,we;Ce("扫描资产…");let n;try{n=await aa(s)}catch(x){_(x instanceof Error?x.message:String(x),!1);return}finally{pe()}if(!n.dirExists||n.textures.length===0&&n.models.length===0){re("📦 资产瘦身",`<p class="modal-hint">关卡集 <b>${i(s)}</b> 的 <code>custom_recipes/</code> 里没有可调整的贴图或模型。</p>`,B("关闭","default",{"data-close":""})),(fe=document.querySelector("[data-close]"))==null||fe.addEventListener("click",W);return}const o=n.textures.filter(x=>x.isIcon),r=n.textures.filter(x=>!x.isIcon),E=new Map;for(const x of n.textures)E.set(x.maxTextureSize,(E.get(x.maxTextureSize)??0)+1);const p=rn([...E.entries()].sort((x,D)=>D[0]-x[0]).map(([x,D])=>({key:String(x),count:D}))),f=new Map;for(const x of n.models)f.set(x.meshCompression,(f.get(x.meshCompression)??0)+1);const I=rn([...f.entries()].map(([x,D])=>({key:x,count:D}))),C=n.models.reduce((x,D)=>x+Math.max(D.triangles,0),0),Y=n.models.filter(x=>x.triangles<0).length,A=n.models.length===0?"":`合计约 ${(C/1e3).toFixed(1)}k 三角形${Y>0?`（${Y} 个已关读写不可读）`:""}`,N=x=>Ia.map(D=>`<option value="${D}"${D===x?" selected":""}>${D}</option>`).join(""),j=x=>Ea.map(D=>`<option value="${D.id}"${D.id===x?" selected":""}>${D.label}</option>`).join(""),t=(x,D,ee)=>`
    <label style="display:flex;align-items:center;gap:12px;margin:8px 0">
      <span style="flex:1">${D}</span>
      <select id="${x}" class="rl-select" style="min-width:160px">${ee}</select>
    </label>`,F=`
    <p class="modal-hint">只调整 <b>${i(s)}</b> 的 <code>custom_recipes/</code> 内资产的<b>导入参数（.meta）</b>：源 FBX/PNG 不动、GUID 不变、引用不受影响；<b>重新导出关卡集</b>后产物体积变小。已达标的资产不会被重导入。</p>
    <div class="m-section-title">当前资产</div>
    <div class="muted small" style="margin-bottom:4px">贴图 ${n.textures.length} 张（材质 ${r.length} + 图标 ${o.length}）· 当前档位：${i(p)}</div>
    ${n.models.length>0?`<div class="muted small">模型 ${n.models.length} 个 · ${i(A)} · 精度：${i(I)}</div>`:""}
    <div class="m-section-title">目标档位</div>
    ${t("ao-tex","材质贴图（base_color / normal / roughness / metallic …）",N(256))}
    ${t("ao-icon","图标（_Icon.png，游戏内点单 UI）",N(256))}
    ${t("ao-mesh","模型网格精度（顶点量化 + 关读写副本 + 网格优化）",j("medium"))}
    <p class="muted small">提示：贴图是体积大头（2048→256 约缩小 98%）；Unity 导入参数没有真正的「减面」，网格只做顶点数据量化。压缩后编辑器内贴图会变糊，属正常现象。</p>
    <p class="mp-status" id="ao-status"></p>
  `;re("📦 资产瘦身",F,`${B("取消","default",{id:"ao-cancel"})}${B("执行瘦身","primary",{id:"ao-apply"})}`,{closeOnBackdrop:!1}),(we=document.getElementById("ao-cancel"))==null||we.addEventListener("click",W);const X=document.getElementById("ao-apply"),G=document.getElementById("ao-status");X.addEventListener("click",()=>{(async()=>{var oe;const x=Number(document.getElementById("ao-tex").value),D=Number(document.getElementById("ao-icon").value),ee=document.getElementById("ao-mesh").value;X.disabled=!0,G.textContent="瘦身中（贴图/模型重导入，可能需要几十秒）…",G.className="mp-status";try{const M=await sa({setName:s,textureMaxSize:x,iconMaxSize:D,meshCompression:ee});W();const U=M.skipped.length>0?` · 跳过 ${M.skipped.length} 项`:"",Me=M.details.length>0?`<details style="margin-top:8px"><summary>查看逐项变更（${M.details.length}）</summary>
               <div style="max-height:240px;overflow:auto">${M.details.map(J=>`<div class="muted small" style="font-family:monospace;word-break:break-all">${i(J)}</div>`).join("")}</div></details>`:"",me=M.skipped.length>0?`<details style="margin-top:8px"><summary>跳过 ${M.skipped.length} 项（含原因）</summary>
               <div style="max-height:160px;overflow:auto">${M.skipped.map(J=>`<div class="muted small" style="font-family:monospace;word-break:break-all">${i(J)}</div>`).join("")}</div></details>`:"",V=M.texturesChanged===0&&M.modelsChanged===0&&M.skipped.length===0,ge=V?"全部资产已达标，无需调整。":`已调整 ${M.texturesChanged} 张贴图、${M.modelsChanged} 个模型的导入参数${U}。<b>重新导出关卡集</b>后产物体积变小。`;re(V?"📦 资产瘦身":"📦 资产瘦身完成",`<p class="modal-hint">${ge}</p>${Me}${me}`,B("关闭","default",{"data-close":""})),(oe=document.querySelector("[data-close]"))==null||oe.addEventListener("click",W),_(V?"资产瘦身：全部资产已达标。":`资产瘦身完成：贴图 ×${M.texturesChanged}，模型 ×${M.modelsChanged}${U}`,!0)}catch(M){X.disabled=!1,G.textContent=M instanceof Error?M.message:String(M),G.className="mp-status err"}})()})}function mn(s){const n=/custom_recipes\/([^/]+)\/[^/]+\.asset$/i.exec(s.replace(/\\/g,"/"));return n?n[1]:""}function Bn(s,n){return n?s.replace(/\\/g,"/").includes(`/LevelSets/${n}/custom_recipes/`):!1}function Ln(s,n,o,r,E){const p=[],f=new Set,I=new Set,C=[],Y=new Set,A=t=>{xt(t)&&(!E||!xn(t.assetPath,E))||o&&t.assetPath===o||f.has(t.id)||(f.add(t.id),I.add(t.id),p.push({id:t.id,nameZh:t.nameZh,nameEn:t.nameEn,score:t.score,cookingStepId:t.cookingStepId,hasIcon:t.hasIcon,assetPath:t.assetPath,source:"levelset",recipeType:t.type||"custom",category:t.category||mn(t.assetPath),ingredients:t.ingredients}))},N=t=>{if(o&&t.assetPath===o||f.has(t.id))return;f.add(t.id),I.add(t.id);const F=!!(t.isCustom&&t.group==="levelset"&&(!r||Bn(t.assetPath,r)));p.push({id:t.id,nameZh:t.nameZh,nameEn:t.nameEn,score:t.score??0,cookingStepId:t.cookingStep??"",hasIcon:t.icon!==!1,assetPath:t.assetPath,source:F?"levelset":"official",recipeType:t.type??(F?"custom":"other"),category:F?mn(t.assetPath):"",ingredients:t.ingredients??[]})},j=t=>{!t.id||Y.has(t.id)||(Y.add(t.id),C.push(t))};for(const t of s)A(t),(!o||t.assetPath!==o)&&j(bn(t));for(const t of qn(n))N(t),(!o||t.assetPath!==o)&&j(t.isCustom?{...t,isCustom:!0,intermediate:!1}:t);return p.sort((t,F)=>(t.source==="levelset"?0:1)-(F.source==="levelset"?0:1)||t.score-F.score||t.nameZh.localeCompare(F.nameZh,"zh")),{subItems:p,recipeIdSet:I,allRecipeLikes:C,scopeSetName:r,scopeLibraryId:E}}async function rt(s,n,o,r){var Zt,_t,Dt,Nt,Yt,jt,Ht,Xt,At,Ot,Ft,Ut,qt,Wt,Gt,Vt,Kt,Jt,Qt;const E=typeof n=="string"?ct(n):n,p=E.kind==="library",f=E.kind==="set"?E.setName:"",I=o!=null;if(p&&!I){await Ee(s,E),_("共享菜谱库不支持新建。",!1);return}const C=(r==null?void 0:r.mode)==="filling",Y=wt(s,p?`开发者 · 编辑 ${i(va(E))}`:I?C?"编辑夹心":"编辑菜谱":C?"🥩 新建夹心":"新建菜谱");kt("加载参考数据…");let A=[],N,j,t,F=[],X=[];const G={cookingSteps:[],platingSteps:[],platingContainers:[],icons:[],reusableModels:[],ingredients:[]};try{[A,N,j]=await Promise.all([un(E.kind==="set"?E.setName:void 0).catch(()=>[]),p?pn(E.libraryId).catch(()=>G):fn(f).catch(()=>G),p?yt(E.libraryId):Je(f)]),A=Zn(A),[F,X]=await Promise.all([lt(E).catch(()=>[]),gn(f).catch(()=>[])]),I&&(t=F.find(e=>e.assetPath===o),t&&ot(E,t.id))}catch(e){Qe(e),pe();return}pe();const fe=Ln(F,X,I?o:null,p?void 0:f,p?E.libraryId:void 0),{subItems:we,recipeIdSet:x,allRecipeLikes:D,scopeSetName:ee,scopeLibraryId:oe}=fe,M=new Map,U=new Map;for(const e of(t==null?void 0:t.compositionIds)??[]){const a=x.has(e)?U:M;a.set(e,(a.get(e)??0)+1)}const Me=!I,me=(t==null?void 0:t.recipeName)??"",V=(t==null?void 0:t.nameZh)??"",ge=(t==null?void 0:t.nameEn)??"",J=(t==null?void 0:t.category)??(r==null?void 0:r.category)??(C?"burger_filling":((Zt=j.categories)==null?void 0:Zt.length)>0?j.categories[0].id:""),He=(t==null?void 0:t.score)??(r==null?void 0:r.score)??0,be=(t==null?void 0:t.type)??(C||(r==null?void 0:r.score)===0?"Cooked":"Composite"),Ze=(t==null?void 0:t.cookingStepId)??"",g=(t==null?void 0:t.cookingStepIconId)??"",P=(t==null?void 0:t.platingStepId)??(C?"Plate":""),ve=(t==null?void 0:t.mixingIconId)??"",ke="",K=p?j.categories??[]:(j.categories??[]).filter(e=>e.id!=="burger"),ye=new Map;for(const e of A)ye.set(e.id,e);const et=A.length===0,ie=new Map;for(const e of we)ie.set(e.id,e);function Oe(e){return e.zh||Ke(e.id)}function Fe(){if(!t||t.boundsSizeX==null||t.boundsSizeY==null||t.boundsSizeZ==null)return;const e=t.modelScale>0?t.modelScale:1;return{x:t.boundsSizeX/e,y:t.boundsSizeY/e,z:t.boundsSizeZ/e,minY:((t.boundsMinY??0)-(t.modelPositionY??0))/e}}function tt(){let e=K.map(u=>`<option value="${i(u.id)}" ${u.id===J?"selected":""}>${i(Oe(u))}</option>`).join("");return!K.some(u=>u.id===J)&&J&&(e+=`<option value="${i(J)}" selected>${i(Ke(J))}</option>`),`<select id="cr-type-cat" class="m-select"${p?" disabled":""}>${e}</select>`}function h(e,a,u){const l=new Map;for(const d of e)l.has(d.id)||l.set(d.id,d.nameZh||d.id);return`<select id="${u}" class="m-select">
      <option value="">— 不设置 —</option>
      ${[...l.entries()].map(([d,v])=>`<option value="${i(d)}" ${d===a?"selected":""}>${i(v)} (${i(d)})</option>`).join("")}
    </select>`}function b(){const e=[];for(const[a,u]of M)for(let l=0;l<u;l++)e.push(a);for(const[a,u]of U)for(let l=0;l<u;l++)e.push(a);return e}function m(e,a,u){const l=u>0,d=l?`<div class="cp-count">
          <button type="button" class="cp-step" data-cpdec data-cpid="${i(e)}" data-cpsub="${a?1:0}">−</button>
          <span class="cp-num">${u}</span>
          <button type="button" class="cp-step" data-cpinc data-cpid="${i(e)}" data-cpsub="${a?1:0}">＋</button>
        </div>`:"";if(a){const S=ie.get(e);if(!S)return"";const O=S.score<=0?'<span class="cr-badge-inter">中间产物</span>':'<span class="cr-badge-done">成品菜</span>',Q=S.source==="levelset"?it(S):`/icons/recipes/${encodeURIComponent(S.id)}.png`,Ie=S.source==="levelset"?"本关卡集":"官方";return`<div class="pick-card cp-card${l?" selected":""}" data-cpid="${i(e)}" data-cpsub="1" title="${i(S.id)}">
        <span class="pc-head"><img class="food-icon" loading="lazy" src="${i(Q)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'" /><span class="pc-name">${i(S.nameZh)}${O}${S.nameEn?` <span class="muted pc-en">${i(S.nameEn)}</span>`:""}</span></span>
        <span class="muted small">${S.cookingStepId?i(S.cookingStepId):"无烹饪步骤"} · ${(S.ingredients??[]).length} 种食材 · ${Ie}</span>
        ${d}
      </div>`}const v=ye.get(e);if(!v)return"";const k=v.group&&v.group!=="core"?` <span class="pc-badge">${ca(v.group)}</span>`:"",H=v.nameEn&&v.nameEn.trim()||"";return`<div class="pick-card cp-card${l?" selected":""}" data-cpid="${i(e)}" data-cpsub="0" title="${i(v.id)}">
      <span class="pc-head">${It("ingredients",v.id)}<span class="pc-name">${i(v.nameZh)}${k}${H?` <span class="muted pc-en">${H}</span>`:""}</span></span>
      ${d}
    </div>`}function c(){var Te,ze;const e=new Map;for(const[$,y]of M)e.set($,y);for(const[$,y]of U)e.set($,y);let a="all",u="";const l=$=>{const y=K.find(L=>L.id===$);return y?Oe(y):Ke($)};function d($){return a==="sub"?$.source==="official"&&$.score>0:a==="inter"?$.source==="official"&&$.score<=0:a==="levelset"?oe?$.source==="levelset"&&xn($.assetPath,oe):$.source==="levelset"&&(!ee||Bn($.assetPath,ee)):!0}function v($,y){const L=u.trim().toLowerCase();if(!L)return!0;if(!y){const T=ye.get($);return T?T.nameZh.toLowerCase().includes(L)||(T.nameEn??"").toLowerCase().includes(L)||T.id.toLowerCase().includes(L):!1}const Z=ie.get($);return Z?Z.nameZh.toLowerCase().includes(L)||(Z.nameEn??"").toLowerCase().includes(L)||Z.id.toLowerCase().includes(L):!1}function k(){const $=[];if(a==="all"||a==="ing")for(const y of A)v(y.id,!1)&&$.push({id:y.id,isSub:!1});if(a==="all"||a==="sub"||a==="inter"||a==="levelset")for(const y of we)d(y)&&v(y.id,!0)&&$.push({id:y.id,isSub:!0});return $}function H($){return $.map(y=>m(y.id,y.isSub,e.get(y.id)??0)).join("")}function S($,y){return y.length?`<div class="cp-section">
        <div class="cp-section-title">${i($)}</div>
        <div class="pick-grid">${H(y)}</div>
      </div>`:""}function O($){var Z;const y=new Map;for(const T of $){const w=((Z=ie.get(T.id))==null?void 0:Z.recipeType)??"other";y.has(w)||y.set(w,[]),y.get(w).push(T)}const L=T=>{const w=ma.indexOf(T);return w<0?99:w};return[...y.entries()].sort((T,w)=>L(T[0])-L(w[0]))}function Q($){var Z;const y=new Map;for(const T of $){const w=((Z=ie.get(T.id))==null?void 0:Z.category)??"";y.has(w)||y.set(w,[]),y.get(w).push(T)}const L=K.map(T=>T.id);return[...y.entries()].sort((T,w)=>{const z=L.indexOf(T[0]),de=L.indexOf(w[0]);return(z<0?99:z)-(de<0?99:de)})}function Ie($){if(!$.length)return"";if(a==="sub")return O($).map(([y,L])=>S(sn(y),L)).join("");if(a==="inter")return O($).map(([y,L])=>S(sn(y),L)).join("");if(a==="levelset"){const y=$.filter(w=>w.isSub),L=y.filter(w=>{var z;return(((z=ie.get(w.id))==null?void 0:z.score)??0)>0}),Z=y.filter(w=>{var z;return(((z=ie.get(w.id))==null?void 0:z.score)??0)<=0});let T="";return L.length&&(T+='<div class="cp-section-band">成品菜</div>',T+=Q(L).map(([w,z])=>S(l(w),z)).join("")),Z.length&&(T+='<div class="cp-section-band">中间产物</div>',T+=Q(Z).map(([w,z])=>S(l(w),z)).join("")),T}return`<div class="pick-grid">${H($)}</div>`}function Pe(){const $=k(),y=[`<button type="button" class="cr-comp-chip${a==="all"?" active":""}" data-filter="all">全部</button>`,`<button type="button" class="cr-comp-chip${a==="ing"?" active":""}" data-filter="ing">食材</button>`,`<button type="button" class="cr-comp-chip${a==="sub"?" active":""}" data-filter="sub">成品菜</button>`,`<button type="button" class="cr-comp-chip${a==="inter"?" active":""}" data-filter="inter">中间产物</button>`,`<button type="button" class="cr-comp-chip${a==="levelset"?" active":""}" data-filter="levelset">本关卡集</button>`].join(""),L=et&&(a==="all"||a==="ing")?'<p class="mp-status err">⚠️ 未加载到食材数据（桥接 /api/catalog/ingredients 异常），请刷新重试或检查 Unity 桥。</p>':"",Z=Ie($);return`
        <div class="cr-comp-toolbar">
          <input type="search" id="cp-search" class="ing-search" placeholder="搜索名称 / ID…" autocomplete="off">
          <div class="ing-groups">${y}</div>
        </div>
        ${L}
        <div class="modal-scroll" id="cp-scroll">
          <div id="cp-grid">${Z}</div>
          ${$.length?"":'<p class="muted">没有匹配的项</p>'}
        </div>`}re("添加食材 / 菜谱",`<p class="modal-hint">点击卡片加入（默认 1 份），再次点击 − / ＋ 调整数量。<b>官方成品菜按类型分组；本关卡集自定义菜谱单独一页</b>（含成品与中间产物）。</p>
       <div id="cp-body">${Pe()}</div>`,`${Ae()}${Et()}`);const Be=document.querySelector(".modal-panel");Be&&(Be.classList.add("wide"),Be.classList.add("cp-panel"));const Ye=document.getElementById("cp-body");function le(){Ye.innerHTML=Pe(),Le()}function Le(){var $,y;($=document.getElementById("cp-search"))==null||$.addEventListener("input",L=>{u=L.target.value;const Z=document.getElementById("cp-grid");if(Z){const T=k();Z.innerHTML=Ie(T);const w=document.getElementById("cp-scroll");if(w){const z=w.querySelector("p.muted");z&&z.remove(),T.length===0&&w.insertAdjacentHTML("beforeend",'<p class="muted">没有匹配的项</p>')}}}),document.querySelectorAll("#cp-body .cr-comp-chip").forEach(L=>{L.addEventListener("click",()=>{a=L.dataset.filter??"all",le()})}),(y=document.getElementById("cp-grid"))==null||y.addEventListener("click",L=>{const Z=L.target,T=Z.closest(".cp-step"),w=Z.closest(".cp-card");if(!w)return;const z=w.dataset.cpid,de=w.dataset.cpsub==="1";let se=e.get(z)??0;if(T){const at=T.dataset.cpinc!==void 0?1:-1;se=Math.max(0,se+at)}else se=se>0?0:1;se<=0?e.delete(z):e.set(z,se),w.outerHTML=m(z,de,se)})}Le(),(Te=document.querySelector("[data-cancel]"))==null||Te.addEventListener("click",W),(ze=document.querySelector("[data-ok]"))==null||ze.addEventListener("click",()=>{M.clear(),U.clear();for(const[$,y]of e)(x.has($)?U:M).set($,y);W(),R(),ae()})}function R(){const e=document.getElementById("cr-comp-list");if(!e)return;const a=[];for(const[d,v]of M){const k=ye.get(d);a.push(`<div class="cr-comp-row" data-rowid="ing:${i(d)}">
        ${It("ingredients",d)}
        <span class="cr-row-name">${i((k==null?void 0:k.nameZh)??d)}</span>
        <span class="cr-row-stepper">
          <button type="button" class="cr-step" data-rowdec="ing:${i(d)}">−</button>
          <span class="cr-step-num">${v}</span>
          <button type="button" class="cr-step" data-rowinc="ing:${i(d)}">＋</button>
        </span>
        <button type="button" class="cr-step-del" data-rowdel="ing:${i(d)}" title="移除">×</button>
      </div>`)}for(const[d,v]of U){const k=ie.get(d),H=k&&k.score<=0?'<span class="cr-chip-tag cr-chip-inter">中间产物</span>':'<span class="cr-chip-tag cr-chip-done">成品菜</span>',S=(k==null?void 0:k.source)==="levelset"?'<span class="cr-chip-tag cr-chip-levelset">本关卡集</span>':'<span class="cr-chip-tag">官方</span>';a.push(`<div class="cr-comp-row cr-comp-row-sub" data-rowid="sub:${i(d)}">
        ${k?`<img class="food-icon" loading="lazy" src="${i(k.source==="levelset"?it(k):`/icons/recipes/${encodeURIComponent(k.id)}.png`)}" alt="" onerror="this.onerror=null;this.src='/icons/_placeholder.png'" />`:""}
        <span class="cr-row-name">${i((k==null?void 0:k.nameZh)??d)}${S}${H}</span>
        <span class="cr-row-stepper">
          <button type="button" class="cr-step" data-rowdec="sub:${i(d)}">−</button>
          <span class="cr-step-num">${v}</span>
          <button type="button" class="cr-step" data-rowinc="sub:${i(d)}">＋</button>
        </span>
        <button type="button" class="cr-step-del" data-rowdel="sub:${i(d)}" title="移除">×</button>
      </div>`)}e.innerHTML=a.length?a.join(""):'<p class="muted small" style="margin:4px 0">尚未选择。点击下方「添加」选择食材或菜谱。</p>';const u=b().length,l=document.getElementById("cr-comp-hint");l&&(l.textContent=a.length?`共 ${u} 份组成（食材 ${[...M.values()].reduce((d,v)=>d+v,0)} · 菜谱 ${[...U.values()].reduce((d,v)=>d+v,0)}）`:""),e.querySelectorAll("[data-rowinc], [data-rowdec], [data-rowdel]").forEach(d=>{d.addEventListener("click",()=>{const v=d.dataset.rowinc??d.dataset.rowdec??d.dataset.rowdel??"",[k,H]=v.split(":"),S=k==="ing"?M:U,O=S.get(H)??0;d.dataset.rowdel!==void 0?S.delete(H):d.dataset.rowinc!==void 0?S.set(H,O+1):O<=1?S.delete(H):S.set(H,O-1),R(),ae()})})}function q(e){const a=[];for(const u of e){const l=ie.get(u);l&&(l.ingredients??[]).length>0?a.push(...l.ingredients):a.push(u)}return a}let Re=null;function ae(){var O,Q,Ie,Pe,Be,Ye;const e=document.getElementById("cr-preview");if(!e)return;const a=((O=document.getElementById("cr-rec-name"))==null?void 0:O.value.trim())??"",u=((Q=document.getElementById("cr-zh"))==null?void 0:Q.value.trim())??"",l=((Ie=document.getElementById("cr-en"))==null?void 0:Ie.value.trim())??"",d=Number((Pe=document.getElementById("cr-score"))==null?void 0:Pe.value)||0,v=((Be=document.getElementById("cr-type"))==null?void 0:Be.value)??"Composite",k=v==="Composite"||v==="Ingredient"||v==="Item"?"":((Ye=document.getElementById("cr-cook-step"))==null?void 0:Ye.value)??"",H=b(),S={guid:"",id:a||"preview",nameZh:u||a||"未命名菜谱",nameEn:l||void 0,assetPath:"",isCustom:!0,mixing:v==="Mixed",group:"levelset",score:d,intermediate:!1,ingredients:q(H),compositionIds:H,cookingStep:k||void 0,type:"custom"};e.innerHTML=vn(S,{allRecipes:D,ingredientName:le=>{var Le;return((Le=ye.get(le))==null?void 0:Le.nameZh)??le},iconSrc:Re?()=>Re:I&&(t!=null&&t.hasIcon)&&t.assetPath?()=>it(t):void 0})}Y.innerHTML=`
    ${p?`<div class="m-block cr-dev-banner"><b>⚠️ 开发者模式</b>：修改将写入共享库 <b>${i(E.libraryId)}</b>，影响全关卡集。仅支持更新已有菜谱。</div>`:""}
    <div class="m-actions-row">
      ${B("← 返回菜谱列表","default",{id:"cr-form-back"})}
      <span class="muted">${p?`共享库：<b>${i(E.libraryId)}</b>`:`关卡集：<b>${i(f)}</b>`} · ${I?`编辑 ${i(me)}`:"新建菜谱"}</span>
      <span style="flex:1"></span>
      ${B("💾 保存","primary",{id:"cr-form-save"})}
    </div>
    <div class="cr-form">
      ${C?'<p class="modal-hint cr-filling-hint">夹心是 0 分中间产物，不单独点单。保存模型后可在汉堡工作台选用；无模型的夹心不会进入候选。</p>':""}
      <div class="cr-section">
        <div class="m-section-title">组装效果（实时预览）</div>
        <div id="cr-preview" class="cr-preview"></div>
      </div>
      <div class="cr-section">
        <div class="m-section-title">基本信息</div>
        <label class="m-field">标识符 recipeName（仅字母/数字/下划线，创建后不可修改）
          <input type="text" id="cr-rec-name" value="${i(me)}" ${I?"disabled":""} placeholder="MyRecipe">
        </label>
        <div class="cr-form-grid">
          <label class="m-field">中文名<input type="text" id="cr-zh" value="${i(V)}" placeholder="我的菜谱"></label>
          <label class="m-field">英文名<input type="text" id="cr-en" value="${i(ge)}" placeholder="My Recipe"></label>
          <label class="m-field">分类 ${tt()}
            ${p?"":B("+ 新建分类","default",{id:"cr-new-cat-inline",style:"margin-top:6px"})}
          </label>
          <label class="m-field">菜谱图标（PNG，卡片图）<input type="file" id="cr-icon-upload" accept="image/png">
            <span class="muted small">上传后立即在组装预览中显示</span></label>
        </div>
      </div>
      <div class="cr-section">
        <div class="m-section-title">组成（食材 / 菜谱）</div>
        <div class="cr-comp-list" id="cr-comp-list"></div>
        <div class="cr-comp-toolbar" style="margin-top:10px">
          ${B("＋ 添加食材 / 菜谱","primary",{id:"cr-add-comp"})}
          <span class="muted small" id="cr-comp-hint" style="margin-left:auto"></span>
        </div>
      </div>
      <div class="cr-section">
        <div class="m-section-title">烹饪与装盘</div>
        <div class="cr-form-grid">
          <label class="m-field">类型
            <select id="cr-type" class="m-select">
              <option value="Composite" ${be==="Composite"?"selected":""}>组合（Composite）</option>
              <option value="Cooked" ${be==="Cooked"?"selected":""}>烹饪（Cooked）</option>
              <option value="Mixed" ${be==="Mixed"?"selected":""}>搅拌（Mixed）</option>
              <option value="Ingredient" ${be==="Ingredient"?"selected":""}>食材（Ingredient）</option>
              <option value="Item" ${be==="Item"?"selected":""}>道具（Item）</option>
            </select>
          </label>
          <label class="m-field">分数<input type="number" id="cr-score" value="${He}" min="0">
            <span class="muted small">${C?"夹心一般保持 <b>0 分</b>：0 分 = 中间产物，不进订单、只作为组成层":"菜谱分值（卡片展示）"}</span>
          </label>
          <label class="m-field" id="cr-cook-step-field">烹饪步骤 ${h(N.cookingSteps,Ze,"cr-cook-step")}</label>
          <label class="m-field" id="cr-cook-prog-field">烹饪程度
            <select id="cr-cook-prog" class="m-select">
              <option value="0">生（Raw）</option>
              <option value="1" selected>熟（Cooked）</option>
              <option value="2">焦（Burnt）</option>
            </select>
          </label>
          <label class="m-field" id="cr-plate-field">装盘容器 ${h((_t=N.platingContainers)!=null&&_t.length?N.platingContainers:(N.platingSteps??[]).filter(e=>e.id==="Plate"||e.id==="Glass"),P,"cr-plate-step")}
            <span class="muted small">决定上桌容器（盘子/杯子）</span>
          </label>
          <label class="m-field" id="cr-mix-prog-field">搅拌程度
            <select id="cr-mix-prog" class="m-select">
              <option value="0">未搅拌（Unmixed）</option>
              <option value="1" selected>已搅拌（Mixed）</option>
              <option value="2">过度搅拌（OverMixed）</option>
            </select>
          </label>
        </div>
      </div>
      <div class="cr-section">
        <div class="m-section-title">模型（3D）</div>
        <div class="cr-model-block">
          <div class="cr-model-upload">
            <label class="m-field">上传 3D 模型（FBX / OBJ，可多选 .mtl 与贴图）<input type="file" id="cr-model-file" accept=".fbx,.obj,.mtl" multiple></label>
            <label class="m-field">复用已有模型 ${h(N.reusableModels,ke,"cr-model-ref")}</label>
          </div>
          <div class="m-field cr-tex-field">补充贴图（点击格子选择/替换）
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
          <details class="cr-model-template cc-advanced" ${C?"open":""}>
            <summary>使用模板网格与自制贴图</summary>
            <p class="muted small">选择网格并制作 1024×1024 贴图；保存时复制到本菜谱目录。已上传 FBX/OBJ 时优先使用上传模型。</p>
            <div class="cr-form-grid">
              <label class="m-field">模板网格
                <select id="cr-tpl-mesh" class="m-select"><option value="">（不使用模板）</option></select>
              </label>
              <label class="m-field">贴图
                ${B("🎨 编辑贴图","default",{id:"cr-tpl-tex"})}
                <span class="muted small" id="cr-tpl-tex-state">未设置</span>
              </label>
              <label class="m-field">预览
                ${B("🔍 预览模板效果","default",{id:"cr-tpl-preview"})}
                <span class="muted small">用当前贴图实时渲染，不写盘</span>
              </label>
            </div>
          </details>
          <div class="cr-model-tools">
            <label class="m-field">在线预览${B("👁 预览并调整方向/大小","default",{id:"cr-preview-model"})}</label>
          </div>
        </div>
      </div>
      <details class="cr-section cc-advanced">
        <summary>高级：唯一编号、引擎图标与模型诊断</summary>
        <div class="cr-form-grid">
          <label class="m-field">UID（自动生成）<input type="text" value="${(t==null?void 0:t.uID)??(Me?j.uidPrefix*1e3+j.nextSequence:"—")}" disabled></label>
          <label class="m-field" id="cr-cook-icon-field">烹饪图标 ${h(N.icons,g,"cr-cook-icon")}</label>
          <label class="m-field" id="cr-mix-icon-field">搅拌图标 ${h(N.icons,ve,"cr-mix-icon")}</label>
          <label class="m-field">模型诊断${B("🔍 检查装盘链路","default",{id:"cr-diagnose"})}</label>
        </div>
      </details>
    </div>
    <div class="cr-form-footer">
      ${B("← 返回菜谱列表","default",{id:"cr-form-back2"})}
      <span style="flex:1"></span>
      ${B("💾 保存","primary",{id:"cr-form-save2"})}
    </div>
  `,ae(),R();let ce="",xe="",he="#c06205";const Ue=document.getElementById("cr-tpl-mesh");Ue&&(_n().then(e=>{var u;if(e.length===0)return;const a=(r==null?void 0:r.templateMeshId)??((u=e.find(l=>l.isDefault))==null?void 0:u.id)??e[0].id;Ue.innerHTML='<option value="">（不使用模板）</option>'+e.map(l=>`<option value="${i(l.id)}">${i(l.id)}${l.isDefault?"（默认）":""}</option>`).join(""),C&&(Ue.value=a,ce=a)}).catch(()=>{}),Ue.addEventListener("change",()=>{ce=Ue.value}));const St=document.getElementById("cr-tpl-tex-state"),Bt=()=>{St&&(St.textContent=xe?`已设置（${he}）`:"未设置（用模板自带贴图）")};Bt(),(Dt=document.getElementById("cr-tpl-tex"))==null||Dt.addEventListener("click",()=>{var a;const e=((a=document.getElementById("cr-rec-name"))==null?void 0:a.value.trim())||me||"Filling";st(()=>import("./textureEditor-gumOTPY-.js"),__vite__mapDeps([0,1,2,3])).then(u=>u.openTextureEditor({fileName:`${e}_Tex.png`,templateId:ce||(r==null?void 0:r.templateMeshId)||"FriedFishCake",initialColor:he,onDone:l=>{xe=l.base64,he=l.color,Bt(),_("贴图已设置，保存时会随模板网格一起写入。")}}))}),(Nt=document.getElementById("cr-tpl-preview"))==null||Nt.addEventListener("click",()=>{(async()=>{var a;const e=ce||(r==null?void 0:r.templateMeshId)||"FriedFishCake";try{const[{fetchTemplateMeshBuffer:u,previewLocalModel:l}]=await Promise.all([st(()=>import("./recipeModelPreview-CEsLkBiP.js"),__vite__mapDeps([3,1,2]))]),d=await u(e),v=[];if(xe){const k=atob(xe),H=new Uint8Array(k.length);for(let S=0;S<k.length;S++)H[S]=k.charCodeAt(S);v.push(new File([H],"tex.png",{type:"image/png"}))}await l({title:`模板模型预览 · ${e}`,buffer:d,fileName:`${e}.fbx`,textures:v,fitTarget:((a=document.getElementById("cr-plate-step"))==null?void 0:a.value)==="Glass"?"cup":"plate"})}catch(u){_(u.message||"模板预览失败。",!1)}})()});function Lt(){var l,d;const e=((l=document.getElementById("cr-type"))==null?void 0:l.value)??"Composite",a=e==="Ingredient"||e==="Item";for(const v of["cr-cook-step-field","cr-cook-icon-field","cr-cook-prog-field","cr-mix-icon-field","cr-mix-prog-field"])(d=document.getElementById(v))==null||d.classList.toggle("hidden",a);const u=document.getElementById("cr-comp-hint");u&&a&&(u.textContent="Ingredient / Item 只能选择 1 个组成节点"),ae()}(Yt=document.getElementById("cr-add-comp"))==null||Yt.addEventListener("click",()=>c());const _e=document.getElementById("cr-model-file"),De=document.getElementById("cr-model-texture"),Ct={scale:(t==null?void 0:t.modelScale)??1,rotationX:(t==null?void 0:t.modelRotationX)??0,rotationY:(t==null?void 0:t.modelRotationY)??0,rotationZ:(t==null?void 0:t.modelRotationZ)??0,positionX:(t==null?void 0:t.modelPositionX)??0,positionY:(t==null?void 0:t.modelPositionY)??0,positionZ:(t==null?void 0:t.modelPositionZ)??0,pivotX:(t==null?void 0:t.modelPivotX)??0,pivotY:(t==null?void 0:t.modelPivotY)??0,pivotZ:(t==null?void 0:t.modelPivotZ)??0};let ut=null;const nt=()=>({...Ct}),Mt=e=>{Object.assign(Ct,e)},Rt=()=>Fe();let Se=null,Xe="",Ne=null,qe=[];const $e={};let pt=null;const Pt=e=>{Mt(e)},Rn=(e,a,u)=>{const l=/\.([^.]+)$/.exec(u);let d=l?"."+l[1].toLowerCase():".png";return d!==".png"&&d!==".jpg"&&d!==".jpeg"&&(d=".png"),`${e}_${a}${d}`},ft=()=>{var u;const e=((u=document.getElementById("cr-rec-name"))==null?void 0:u.value.trim())||"texture",a={};for(const l of Object.keys($e)){const d=$e[l];d&&(a[l]=Rn(e,l,d.file.name))}return a},Pn=e=>{if(!e||e.length===0)return;const a=Array.from(e),u=a.find(d=>/\.(fbx|obj)$/i.test(d.name)),l=a.find(d=>/\.mtl$/i.test(d.name));if(!u){_("请选择 FBX 或 OBJ 模型文件。",!1);return}Ge("正在读取模型文件…",async()=>{Se=new Uint8Array(await u.arrayBuffer()),Xe=u.name,Ne=l?new TextDecoder("utf-8").decode(await l.arrayBuffer()):null,qe=[];for(const d of a){if(d===u||d===l||!/\.(png|jpg|jpeg)$/i.test(d.name))continue;const v=pa(d.name);v&&qe.push({orig:d.name,disk:v,bytes:new Uint8Array(await d.arrayBuffer()),file:d})}gt()})},Tn=async()=>{var H;if(!Se)return null;const e=/\.fbx$/i.test(Xe),a=ft();if(e){const{renameFbxTextureRefs:S}=await st(async()=>{const{renameFbxTextureRefs:Q}=await import("./fbxTextureRename-CZchYSrG.js");return{renameFbxTextureRefs:Q}},[]),O=S(Se,a);return{bytes:O.bytes,mtlText:null,renamed:O.renamed,fbx:!0}}const u=((H=document.getElementById("cr-rec-name"))==null?void 0:H.value.trim())||"texture";let l=Ne??null,d=0;if(l!=null){const S={};for(const Q of qe)S[Q.orig]=Q.disk;const O=fa(l,a,S);l=O.mtl,d+=O.renamed}const v=new TextDecoder("utf-8").decode(Se),k=Ne!=null?ga(v,u+".mtl"):v;return k!==v&&(d+=1),{bytes:new TextEncoder().encode(k),mtlText:l,renamed:d,fbx:!1}},gt=()=>{const e=Se;e&&Ge("正在加载 3D 预览…",async()=>{const a=$e.base_color,{openModelPreview:u}=await st(async()=>{const{openModelPreview:v}=await import("./dom-fmO7ZFgN.js").then(k=>k.dG);return{openModelPreview:v}},__vite__mapDeps([1,2])),l=[...a?[a.file]:[],...qe.map(v=>v.file)],d=document.getElementById("cr-plate-step");u({title:Xe,resourceBase:"",modelFileName:Xe,localBuffer:e.buffer.slice(e.byteOffset,e.byteOffset+e.byteLength),localMtl:Ne??void 0,localTextures:l.length>0?l:void 0,fitTarget:(d==null?void 0:d.value)==="Glass"?"cup":"plate",...nt(),unitySize:Rt(),boundsMinY:t==null?void 0:t.boundsMinY,onAdjust:Pt})})};_e==null||_e.addEventListener("change",()=>Pn(_e.files)),document.querySelectorAll(".cr-tex-slot").forEach(e=>{e.addEventListener("click",()=>{pt=e.dataset.tex??null,!(!pt||!De)&&(De.value="",De.click())})}),De==null||De.addEventListener("change",()=>{var u;const e=(u=De.files)==null?void 0:u[0],a=pt;!e||!a||Ge("正在加载贴图…",async()=>{const l=$e[a];l&&URL.revokeObjectURL(l.url);const d=URL.createObjectURL(e);$e[a]={file:e,bytes:new Uint8Array(await e.arrayBuffer()),url:d};const v=document.getElementById("cr-tex-thumb-"+a);v&&(v.innerHTML=`<img src="${d}" alt="${a}">`),Se&&gt()})}),(jt=document.getElementById("cr-preview-model"))==null||jt.addEventListener("click",()=>{var a;if((a=_e==null?void 0:_e.files)!=null&&a[0]){gt();return}if(!o)return;const e=document.getElementById("cr-plate-step");Ge("正在加载 3D 预览（首次加载较慢）…",async()=>{await In(o,me||(o.split("/").pop()??o),{readonly:p?!1:void 0,fitTarget:(e==null?void 0:e.value)==="Glass"?"cup":"plate",...nt(),unitySize:Rt(),boundsMinY:t==null?void 0:t.boundsMinY,onAdjust:Pt})})}),(Ht=document.getElementById("cr-diagnose"))==null||Ht.addEventListener("click",()=>{if(!o){alert("保存菜谱后才能诊断。");return}Ge("正在诊断…",async()=>{try{const e=await ua(o),a=[];if(e.error&&a.push(`错误：${e.error}`),a.push(`模型引用：${e.modelDirect?"model 直引 ✓":"无直引"}${e.modelSOBased?"（另有 modelSO）":""}`),a.push(`模型路径：${e.modelPath||"—"}${e.modelPath?`（${e.modelType}）`:""}`),a.push(`模型结构：${e.modelStructure||"—"}`),a.push(`渲染器 ${e.rendererCount} 个 · 含网格 ${e.meshCount} 个 · 含材质 ${e.materialCount} 个（网格/材质齐全才可见）`),e.boundsSizeX||e.boundsSizeY||e.boundsSizeZ){const l=[...[["X",e.boundsSizeX],["Y",e.boundsSizeY],["Z",e.boundsSizeZ]]].sort((d,v)=>d[1]-v[1])[0];a.push(`Unity 内模型包围盒（含变换）：X ${te(ne(e.boundsSizeX))} cm · Y ${te(ne(e.boundsSizeY))} cm · Z ${te(ne(e.boundsSizeZ))} cm`),a.push(`薄轴 = ${l[0]}（${te(ne(l[1]))} cm）：${l[0]==="Y"?"模型平躺 ✓":"模型竖立 ✗，需在预览中用「旋转 90°」或手动设 X/Z 旋转摆平"}；底面 Y = ${te(ne(e.boundsMinY))} cm`)}a.push(`组成：${e.compositionCount} 项（≥1 才可装盘匹配）`),a.push(`烹饪步骤：${e.cookingStepSet?"已配置 ✓":"未配置"} · 装盘步骤：${e.platingStepSet?"已配置 ✓":"未配置"}`),a.push(`装盘模型 GetModel：${e.platingPrefabSet?"非空 ✓":"为空 ✗（游戏中会显示空盘子）"}`),a.push(`变换：缩放 ${nn(e.modelScale)} · 旋转 X/Y/Z ${e.modelRotationX}°/${e.modelRotationY}°/${e.modelRotationZ}° · 位置 ${te(ne(e.modelPositionX))}/${te(ne(e.modelPositionY))}/${te(ne(e.modelPositionZ))} cm（1 单位 = 100 cm）`),Tt(a)}catch(e){Tt([`诊断失败：${e.message||String(e)}`])}})});function Tt(e){var d;const a=e.join(`
`),u=`<div class="diag-scroll"><pre class="diag-text">${i(a)}</pre></div>`;re("装盘链路诊断",u,`${B("📋 复制链路数据","default",{id:"diag-copy"})}${B("关闭","primary",{"data-cancel":""})}`),(d=document.querySelector("[data-cancel]"))==null||d.addEventListener("click",W);const l=document.getElementById("diag-copy");l==null||l.addEventListener("click",async()=>{let v=!1;try{await navigator.clipboard.writeText(a),v=!0}catch{try{const k=document.createElement("textarea");k.value=a,k.style.position="fixed",k.style.opacity="0",document.body.appendChild(k),k.select(),v=document.execCommand("copy"),document.body.removeChild(k)}catch{v=!1}}l.textContent=v?"已复制 ✓":"复制失败",setTimeout(()=>{l&&(l.textContent="📋 复制链路数据")},2e3)})}(Xt=document.getElementById("cr-type"))==null||Xt.addEventListener("change",Lt),(At=document.getElementById("cr-score"))==null||At.addEventListener("input",ae),(Ot=document.getElementById("cr-zh"))==null||Ot.addEventListener("input",ae),(Ft=document.getElementById("cr-en"))==null||Ft.addEventListener("input",ae),(Ut=document.getElementById("cr-rec-name"))==null||Ut.addEventListener("input",ae),(qt=document.getElementById("cr-cook-step"))==null||qt.addEventListener("change",ae),(Wt=document.getElementById("cr-icon-upload"))==null||Wt.addEventListener("change",e=>{var u;Re&&(URL.revokeObjectURL(Re),Re=null);const a=(u=e.target.files)==null?void 0:u[0];a&&(Re=URL.createObjectURL(a)),ae()}),Lt(),p||(Gt=document.getElementById("cr-new-cat-inline"))==null||Gt.addEventListener("click",()=>Sa(f,async e=>{j=await Je(f);const a=document.getElementById("cr-type-cat");a&&e&&(a.innerHTML=j.categories.map(u=>`<option value="${i(u.id)}">${i(u.zh||u.id)}</option>`).join(""),a.value=e)}));const bt=()=>{r!=null&&r.onBack?r.onBack():Ee(s,E)};(Vt=document.getElementById("cr-form-back"))==null||Vt.addEventListener("click",bt),(Kt=document.getElementById("cr-form-back2"))==null||Kt.addEventListener("click",bt);const zt=async()=>{var k,H,S,O,Q,Ie,Pe,Be,Ye;const e=document.getElementById("cr-rec-name").value.trim();if(!e)return alert("请填写标识符");if(!mt.test(e))return alert("标识符仅允许英文字母/数字/下划线，且不能以数字开头");const a=document.getElementById("cr-type").value,u=b();if((a==="Ingredient"||a==="Item")&&u.length!==1)return alert("Ingredient / Item 类型必须且只能选择 1 个食材/道具组成节点");const l=nt(),d={setName:I?void 0:f,assetPath:I?o:void 0,recipeName:e,nameZh:document.getElementById("cr-zh").value.trim(),nameEn:document.getElementById("cr-en").value.trim(),category:document.getElementById("cr-type-cat").value,score:Number(document.getElementById("cr-score").value)||0,type:a,compositionIds:u,cookingStepId:a==="Composite"||a==="Ingredient"||a==="Item"?"":((k=document.getElementById("cr-cook-step"))==null?void 0:k.value)??"",cookingStepIconId:((H=document.getElementById("cr-cook-icon"))==null?void 0:H.value)??"",platingStepId:((S=document.getElementById("cr-plate-step"))==null?void 0:S.value)??"",mixingIconId:((O=document.getElementById("cr-mix-icon"))==null?void 0:O.value)??"",modelPrefabId:((Q=document.getElementById("cr-model-ref"))==null?void 0:Q.value)??"",cookingProgress:Number(((Ie=document.getElementById("cr-cook-prog"))==null?void 0:Ie.value)??"1")||1,mixingProgress:Number(((Pe=document.getElementById("cr-mix-prog"))==null?void 0:Pe.value)??"1")||1,modelScale:l.scale,modelRotationX:l.rotationX,modelRotationY:l.rotationY,modelRotationZ:l.rotationZ,modelPositionX:l.positionX,modelPositionY:l.positionY,modelPositionZ:l.positionZ,modelPivotX:l.pivotX,modelPivotY:l.pivotY,modelPivotZ:l.pivotZ};Ce("保存中…");const v=p?"":f;try{if(p){if(!I)throw new Error("共享菜谱库不支持新建。");await vt(d)}else I?await vt(d):await Wn(d);const le=o||(p?"":`Assets/LevelSets/${f}/custom_recipes/${d.category}/${e}.asset`),Le=(Be=document.getElementById("cr-icon-upload").files)==null?void 0:Be[0];if(Le&&le){const y=await zn(Le);await Gn(v,le,Le.name,y)}const Te=document.getElementById("cr-model-file").files,ze=Te&&Te.length>0?Array.from(Te).find(y=>/\.(fbx|obj)$/i.test(y.name)):void 0;if(!ze&&ce){const y=[];xe&&y.push({fileName:`${e}_Tex.png`,base64:xe});const L=ft();for(const T of Object.keys($e)){const w=$e[T],z=L[T];w&&z&&y.push({fileName:z,base64:await We(w.bytes)})}const Z=await Vn(v,le,ce,y);Z&&Z.rawSizeX!=null&&Z.rawSizeZ!=null&&(ut={x:Z.rawSizeX,y:Z.rawSizeY??0,z:Z.rawSizeZ}),_(`已用模板网格「${ce}」生成模型${xe?"（含自制贴图）":""}。`)}if(ze){const y=/\.fbx$/i.test(ze.name);if(!Se||!Xe){Se=new Uint8Array(await ze.arrayBuffer()),Xe=ze.name;const z=Te?Array.from(Te).find(de=>/\.mtl$/i.test(de.name)):void 0;Ne=z?new TextDecoder("utf-8").decode(await z.arrayBuffer()):null}if(y&&!$e.base_color){_("请先在 base_color 格子中选择彩色贴图。",!1);return}const L=await Tn()??{bytes:Se,mtlText:Ne,renamed:0,fbx:y};L.renamed===0&&(y||Ne)&&alert(y?"警告：FBX 中未找到可改写的贴图引用（可能不是二进制 FBX 或不含贴图引用），贴图引用名未修改。":"警告：OBJ/MTL 中未找到可改写的贴图引用，贴图引用名未修改。");const Z=[{fileName:e+(y?".fbx":".obj"),base64:await We(L.bytes)}];!y&&L.mtlText!=null&&Z.push({fileName:e+".mtl",base64:await We(new TextEncoder().encode(L.mtlText))});const T=ft();for(const z of Object.keys($e)){const de=$e[z],se=T[z];de&&se&&Z.push({fileName:se,base64:await We(de.bytes)})}for(const z of qe)Z.push({fileName:z.disk,base64:await We(z.bytes)});const w=await Kn(v,le,Z);if(w&&w.rawSizeX!=null&&w.rawSizeZ!=null){ut={x:w.rawSizeX,y:w.rawSizeY??0,z:w.rawSizeZ};const z=Math.max(Math.abs(w.rawSizeX),Math.abs(w.rawSizeZ))||1,de=((Ye=document.getElementById("cr-plate-step"))==null?void 0:Ye.value)==="Glass",se=de?.37:.85,at=Math.max(1e-6,se/z),en=0,ue={...l,scale:at,positionX:0,positionY:en,positionZ:0};Mt(ue),await vt({...d,setName:void 0,assetPath:le,modelScale:ue.scale,modelRotationX:ue.rotationX,modelRotationY:ue.rotationY,modelRotationZ:ue.rotationZ,modelPositionX:ue.positionX,modelPositionY:ue.positionY,modelPositionZ:ue.positionZ,modelPivotX:ue.pivotX,modelPivotY:ue.pivotY,modelPivotZ:ue.pivotZ}),_(`已按 Unity 实际尺寸自动校准：尺寸 ${te(ne(se))} cm（缩放 ${nn(at)}）· 位置Y ${te(ne(en))} cm（${de?"杯子":"盘子"}参照；可重新打开预览微调，游戏内直接生效）`)}}const $=nt();_(`${I?"已更新菜谱":"已创建菜谱"} · 模型变换已保存：足迹 ${te(ne((Jn(ut??Fe()??{x:1,y:1,z:1})||1)*$.scale))} cm · 旋转 ${$.rotationX}°/${$.rotationY}°/${$.rotationZ}° · 位置 ${te(ne($.positionX))}/${te(ne($.positionY))}/${te(ne($.positionZ))} cm（重新打开可回显，游戏内直接生效）`),Ve=d.category,bt()}catch(le){_(le.message,!1)}finally{pe()}};(Jt=document.getElementById("cr-form-save"))==null||Jt.addEventListener("click",()=>void zt()),(Qt=document.getElementById("cr-form-save2"))==null||Qt.addEventListener("click",()=>void zt());function zn(e){return new Promise((a,u)=>{const l=new FileReader;l.onload=()=>{const d=l.result,v=d.indexOf(",");a(v>=0?d.substring(v+1):d)},l.onerror=u,l.readAsDataURL(e)})}function We(e){let a="";for(let l=0;l<e.length;l+=32768)a+=String.fromCharCode(...e.subarray(l,l+32768));return Promise.resolve(btoa(a))}}function ka(s,n,o,r){var p,f;const E=o.split("/").pop()??o;re(`删除菜谱 · ${i(E)}`,"<p>将永久删除菜谱资源及其模型文件夹，且<b>不可恢复</b>。若其他菜谱引用了它作为子菜谱，组成将失效。</p>",`${Ae()}${$n("确认删除",{"data-ok":""})}`),(p=document.querySelector("[data-cancel]"))==null||p.addEventListener("click",W),(f=document.querySelector("[data-ok]"))==null||f.addEventListener("click",async()=>{Ce("删除中…");try{await ia(o),W(),_("已删除菜谱"),r()}catch(I){_(I.message,!1)}finally{pe()}})}function xa(s,n){re("新建分类",`<label class="m-field">分类ID（仅字母/数字/下划线，用于目录名）<input type="text" id="cr-cat-id" placeholder="MyCategory"></label>
     <label class="m-field">中文名<input type="text" id="cr-cat-zh" placeholder="我的分类"></label>
     <label class="m-field">英文名<input type="text" id="cr-cat-en" placeholder="My Category"></label>`,`${Ae()}${Et("创建")}`),Mn(W,async()=>{const o=document.getElementById("cr-cat-id").value.trim();if(!o)return alert("请填写分类ID");if(!mt.test(o))return alert("分类ID仅允许英文字母/数字/下划线");const r=document.getElementById("cr-cat-zh").value.trim(),E=document.getElementById("cr-cat-en").value.trim();Ce("创建分类…");try{await yn(s,o,r||o,E||o),W(),_("已创建分类"),n(o)}catch(p){_(p.message,!1)}finally{pe()}})}function Sa(s,n){const o=prompt("分类ID（仅字母/数字/下划线）：");if(!o||!mt.test(o)){alert("ID非法");return}const r=prompt("分类中文名：")||o,E=prompt("分类英文名：")||o;Ce("创建分类…"),yn(s,o,r,E).then(()=>{_("已创建分类"),n(o)}).catch(p=>_(p.message,!1)).finally(()=>pe())}function Cn(s,n,o){var f;function r(I){return`${I.zh||I.id}${I.en?` (${I.en})`:""}`}const E=n.map(I=>`
    <div class="m-row" style="margin-bottom:8px">
      <span style="flex:1">${i(r(I))} <span class="muted">[${i(I.id)}]</span></span>
      ${B("重命名","default",{"data-rename":i(I.id)})}
      ${B("删除","danger",{"data-delcat":i(I.id)})}
    </div>`).join(""),p=async()=>{try{const I=await Je(s);Cn(s,I.categories,o)}catch(I){_(I.message,!1)}};re("管理分类",`<div class="modal-scroll">${E||'<p class="muted">暂无分类</p>'}</div>`,Ae("关闭"),{id:"category-manager"}),(f=document.querySelector("[data-cancel]"))==null||f.addEventListener("click",W),document.querySelectorAll("[data-rename]").forEach(I=>{I.addEventListener("click",()=>{const C=I.dataset.rename,Y=n.find(A=>A.id===C);Ba(s,C,(Y==null?void 0:Y.zh)??C,(Y==null?void 0:Y.en)??C,()=>{p(),o()})})}),document.querySelectorAll("[data-delcat]").forEach(I=>{I.addEventListener("click",()=>{var A,N;const C=I.dataset.delcat,Y=n.find(j=>j.id===C);re(`删除分类 · ${i((Y==null?void 0:Y.zh)||C)}`,"<p>将检查关卡使用情况。如果有关卡正在使用该分类的菜谱，则不允许删除。</p>",`${Ae()}${$n("确认删除",{"data-ok":""})}`),(A=document.querySelector("[data-cancel]"))==null||A.addEventListener("click",W),(N=document.querySelector("[data-ok]"))==null||N.addEventListener("click",async()=>{Ce("检查中…");try{await oa(s,C),W(),_("已删除分类"),p(),o()}catch(j){_(j.message,!1),pe()}})})})}function Ba(s,n,o,r,E){re(`重命名分类 · ${i(o||n)}`,`<label class="m-field">分类ID（仅字母/数字/下划线）<input type="text" id="cr-rename-id" value="${i(n)}"></label>
     <label class="m-field">中文名<input type="text" id="cr-rename-zh" value="${i(o)}"></label>
     <label class="m-field">英文名<input type="text" id="cr-rename-en" value="${i(r)}"></label>`,`${Ae()}${Et("确认")}`),Mn(W,async()=>{const p=document.getElementById("cr-rename-id").value.trim();if(!p)return alert("请填写分类ID");if(!mt.test(p))return alert("分类ID仅允许英文字母/数字/下划线");const f=document.getElementById("cr-rename-zh").value.trim(),I=document.getElementById("cr-rename-en").value.trim();Ce("重命名…");try{await la(s,n,p,f||p,I||p),W(),_("已重命名分类"),E()}catch(C){_(C.message,!1)}finally{pe()}})}function Mn(s,n){var o,r;(o=document.querySelector("[data-cancel]"))==null||o.addEventListener("click",s),(r=document.querySelector("[data-ok]"))==null||r.addEventListener("click",n)}export{Pa as renderCustomRecipesView,rt as renderRecipeForm};
