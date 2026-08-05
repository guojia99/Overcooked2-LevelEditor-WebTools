import{f as ue,n as pe,w as ye,a as q,b as A,c as ge,d as be,h as k,s as x,u as ve,e as fe,g as $e,i as he,o as R,j as h,k as Ie,l as Ee,m as we,p as ne,r as ke}from"./index-JGdZcEN5.js";function n(l){return String(l??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}const H=/^[A-Za-z_][A-Za-z0-9_]*$/;function b(l,e=!0){const a=document.getElementById("cr-status");a&&(a.textContent=l,a.classList.toggle("err",!e),a.classList.toggle("ok",e&&l.length>0))}function _(l,e){return document.body.classList.add("manage-bg"),l.innerHTML=`
    ${pe("custom-recipes")}
    <div class="manage-bar">
      <h1 class="m-title">${n(e)}</h1>
      <span class="status" id="cr-status"></span>
      <span style="flex:1"></span>
    </div>
    <div class="cr-warn-banner">⚠️ 本功能研发中，请勿使用</div>
    <div class="manage-content" id="cr-content"></div>
  `,ye(a=>{a==="layout"?(location.hash="#/layout",location.reload()):a==="manage"&&(location.hash="#/manage",location.reload())}),document.getElementById("cr-content")}async function Be(l){const e=_(l,"自定义菜谱管理");Z("加载关卡集…");let a=[];try{a=await ue()}catch(c){z(c);return}b(`共 ${a.length} 个关卡集`),e.innerHTML=`
    <div class="m-section-title">选择关卡集</div>
    <p class="modal-hint">选择要管理自定义菜谱的关卡集。首次进入会自动初始化配置。</p>
    <div class="m-grid">${a.map(c=>`
      <div class="m-card">
        <h3>${n(c.levelSetNameZH||c.setName)} <span class="muted">(${n(c.levelSetName||c.setName)})</span></h3>
        <div class="m-meta">
          标识：${n(c.setName)} · 关卡数：${c.levelCount}<br>
          作者：${n(c.author||"—")} · 版本：${n(c.version||"—")}
        </div>
        <div class="m-actions">
          <button class="m-btn primary" data-open="${n(c.setName)}">管理菜谱</button>
        </div>
      </div>`).join("")||'<p class="muted">暂无关卡集</p>'}
    </div>
  `,e.querySelectorAll("[data-open]").forEach(c=>c.addEventListener("click",()=>void j(l,c.dataset.open)))}function Z(l){const e=document.getElementById("cr-content");e&&(e.innerHTML=`<p class="muted">${n(l)}</p>`),b(l)}function z(l){const e=l instanceof Error?l.message:String(l);b(e,!1);const a=document.getElementById("cr-content");a&&(a.innerHTML=`<div class="m-block"><h3>出错</h3><p>${n(e)}</p></div>`)}async function j(l,e){var B,D;const a=_(l,`自定义菜谱 · ${n(e)}`);Z("加载菜谱配置…");let c,p;try{c=await q(e),p=await A(e)}catch(d){z(d);return}b(`${p.length} 个菜谱 · UID前缀：${c.uidPrefix}`);const u=c.categories??[];function s(d){return d.zh||d.id}let i="";function t(d){const w=d?p.filter(r=>r.category===d):p;return w.length===0?'<p class="muted">该分类下暂无菜谱</p>':`<div class="m-grid">${w.map(r=>`
      <div class="m-card cr-card">
        <div class="cr-card-head">
          ${r.hasIcon?`<img class="cr-icon" src="/api/level/data-file?path=${encodeURIComponent(r.assetPath.replace(/_SO\.asset$/,""))}/models/${encodeURIComponent(r.id.replace(/_SO$/,""))}_Icon.png" alt="" onerror="this.style.display='none'" />`:""}
          <span class="cr-type-badge">${n(r.type)}</span>
          ${r.score>0?"":'<span class="m-badge warn">半成品</span>'}
        </div>
        <h4>${n(r.nameZh||r.recipeName)}</h4>
        <div class="m-meta">
          ${r.nameEn?`${n(r.nameEn)}<br>`:""}
          id: ${n(r.recipeName)}<br>
          UID: ${r.uID} · 分数: ${r.score}<br>
          食材: ${(r.compositionIds??[]).length} 种
          ${r.cookingStepId?` · ${n(r.cookingStepId)}`:""}
          ${r.hasModel?' · <span class="ok">模型</span>':""}
        </div>
        <div class="m-actions">
          <button class="m-btn" data-edit="${n(r.assetPath)}">编辑</button>
          <button class="m-btn danger" data-del="${n(r.assetPath)}">删除</button>
        </div>
      </div>`).join("")}</div>`}function f(){return`
    <div class="cr-sidebar">
      <div class="m-section-title">分类</div>
      <div class="cr-cat-list">
        <button class="m-btn cr-cat-item${i===""?" primary":""}" data-cat="">全部 (${p.length})</button>
        ${u.map(d=>{const w=p.filter(r=>r.category===d.id).length;return`<button class="m-btn cr-cat-item${i===d.id?" primary":""}" data-cat="${n(d.id)}">${n(s(d))} (${w})</button>`}).join("")}
        <div class="cr-cat-actions">
          <button class="m-btn" id="cr-new-cat">+ 新建分类</button>
          ${u.length>0?'<button class="m-btn" id="cr-manage-cat">管理分类</button>':""}
        </div>
      </div>
    </div>`}a.innerHTML=`
    <div class="m-actions-row">
      <button class="m-btn" id="cr-back">← 返回关卡集列表</button>
      <span class="muted">当前关卡集：<b>${n(e)}</b></span>
      <span style="flex:1"></span>
      <button class="m-btn primary" id="cr-new-recipe">+ 新建菜谱</button>
    </div>
    <div class="cr-layout">
      <div id="cr-sidebar">${f()}</div>
      <div id="cr-grid">${t("")}</div>
    </div>
  `;function I(){(async()=>{x("加载…");try{c=await q(e),p=await A(e)}catch(d){z(d);return}finally{k()}b(`${p.length} 个菜谱 · UID前缀：${c.uidPrefix}`),document.getElementById("cr-sidebar").innerHTML=f(),E(),document.getElementById("cr-grid").innerHTML=t(i),L()})()}function E(){var d,w;document.querySelectorAll(".cr-cat-item").forEach(r=>{r.addEventListener("click",()=>{i=r.dataset.cat??"",document.getElementById("cr-sidebar").innerHTML=f(),E(),document.getElementById("cr-grid").innerHTML=t(i),L()})}),(d=document.getElementById("cr-new-cat"))==null||d.addEventListener("click",()=>Se(e,I)),(w=document.getElementById("cr-manage-cat"))==null||w.addEventListener("click",()=>Le(e,c.categories,I))}function L(){document.querySelectorAll("[data-edit]").forEach(d=>d.addEventListener("click",()=>void te(l,e,d.dataset.edit))),document.querySelectorAll("[data-del]").forEach(d=>d.addEventListener("click",()=>xe(l,e,d.dataset.del,I)))}E(),L(),(B=document.getElementById("cr-back"))==null||B.addEventListener("click",()=>void Be(l)),(D=document.getElementById("cr-new-recipe"))==null||D.addEventListener("click",()=>void te(l,e,null))}async function te(l,e,a){var G,N,V,J,X;const c=a!=null,p=_(l,c?"编辑菜谱":"新建菜谱");Z("加载参考数据…");let u=[],s,i,t;try{[u,s,i]=await Promise.all([ge().catch(()=>[]),be(e),q(e)]),c&&(t=(await A(e)).find(m=>m.assetPath===a))}catch(o){z(o),k();return}k();const f=!c,I=(t==null?void 0:t.recipeName)??"",E=(t==null?void 0:t.nameZh)??"",L=(t==null?void 0:t.nameEn)??"",B=(t==null?void 0:t.category)??(((G=i.categories)==null?void 0:G.length)>0?i.categories[0].id:""),D=(t==null?void 0:t.score)??0,d=(t==null?void 0:t.type)??"Cooked",w=(t==null?void 0:t.compositionIds)??[],r=(t==null?void 0:t.cookingStepId)??"",ce="",oe=(t==null?void 0:t.platingStepId)??"",le="",se="",U=i.categories??[];function ie(o){return o.zh||o.id}function de(){let o=U.map(m=>`<option value="${n(m.id)}" ${m.id===B?"selected":""}>${n(ie(m))}</option>`).join("");return!U.some(m=>m.id===B)&&B&&(o+=`<option value="${n(B)}" selected>${n(B)}</option>`),`<select id="cr-type-cat" class="m-select">${o}</select>`}function M(o,m,v){const g=new Map;for(const y of o)g.has(y.id)||g.set(y.id,y.nameZh||y.id);return`<select id="${v}" class="m-select">
      <option value="">— 不设置 —</option>
      ${[...g.entries()].map(([y,$])=>`<option value="${n(y)}" ${y===m?"selected":""}>${n($)} (${n(y)})</option>`).join("")}
    </select>`}function re(o,m){const v=o.group&&o.group!=="core"?` <span class="pc-badge">${we(o.group)}</span>`:"",g=o.nameEn&&o.nameEn.trim()||"";return`<label class="pick-card${m?" selected":""}" data-guid="${o.guid}">
      <input type="checkbox" value="${n(o.id)}" data-ingid="${n(o.id)}" ${m?"checked":""}>
      <span class="pc-head"><img class="food-icon" src="/icons/ingredients/${n(o.id)}.png" alt="" onerror="this.src='/icons/_placeholder.png'" /><span class="pc-name">${n(o.nameZh)}${v}${g?` <span class="muted pc-en">${g}</span>`:""}</span></span>
    </label>`}const me=`<div class="pick-grid">${u.map(o=>re(o,w.includes(o.id))).join("")}</div>`;p.innerHTML=`
    <div class="m-actions-row">
      <button class="m-btn" id="cr-form-back">← 返回菜谱列表</button>
      <span class="muted">关卡集：<b>${n(e)}</b> · ${c?`编辑 ${n(I)}`:"新建菜谱"}</span>
      <span style="flex:1"></span>
      <button class="m-btn primary" id="cr-form-save">💾 保存</button>
    </div>
    <div class="modal-scroll" style="max-height:calc(100vh - 160px);padding:0 8px;">
    <label class="m-field">标识符 recipeName（仅字母/数字/下划线，创建后不可修改）
      <input type="text" id="cr-rec-name" value="${n(I)}" ${c?"disabled":""} placeholder="MyRecipe">
    </label>
    <div class="m-row">
      <label class="m-field">中文名<input type="text" id="cr-zh" value="${n(E)}" placeholder="我的菜谱"></label>
      <label class="m-field">英文名<input type="text" id="cr-en" value="${n(L)}" placeholder="My Recipe"></label>
    </div>
    <label class="m-field">分类 ${de()}
      <button type="button" class="m-btn" id="cr-new-cat-inline" style="margin-left:8px">+ 新建分类</button>
    </label>
    <div class="m-row">
      <label class="m-field">类型
        <select id="cr-type" class="m-select">
          <option value="Composite" ${d==="Composite"?"selected":""}>Composite（组合）</option>
          <option value="Cooked" ${d==="Cooked"?"selected":""}>Cooked（烹饪）</option>
          <option value="Mixed" ${d==="Mixed"?"selected":""}>Mixed（搅拌）</option>
        </select>
      </label>
      <label class="m-field">分数<input type="number" id="cr-score" value="${D}" min="0"></label>
    </div>
    <label class="m-field">UID（自动生成）<input type="text" value="${(t==null?void 0:t.uID)??(f?i.uidPrefix*1e3+i.nextSequence:"—")}" disabled></label>
    <div class="m-section-title">食材</div>
    <p class="modal-hint">选择菜谱所需的食材（至少一种）</p>
    <div id="cr-ing-grid">${me}</div>
    <div class="m-row">
      <label class="m-field">烹饪步骤 ${M(s.cookingSteps,r,"cr-cook-step")}</label>
      <label class="m-field">烹饪图标 ${M(s.icons,ce,"cr-cook-icon")}</label>
    </div>
    <div class="m-row" id="cr-cook-row">
      <label class="m-field">烹饪程度
        <select id="cr-cook-prog" class="m-select">
          <option value="0">Raw（生）</option>
          <option value="1" selected>Cooked（熟）</option>
          <option value="2">Burnt（焦）</option>
        </select>
      </label>
    </div>
    <label class="m-field">装盘步骤 ${M(s.platingSteps,oe,"cr-plate-step")}</label>
    <div class="m-row" id="cr-mix-row" style="display:none">
      <label class="m-field">搅拌图标 ${M(s.icons,le,"cr-mix-icon")}</label>
    </div>
    <div class="m-row" id="cr-mix-prog-row" style="display:none">
      <label class="m-field">搅拌程度
        <select id="cr-mix-prog" class="m-select">
          <option value="0">Unmixed（未搅拌）</option>
          <option value="1" selected>Mixed（已搅拌）</option>
          <option value="2">OverMixed（过度搅拌）</option>
        </select>
      </label>
    </div>
    <div class="m-section-title">模型</div>
    <div class="m-row">
      <label class="m-field">复用已有模型 ${M(s.reusableModels,se,"cr-model-ref")}</label>
    </div>
    <p class="modal-hint">或上传新的 3D 模型（FBX/OBJ）</p>
    <div class="m-row">
      <label class="m-field">上传图标（PNG，用作菜谱卡片图）<input type="file" id="cr-icon-upload" accept="image/png"></label>
      <label class="m-field">上传 3D 模型<input type="file" id="cr-model-upload" accept=".fbx,.obj"></label>
    </div>
    </div>
  `;function F(){var $;const o=document.getElementById("cr-type").value,m=document.getElementById("cr-cook-row"),v=($=document.getElementById("cr-cook-step"))==null?void 0:$.closest(".m-field"),g=document.getElementById("cr-mix-row"),y=document.getElementById("cr-mix-prog-row");m&&(m.style.display=o==="Composite"?"none":""),v&&(v.style.display=o==="Composite"?"none":""),g&&(g.style.display=o==="Mixed"?"":"none"),y&&(y.style.display=o==="Mixed"?"":"none")}(N=document.getElementById("cr-type"))==null||N.addEventListener("change",F),F(),(V=document.getElementById("cr-new-cat-inline"))==null||V.addEventListener("click",()=>Ce(e,async()=>{i=await q(e);const o=document.getElementById("cr-type-cat");if(o){const m=i.categories[i.categories.length-1];m&&(o.innerHTML=i.categories.map(v=>`<option value="${n(v.id)}">${n(v.zh||v.id)}</option>`).join(""),o.value=m.id)}})),(J=document.getElementById("cr-form-back"))==null||J.addEventListener("click",()=>void j(l,e)),(X=document.getElementById("cr-form-save"))==null||X.addEventListener("click",async()=>{var v,g,y,$,K,Q,W,Y,ee;const o=document.getElementById("cr-rec-name").value.trim();if(!o)return alert("请填写标识符");if(!H.test(o))return alert("标识符仅允许英文字母/数字/下划线，且不能以数字开头");const m={setName:c?void 0:e,assetPath:c?a:void 0,recipeName:o,nameZh:document.getElementById("cr-zh").value.trim(),nameEn:document.getElementById("cr-en").value.trim(),category:document.getElementById("cr-type-cat").value,score:Number(document.getElementById("cr-score").value)||0,type:document.getElementById("cr-type").value,compositionIds:[],cookingStepId:((v=document.getElementById("cr-cook-step"))==null?void 0:v.value)??"",cookingStepIconId:((g=document.getElementById("cr-cook-icon"))==null?void 0:g.value)??"",platingStepId:((y=document.getElementById("cr-plate-step"))==null?void 0:y.value)??"",mixingIconId:(($=document.getElementById("cr-mix-icon"))==null?void 0:$.value)??"",modelPrefabId:((K=document.getElementById("cr-model-ref"))==null?void 0:K.value)??"",cookingProgress:Number(((Q=document.getElementById("cr-cook-prog"))==null?void 0:Q.value)??"1")||1,mixingProgress:Number(((W=document.getElementById("cr-mix-prog"))==null?void 0:W.value)??"1")||1};document.querySelectorAll("#cr-ing-grid input:checked").forEach(S=>{const C=S.dataset.ingid;C&&m.compositionIds.push(C)}),x("保存中…");try{c?await ve(m):await fe(m);const S=a||`Assets/LevelSets/${e}/custom_recipes/${m.category}/${o}.asset`,C=(Y=document.getElementById("cr-icon-upload").files)==null?void 0:Y[0],T=(ee=document.getElementById("cr-model-upload").files)==null?void 0:ee[0];if(C){const P=await O(C);await $e(e,S,C.name,P)}if(T){const P=await O(T);await he(e,S,T.name,P)}b(c?"已更新菜谱":"已创建菜谱"),j(l,e)}catch(S){b(S.message,!1)}finally{k()}});function O(o){return new Promise((m,v)=>{const g=new FileReader;g.onload=()=>{const y=g.result,$=y.indexOf(",");m($>=0?y.substring($+1):y)},g.onerror=v,g.readAsDataURL(o)})}}function xe(l,e,a,c){var u,s;const p=a.split("/").pop()??a;R(`删除菜谱 · ${n(p)}`,"<p>将永久删除菜谱资源及其模型文件夹，且<b>不可恢复</b>。</p>",'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn danger" data-ok>确认删除</button>'),(u=document.querySelector("[data-cancel]"))==null||u.addEventListener("click",h),(s=document.querySelector("[data-ok]"))==null||s.addEventListener("click",async()=>{x("删除中…");try{await Ee(a),h(),b("已删除菜谱"),c()}catch(i){b(i.message,!1)}finally{k()}})}function Se(l,e){R("新建分类",`<label class="m-field">分类ID（仅字母/数字/下划线，用于目录名）<input type="text" id="cr-cat-id" placeholder="MyCategory"></label>
     <label class="m-field">中文名<input type="text" id="cr-cat-zh" placeholder="我的分类"></label>
     <label class="m-field">英文名<input type="text" id="cr-cat-en" placeholder="My Category"></label>`,'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>创建</button>'),ae(h,async()=>{const a=document.getElementById("cr-cat-id").value.trim();if(!a)return alert("请填写分类ID");if(!H.test(a))return alert("分类ID仅允许英文字母/数字/下划线");const c=document.getElementById("cr-cat-zh").value.trim(),p=document.getElementById("cr-cat-en").value.trim();x("创建分类…");try{await ne(l,a,c||a,p||a),h(),b("已创建分类"),e()}catch(u){b(u.message,!1)}finally{k()}})}function Ce(l,e){const a=prompt("分类ID（仅字母/数字/下划线）：");if(!a||!H.test(a)){alert("ID非法");return}const c=prompt("分类中文名：")||a,p=prompt("分类英文名：")||a;x("创建分类…"),ne(l,a,c,p).then(()=>{b("已创建分类"),e()}).catch(u=>b(u.message,!1)).finally(()=>k())}function Le(l,e,a){var u;function c(s){return`${s.zh||s.id}${s.en?` (${s.en})`:""}`}const p=e.map(s=>`
    <div class="m-row" style="margin-bottom:8px">
      <span style="flex:1">${n(c(s))} <span class="muted">[${n(s.id)}]</span></span>
      <button class="m-btn" data-rename="${n(s.id)}">重命名</button>
      <button class="m-btn danger" data-delcat="${n(s.id)}">删除</button>
    </div>`).join("");R("管理分类",`<div class="modal-scroll">${p||'<p class="muted">暂无分类</p>'}</div>`,'<button type="button" class="m-btn" data-cancel>关闭</button>'),(u=document.querySelector("[data-cancel]"))==null||u.addEventListener("click",h),document.querySelectorAll("[data-rename]").forEach(s=>{s.addEventListener("click",()=>{const i=s.dataset.rename,t=e.find(f=>f.id===i);Me(l,i,(t==null?void 0:t.zh)??i,(t==null?void 0:t.en)??i,()=>{h(),a()})})}),document.querySelectorAll("[data-delcat]").forEach(s=>{s.addEventListener("click",()=>{var f,I;const i=s.dataset.delcat,t=e.find(E=>E.id===i);R(`删除分类 · ${n((t==null?void 0:t.zh)||i)}`,"<p>将检查关卡使用情况。如果有关卡正在使用该分类的菜谱，则不允许删除。</p>",'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn danger" data-ok>确认删除</button>'),(f=document.querySelector("[data-cancel]"))==null||f.addEventListener("click",h),(I=document.querySelector("[data-ok]"))==null||I.addEventListener("click",async()=>{x("检查中…");try{await Ie(l,i),h(),b("已删除分类"),a()}catch(E){b(E.message,!1),k()}})})})}function Me(l,e,a,c,p){R(`重命名分类 · ${n(a||e)}`,`<label class="m-field">分类ID（仅字母/数字/下划线）<input type="text" id="cr-rename-id" value="${n(e)}"></label>
     <label class="m-field">中文名<input type="text" id="cr-rename-zh" value="${n(a)}"></label>
     <label class="m-field">英文名<input type="text" id="cr-rename-en" value="${n(c)}"></label>`,'<button type="button" class="m-btn" data-cancel>取消</button><button type="button" class="m-btn primary" data-ok>确认</button>'),ae(h,async()=>{const u=document.getElementById("cr-rename-id").value.trim();if(!u)return alert("请填写分类ID");if(!H.test(u))return alert("分类ID仅允许英文字母/数字/下划线");const s=document.getElementById("cr-rename-zh").value.trim(),i=document.getElementById("cr-rename-en").value.trim();x("重命名…");try{await ke(l,e,u,s||u,i||u),h(),b("已重命名分类"),p()}catch(t){b(t.message,!1)}finally{k()}})}function ae(l,e){var a,c;(a=document.querySelector("[data-cancel]"))==null||a.addEventListener("click",l),(c=document.querySelector("[data-ok]"))==null||c.addEventListener("click",e)}export{Be as renderCustomRecipesView};
