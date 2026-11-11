const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["assets/dom-Cx9-C33g.js","assets/dom-9-Thaz6b.css","assets/mtlTextureRename-vGs99CsZ.js","assets/fbxTextureRename-CZchYSrG.js"])))=>i.map(i=>d[i]);
import{$ as Rt,a1 as Mt,a2 as H,a3 as K,k as P,a4 as ke,a5 as gt,a6 as jt,a7 as Nt,q as yt,a8 as ht,a9 as bt,aa as Zt,ab as M,ac as $t,_ as z,ad as Be,ae as rt,af as qt,ag as ue,n as Dt,w as Xt,v as Y,ah as _t,ai as wt,aj as zt,ak as we,al as Ee,V as ge,am as ye,W as j,an as Ht,ao as Kt,ap as oe,aq as Yt,ar as Jt,as as Vt,at as Wt}from"./dom-Cx9-C33g.js";import{classifyTextureName as ut}from"./fbxTextureRename-CZchYSrG.js";function i(e){return String(e??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}const pt=/^[A-Za-z_][A-Za-z0-9_]*$/,Ce=[{id:"ingredient",icon:"🥕",zh:"自定义食材",en:"Custom ingredients",desc:"可加工 / 不可加工 / 备餐容器食材，含 OrderNode、图标与模型",prep:["PNG 图标一张（建议 128×128 透明底，用于订单与道具箱）。","3D 模型：FBX 或 OBJ + 贴图（PNG/JPG/TGA）。可加工食材需要「切割前 + 切割后」两套外观——切割后形态由系统自动生成默认版，也可另建食材后在「可加工下一形态」里指定。","模型会自动挂到食材上，切割进度条、内容 UI 等由模板默认提供，无需自备。"]},{id:"counter",icon:"🧱",zh:"自定义工作台",en:"Custom counters",desc:"普通 / 切菜 / 火锅灶台等工作台，含火焰、切菜 PFX、教程图标",prep:["3D 模型：FBX 或 OBJ + 贴图（会替换台面外观，占格与碰撞沿用模板）。","不需要图标；着火特效 / 切菜粒子 / 教程图标都有默认值，可后换。","切菜台模板（counter_chopping）请先想好配套的「切割后食材」，切割逻辑由食材侧决定。"]},{id:"utensil",icon:"🍳",zh:"自定义锅具",en:"Custom utensils",desc:"火锅、烤盘、煎盘、汤锅、蒸笼等；支持站台类型与锅内食材模型",prep:["3D 模型：FBX 或 OBJ + 贴图（替换锅具外观）。","想好允许放置的灶台类型（灶台 / 烤箱 / 篝火 / 烧烤架 / 火锅灶……），决定能放到哪种台上。","「锅内食材展示模型」可选：不选时沿用模板默认；选择后按「额外食材」顺序一一对应。"]},{id:"sink",icon:"🚰",zh:"自定义水槽",en:"Custom sinks",desc:"清洗/沥干部件与干净盘堆绑定",prep:["3D 模型：FBX 或 OBJ + 贴图（替换水槽外观，清洗/沥干分区沿用模板子物体）。","清洗部件 / 沥干部件通常保持默认（从模板子物体中选）；干净盘堆可用默认。"]},{id:"tray",icon:"🍽️",zh:"自定义托盘",en:"Custom trays",desc:"多槽上菜容器与槽位配方",prep:["3D 模型：FBX 或 OBJ + 贴图（替换托盘外观）。","槽位配置（每个槽接受哪些菜谱）沿用 common04 默认 TraySlot，一般无需准备。"]},{id:"prep",icon:"🥣",zh:"自定义备餐容器",en:"Preparation containers",desc:"OrderNode、容器限制与 cosmetics prefab",prep:["PNG 图标一张（建议 128×128 透明底）。","3D 模型：FBX 或 OBJ + 贴图。","「容器限制」决定能放进哪些食材，不设置 = 接受默认集合。"]},{id:"splitter",icon:"🔀",zh:"分料机",en:"Ingredient splitter",desc:"输入/输出、容量、每份拆分数量与音效",prep:["3D 模型：FBX 或 OBJ + 贴图（替换机身外观）。","必须先有一道「输入菜谱」（可在自定义菜谱里创建）与一个「输出 SO」（食材或菜谱成品）。","容量 / 每次拆分数量 / 音效都有默认值，可后调。"]}];function xe(e){return Ce.find(c=>c.id===e)??{id:e,icon:"🧩",zh:e,en:e,desc:"",prep:[]}}const pe={utensil_hotpot:{capacity:4,stationTypes:["Invalid_FloorBurner"]},utensil_roasting_tray:{capacity:4,stationTypes:["Oven"]},utensil_griddle_pan:{capacity:4,stationTypes:["FirePit"]},utensil_skewer:{capacity:3,stationTypes:["Barbeque"]},utensil_toasting_fork:{capacity:1,stationTypes:["FirePit"]},utensil_pot:{capacity:3,stationTypes:["Hob"]},utensil_steamer:{capacity:4,stationTypes:["Hob"]},splitter:{capacity:8,splitNum:2,splitAudioTag:"PutDown"}},Qt={CookingUI:"烹饪进度 UI",HoverProgressUI:"悬停进度条",IngredientsContentsUI:"食材内容面板",TutorialIconUI:"教程图标",UI_Move:"移动指示 UI",horde_order_ui:"人潮订单 UI",PFX_chop_01:"切菜粒子特效",PFX_Fire_Station:"火焰特效（着火）",PFX_FireStatic_Intro:"登场/生成特效",PFX_throw_01:"投掷粒子",Sludge:"焦糊物（无匹配菜谱）",Plate:"盘子",Glass:"玻璃杯",Mug:"马克杯",TraySlot:"托盘槽位配置（默认）",custom_counter:"工作台模板（普通）",custom_counter_chopping:"工作台模板（切菜）",custom_counter_sink:"工作台模板（水槽）",custom_ingredient_workable:"食材模板（可加工）",custom_ingredient_unworkable:"食材模板（不可加工）",custom_ingredient_preparation:"食材模板（备餐容器）",custom_utensil_plate:"盘子（模板）",custom_utensil_tray:"托盘（模板）",custom_utensil_clean_plate_stack:"干净盘堆",custom_utensil_dirty_plate:"单个脏盘",custom_utensil_dirty_plate_stack:"脏盘堆",custom_utensil_clean_tray_stack:"干净托盘堆",custom_utensil_dirty_tray:"单个脏托盘",custom_utensil_dirty_tray_stack:"脏托盘堆",web_cooking_region_floorburner:"火锅灶台",web_dlc10_cooking_region_floorburner:"火锅灶台（DLC10）",web_utensil_large_pot_01:"火锅大锅",web_utensil_large_pot_01_pushable:"火锅大锅（可推动）",web_utensil_dlc10_large_pot_01:"火锅大锅（DLC10）",web_dlc10_pushable_object:"可推动物件（DLC10）"};function Et(e){return Qt[e.id]??(e.nameZh&&e.nameZh!==e.id?e.nameZh:e.id)}function ea(e){const c=Et(e),n=e.nameEn&&e.nameEn!==e.id?e.nameEn:e.id;return c===n?c:`${c} · ${n}`}function Le(e){return e==="common"?'<span class="cc-tag cc-tag-common">常用</span>':'<span class="cc-tag cc-tag-adv">进阶·保持默认</span>'}const fe={ingredient_workable:"可以拿去切/加工的食材（如整番茄）。系统会自动生成切割后形态，也可稍后在「可加工下一形态」里换成你自己做的。",ingredient_unworkable:"直接使用的食材（如生菜叶），不能切割加工。",ingredient_preparation:"备餐容器型食材（如碗/盘子类），可限制能放入的食材。",counter:"普通工作台：可放置食材/锅具，不着火。",counter_chopping:"切菜工作台：玩家可在此切割食材。配套的「切割后形态」在食材侧设置。",counter_sink:"水槽工作台模板（一般请用「自定义水槽」分类创建）。",hotpot_burner:"火锅专用灶台（FloorBurner），与火锅大锅配套。",utensil_plate:"盘子（装盘容器），一般无需自定义。",utensil_tray:"托盘（多槽上菜容器）。",utensil_hotpot:"火锅大锅：允许站台自动设为火锅灶（FloorBurner），容量 4。",utensil_roasting_tray:"烤箱烤盘：放烤箱（Oven）上使用。",utensil_griddle_pan:"煎盘：放篝火/明火（FirePit）上使用。",utensil_skewer:"烤串架：烧烤架（Barbeque），容量 3。",utensil_toasting_fork:"烤面包叉：篝火（FirePit），容量 1。",utensil_pot:"汤锅：普通灶台（Hob），容量 3。",utensil_steamer:"蒸笼：普通灶台（Hob），容量 4。",sink:"标准水槽：清洗（左）+ 沥干（右）两段。",tray:"多槽托盘：每个槽可放置一道菜谱成品。",prep:"备餐容器：自带 OrderNode，可限制能放入的食材。",splitter:"分料机：投入输入菜谱成品，按份数拆分输出。",clean_plate_stack:"干净盘堆（水槽沥干侧返回物）。",dirty_plate:"单个脏盘。",dirty_plate_stack:"脏盘堆（返回脏盘配置用）。",clean_tray_stack:"干净托盘堆。",dirty_tray:"单个脏托盘。",dirty_tray_stack:"脏托盘堆。"},ta={ingredient_workable:["WorkableItem（可被切割/加工）","ThrowableItem / CarryableItem（可拿可扔）","切割进度条 + 内容 UI 默认","自动生成「切割后形态」与独立 OrderNode（uID/图标）"],ingredient_unworkable:["ThrowableItem / CarryableItem","IngredientPropertiesComponent + 独立 OrderNode","内容 UI 默认"],ingredient_preparation:["PreparationContainer（可放入食材）","独立 OrderNode + 默认容器限制","Cosmetics 外观默认"],counter:["可放置食材/锅具（AttachStation）","Flammable（长时间无人处理会着火）","进度条 / 火焰特效 / 教程图标默认"],counter_chopping:["工作台全部功能","Workstation：玩家可在此切割食材","切菜粒子特效默认"],counter_sink:["清洗（左）+ 沥干/返盘（右）两段分区","干净盘堆默认","进度条默认"],hotpot_burner:["FloorBurner 烹饪区（火锅大锅专属灶台）","占格 1×1，可与工作台组合排布"],utensil_hotpot:["容量 4，允许站台=火锅灶（FloorBurner）","内置 BoxCollider 锅体判定","内容 UI / Sludge / 生成特效默认"],utensil_roasting_tray:["容量 4，允许站台=烤箱（Oven）","内容 UI / Sludge / 生成特效默认"],utensil_griddle_pan:["容量 4，允许站台=篝火（FirePit）","内容 UI / Sludge / 生成特效默认"],utensil_skewer:["容量 3，允许站台=烧烤架（Barbeque）","内容 UI / Sludge / 生成特效默认"],utensil_toasting_fork:["容量 1，允许站台=篝火（FirePit）","内容 UI / Sludge / 生成特效默认"],utensil_pot:["容量 3，允许站台=普通灶台（Hob）","内容 UI / Sludge / 生成特效默认"],utensil_steamer:["容量 4，允许站台=普通灶台（Hob）","内容 UI / Sludge / 生成特效默认"],utensil_plate:["盘子（装盘容器）","可放置成品菜谱"],utensil_tray:["TrayPlacementContainer 多槽（默认 TraySlot 配置）","每个槽可放一道菜谱成品","Sludge 默认"],sink:["清洗（WashingStation）+ 沥干（PlateReturnStation）分区","干净盘堆默认","进度条默认"],tray:["多槽上菜容器（默认 TraySlot 配置）","可拿起/放置多道成品","Sludge 默认"],prep:["PreparationContainer（限制可放入食材）","独立 OrderNode + 图标","Cosmetics 外观默认"],splitter:["IngredientSplitter 组件（投入→拆分）","4 档填充模型（按存量显示）","进度条 UI 默认（CookingUI）"],clean_plate_stack:["水槽沥干侧的干净盘堆返回物"],dirty_plate:["单个脏盘（返回系统用）"],dirty_plate_stack:["脏盘堆（返回系统用）"],clean_tray_stack:["干净托盘堆（返回系统用）"],dirty_tray:["单个脏托盘（返回系统用）"],dirty_tray_stack:["脏托盘堆（返回系统用）"]};function aa(e){return e.id==="utensil_hotpot"?"2×2":e.id==="sink"||e.id==="counter_sink"?"2×1":e.kind==="utensil"||e.kind==="tray"?"0.9×0.9（手持/放置道具）":"1×1"}const ca={ingredient_workable:["切割链路一步完成：在「🔪 切片形态」区一并上传切片模型（+可选切片图标），保存时自动创建切片食材并关联——例：整番茄 → 切片番茄","不传切片模型 = 自动生成默认切割形态（模板外观）；之后也可编辑本条，在「可加工下一形态」里换成自制切片食材"],counter_chopping:["切菜行为由食材侧决定：场景里需存在「可加工食材」才能验证切割"],utensil_hotpot:["允许站台已自动设为火锅灶（FloorBurner）；场景需放置火锅灶台配套验证（模板 hotpot_burner 或原版火锅灶）"],utensil_roasting_tray:["允许站台已自动设为烤箱（Oven）"],utensil_griddle_pan:["允许站台已自动设为篝火（FirePit）"],utensil_skewer:["允许站台已自动设为烧烤架（Barbeque）"],utensil_toasting_fork:["允许站台已自动设为篝火（FirePit）"],utensil_pot:["允许站台已自动设为普通灶台（Hob）"],utensil_steamer:["允许站台已自动设为普通灶台（Hob）"],splitter:["前置：先有一道自定义菜谱（输入）与一个输出 SO（如自定义食材）——没有就先去对应页面创建"]},me="/test-assets";function sa(e){const c=e==="ingredient"||e==="prep"?"model_test_ingredient":"model_test_counter";return{icon:`${me}/icon_ingredient_128.png`,modelStem:c,modelFiles:[`${c}.obj`,`${c}.mtl`,`${c}_base_color.png`]}}async function de(e,c){const n=await fetch(e);if(!n.ok)throw new Error("下载测试素材失败："+e);return new File([await n.blob()],c,{type:n.headers.get("Content-Type")??"application/octet-stream"})}function he(e,c,n,s){return document.body.classList.add("manage-bg"),e.innerHTML=`
    ${Dt("custom-content")}
    <div class="manage-bar">
      <h1 class="m-title">${i(c)}</h1>
      <span class="cc-spacer"></span>
    </div>
    ${n?na(n,s):""}
    <div class="manage-content cc-content" id="cc-content"></div>
  `,Xt(),document.getElementById("cc-content")}function na(e,c){const n=(s,p,d)=>`<a class="cc-tab${c===s?" active":""}" href="${K(e,s||void 0)}">${d} ${i(p)}</a>`;return`<div class="cc-tabs-wrap"><nav class="cc-tabs">
    <a class="cc-tab${c?"":" active"}" href="${K(e)}">🏠 总览</a>
    ${Ce.map(s=>n(s.id,s.zh,s.icon)).join("")}
  </nav></div>`}function Fe(e){const c=document.getElementById("cc-content");c&&(c.innerHTML=`<p class="muted">${i(e)}</p>`),Y(e)}function ve(e){const c=e instanceof Error?e.message:String(e);Y(c,!1);const n=document.getElementById("cc-content");n&&(n.innerHTML=`<div class="m-block"><h3>出错 · Error</h3><p>${i(c)}</p></div>`)}function G(e,c){location.pathname!==e&&history.pushState(null,"",e),Te(c)}function ia(e){location.pathname!==e&&history.pushState(null,"",e)}let mt=!1;async function Ea(e){mt||(mt=!0,window.addEventListener("popstate",()=>void Te(e))),await Te(e)}async function Te(e){const c=Rt();if(c.page==="custom-content"){if(!c.setId){await la(e);return}if(!c.contentKind){await oa(e,c.setId);return}if(!c.contentId){await St(e,c.setId,c.contentKind);return}await va(e,c.setId,c.contentKind,c.contentId)}}async function la(e){ia("/custom-content");const c=he(e,"自定义内容管理 · Custom Content");Fe("加载关卡集…");let n=[];try{n=await Mt()}catch(s){ve(s);return}c.innerHTML=`
    <div class="m-section-title">选择关卡集 <span class="cc-en">Choose a level set</span></div>
    <p class="modal-hint">自定义内容按关卡集保存（Assets/LevelSets/&lt;set&gt;/custom_content）。模板来自 common04 / common02 / common03 / commonW1，模板本身只读、永不修改。</p>
    <div class="m-grid">${n.map(s=>`<div class="m-card cc-set-card">
          <div class="cc-set-icon">🧩</div>
          <h3>${i(s.levelSetNameZH||s.setName)} <span class="muted">(${i(s.levelSetName||s.setName)})</span></h3>
          <div class="m-meta">标识：${i(s.setName)} · 关卡数：${s.levelCount}<br>作者：${i(s.author||"—")}</div>
          <div class="m-actions">${H("进入自定义管理",{"data-open":i(s.setName)})}</div>
        </div>`).join("")||'<p class="muted">暂无关卡集</p>'}</div>`,c.querySelectorAll("[data-open]").forEach(s=>s.addEventListener("click",()=>G(K(s.dataset.open),e)))}async function oa(e,c){var s,p,d;const n=he(e,`自定义内容 · ${c}`,c);n.innerHTML=`
    <div class="cc-hero">
      <div><h2>🧩 自定义内容 <span class="cc-en">Custom content</span></h2>
        <p>选择类型进入管理。所有引用字段都用下拉/选择器选择，无需手输 GUID。</p></div>
      <div class="cc-hero-actions">
        ${P("🍽️ 自定义菜谱","default",{"data-nav-recipes":""})}
        ${P("🍔 汉堡工作台","default",{"data-nav-burger":""})}
        ${P("🥩 夹心工作台","default",{"data-nav-filling":""})}
      </div>
    </div>
    <p class="modal-hint">💡 命名规则：标识 id 仅限英文字母/数字/下划线，且在同一关卡集内全局唯一（跨类型也不能重名）。模板克隆自 common04 等素材库，创建后可以自由修改自己的副本。</p>
    <div class="m-grid cc-kind-grid">
      ${Ce.map(m=>`<div class="m-card cc-kind-card" data-kind="${m.id}">
          <div class="cc-kind-icon">${m.icon}</div>
          <h3>${i(m.zh)} <span class="muted">(${i(m.en)})</span></h3>
          <div class="m-meta">${i(m.desc)}</div>
          <div class="m-actions">${P("进入管理 · Manage","primary",{"data-kind":m.id})}</div>
        </div>`).join("")}
    </div>`,n.querySelectorAll("[data-kind]").forEach(m=>m.addEventListener("click",()=>G(K(c,m.dataset.kind),e))),(s=n.querySelector("[data-nav-recipes]"))==null||s.addEventListener("click",()=>G(Jt(c),e)),(p=n.querySelector("[data-nav-burger]"))==null||p.addEventListener("click",()=>G(Vt(c),e)),(d=n.querySelector("[data-nav-filling]"))==null||d.addEventListener("click",()=>G(Wt(c),e))}function da(e){return e||"—"}async function St(e,c,n){var U;const s=xe(n),p=he(e,`${s.icon} ${s.zh} · ${c}`,c,n);Fe("加载列表…");let d=[];try{d=await ke(c,n)}catch(a){ve(a);return}let m="";const u=()=>{const a=m.trim().toLowerCase();return a?d.filter(g=>[g.nameZh,g.nameEn,g.id,g.templateId].some(b=>(b??"").toLowerCase().includes(a))):d},_=()=>{const a=u(),g=document.getElementById("cc-item-grid");g.innerHTML=a.map(b=>{var I;return`<div class="m-card cc-item-card">
          <div class="cc-item-top">
            <img class="cc-item-icon" src="${i(bt(b))}" alt="" onerror="this.style.visibility='hidden'"/>
            <div>
              <h3>${i(b.nameZh||b.nameEn||b.id)}</h3>
              <div class="muted small">${i(b.nameEn&&b.nameEn!==b.id?b.nameEn+" · ":"")}${i(b.id)}</div>
            </div>
          </div>
          <div class="cc-card-foot">
            <span class="cc-chip-tag">📦 ${i(da(b.templateId))}</span>
            ${b.orderUid?`<span class="cc-chip-tag">uID ${b.orderUid}</span>`:""}
            ${b.hasModel?'<span class="cc-chip-tag ok">👁 有模型</span>':'<span class="cc-chip-tag dim">无模型</span>'}
          </div>
          <div class="m-meta"><span class="cc-path">${i(b.assetPath)}</span>
            ${(I=b.warnings)!=null&&I.length?`<div class="cc-warn">⚠ ${i(b.warnings.join("；"))}</div>`:""}</div>
          <div class="m-actions">
            ${P("编辑 · Edit","primary",{"data-edit":i(b.id)})}
            ${b.hasModel?P("👁","default",{"data-preview":i(b.id),title:"3D 预览"}):""}
            ${_t("删除",{"data-del":i(b.id)})}
          </div>
        </div>`}).join("")||`<div class="cc-empty"><div>${s.icon}</div><p>${m?"没有匹配的自定义内容":"还没有自定义内容 · Nothing here yet"}</p>
        ${m?"":H("📋 从模板创建（先看模板参考）",{"data-browse":""})}</div>`,A(g)};p.innerHTML=`
    <div class="cc-list-head">
      <div>
        <h2>${s.icon} ${i(s.zh)} <span class="muted">(${i(s.en)}) · ${d.length}</span></h2>
        <p>${i(s.desc)}</p>
      </div>
      <div class="cc-actions">
        <input type="search" id="cc-search" class="rl-search" placeholder="搜索名称 / id / 模板…">
        ${P("📋 模板参考","default",{"data-browse":""})}
        ${H("＋ 新建 "+s.zh,{"data-new":""})}
      </div>
    </div>
    <div class="m-grid cc-item-grid" id="cc-item-grid"></div>`,(U=document.getElementById("cc-search"))==null||U.addEventListener("input",a=>{m=a.target.value,_()});const v=()=>{ya(n,a=>{X=a,G(oe(c,n,"new"),e)})};p.querySelectorAll("[data-browse]").forEach(a=>a.addEventListener("click",v));const A=a=>{a.querySelectorAll("[data-new]").forEach(g=>g.addEventListener("click",()=>G(oe(c,n,"new"),e))),a.querySelectorAll("[data-edit]").forEach(g=>g.addEventListener("click",()=>G(oe(c,n,g.dataset.edit),e))),a.querySelectorAll("[data-del]").forEach(g=>g.addEventListener("click",()=>{const b=g.dataset.del,I=d.find(O=>O.id===b);ra(c,n,b,I,()=>void St(e,c,n))})),a.querySelectorAll("[data-preview]").forEach(g=>g.addEventListener("click",()=>{const b=d.find(I=>I.id===g.dataset.preview);b&&ua(b)}))};_(),p.querySelectorAll("[data-new]").forEach(a=>a.addEventListener("click",()=>G(oe(c,n,"new"),e)))}function ra(e,c,n,s,p){var m,u;const d=(s==null?void 0:s.nameZh)||(s==null?void 0:s.nameEn)||n;ge(`删除 · ${i(d)}`,`<p>将永久删除该自定义内容及其 <b>prefab / OrderNode / 图标 / 模型文件</b>，<b>不可恢复</b>。</p>
     <p class="modal-hint">⚠️ 若已有关卡放置了它，对应场景位置会失效（变回空位）；请先在编辑器里移除相关摆放再删除。</p>`,`${ye()}${_t("确认删除",{"data-ok":""})}`),(m=document.querySelector("[data-cancel]"))==null||m.addEventListener("click",j),(u=document.querySelector("[data-ok]"))==null||u.addEventListener("click",async()=>{Be("删除中…");try{await Yt(e,c,n),j(),Y("已删除"),(c==="ingredient"||c==="prep")&&kt(e),p()}catch(_){Y(_.message||String(_),!1)}finally{ue()}})}function Ie(e){return e!=null&&e.hasModel?{scale:e.modelScale??1,rotationX:e.modelRotationX??0,rotationY:e.modelRotationY??0,rotationZ:e.modelRotationZ??0,positionX:e.modelPositionX??0,positionY:e.modelPositionY??0,positionZ:e.modelPositionZ??0,pivotX:e.modelPivotX??0,pivotY:e.modelPivotY??0,pivotZ:e.modelPivotZ??0}:{scale:1,rotationX:0,rotationY:0,rotationZ:0,positionX:0,positionY:0,positionZ:0,pivotX:0,pivotY:0,pivotZ:0}}async function ua(e){await M("正在加载 3D 预览…",async()=>{const c=await ht(e.assetPath);if(!c.modelFile){Y("该条目没有已保存的模型文件。",!1);return}const n=c.files.filter(p=>/\.(png|jpg|jpeg|tga)$/i.test(p)).map(p=>$t(e.assetPath,p)),{openModelPreview:s}=await z(async()=>{const{openModelPreview:p}=await import("./dom-Cx9-C33g.js").then(d=>d.dG);return{openModelPreview:p}},__vite__mapDeps([0,1]));s({title:e.nameZh||e.id,resourceBase:`/api/custom-content/model-file?assetPath=${encodeURIComponent(e.assetPath)}&file=`,modelFileName:c.modelFile,remoteTextures:n.length?n:void 0,fitTarget:"plate",showReference:e.kind==="ingredient"||e.kind==="prep",...Ie(e)})})}function pa(e,c,n,s){const p=new Set,d=e.filter(m=>{if(!m)return!1;const u=s==="id"?m.id:m.guid;return!u||p.has(u)?!1:(p.add(u),!0)}).map(m=>{const u=s==="id"?m.id:m.guid;return`<option value="${i(u)}" ${u===c?"selected":""}>${i(ea(m))}</option>`}).join("");return`<option value="">${i(n)}</option>${d}`}function S(e){const c=e.emptyLabel??"— 不设置 —",n=e.valueField??"guid",s=e.badge?` ${Le(e.badge)}`:"",p=e.clearKey&&e.editable?`<button type="button" class="m-btn small default cc-clear-btn" data-clear-field="${i(e.clearKey)}" style="${e.current?"":"display:none"}">✖ 清除</button>`:"";return`<label class="m-field">${i(e.label)}${s}
    <div class="cc-field-row">
      <select id="${e.id}" class="m-select cc-select">${pa(e.refs,e.current,c,n)}</select>
      ${p}
    </div>
    <span class="muted small">${i(e.hint)}</span>
    <span class="cc-cleared-note muted small" data-cleared-note="${i(e.clearKey??"")}" style="display:none">已标记清除，保存后生效</span>
  </label>`}function ma(e,c,n){if(!n){const s=e.find(p=>p.id===c);return`<div class="cc-template-current">当前模板：<b>${i((s==null?void 0:s.labelZh)||c||"—")}</b> <span class="muted">(${i((s==null?void 0:s.labelEn)||"")} · ${i(c||"—")})</span>
      <div class="muted small">模板在创建后不可更换；如需其它模板请新建一条。</div>
      ${s?P("🔍 查看模板参考","default",{"data-tpl-view":i(s.id)}):""}
    </div>`}return`<div class="cc-template-grid">${e.map(s=>{const p=fe[s.id]?`<p class="muted small cc-template-tip">${i(fe[s.id])}</p>`:"";return`<button type="button" class="cc-template-card${s.id===c?" active":""}" data-template="${i(s.id)}">
        <b>${i(s.labelZh)} <span class="muted">· ${i(s.labelEn)}</span></b><small>${i(s.id)}</small>${p}
        <span class="cc-tpl-view" data-tpl-view="${i(s.id)}" title="查看模板详情（不选择）">🔍 详情</span>
      </button>`}).join("")}</div>`}function fa(e,c){const n=new Set(c);return(e.stationTypes??[]).map(s=>`<label class="cc-check" title="${i(s.labelEn)}"><input type="checkbox" class="cc-station" value="${i(s.name)}" ${n.has(s.name)?"checked":""}/> <span>${i(s.labelZh)}</span></label>`).join("")}function Se(e,c){return`<button type="button" class="m-btn default cc-multi-btn" id="${e}">${i(c)}</button>`}async function va(e,c,n,s){var Je,Ve,We,Qe,et,tt,at,ct,st,nt,it,lt,ot;const p=xe(n),d=s==="new",m=he(e,`${d?"新建":"编辑"}${p.zh} · ${c}`,c,n);Fe("加载模板与引用…");let u=[],_={stationTypes:[]},v={setName:c,prefabs:[],soRefs:[],modelPrefabs:[],modelSOs:[],orderLists:[],lookups:[],recipes:[],plating:[],audioTags:[]},A=[],U=[];try{[u,_,v,A,U]=await Promise.all([gt(n),jt(),Nt(c),d?Promise.resolve([]):ke(c,n),yt(c).catch(()=>[])])}catch(t){ve(t);return}const a=d?void 0:A.find(t=>t.id===s);if(!d&&!a){ve(`未找到「${s}」，可能已被删除。`);return}let g=(a==null?void 0:a.templateId)||X||((Je=u[0])==null?void 0:Je.id)||"";X&&!u.some(t=>t.id===X)&&(g=((Ve=u[0])==null?void 0:Ve.id)??"");const b=d&&!!X;X="";let I=(a==null?void 0:a.assetPath)||((We=u.find(t=>t.id===g))==null?void 0:We.assetPath)||"",O=(a==null?void 0:a.allowedIngredientGuids)??[],$=(a==null?void 0:a.modelGuids)??[],k=(a==null?void 0:a.modelSoGuids)??[],B=[],C=null,T=Ie(a);const q=new Set;let x=null,J=[],te,V,R=null,W=[],ae,F=Ie(void 0);const Ge=n==="ingredient"&&d;async function Ae(t){if(!t){B=[];return}try{B=(await wt(t)).paths??[]}catch{B=[]}}if(await Ae(I),a)try{const t=await ht(a.assetPath);t.modelFile&&(C=t)}catch{C=null}const ce=pe[g]??{},Ue=(a==null?void 0:a.capacity)??ce.capacity??0,Bt=(a==null?void 0:a.splitNum)??ce.splitNum??2,Lt=(a==null?void 0:a.splitAudioTag)??ce.splitAudioTag??"",Tt=(a==null?void 0:a.stationTypes)??ce.stationTypes??[],Oe=n==="prep",It=`
    <div class="cc-section"><div class="cc-section-title">🥕 食材参数 <span class="cc-en">Ingredient settings</span></div>
      <p class="modal-hint">✅ <b>普通食材这里全部保持默认即可</b>——模板已带全套默认。只有三种情况需要改：
        ① 食材要被切、且切完变成<b>指定</b>食材 → 填「可加工下一形态」（例：整番茄 → 切片番茄）；
        ② 上桌容器不是普通盘子（饮料→玻璃杯、汤→马克杯）→ 改「装盘容器」；
        ③ 备餐容器要限制能放哪些食材 → 设「容器限制」。
        其余字段是引擎内部 UI / 装饰引用，属进阶选项。</p>
      <div class="cc-grid">
        ${S({label:"装盘容器 · Plating",id:"cc-plating",refs:v.plating,current:(a==null?void 0:a.platingStepId)??"",valueField:"id",clearKey:"platingStepId",editable:!d,badge:"common",hint:"该食材作为订单出现时用的容器：盘子（默认）/ 玻璃杯（饮料类）/ 马克杯（汤类）。普通食材不用改。"})}
        ${S({label:"可加工下一形态 · Next form（切割后）",id:"cc-next-prefab",refs:v.prefabs,current:(a==null?void 0:a.nextPrefabGuid)??"",clearKey:"nextPrefabGuid",editable:!d,badge:"common",hint:"仅「可加工食材」模板生效：切完变成它（例：整番茄 → 切片番茄）。需先建好切割后食材，可先保存本条再回来指定；留空 = 自动生成的默认切割形态。"})}
        ${Oe?S({label:"容器限制 · Restrictions",id:"cc-restrictions",refs:v.lookups,current:(a==null?void 0:a.restrictionsGuid)??"",clearKey:"restrictionsGuid",editable:!d,badge:"common",hint:"备餐容器核心参数：限制能放进本容器的食材集合（如「只许放生菜/番茄」的沙拉碗）。不设置 = 模板默认集合。"}):""}
        <label class="m-field">OrderNode 唯一 uID ${Le("adv")}
          <input id="cc-order-uid" type="number" min="0" value="${(a==null?void 0:a.orderUid)??0}" class="cc-num">
          <span class="muted small">0 = 自动分配（推荐，保持 0 即可）。手填必须全工程唯一（与原版食材也不能重复），重复会被拒绝。</span>
        </label>
      </div>
      <details class="cc-advanced">
        <summary>高级选项 · Advanced（引擎内部引用，保持默认）</summary>
        <div class="cc-grid">
          ${Oe?"":S({label:"容器限制 · Restrictions",id:"cc-restrictions",refs:v.lookups,current:(a==null?void 0:a.restrictionsGuid)??"",clearKey:"restrictionsGuid",editable:!d,badge:"adv",hint:"仅备餐容器生效：限制能放入本容器的食材集合（OrderToPrefabLookup）。普通食材无效果，保持默认。"})}
          ${S({label:"Cosmetics 外观 · Cosmetics prefab",id:"cc-cosmetics",refs:v.prefabs,current:(a==null?void 0:a.cosmeticsPrefabGuid)??"",clearKey:"cosmeticsPrefabGuid",editable:!d,badge:"adv",hint:"食材/容器附带的外部装饰件（如碗沿装饰）。99% 情况保持默认。"})}
          ${S({label:"食材内容 UI · Content UI",id:"cc-ingredient-ui",refs:v.soRefs,current:(a==null?void 0:a.ingredientContentUiGuid)??"",clearKey:"ingredientContentUiGuid",editable:!d,badge:"adv",hint:"拿起食材时显示内容的小面板（引擎 UI）。保持默认。"})}
          ${S({label:"进度 UI · Progress UI",id:"cc-progress-ui",refs:v.soRefs,current:(a==null?void 0:a.progressUiGuid)??"",clearKey:"progressUiGuid",editable:!d,badge:"adv",hint:"切割/加工时的进度条（引擎 UI）。保持默认。"})}
        </div>
      </details>
    </div>`,be=`
    <div class="cc-section"><div class="cc-section-title">🎨 通用 UI / 特效 <span class="cc-en">Common UI & FX</span></div>
      <p class="modal-hint">✅ 模板已带全套默认（进度条 / 着火火焰 / 切菜粒子 / 教程图标）。<b>通常整个区块都不用打开</b>——只有想换特效风格时才需要。</p>
      <details class="cc-advanced">
        <summary>高级选项 · Advanced（4 项引擎引用，保持默认）</summary>
        <div class="cc-grid">
          ${S({label:"进度 UI · Progress UI",id:"cc-progress-ui",refs:v.soRefs,current:(a==null?void 0:a.progressUiGuid)??"",clearKey:"progressUiGuid",editable:!d,badge:"adv",hint:"进度条（清洗/烹饪/燃烧通用）。保持默认。"})}
          ${S({label:"火焰特效 · Fire FX",id:"cc-fire-fx",refs:v.soRefs,current:(a==null?void 0:a.fireEffectGuid)??"",clearKey:"fireEffectGuid",editable:!d,badge:"adv",hint:"工作台长时间无人处理后着火的火焰特效。保持默认。"})}
          ${S({label:"切菜粒子 · Chop PFX",id:"cc-chop-pfx",refs:v.soRefs,current:(a==null?void 0:a.chopPfxGuid)??"",clearKey:"chopPfxGuid",editable:!d,badge:"adv",hint:"切菜工作台的切割粒子特效。保持默认。"})}
          ${S({label:"教程图标 · Tutorial icon",id:"cc-tutorial-icon",refs:v.soRefs,current:(a==null?void 0:a.tutorialIconGuid)??"",clearKey:"tutorialIconGuid",editable:!d,badge:"adv",hint:"教学模式中悬停提示的图标。保持默认。"})}
        </div>
      </details>
    </div>`,Ct=`
    <div class="cc-section"><div class="cc-section-title">🍳 锅具参数 <span class="cc-en">Utensil settings</span></div>
      <div class="cc-grid">
        <label class="m-field">容量 · Capacity
          <input id="cc-capacity" type="number" min="0" value="${Ue}" class="cc-num">
          <span class="muted small">锅内能同时放的食材数（0 = 模板默认）。</span>
        </label>
        <label class="m-field">允许站台 · Allowed stations
          <div class="cc-checks">${fa(_,Tt)}</div>
          <span class="muted small">决定锅能放到哪种灶台上：火锅=FloorBurner、烤箱=Oven、篝火/煎盘=FirePit、烤串=Barbeque、普通灶=Hob。必须至少勾一个，否则真机无法放置。</span>
        </label>
        <label class="m-field">额外食材 · Extra ingredients（多选）
          <div class="cc-multi-row">${Se("cc-pick-ings","选择额外食材…")}<span id="cc-ings-count" class="muted small"></span></div>
          <span class="muted small">限制本锅能煮哪些食材。不选 = 接受所有主线食材。</span>
        </label>
        <label class="m-field">锅内模型 prefab（多选）
          <div class="cc-multi-row">${Se("cc-pick-models","选择模型 prefab…")}<span id="cc-models-count" class="muted small"></span></div>
          <span class="muted small">锅内食材的 3D 展示模型，顺序与「额外食材」一一对应；某个留空时用该食材默认模型。</span>
        </label>
        <label class="m-field">锅内模型 SO（多选）
          <div class="cc-multi-row">${Se("cc-pick-model-sos","选择模型 SO…")}<span id="cc-model-sos-count" class="muted small"></span></div>
          <span class="muted small">同上，但引用 PseudoPrefabSO（跨 bundle 模型）。与 prefab 二选一即可。</span>
        </label>
      </div>
      <details class="cc-advanced">
        <summary>高级选项 · Advanced（3 项引擎引用，保持默认）</summary>
        <div class="cc-grid">
          ${S({label:"食材内容 UI · Content UI",id:"cc-ingredient-ui",refs:v.soRefs,current:(a==null?void 0:a.ingredientContentUiGuid)??"",clearKey:"ingredientContentUiGuid",editable:!d,badge:"adv",hint:"锅内内容显示面板（引擎 UI）。保持默认。"})}
          ${S({label:"Sludge 焦糊模型 · Sludge",id:"cc-sludge",refs:v.soRefs,current:(a==null?void 0:a.sludgeGuid)??"",clearKey:"sludgeGuid",editable:!d,badge:"adv",hint:"锅里放了不匹配食材时显示的「糊」模型。保持默认。"})}
          ${S({label:"生成特效 · Spawn FX",id:"cc-spawn-fx",refs:v.soRefs,current:(a==null?void 0:a.spawnEffectGuid)??"",clearKey:"spawnEffectGuid",editable:!d,badge:"adv",hint:"锅具重置/生成时的特效。保持默认。"})}
        </div>
      </details>
    </div>`,xt=`
    <div class="cc-section"><div class="cc-section-title">🚰 水槽参数 <span class="cc-en">Sink settings</span></div>
      <div class="cc-grid">
        <label class="m-field">清洗部件 · Washing part
          <select id="cc-washing-part" class="m-select cc-select"><option value="">— 不设置 —</option>${B.map(t=>`<option value="${i(t)}" ${t===((a==null?void 0:a.washingPartPath)??"")?"selected":""}>${i(t)}</option>`).join("")}</select>
          <span class="muted small">水槽左侧「清洗」功能的子物体。通常保持模板默认。</span>
        </label>
        <label class="m-field">沥干部件 · Drying part
          <select id="cc-drying-part" class="m-select cc-select"><option value="">— 不设置 —</option>${B.map(t=>`<option value="${i(t)}" ${t===((a==null?void 0:a.dryingPartPath)??"")?"selected":""}>${i(t)}</option>`).join("")}</select>
          <span class="muted small">水槽右侧「沥干/返盘」功能的子物体。通常保持模板默认。</span>
        </label>
        ${S({label:"干净盘堆 · Clean plate stack",id:"cc-clean-stack",refs:v.soRefs,current:(a==null?void 0:a.cleanPlateStackGuid)??"",clearKey:"cleanPlateStackGuid",editable:!d,badge:"adv",hint:"沥干完成后弹出的干净盘子堆（引擎引用）。保持默认。"})}
      </div>
    </div>`,Ft=`
    <div class="cc-section"><div class="cc-section-title">🍽️ 托盘参数 <span class="cc-en">Tray settings</span></div>
      <div class="cc-grid">
        ${S({label:"槽位配置 · Slot config",id:"cc-tray-slot",refs:v.orderLists,current:(a==null?void 0:a.traySlotGuid)??"",clearKey:"traySlotGuid",editable:!d,hint:"每个槽接受哪些菜谱成品（OrderToPrefabListSO）。当前版本所有槽位共用此配置，一般保持默认 TraySlot。"})}
        ${S({label:"Sludge 焦糊模型 · Sludge",id:"cc-sludge",refs:v.soRefs,current:(a==null?void 0:a.sludgeGuid)??"",clearKey:"sludgeGuid",editable:!d,badge:"adv",hint:"托盘内无匹配菜谱时的「糊」模型（引擎引用）。保持默认。"})}
      </div>
    </div>`,Gt=`
    <div class="cc-section"><div class="cc-section-title">🔀 分料机参数 <span class="cc-en">Splitter settings</span></div>
      <p class="modal-hint">分料机把一份「输入菜谱成品」拆成多份「输出 SO」。例：输入=一份米饭菜谱，输出=米饭食材 ×2；输出为 optional 组装定义时会自动套用（如三明治底）。</p>
      <div class="cc-grid">
        <label class="m-field">容量 · Capacity
          <input id="cc-capacity" type="number" min="1" value="${Ue}" class="cc-num">
          <span class="muted small">机内最多容纳的份数（默认 8）。</span>
        </label>
        <label class="m-field">每次拆分 · Split count
          <input id="cc-split-num" type="number" min="1" value="${Bt}" class="cc-num">
          <span class="muted small">投入 1 份拆出的数量（默认 2）。</span>
        </label>
        ${S({label:"输入菜谱 · Input recipe（必填）",id:"cc-input-recipe",refs:v.recipes,current:(a==null?void 0:a.inputRecipeGuid)??"",clearKey:"inputRecipeGuid",editable:!d,hint:"玩家投入什么会被拆分。只列自定义菜谱；没有合适的菜谱请先去「自定义菜谱」创建。"})}
        ${S({label:"输出食材/菜谱 · Output SO（必填）",id:"cc-output-prefab",refs:v.soRefs,current:(a==null?void 0:a.outputPrefabGuid)??"",clearKey:"outputPrefabGuid",editable:!d,hint:"拆分后生成什么（PseudoPrefabSO；支持可选组装菜谱自动套用，如自定义食材、米饭或三明治底）。"})}
        <label class="m-field">音效 tag · Audio tag
          <select id="cc-split-audio" class="m-select cc-select"><option value="">— 默认 PutDown —</option>${v.audioTags.map(t=>`<option value="${i(t)}" ${t===Lt?"selected":""}>${i(t)}</option>`).join("")}</select>
          <span class="muted small">拆分完成时播放的音效。默认 PutDown。</span>
        </label>
      </div>
    </div>`;let N="";n==="ingredient"||n==="prep"?N=It:n==="counter"?N=be:n==="utensil"?N=Ct+be:n==="sink"?N=xt+be:n==="tray"?N=Ft:n==="splitter"&&(N=Gt);const Re=()=>{const t=T;return`缩放 ${zt(t.scale)} · 旋转 (${t.rotationX.toFixed(0)}, ${t.rotationY.toFixed(0)}, ${t.rotationZ.toFixed(0)})° · 位置 (${we(Ee(t.positionX))}, ${we(Ee(t.positionY))}, ${we(Ee(t.positionZ))}) cm`};m.innerHTML=`
    <div class="m-actions-row">
      ${P("← 返回列表","default",{"data-back":""})}
      <span class="muted">关卡集：<b>${i(c)}</b> · ${d?`新建 ${i(p.zh)}`:`编辑 ${i((a==null?void 0:a.nameZh)||a.id)}`}</span>
      <span style="flex:1"></span>
      ${P("💾 保存 · Save","primary",{"data-save":""})}
    </div>
    <div class="cc-form">
      <div class="cc-section cc-prep">
        <div class="cc-section-title">🧾 开始前请准备 <span class="cc-en">Before you start</span>
          <span style="flex:1"></span>
          ${P("🧪 一键填充测试素材","default",{"data-test-fill":""})}
        </div>
        <ul class="cc-prep-list" id="cc-prep-list"></ul>
        <p class="muted small" style="margin:8px 0 0">🧪 会自动填入：图标（PNG）+ 模型（OBJ/MTL/贴图，走完整改名与预览管线）+ 中文名示例——之后直接点「💾 保存」即可体验全流程；素材也可在 <span class="cc-path">Docs/test/assets/</span> 查看。</p>
      </div>
      <div class="cc-section"><div class="cc-section-title">🎯 模板 <span class="cc-en">Template</span></div>
        ${ma(u,g,d)}
        <p class="muted small">选择模板后自动带入容量/站台/音效等默认值；模板来自 common04 等素材库（只读），克隆副本完全归你。</p>
      </div>
      <div class="cc-section"><div class="cc-section-title">📝 基本信息 <span class="cc-en">Basics</span></div>
        <div class="cc-grid">
          <label class="m-field">标识 id（仅字母/数字/下划线，创建后不可改）
            <input id="cc-id" value="${i((a==null?void 0:a.id)??"")}" ${d?"":"disabled"} placeholder="MyItem" class="cc-text">
            <span class="muted small">用于目录与资产名；同一关卡集内全局唯一（跨类型也不能重名）。</span>
          </label>
          <label class="m-field">中文名（必填）<input id="cc-zh" value="${i((a==null?void 0:a.nameZh)??"")}" placeholder="我的食材" class="cc-text"></label>
          <label class="m-field">英文名 English name<input id="cc-en" value="${i((a==null?void 0:a.nameEn)??"")}" placeholder="My item" class="cc-text"></label>
        </div>
      </div>
      ${N}
      <div class="cc-section"><div class="cc-section-title">🖼️ 图标 <span class="cc-en">Icon</span></div>
        <div class="cc-grid">
          <label class="m-field">图标图片（PNG，建议 128×128 透明底）
            <input type="file" id="cc-icon" accept="image/png,image/jpeg" class="cc-text">
            <span class="muted small">食材/备餐容器的订单图标与道具箱盖图。JPG 会自动转换为 PNG。</span>
          </label>
          <label class="m-field">当前图标
            <div class="cc-icon-preview"><img id="cc-icon-img" src="${i(a?bt(a,a.updatedAt):V??"")}" alt="" onerror="this.style.display='none'"><span class="muted small" id="cc-icon-note">${a!=null&&a.iconUrl?"":"未设置（用模板默认）"}</span></div>
          </label>
        </div>
      </div>
      <div class="cc-section"><div class="cc-section-title">📦 模型（3D） <span class="cc-en">3D model</span></div>
        <div class="cc-model-block">
          <label class="m-field">上传模型与贴图（FBX / OBJ + .mtl + PNG/JPG/TGA 贴图，可多选）
            <input type="file" id="cc-model" multiple accept=".fbx,.obj,.mtl,.png,.jpg,.jpeg,.tga" class="cc-text">
            <span class="muted small">贴图按文件名自动分类（base_color/normal/roughness/metallic…，含关键词即可）；中文/空格文件名会自动改写为安全名，模型内引用同步修正。</span>
          </label>
          <div class="cc-model-state" id="cc-model-state"></div>
          <div class="cc-model-tools">
            <label class="m-field">在线预览与调整
              <div class="cc-multi-row" id="cc-preview-row">
                ${P("👁 预览并调整方向/大小","default",{"data-preview-local":"",style:x?"":"display:none"})}
                ${P("👁 预览已保存模型","default",{"data-preview-saved":"",style:!x&&C?"":"display:none"})}
                ${!x&&!C?'<span class="muted small">上传模型或保存后可预览</span>':""}
              </div>
              <span class="muted small">变换（缩放/旋转/位置）请在 3D 预览里用「✨自动适配 / ✅应用」调整，保存时写回。</span>
            </label>
          </div>
          <div class="muted small" id="cc-transform-summary">${Re()}</div>
        </div>
      </div>
      ${Ge?`
      <div class="cc-section" id="cc-sliced-section" style="${g==="ingredient_workable"?"":"display:none"}">
        <div class="cc-section-title">🔪 切片形态（切割后食材） <span class="cc-en">Sliced form</span> ${Le("common")}</div>
        <p class="modal-hint">可加工食材专属：在这里一并上传切片模型，保存时<b>自动创建「切割后食材」条目并自动关联</b>——整番茄 → 切片番茄一步完成（切片条目之后可单独编辑升级）。全部留空 = 系统自动生成默认切割形态（模板外观）。</p>
        <div class="cc-grid">
          <label class="m-field">切片中文名<input id="cc-sliced-zh" class="cc-text" placeholder="切片·我的食材"></label>
          <label class="m-field">切片 id（默认 = 主 id + _sliced）<input id="cc-sliced-id" class="cc-text" placeholder="my_item_sliced"></label>
          <label class="m-field">切片英文名<input id="cc-sliced-en" class="cc-text" placeholder="Sliced My item"></label>
          <label class="m-field">切片图标（可选）<input type="file" id="cc-sliced-icon" accept="image/png,image/jpeg" class="cc-text">
            <span class="muted small">默认沿用主食材图标。</span></label>
          <label class="m-field">切片模型（OBJ/FBX + .mtl + 贴图）<input type="file" id="cc-sliced-model" multiple accept=".fbx,.obj,.mtl,.png,.jpg,.jpeg,.tga" class="cc-text"></label>
        </div>
        <div class="cc-model-state" id="cc-sliced-state"></div>
        <div class="cc-multi-row" style="margin-top:8px">
          ${P("🧪 切片测试素材","default",{"data-sliced-test":""})}
          ${P("👁 预览切片模型","default",{"data-sliced-preview":"",style:"display:none"})}
          <span class="muted small" id="cc-sliced-icon-note"></span>
        </div>
      </div>`:""}
    </div>
    <div class="cc-form-footer">
      ${P("← 返回列表","default",{"data-back":""})}
      <span style="flex:1"></span>
      ${P("💾 保存 · Save","primary",{"data-save":""})}
    </div>
    <pre id="cc-status" class="muted cc-status"></pre>`;const h=t=>{var o;return((o=document.getElementById(t))==null?void 0:o.value)??""},$e=t=>Number(h(t))||0,E=(t,o=!0)=>{const l=document.getElementById("cc-status");l&&(l.textContent=t),Y(t,o)},D=(t,o,l)=>{const r=document.getElementById(t);r&&(r.textContent=o>0?`已选 ${o} 个${l}`:"未选择")};D("cc-ings-count",O.length,"食材"),D("cc-models-count",$.length,"模型"),D("cc-model-sos-count",k.length,"模型 SO");const Me=()=>{const t=document.getElementById("cc-prep-list");if(!t)return;const o=ca[g]??[];t.innerHTML=[...p.prep,...o].map(l=>`<li>${i(l)}</li>`).join("")};Me();const je=()=>{const t=u.find(r=>r.id===g),o=document.getElementById("cc-zh"),l=document.getElementById("cc-en");o&&(o.placeholder=t?`我的${t.labelZh}`:"我的食材"),l&&(l.placeholder=t?`My ${t.labelEn}`:"My item")};if(je(),b){const t=pe[g],o=[(t==null?void 0:t.capacity)!=null?`容量 ${t.capacity}`:"",(Qe=t==null?void 0:t.stationTypes)!=null&&Qe.length?`允许站台 ${t.stationTypes.join(" / ")}`:""].filter(Boolean).join(" · ");E(`已按模板「${((et=u.find(l=>l.id===g))==null?void 0:et.labelZh)??g}」进入${o?"，预填："+o:""}。可点「🧪 一键填充测试素材」直接体验，或按上方清单自备素材`)}const Ne=()=>{const t=document.getElementById("cc-model-state");if(!t)return;const o=[];if(x){o.push(`📥 待上传：${i(x.name)}${x.mtl?" + .mtl":""}`);for(const y of J)o.push(`🖼 ${y.cls} → ${y.diskName}`)}else C?o.push(`✅ 已保存模型：${i(C.modelFile)}${C.files.length>1?`（目录共 ${C.files.length} 个文件）`:""}`):o.push('<span class="muted">未上传模型（使用模板默认外观）</span>');t.innerHTML=o.map(y=>`<div>${y}</div>`).join("");const l=document.querySelector("[data-preview-local]"),r=document.querySelector("[data-preview-saved]");l&&(l.style.display=x?"":"none"),r&&(r.style.display=!x&&C?"":"none")};Ne();const At=()=>{const t=document.getElementById("cc-transform-summary");t&&(t.textContent=Re())},Ze=async t=>{g=t;const o=u.find(L=>L.id===t);I=(o==null?void 0:o.assetPath)||"",document.querySelectorAll(".cc-template-card").forEach(L=>{L.classList.toggle("active",L.dataset.template===t)});const l=pe[t]??{},r=document.getElementById("cc-capacity");r&&(r.value=String(l.capacity??0));const y=document.getElementById("cc-split-num");y&&(y.value=String(l.splitNum??2));const f=document.getElementById("cc-split-audio");f&&l.splitAudioTag&&(f.value=l.splitAudioTag);const w=new Set(l.stationTypes??[]);document.querySelectorAll(".cc-station").forEach(L=>{L.checked=w.has(L.value)}),await Ae(I);const Z=document.getElementById("cc-washing-part"),Q=document.getElementById("cc-drying-part");for(const L of[Z,Q]){if(!L)continue;const ee=L.value;L.innerHTML=`<option value="">— 不设置 —</option>${B.map(dt=>`<option value="${i(dt)}">${i(dt)}</option>`).join("")}`,ee&&B.includes(ee)&&(L.value=ee)}Me(),je();const _e=document.getElementById("cc-sliced-section");_e&&(_e.style.display=t==="ingredient_workable"?"":"none")};document.querySelectorAll(".cc-template-card").forEach(t=>{t.addEventListener("click",()=>void Ze(t.dataset.template))}),m.querySelectorAll("[data-tpl-view]").forEach(t=>{t.addEventListener("click",o=>{o.stopPropagation();const l=u.find(r=>r.id===t.dataset.tplView);l&&Pt(l,d?()=>void Ze(l.id):void 0)})}),(()=>{m.querySelectorAll("[data-clear-field]").forEach(t=>{t.addEventListener("click",()=>{var y;const o=t.dataset.clearField,l=(y=t.parentElement)==null?void 0:y.querySelector("select"),r=m.querySelector(`[data-cleared-note="${CSS.escape(o)}"]`);q.has(o)?(q.delete(o),l&&(l.disabled=!1),r&&(r.style.display="none"),t.textContent="✖ 清除",t.classList.remove("danger")):(q.add(o),l&&(l.value="",l.disabled=!0),r&&(r.style.display=""),t.textContent="↩ 撤销清除",t.classList.add("danger"))})})})(),(tt=document.getElementById("cc-pick-ings"))==null||tt.addEventListener("click",()=>{Zt("锅内额外食材","不选 = 接受所有主线食材；选择后仅这些食材可入锅（自定义食材也在列表中）。",U,O,t=>{O=t,D("cc-ings-count",O.length,"食材")},[],{})}),(at=document.getElementById("cc-pick-models"))==null||at.addEventListener("click",()=>{ft("选择锅内模型 prefab · Model prefabs","顺序与「额外食材」一一对应；选中项可拖动排序。",v.modelPrefabs,$,t=>{$=t,D("cc-models-count",$.length,"模型")},!0)}),(ct=document.getElementById("cc-pick-model-sos"))==null||ct.addEventListener("click",()=>{ft("选择锅内模型 SO · Model SOs","PseudoPrefabSO 形式的模型引用（跨 bundle）。",v.modelSOs,k,t=>{k=t,D("cc-model-sos-count",k.length,"模型 SO")},!0)}),m.querySelectorAll("[data-back]").forEach(t=>t.addEventListener("click",()=>G(K(c,n),e)));const se=document.getElementById("cc-icon"),qe=async(t,o="")=>{const{base64:l,width:r,height:y}=await Pe(t);te=l,V&&URL.revokeObjectURL(V),V=URL.createObjectURL(t);const f=document.getElementById("cc-icon-img");f&&(f.src=V,f.style.display="");const w=document.getElementById("cc-icon-note");w&&(w.textContent=`${r}×${y}${/\.jpe?g$/i.test(t.name)?"（已转 PNG）":o}`)};se==null||se.addEventListener("change",async()=>{var o;const t=(o=se.files)==null?void 0:o[0];if(!t){te=void 0;return}try{await qe(t)}catch(l){E(`图标读取失败：${l.message}`,!1)}});const De=t=>{if(t.length===0)return;const o=t.find(l=>/\.(fbx|obj)$/i.test(l.name));if(!o){E("请至少包含一个 .fbx 或 .obj 模型文件。",!1);return}M("正在读取模型…",async()=>{const l=t.find(f=>/\.mtl$/i.test(f.name));x={name:o.name,bytes:new Uint8Array(await o.arrayBuffer()),mtl:l?new TextDecoder("utf-8").decode(await l.arrayBuffer()):null},J=[];const r=(d?h("cc-id")||"model":a.id).replace(/[^A-Za-z0-9_]/g,""),y=new Set;for(const f of t){if(f===o||f===l||!/\.(png|jpg|jpeg|tga)$/i.test(f.name))continue;const w=ut(f.name);if(y.has(w)){E(`贴图「${f.name}」与其它 ${w} 贴图重复，仅保留第一个。`,!1);continue}y.add(w);const Z="."+(f.name.split(".").pop()??"png").toLowerCase();J.push({cls:w,diskName:`${r}_${w}${Z}`,bytes:new Uint8Array(await f.arrayBuffer()),file:f})}Ne(),ze()})},ne=document.getElementById("cc-model");ne==null||ne.addEventListener("change",()=>De(Array.from(ne.files??[]))),(st=m.querySelector("[data-test-fill]"))==null||st.addEventListener("click",()=>{M("正在载入测试素材…",async()=>{try{const t=sa(n),o=await Promise.all(t.modelFiles.map(l=>de(`${me}/${l}`,l)));if(De(o),await qe(await de(t.icon,"icon_ingredient_128.png"),"（测试素材）"),d){const l=u.find(w=>w.id===g),r=document.getElementById("cc-zh"),y=document.getElementById("cc-en"),f=document.getElementById("cc-id");r&&!r.value.trim()&&(r.value=`测试${(l==null?void 0:l.labelZh)??p.zh}`),y&&!y.value.trim()&&(y.value=`Test${((l==null?void 0:l.labelEn)??p.en).replace(/\s+/g,"")}`),f&&!f.value.trim()&&(f.value=`test_${((l==null?void 0:l.id)??n).replace(/[^a-z0-9_]/gi,"")}`)}E("🧪 测试素材已填入（图标 + 模型 + 名称示例）：在 3D 预览里 ✨自动适配 后点「💾 保存」即可跑通全流程")}catch(t){E(t.message,!1)}})});const Xe=t=>{T=t,At()},ze=()=>{const t=x;t&&M("正在加载 3D 预览…",async()=>{const{openModelPreview:o}=await z(async()=>{const{openModelPreview:r}=await import("./dom-Cx9-C33g.js").then(y=>y.dG);return{openModelPreview:r}},__vite__mapDeps([0,1])),l=J.map(r=>r.file);o({title:t.name,resourceBase:"",modelFileName:t.name,localBuffer:t.bytes.buffer.slice(t.bytes.byteOffset,t.bytes.byteOffset+t.bytes.byteLength),localMtl:t.mtl??void 0,localTextures:l.length?l:void 0,fitTarget:"plate",showReference:n==="ingredient"||n==="prep",...T,onAdjust:Xe})})};(nt=document.querySelector("[data-preview-local]"))==null||nt.addEventListener("click",ze),(it=document.querySelector("[data-preview-saved]"))==null||it.addEventListener("click",()=>{!a||!C||M("正在加载 3D 预览…",async()=>{const t=C.files.filter(l=>/\.(png|jpg|jpeg|tga)$/i.test(l)).map(l=>$t(a.assetPath,l)),{openModelPreview:o}=await z(async()=>{const{openModelPreview:l}=await import("./dom-Cx9-C33g.js").then(r=>r.dG);return{openModelPreview:l}},__vite__mapDeps([0,1]));o({title:a.nameZh||a.id,resourceBase:`/api/custom-content/model-file?assetPath=${encodeURIComponent(a.assetPath)}&file=`,modelFileName:C.modelFile,remoteTextures:t.length?t:void 0,fitTarget:"plate",showReference:n==="ingredient"||n==="prep",...T,onAdjust:Xe})})});const He=()=>{const t=document.getElementById("cc-sliced-state");if(!t)return;const o=[];if(R){o.push(`📥 待上传切片模型：${i(R.name)}${R.mtl?" + .mtl":""}`);for(const r of W)o.push(`🖼 ${r.cls} → ${r.diskName}`)}else o.push('<span class="muted">未上传切片模型（保存后 = 自动默认切割形态）</span>');t.innerHTML=o.map(r=>`<div>${r}</div>`).join("");const l=document.querySelector("[data-sliced-preview]");l&&(l.style.display=R?"":"none")};He();const Ke=t=>{if(t.length===0)return;const o=t.find(l=>/\.(fbx|obj)$/i.test(l.name));if(!o){E("切片请至少包含一个 .fbx 或 .obj 模型文件。",!1);return}M("正在读取切片模型…",async()=>{const l=t.find(f=>/\.mtl$/i.test(f.name));R={name:o.name,bytes:new Uint8Array(await o.arrayBuffer()),mtl:l?new TextDecoder("utf-8").decode(await l.arrayBuffer()):null},W=[];const r=(h("cc-sliced-id").trim()||h("cc-id").trim()||"model").replace(/[^A-Za-z0-9_]/g,""),y=new Set;for(const f of t){if(f===o||f===l||!/\.(png|jpg|jpeg|tga)$/i.test(f.name))continue;const w=ut(f.name);if(y.has(w))continue;y.add(w);const Z="."+(f.name.split(".").pop()??"png").toLowerCase();W.push({cls:w,diskName:`${r}_sliced_${w}${Z}`,bytes:new Uint8Array(await f.arrayBuffer()),file:f})}He(),Ye()})},ie=document.getElementById("cc-sliced-model");ie==null||ie.addEventListener("change",()=>Ke(Array.from(ie.files??[])));const le=document.getElementById("cc-sliced-icon");le==null||le.addEventListener("change",async()=>{var o;const t=(o=le.files)==null?void 0:o[0];if(!t){ae=void 0;return}try{const{base64:l}=await Pe(t);ae=l;const r=document.getElementById("cc-sliced-icon-note");r&&(r.textContent=`✓ 切片图标已选（${t.name}）`)}catch(l){E(`切片图标读取失败：${l.message}`,!1)}});const Ye=()=>{const t=R;t&&M("正在加载切片 3D 预览…",async()=>{const{openModelPreview:o}=await z(async()=>{const{openModelPreview:r}=await import("./dom-Cx9-C33g.js").then(y=>y.dG);return{openModelPreview:r}},__vite__mapDeps([0,1])),l=W.map(r=>r.file);o({title:`切片 · ${t.name}`,resourceBase:"",modelFileName:t.name,localBuffer:t.bytes.buffer.slice(t.bytes.byteOffset,t.bytes.byteOffset+t.bytes.byteLength),localMtl:t.mtl??void 0,localTextures:l.length?l:void 0,fitTarget:"plate",showReference:!0,...F,onAdjust:r=>{F=r}})})};(lt=document.querySelector("[data-sliced-preview]"))==null||lt.addEventListener("click",Ye),(ot=document.querySelector("[data-sliced-test]"))==null||ot.addEventListener("click",()=>{M("正在载入切片测试素材…",async()=>{try{const t=await Promise.all(["model_test_sliced.obj","model_test_sliced.mtl","model_test_sliced_base_color.png"].map(o=>de(`${me}/${o}`,o)));Ke(t),ae=await(async()=>{const o=await de(`${me}/icon_sliced_128.png`,"icon_sliced_128.png"),{base64:l}=await Pe(o),r=document.getElementById("cc-sliced-icon-note");return r&&(r.textContent="✓ 切片图标已选（测试素材）"),l})()}catch(t){E(t.message,!1)}})});const Ut=()=>{const t=h("cc-id").trim(),o=h("cc-zh").trim(),l=document.getElementById("cc-sliced-id"),r=document.getElementById("cc-sliced-zh"),y=document.getElementById("cc-sliced-en");l&&(l.placeholder=`${t||"my_item"}_sliced`),r&&(r.placeholder=`切片·${o||"我的食材"}`),y&&(y.placeholder=`Sliced ${h("cc-en").trim()||"My item"}`)};["cc-id","cc-zh","cc-en"].forEach(t=>{var o;(o=document.getElementById(t))==null||o.addEventListener("input",Ut)}),m.querySelectorAll("[data-save]").forEach(t=>t.addEventListener("click",()=>void Ot()));const Ot=async()=>{const t=d?h("cc-id").trim():a.id;if(d){if(!t)return E("请填写标识 id。",!1);if(!pt.test(t))return E("标识 id 仅允许英文字母/数字/下划线，且不能以数字开头。",!1)}const o=h("cc-zh").trim();if(!o)return E("请填写中文名。",!1);if(n==="splitter"){if(!h("cc-input-recipe")&&!q.has("inputRecipeGuid"))return E("分料机必须选择「输入菜谱」。没有合适的菜谱请先到「自定义菜谱」创建。",!1);if(!h("cc-output-prefab")&&!q.has("outputPrefabGuid"))return E("分料机必须选择「输出食材/菜谱 SO」（如自定义食材）。",!1)}if(n==="utensil"&&Array.from(document.querySelectorAll(".cc-station")).filter(w=>w.checked).length===0)return E("锅具至少勾选一个「允许站台」，否则真机无法放置到任何灶台。",!1);const l=$e("cc-order-uid");if(l<0||!Number.isInteger(l))return E("OrderNode uID 必须是非负整数（0 = 自动分配）。",!1);const r={setName:c,kind:n,id:t,assetPath:a==null?void 0:a.assetPath,nameZh:o,nameEn:h("cc-en").trim(),templateId:g,orderUid:l,platingStepId:h("cc-plating"),restrictionsGuid:h("cc-restrictions"),cosmeticsPrefabGuid:h("cc-cosmetics"),nextPrefabGuid:h("cc-next-prefab"),capacity:$e("cc-capacity"),allowedIngredientGuids:O,modelGuids:$,modelSoGuids:k,stationTypes:Array.from(document.querySelectorAll(".cc-station")).filter(f=>f.checked).map(f=>f.value),progressUiGuid:h("cc-progress-ui"),ingredientContentUiGuid:h("cc-ingredient-ui"),sludgeGuid:h("cc-sludge"),spawnEffectGuid:h("cc-spawn-fx"),fireEffectGuid:h("cc-fire-fx"),chopPfxGuid:h("cc-chop-pfx"),tutorialIconGuid:h("cc-tutorial-icon"),washingPartPath:h("cc-washing-part"),dryingPartPath:h("cc-drying-part"),cleanPlateStackGuid:h("cc-clean-stack"),traySlotGuid:h("cc-tray-slot"),inputRecipeGuid:h("cc-input-recipe"),outputPrefabGuid:h("cc-output-prefab"),splitNum:$e("cc-split-num"),splitAudioTag:h("cc-split-audio"),modelScale:T.scale,modelPositionX:T.positionX,modelPositionY:T.positionY,modelPositionZ:T.positionZ,modelRotationX:T.rotationX,modelRotationY:T.rotationY,modelRotationZ:T.rotationZ,modelPivotX:T.pivotX,modelPivotY:T.pivotY,modelPivotZ:T.pivotZ,iconBase64:te,clearFields:d?void 0:Array.from(q)};if(x)try{r.modelFiles=await vt(t,x,J)}catch(f){return E(`模型处理失败：${f.message}`,!1)}let y="";if(Ge&&R){if(g!=="ingredient_workable")return E("切片模型仅在「可加工食材」模板下生效；请清空切片模型或改用可加工模板。",!1);const f=h("cc-sliced-id").trim()||`${t}_sliced`;if(!pt.test(f))return E(`切片 id「${f}」非法（仅英文字母/数字/下划线，且不能以数字开头）。`,!1);const w=h("cc-sliced-zh").trim()||`切片·${o}`,Z=h("cc-sliced-en").trim()||`Sliced ${h("cc-en").trim()||t}`;Be("正在创建切片食材…");try{const Q=await vt(f,R,W);await rt({setName:c,kind:"ingredient",id:f,nameZh:w,nameEn:Z,templateId:"ingredient_unworkable",orderUid:0,iconBase64:ae??te,modelFiles:Q,modelScale:F.scale,modelPositionX:F.positionX,modelPositionY:F.positionY,modelPositionZ:F.positionZ,modelRotationX:F.rotationX,modelRotationY:F.rotationY,modelRotationZ:F.rotationZ,modelPivotX:F.pivotX,modelPivotY:F.pivotY,modelPivotZ:F.pivotZ}),y=f;const L=(await ke(c,"ingredient")).find(ee=>ee.id===f);if(!L)throw new Error("切片食材创建成功但读取失败（可稍后在「可加工下一形态」里手动关联）");r.nextPrefabGuid=L.guid}catch(Q){return ue(),E(`切片食材创建失败：${Q.message}`,!1)}}Be("保存中…");try{d?await rt(r):await qt(r),ue(),E(`已保存 ${o}${r.modelFiles?"（含模型/贴图）":""}${y?`，并自动创建切片食材「${y}」`:""}`),(n==="ingredient"||n==="prep")&&kt(c),G(K(c,n),e)}catch(f){ue();const w=f.message||String(f);E(y?`${w}（切片条目「${y}」已创建：可删除后重试，或编辑本条在「可加工下一形态」手动关联）`:w,!1)}}}function ft(e,c,n,s,p,d=!1){var g,b,I,O;const m=new Set(s);let u="";const _=()=>[...s.filter($=>m.has($)),...Array.from(m).filter($=>!s.includes($))],v=()=>{const $=u.trim().toLowerCase();return $?n.filter(k=>[k.nameZh,k.nameEn,k.id].some(B=>(B??"").toLowerCase().includes($))):n},A=()=>v().map($=>{const k=m.has($.guid),B=k&&d?_().indexOf($.guid)+1:0;return`<label class="cc-picker-row${k?" picked":""}">
        <input type="checkbox" data-cc-ref="${i($.guid)}" ${k?"checked":""}/>
        <span class="cc-picker-name">${i(Et($))}</span>
        <span class="cc-picker-en muted">${i($.nameEn&&$.nameEn!==$.id?$.nameEn:$.id)}</span>
        ${B>0?`<span class="cc-picker-order">#${B}</span>`:""}
      </label>`}).join("")||'<p class="muted">没有可选条目</p>';ge(e,`<p class="modal-hint">${i(c)}</p>
     <input type="search" id="cc-picker-search" class="rl-search" placeholder="搜索名称 / id…" style="width:100%;margin-bottom:8px">
     <div class="cc-picker-list" id="cc-picker-list">${A()}</div>`,`${ye()}${P("清空已选","default",{"data-cc-clear":""})}${H("确定",{"data-cc-pick-ok":""})}`);const U=()=>{const $=document.getElementById("cc-picker-list");$&&($.innerHTML=A()),a()},a=()=>{document.querySelectorAll("[data-cc-ref]").forEach($=>{$.addEventListener("change",()=>{const k=$.dataset.ccRef;$.checked?m.add(k):m.delete(k);const B=$.closest(".cc-picker-row");B&&B.classList.toggle("picked",$.checked)})})};a(),(g=document.getElementById("cc-picker-search"))==null||g.addEventListener("input",$=>{u=$.target.value,U()}),(b=document.querySelector("[data-cancel]"))==null||b.addEventListener("click",j),(I=document.querySelector("[data-cc-clear]"))==null||I.addEventListener("click",()=>{m.clear(),U()}),(O=document.querySelector("[data-cc-pick-ok]"))==null||O.addEventListener("click",()=>{j(),p(_())})}let X="";function ga(e){return e.length?e.map(c=>{const n=c.split("/").length-1,s=c.split("/").pop()??c;return`<div class="cc-tree-row" style="padding-left:${6+n*14}px" title="${i(c)}">${n===0?"📦":"·"} ${i(s)}</div>`}).join(""):'<p class="muted small">（模板无子物体）</p>'}function Pt(e,c){var d,m,u;const n=ta[e.id]??[],s=pe[e.id],p=s?[s.capacity!=null?`容量 ${s.capacity}`:"",(d=s.stationTypes)!=null&&d.length?`允许站台 ${s.stationTypes.join(" / ")}`:"",s.splitNum!=null?`每次拆分 ${s.splitNum}`:"",s.splitAudioTag?`音效 ${s.splitAudioTag}`:""].filter(Boolean).join(" · "):"";ge(`${i(e.labelZh)} <span class="muted">· ${i(e.labelEn)}</span>`,`
    <div class="cc-tpl-detail">
      <p>${i(fe[e.id]??"")}</p>
      ${n.length?`<div class="cc-tpl-meta"><span class="cc-tpl-meta-k">模板自带</span><ul>${n.map(_=>`<li>${i(_)}</li>`).join("")}</ul></div>`:""}
      <div class="cc-tpl-meta"><span class="cc-tpl-meta-k">占格</span><span>${i(aa(e))}</span></div>
      ${p?`<div class="cc-tpl-meta"><span class="cc-tpl-meta-k">预设默认</span><span>${i(p)}</span></div>`:""}
      <div class="cc-tpl-meta"><span class="cc-tpl-meta-k">来源</span><span class="cc-path">${i(e.assetPath)}（模板库只读，克隆副本归你）</span></div>
      <div class="cc-tpl-meta"><span class="cc-tpl-meta-k">子物体结构</span>
        <div class="cc-tree" id="cc-tpl-hierarchy"><span class="muted small">加载中…</span></div>
      </div>
    </div>`,`${ye()}${c?H("用此模板创建 →",{"data-tpl-use":""}):""}`),(m=document.querySelector("[data-cancel]"))==null||m.addEventListener("click",j),(u=document.querySelector("[data-tpl-use]"))==null||u.addEventListener("click",()=>{j(),c==null||c()}),wt(e.assetPath).then(_=>{const v=document.getElementById("cc-tpl-hierarchy");v&&(v.innerHTML=ga(_.paths??[]))}).catch(()=>{const _=document.getElementById("cc-tpl-hierarchy");_&&(_.innerHTML='<span class="muted small">（结构加载失败）</span>')})}function ya(e,c){var p;const n=xe(e),s=`<p class="modal-hint">以下是「${i(n.zh)}」可用的标准模板（来自 common04 等素材库，只读）。
    点击卡片右下角「🔍 详情」查看模板自带内容 / 占格 / 子物体结构，再决定用哪个创建。</p>
    <div class="modal-scroll"><div class="cc-template-grid" id="cc-tpl-browser-grid"><p class="muted">加载模板…</p></div></div>`;ge(`📋 模板参考 · ${i(n.zh)} <span class="muted">${i(n.en)}</span>`,s,ye()),(p=document.querySelector("[data-cancel]"))==null||p.addEventListener("click",j),gt(e).then(d=>{const m=document.getElementById("cc-tpl-browser-grid");m&&(m.innerHTML=d.map(u=>`<div class="cc-template-card cc-tpl-browser-card">
          <b>${i(u.labelZh)} <span class="muted">· ${i(u.labelEn)}</span></b>
          <small>${i(u.id)}</small>
          <p class="muted small cc-template-tip">${i(fe[u.id]??"")}</p>
          <div class="cc-tpl-browser-actions">
            ${P("🔍 详情","default",{"data-tpl-view":i(u.id)})}
            ${H("用此模板创建 →",{"data-tpl-create":i(u.id)})}
          </div>
        </div>`).join("")||'<p class="muted">该类型暂无可用模板。</p>',m.querySelectorAll("[data-tpl-view]").forEach(u=>u.addEventListener("click",()=>{const _=d.find(v=>v.id===u.dataset.tplView);_&&Pt(_)})),m.querySelectorAll("[data-tpl-create]").forEach(u=>u.addEventListener("click",()=>{j(),c(u.dataset.tplCreate)})))})}async function Pe(e){if(e.type==="image/png"||/\.png$/i.test(e.name)){const d=await ba(e),m=await ha(e);return{base64:d,...m}}const c=await createImageBitmap(e),n=document.createElement("canvas");n.width=c.width,n.height=c.height;const s=n.getContext("2d");if(!s)throw new Error("无法创建画布（Canvas）");s.drawImage(c,0,0);const p=n.toDataURL("image/png");return{base64:p.slice(p.indexOf(",")+1),width:c.width,height:c.height}}function ha(e){return new Promise(c=>{const n=URL.createObjectURL(e),s=new Image;s.onload=()=>{URL.revokeObjectURL(n),c({width:s.naturalWidth,height:s.naturalHeight})},s.onerror=()=>{URL.revokeObjectURL(n),c({width:0,height:0})},s.src=n})}async function vt(e,c,n){const s=(e||"model").replace(/[^A-Za-z0-9_]/g,"")||"model",p={};for(const u of n)p[u.cls]=u.diskName;const d=[];if(/\.fbx$/i.test(c.name)){const{renameFbxTextureRefs:u}=await z(async()=>{const{renameFbxTextureRefs:v}=await import("./fbxTextureRename-CZchYSrG.js");return{renameFbxTextureRefs:v}},[]),_=u(c.bytes,p);d.push({fileName:`${s}.fbx`,base64:re(_.bytes)})}else{const{renameMtlTextureRefs:u,ensureObjMtllib:_}=await z(async()=>{const{renameMtlTextureRefs:a,ensureObjMtllib:g}=await import("./mtlTextureRename-vGs99CsZ.js");return{renameMtlTextureRefs:a,ensureObjMtllib:g}},__vite__mapDeps([2,3]));let v=c.mtl;v!=null&&(v=u(v,p,{}).mtl);const A=new TextDecoder("utf-8").decode(c.bytes),U=v!=null?_(A,`${s}.mtl`):A;d.push({fileName:`${s}.obj`,base64:re(new TextEncoder().encode(U))}),v!=null&&d.push({fileName:`${s}.mtl`,base64:re(new TextEncoder().encode(v))})}for(const u of n)d.push({fileName:u.diskName,base64:re(u.bytes)});return d}function re(e){let c="";for(let s=0;s<e.length;s+=32768)c+=String.fromCharCode.apply(null,Array.from(e.subarray(s,s+32768)));return btoa(c)}function ba(e){return new Promise((c,n)=>{const s=new FileReader;s.onload=()=>{const p=String(s.result??"");c(p.includes(",")?p.slice(p.indexOf(",")+1):p)},s.onerror=()=>n(s.error??new Error("读取文件失败")),s.readAsDataURL(e)})}function kt(e){yt(e).then(c=>{Ht.ingredientsCache=c,Kt()}).catch(()=>{})}export{Ea as renderCustomContentView};
