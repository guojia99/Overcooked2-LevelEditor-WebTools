const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["assets/dom-DdkAHNGl.js","assets/dom-Dseup-pk.css"])))=>i.map(i=>d[i]);
import{Y as Be,$ as Ae,a0 as j,a1 as B,k as w,a2 as Ne,a3 as Re,a4 as _e,a5 as be,a6 as Ie,a7 as xe,a8 as oe,a9 as je,v as A,aa as Fe,ab as Ze,ac as qe,q as He,ad as Me,ae as ze,_ as de,af as Oe,ag as Ue,ah as Xe,ai as re,n as Ye,w as De,aj as Ke,T as Ve,U as ue}from"./dom-DdkAHNGl.js";function s(l){return String(l??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}const D=[{id:"ingredient",icon:"🥕",zh:"自定义食材",en:"Custom ingredients",desc:"可加工 / 不可加工 / 备餐容器食材，含 OrderNode、图标与模型"},{id:"counter",icon:"🧱",zh:"自定义工作台",en:"Custom counters",desc:"普通 / 切菜 / 水槽工作台，含火焰、切菜 PFX、教程图标"},{id:"utensil",icon:"🍳",zh:"自定义锅具",en:"Custom utensils",desc:"火锅、烤盘、煎盘、汤锅、蒸笼等；支持站台类型与锅内食材模型"},{id:"sink",icon:"🚰",zh:"自定义水槽",en:"Custom sinks",desc:"清洗/沥干部件与干净盘堆绑定"},{id:"tray",icon:"🍽️",zh:"自定义托盘",en:"Custom trays",desc:"多槽上菜容器与槽位配方"},{id:"prep",icon:"🥣",zh:"自定义备餐容器",en:"Preparation containers",desc:"OrderNode、容器限制与 cosmetics prefab"},{id:"splitter",icon:"🔀",zh:"分料机",en:"Ingredient splitter",desc:"输入/输出、容量、每份拆分数量与音效"}];function he(l){return D.find(t=>t.id===l)??{id:l,icon:"🧩",zh:l,en:l,desc:""}}const pe={utensil_hotpot:{capacity:4,stationTypes:["Invalid_FloorBurner"]},utensil_roasting_tray:{capacity:4,stationTypes:["Oven"]},utensil_griddle_pan:{capacity:4,stationTypes:["FirePit"]},utensil_skewer:{capacity:3,stationTypes:["Barbeque"]},utensil_toasting_fork:{capacity:1,stationTypes:["FirePit"]},utensil_pot:{capacity:3,stationTypes:["Hob"]},utensil_steamer:{capacity:4,stationTypes:["Hob"]},splitter:{capacity:8,splitNum:2,splitAudioTag:"PutDown"}};function z(l,t,i,c){return document.body.classList.add("manage-bg"),l.innerHTML=`
    ${Ye("custom-content")}
    <div class="manage-bar">
      <h1 class="m-title">${s(t)}</h1>
      <span class="cc-spacer"></span>
    </div>
    ${i?We(i,c):""}
    <div class="manage-content cc-content" id="cc-content"></div>
  `,De(),document.getElementById("cc-content")}function We(l,t){const i=(c,r,m)=>`<a class="cc-tab${t===c?" active":""}" href="${B(l,c||void 0)}">${m} ${s(r)}</a>`;return`<div class="cc-tabs-wrap"><nav class="cc-tabs">
    <a class="cc-tab${t?"":" active"}" href="${B(l)}">🏠 总览</a>
    ${D.map(c=>i(c.id,c.zh,c.icon)).join("")}
  </nav></div>`}function K(l){const t=document.getElementById("cc-content");t&&(t.innerHTML=`<p class="muted">${s(l)}</p>`),A(l)}function M(l){const t=l instanceof Error?l.message:String(l);A(t,!1);const i=document.getElementById("cc-content");i&&(i.innerHTML=`<div class="m-block"><h3>出错</h3><p>${s(t)}</p></div>`)}let me=!1;async function st(l){me||(me=!0,window.addEventListener("popstate",()=>void ve(l))),await ve(l)}async function ve(l){const t=Be();if(t.page==="custom-content"){if(!t.setId){await Je(l);return}if(!t.contentKind){await Qe(l,t.setId);return}if(!t.contentId){await $e(l,t.setId,t.contentKind);return}await ct(l,t.setId,t.contentKind,t.contentId)}}async function Je(l){const t=z(l,"自定义管理");K("加载关卡集…");let i=[];try{i=await Ae()}catch(c){M(c);return}t.innerHTML=`
    <div class="m-section-title">选择关卡集</div>
    <p class="modal-hint">自定义内容按关卡集保存（Assets/LevelSets/&lt;set&gt;/custom_content）。模板来自 common04 / common03 / commonW1，模板本身只读。</p>
    <div class="m-grid">${i.map(c=>`<div class="m-card cc-set-card">
          <div class="cc-set-icon">🧩</div>
          <h3>${s(c.levelSetNameZH||c.setName)} <span class="muted">(${s(c.levelSetName||c.setName)})</span></h3>
          <div class="m-meta">标识：${s(c.setName)} · 关卡数：${c.levelCount}<br>作者：${s(c.author||"—")}</div>
          <div class="m-actions">${j("进入自定义管理",{"data-open":s(c.setName)})}</div>
        </div>`).join("")||'<p class="muted">暂无关卡集</p>'}</div>`,t.querySelectorAll("[data-open]").forEach(c=>c.addEventListener("click",()=>location.assign(B(c.dataset.open))))}async function Qe(l,t){var c,r,m;const i=z(l,`自定义管理 · ${t}`,t);i.innerHTML=`
    <div class="cc-hero">
      <div><h2>🧩 自定义内容</h2><p>选择类型进入管理。所有引用字段都使用下拉/选择器，不再手输 GUID。</p></div>
      <div class="cc-hero-actions">
        ${w("🍽️ 自定义菜谱","default",{"data-nav-recipes":""})}
        ${w("🍔 汉堡工作台","default",{"data-nav-burger":""})}
        ${w("🥩 夹心工作台","default",{"data-nav-filling":""})}
      </div>
    </div>
    <div class="m-grid cc-kind-grid">
      ${D.map(n=>`<div class="m-card cc-kind-card" data-kind="${n.id}">
          <div class="cc-kind-icon">${n.icon}</div>
          <h3>${s(n.zh)} <span class="muted">(${s(n.en)})</span></h3>
          <div class="m-meta">${s(n.desc)}</div>
          <div class="m-actions">${w("进入管理","primary",{"data-kind":n.id})}</div>
        </div>`).join("")}
    </div>`,i.querySelectorAll("[data-kind]").forEach(n=>n.addEventListener("click",()=>location.assign(B(t,n.dataset.kind)))),(c=i.querySelector("[data-nav-recipes]"))==null||c.addEventListener("click",()=>location.assign(Ne(t))),(r=i.querySelector("[data-nav-burger]"))==null||r.addEventListener("click",()=>location.assign(Re(t))),(m=i.querySelector("[data-nav-filling]"))==null||m.addEventListener("click",()=>location.assign(_e(t)))}async function $e(l,t,i){const c=he(i),r=z(l,`${c.icon} ${c.zh} · ${t}`,t,i);K("加载列表…");let m=[];try{m=await be(t,i)}catch(n){M(n);return}r.innerHTML=`
    <div class="cc-list-head">
      <div>
        <h2>${c.icon} ${s(c.zh)} <span class="muted">(${m.length})</span></h2>
        <p>${s(c.desc)}</p>
      </div>
      <div class="cc-actions">${j("＋ 新建 "+c.zh,{"data-new":""})}</div>
    </div>
    <div class="m-grid cc-item-grid">${m.map(n=>{var d;return`<div class="m-card cc-item-card">
          <div class="cc-item-top">
            <img class="cc-item-icon" src="${s(Ie(n))}" alt="" onerror="this.style.display='none'"/>
            <div><h3>${s(n.nameZh||n.id)}</h3><div class="muted small">${s(n.id)}</div></div>
          </div>
          <div class="m-meta">模板：${s(n.templateId||"—")}<br><span class="cc-path">${s(n.assetPath)}</span>
          ${(d=n.warnings)!=null&&d.length?`<div class="cc-warn">⚠ ${s(n.warnings.join("；"))}</div>`:""}</div>
          <div class="m-actions">
            ${w("编辑","primary",{"data-edit":s(n.id)})}
            ${xe("删除",{"data-del":s(n.id)})}
          </div>
        </div>`}).join("")||`<div class="cc-empty"><div>${c.icon}</div><p>还没有自定义内容</p>${j("从模板创建",{"data-new":""})}</div>`}</div>`,r.querySelectorAll("[data-new]").forEach(n=>n.addEventListener("click",()=>location.assign(oe(t,i,"new")))),r.querySelectorAll("[data-edit]").forEach(n=>n.addEventListener("click",()=>location.assign(oe(t,i,n.dataset.edit)))),r.querySelectorAll("[data-del]").forEach(n=>n.addEventListener("click",async()=>{const d=n.dataset.del;if(confirm(`删除自定义内容「${d}」？此操作会删除其 prefab / OrderNode / 图标 / 模型。`))try{await je(t,i,d),A("已删除"),await $e(l,t,i)}catch(g){A(g.message||String(g),!1)}}))}function f(l,t,i,c="— 不设置 —",r="guid"){const m=new Set,n=t.filter(d=>d&&d.guid&&!m.has(d.guid)&&m.add(d.guid)).map(d=>{const g=r==="id"?d.id:d.guid;return`<option value="${s(g)}" ${g===i?"selected":""}>${s(d.nameZh||d.id)}${d.nameEn&&d.nameEn!==d.id?` · ${s(d.nameEn)}`:""}</option>`}).join("");return`<select id="${l}" class="m-select cc-select"><option value="">${s(c)}</option>${n}</select>`}function et(l,t,i){if(!i){const c=l.find(r=>r.id===t);return`<div class="cc-template-current">当前模板：<b>${s((c==null?void 0:c.labelZh)||t||"—")}</b> <span class="muted">(${s(t||"—")})</span></div>`}return`<div class="cc-template-grid">${l.map(c=>`<button type="button" class="cc-template-card${c.id===t?" active":""}" data-template="${s(c.id)}">
        <b>${s(c.labelZh)}</b><span>${s(c.labelEn||c.id)}</span><small>${s(c.id)}</small>
      </button>`).join("")}</div>`}function tt(l,t){const i=new Set(t);return(l.stationTypes??[]).map(c=>`<label class="cc-check"><input type="checkbox" class="cc-station" value="${s(c.name)}" ${i.has(c.name)?"checked":""}/> <span>${s(c.labelZh)}</span></label>`).join("")}function Y(l,t){return`<button type="button" class="m-btn default cc-multi-btn" id="${l}">${s(t)}</button>`}async function ct(l,t,i,c){var te,ce,ae,se,ie,le;const r=he(i),m=c==="new",n=z(l,`${m?"新建":"编辑"} ${r.zh} · ${t}`,t,i);K("加载模板与引用…");let d=[],g={stationTypes:[]},u={setName:t,prefabs:[],soRefs:[],modelPrefabs:[],modelSOs:[],orderLists:[],lookups:[],recipes:[],plating:[],audioTags:[]},V=[],W=[];try{[d,g,u,V,W]=await Promise.all([Fe(i),Ze(),qe(t),m?Promise.resolve([]):be(t,i),He(t).catch(()=>[])])}catch(a){M(a);return}const e=m?void 0:V.find(a=>a.id===c);if(!m&&!e){M(`未找到「${c}」`);return}let N=(e==null?void 0:e.templateId)||((te=d[0])==null?void 0:te.id)||"",O=(e==null?void 0:e.assetPath)||((ce=d.find(a=>a.id===N))==null?void 0:ce.assetPath)||"",R=(e==null?void 0:e.allowedIngredientGuids)??[],_=(e==null?void 0:e.modelGuids)??[],I=(e==null?void 0:e.modelSoGuids)??[],S=[],x=null,J,U;async function Q(a){if(!a){S=[];return}try{S=(await Ke(a)).paths??[]}catch{S=[]}}if(await Q(O),e)try{const a=await Me(e.assetPath);a.modelFile&&(x=a)}catch{x=null}const F=pe[N]??{},ee=(e==null?void 0:e.capacity)??F.capacity??0,ye=(e==null?void 0:e.splitNum)??F.splitNum??2,Ee=(e==null?void 0:e.splitAudioTag)??F.splitAudioTag??"",Pe=(e==null?void 0:e.stationTypes)??F.stationTypes??[],we=`
    <div class="cc-section"><div class="cc-section-title">🥕 食材 / OrderNode</div>
      <div class="cc-grid">
        <label class="m-field">模板
          <div class="muted small">编辑时模板只读；新建请看上方模板卡片</div>
        </label>
        <label class="m-field">OrderNode 唯一 uID
          <input id="cc-order-uid" type="number" min="1" value="${(e==null?void 0:e.orderUid)??0}" class="m-input"><span class="muted small">0 = 自动分配</span>
        </label>
        <label class="m-field">装盘容器 ${f("cc-plating",u.plating,(e==null?void 0:e.platingStepId)??"","— 不设置 —","id")}</label>
        <label class="m-field">容器限制（OrderToPrefabLookup） ${f("cc-restrictions",u.lookups,(e==null?void 0:e.restrictionsGuid)??"")}</label>
        <label class="m-field">Cosmetics prefab ${f("cc-cosmetics",u.prefabs,(e==null?void 0:e.cosmeticsPrefabGuid)??"")}</label>
        <label class="m-field">可加工下一形态 ${f("cc-next-prefab",u.prefabs,(e==null?void 0:e.nextPrefabGuid)??"")}</label>
        <label class="m-field">食材内容 UI ${f("cc-ingredient-ui",u.soRefs,(e==null?void 0:e.ingredientContentUiGuid)??"")}</label>
        <label class="m-field">进度 UI ${f("cc-progress-ui",u.soRefs,(e==null?void 0:e.progressUiGuid)??"")}</label>
      </div>
    </div>`,X=`
    <div class="cc-section"><div class="cc-section-title">🧱 工作台参数</div>
      <div class="cc-grid">
        <label class="m-field">进度 UI ${f("cc-progress-ui",u.soRefs,(e==null?void 0:e.progressUiGuid)??"")}</label>
        <label class="m-field">火焰特效 ${f("cc-fire-fx",u.soRefs,(e==null?void 0:e.fireEffectGuid)??"")}</label>
        <label class="m-field">切菜 PFX ${f("cc-chop-pfx",u.soRefs,(e==null?void 0:e.chopPfxGuid)??"")}</label>
        <label class="m-field">教程图标 ${f("cc-tutorial-icon",u.soRefs,(e==null?void 0:e.tutorialIconGuid)??"")}</label>
      </div>
    </div>`,Se=`
    <div class="cc-section"><div class="cc-section-title">🍳 锅具参数</div>
      <div class="cc-grid">
        <label class="m-field">容量<input id="cc-capacity" type="number" min="0" value="${ee}" class="m-input"></label>
        <label class="m-field">允许站台
          <div class="cc-checks">${tt(g,Pe)}</div>
        </label>
        <label class="m-field">食材内容 UI ${f("cc-ingredient-ui",u.soRefs,(e==null?void 0:e.ingredientContentUiGuid)??"")}</label>
        <label class="m-field">Sludge 模型 ${f("cc-sludge",u.soRefs,(e==null?void 0:e.sludgeGuid)??"")}</label>
        <label class="m-field">生成特效 ${f("cc-spawn-fx",u.soRefs,(e==null?void 0:e.spawnEffectGuid)??"")}</label>
        <label class="m-field">额外食材
          <div class="cc-multi-row">${Y("cc-pick-ings","选择额外食材…")}<span id="cc-ings-count" class="muted small"></span></div>
        </label>
        <label class="m-field">锅内模型 prefab
          <div class="cc-multi-row">${Y("cc-pick-models","选择模型…")}<span id="cc-models-count" class="muted small"></span></div>
        </label>
        <label class="m-field">锅内模型 SO
          <div class="cc-multi-row">${Y("cc-pick-model-sos","选择模型 SO…")}<span id="cc-model-sos-count" class="muted small"></span></div>
        </label>
      </div>
    </div>`,Te=`
    <div class="cc-section"><div class="cc-section-title">🚰 水槽参数</div>
      <div class="cc-grid">
        <label class="m-field">清洗部件
          <select id="cc-washing-part" class="m-select cc-select"><option value="">— 不设置 —</option>${S.map(a=>`<option value="${s(a)}" ${a===((e==null?void 0:e.washingPartPath)??"")?"selected":""}>${s(a)}</option>`).join("")}</select>
        </label>
        <label class="m-field">沥干部件
          <select id="cc-drying-part" class="m-select cc-select"><option value="">— 不设置 —</option>${S.map(a=>`<option value="${s(a)}" ${a===((e==null?void 0:e.dryingPartPath)??"")?"selected":""}>${s(a)}</option>`).join("")}</select>
        </label>
        <label class="m-field">干净盘堆 ${f("cc-clean-stack",u.soRefs,(e==null?void 0:e.cleanPlateStackGuid)??"")}</label>
      </div>
    </div>`,Ce=`
    <div class="cc-section"><div class="cc-section-title">🍽️ 托盘参数</div>
      <div class="cc-grid"><label class="m-field">槽位配置 ${f("cc-tray-slot",u.orderLists,(e==null?void 0:e.traySlotGuid)??"")}</label></div>
    </div>`,Le=`
    <div class="cc-section"><div class="cc-section-title">🔀 分料机参数</div>
      <div class="cc-grid">
        <label class="m-field">容量<input id="cc-capacity" type="number" min="0" value="${ee}" class="m-input"></label>
        <label class="m-field">每次拆分<input id="cc-split-num" type="number" min="1" value="${ye}" class="m-input"></label>
        <label class="m-field">输入菜谱 ${f("cc-input-recipe",u.recipes,(e==null?void 0:e.inputRecipeGuid)??"")}</label>
        <label class="m-field">输出 prefab ${f("cc-output-prefab",u.prefabs,(e==null?void 0:e.outputPrefabGuid)??"")}</label>
        <label class="m-field">音效 tag
          <select id="cc-split-audio" class="m-select cc-select"><option value="">— 默认 PutDown —</option>${u.audioTags.map(a=>`<option value="${s(a)}" ${a===Ee?"selected":""}>${s(a)}</option>`).join("")}</select>
        </label>
      </div>
    </div>`;let T="";i==="ingredient"||i==="prep"?T=we:i==="counter"?T=X:i==="utensil"?T=Se+X:i==="sink"?T=Te+X:i==="tray"?T=Ce:i==="splitter"&&(T=Le),n.innerHTML=`
    <div class="cc-form-head">
      <div><h2>${m?"＋ 新建":"✏️ 编辑"} ${r.icon} ${s(r.zh)}</h2><p>${s(r.desc)}</p></div>
      <div class="cc-actions">
        ${j("💾 保存",{"data-save":""})}
        ${w("返回列表","default",{"data-back":""})}
      </div>
    </div>
    <div class="cc-section"><div class="cc-section-title">🎯 模板</div>
      ${et(d,N,m)}
      <p class="muted small">选择模板后会自动带入容量/站台/音效等默认值；所有引用字段均可从下拉列表选择。</p>
    </div>
    <div class="cc-section"><div class="cc-section-title">📝 基础信息</div>
      <div class="cc-grid">
        <label class="m-field">标识 id<input id="cc-id" value="${s((e==null?void 0:e.id)??"")}" ${m?"":"disabled"} placeholder="字母/数字/下划线" class="m-input"></label>
        <label class="m-field">中文名<input id="cc-zh" value="${s((e==null?void 0:e.nameZh)??"")}" class="m-input"></label>
        <label class="m-field">英文名<input id="cc-en" value="${s((e==null?void 0:e.nameEn)??"")}" class="m-input"></label>
      </div>
    </div>
    ${T}
    <div class="cc-section"><div class="cc-section-title">🖼️ 图标与模型</div>
      <div class="cc-grid">
        <label class="m-field">图标 PNG<input type="file" id="cc-icon" accept="image/png,image/jpeg" class="m-input"></label>
        <label class="m-field">模型 FBX/OBJ + 贴图<input type="file" id="cc-model" multiple accept=".fbx,.obj,.png,.jpg,.jpeg,.tga" class="m-input"></label>
        <label class="m-field">模型缩放<input type="number" id="cc-model-scale" step="0.01" value="1" class="m-input"></label>
        <label class="m-field">模型位置 X/Y/Z<input id="cc-model-pos" value="0,0,0" placeholder="0,0,0" class="m-input"></label>
        <label class="m-field">模型旋转 X/Y/Z<input id="cc-model-rot" value="0,0,0" placeholder="0,0,0" class="m-input"></label>
        <label class="m-field">模型 Pivot X/Y/Z<input id="cc-model-pivot" value="0,0,0" placeholder="0,0,0" class="m-input"></label>
        <label class="m-field">模型预览
          <div class="cc-multi-row">${x?w("👁 打开 3D 预览","default",{"data-preview":""}):'<span class="muted small">保存并上传模型后可预览</span>'}</div>
        </label>
      </div>
    </div>
    <pre id="cc-status" class="muted cc-status"></pre>`;const p=a=>{var o;return((o=document.getElementById(a))==null?void 0:o.value)??""},Z=a=>Number(p(a))||0,k=(a,o,y)=>{const E=document.getElementById(a);E&&(E.textContent=o>0?`${o} 个${y}`:"未选择")};k("cc-ings-count",R.length,"食材"),k("cc-models-count",_.length,"模型"),k("cc-model-sos-count",I.length,"模型 SO");const ke=async a=>{N=a;const o=d.find($=>$.id===a);O=(o==null?void 0:o.assetPath)||"",document.querySelectorAll(".cc-template-card").forEach($=>{$.classList.toggle("active",$.dataset.template===a)});const y=pe[a]??{},E=document.getElementById("cc-capacity");E&&(E.value=String(y.capacity??0));const v=document.getElementById("cc-split-num");v&&(v.value=String(y.splitNum??2));const P=document.getElementById("cc-split-audio");P&&y.splitAudioTag&&(P.value=y.splitAudioTag);const b=new Set(y.stationTypes??[]);document.querySelectorAll(".cc-station").forEach($=>{$.checked=b.has($.value)}),await Q(O);const h=document.getElementById("cc-washing-part"),L=document.getElementById("cc-drying-part");for(const $ of[h,L]){if(!$)continue;const G=$.value;$.innerHTML=`<option value="">— 不设置 —</option>${S.map(H=>`<option value="${s(H)}">${s(H)}</option>`).join("")}`,G&&S.includes(G)&&($.value=G)}};document.querySelectorAll(".cc-template-card").forEach(a=>{a.addEventListener("click",()=>void ke(a.dataset.template))}),(ae=document.getElementById("cc-pick-ings"))==null||ae.addEventListener("click",()=>{ze("锅内额外食材","不选 = 接受所有主线食材；选择后作为额外可煮食材",W,R,a=>{R=a,k("cc-ings-count",R.length,"食材")},[],{})}),(se=document.getElementById("cc-pick-models"))==null||se.addEventListener("click",()=>{fe("选择锅内模型 prefab",u.modelPrefabs,_,a=>{_=a,k("cc-models-count",_.length,"模型")})}),(ie=document.getElementById("cc-pick-model-sos"))==null||ie.addEventListener("click",()=>{fe("选择锅内模型 SO",u.modelSOs,I,a=>{I=a,k("cc-model-sos-count",I.length,"模型 SO")})}),n.querySelectorAll("[data-back]").forEach(a=>a.addEventListener("click",()=>location.assign(B(t,i)))),(le=n.querySelector("[data-preview]"))==null||le.addEventListener("click",()=>{!e||!x||de(()=>import("./dom-DdkAHNGl.js").then(a=>a.dx),__vite__mapDeps([0,1])).then(a=>a.openModelPreview({title:e.nameZh||e.id,resourceBase:`/api/custom-content/model-file?assetPath=${encodeURIComponent(e.assetPath)}&file=`,modelFileName:x.modelFile,fitTarget:"plate"}))});const C=document.getElementById("cc-icon");C==null||C.addEventListener("change",async()=>{var o;const a=(o=C.files)==null?void 0:o[0];J=a?await ge(a):void 0});const q=document.getElementById("cc-model");q==null||q.addEventListener("change",async()=>{U=[];const a=Array.from(q.files??[]);for(const v of a)U.push({fileName:v.name,base64:await ge(v)});const o=a.find(v=>/\.(fbx|obj)$/i.test(v.name));if(!o)return;const y=a.filter(v=>/\.(png|jpg|jpeg|tga)$/i.test(v.name)),E=await o.arrayBuffer();de(()=>import("./dom-DdkAHNGl.js").then(v=>v.dx),__vite__mapDeps([0,1])).then(v=>{var P;return v.openModelPreview({title:o.name,resourceBase:"",modelFileName:o.name,localBuffer:E,localTextures:y.length?y:void 0,fitTarget:"plate",scale:Number((P=document.getElementById("cc-model-scale"))==null?void 0:P.value)||1,onAdjust:b=>{((H,Ge)=>{const ne=document.getElementById(H);ne&&(ne.value=String(Math.round(Ge*1e3)/1e3))})("cc-model-scale",b.scale);const L=document.getElementById("cc-model-pos");L&&(L.value=`${b.positionX},${b.positionY},${b.positionZ}`);const $=document.getElementById("cc-model-rot");$&&($.value=`${b.rotationX},${b.rotationY},${b.rotationZ}`);const G=document.getElementById("cc-model-pivot");G&&(G.value=`${b.pivotX},${b.pivotY},${b.pivotZ}`)}})})}),n.querySelectorAll("[data-save]").forEach(a=>a.addEventListener("click",async()=>{var P,b;const o={setName:t,kind:i,id:p("cc-id"),assetPath:e==null?void 0:e.assetPath,nameZh:p("cc-zh"),nameEn:p("cc-en"),templateId:N,orderUid:Z("cc-order-uid"),platingStepId:p("cc-plating"),restrictionsGuid:p("cc-restrictions"),cosmeticsPrefabGuid:p("cc-cosmetics"),nextPrefabGuid:p("cc-next-prefab"),capacity:Z("cc-capacity"),allowedIngredientGuids:R,modelGuids:_,modelSoGuids:I,stationTypes:Array.from(document.querySelectorAll(".cc-station")).filter(h=>h.checked).map(h=>h.value),progressUiGuid:p("cc-progress-ui"),ingredientContentUiGuid:p("cc-ingredient-ui"),sludgeGuid:p("cc-sludge"),spawnEffectGuid:p("cc-spawn-fx"),fireEffectGuid:p("cc-fire-fx"),chopPfxGuid:p("cc-chop-pfx"),tutorialIconGuid:p("cc-tutorial-icon"),washingPartPath:p("cc-washing-part"),dryingPartPath:p("cc-drying-part"),cleanPlateStackGuid:p("cc-clean-stack"),traySlotGuid:p("cc-tray-slot"),inputRecipeGuid:p("cc-input-recipe"),outputPrefabGuid:p("cc-output-prefab"),splitNum:Z("cc-split-num"),splitAudioTag:p("cc-split-audio"),modelScale:Z("cc-model-scale"),modelPositionX:0,modelPositionY:0,modelPositionZ:0,modelRotationX:0,modelRotationY:0,modelRotationZ:0,modelPivotX:0,modelPivotY:0,modelPivotZ:0,iconBase64:J,iconFileName:(b=(P=C==null?void 0:C.files)==null?void 0:P[0])==null?void 0:b.name,modelFiles:U},y=p("cc-model-pos").split(",").map(h=>Number(h.trim())||0);o.modelPositionX=y[0]??0,o.modelPositionY=y[1]??0,o.modelPositionZ=y[2]??0;const E=p("cc-model-rot").split(",").map(h=>Number(h.trim())||0);o.modelRotationX=E[0]??0,o.modelRotationY=E[1]??0,o.modelRotationZ=E[2]??0;const v=p("cc-model-pivot").split(",").map(h=>Number(h.trim())||0);if(o.modelPivotX=v[0]??0,o.modelPivotY=v[1]??0,o.modelPivotZ=v[2]??0,!o.id&&m){document.getElementById("cc-status").textContent="请填写标识 id";return}Oe("保存中…");try{m?await Ue(o):await Xe(o),re(),A("已保存"),location.assign(B(t,i))}catch(h){re();const L=h.message||String(h);A(L,!1),document.getElementById("cc-status").textContent=L}}))}function fe(l,t,i,c){var n,d;const r=new Set(i),m=`<div class="cc-picker-list">${t.map(g=>`<label class="cc-picker-row"><input type="checkbox" data-cc-ref="${s(g.guid)}" ${r.has(g.guid)?"checked":""}/><span>${s(g.nameZh||g.id)} <span class="muted">${s(g.id)}</span></span></label>`).join("")||'<p class="muted">没有可选条目</p>'}</div>`;Ve(l,m,`${j("确定",{"data-cc-pick-ok":""})}${w("取消","default",{"data-cancel":""})}`),(n=document.querySelector("[data-cancel]"))==null||n.addEventListener("click",ue),(d=document.querySelector("[data-cc-pick-ok]"))==null||d.addEventListener("click",()=>{const g=Array.from(document.querySelectorAll("[data-cc-ref]")).filter(u=>u.checked).map(u=>u.dataset.ccRef);ue(),c(g)})}function ge(l){return new Promise((t,i)=>{const c=new FileReader;c.onload=()=>{const r=String(c.result??"");t(r.includes(",")?r.slice(r.indexOf(",")+1):r)},c.onerror=()=>i(c.error??new Error("读取文件失败")),c.readAsDataURL(l)})}export{st as renderCustomContentView};
