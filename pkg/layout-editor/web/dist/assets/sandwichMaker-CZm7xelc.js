import{$ as Y,a1 as D,n as g,w as v,q as V,aT as f,aN as W,aO as F,aD as J,aB as Q,ax as U,v as O}from"./dom-Cx9-C33g.js";function t(a){return String(a??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function ee(a){O(a.message??String(a),!1)}function k(a,n){return(a??[]).includes(n)?"checked":""}function y(a,n){return a===n?"selected":""}async function ae(a){var E,P,C,N,Z;const n=Y();let d=[];try{d=await D()}catch(e){a.innerHTML=`${g("recipes")}<div class="panel"><p class="muted">无法读取关卡集：${t(e.message)}</p></div>`,v();return}const b=n.setId??"";let l=d.some(e=>e.setName===b)?b:((E=d[0])==null?void 0:E.setName)??"";if(!l){a.innerHTML=`${g("recipes")}<div class="panel"><p class="muted">还没有关卡集。</p></div>`,v();return}async function R(){const e=await J(l),i=await Q(l),p=await U(l),r=n.sandwichId??"",w=r?p.find(u=>u.id===r&&u.optionalKind==="sandwich")??null:null;return{cfg:e,refs:i,recipes:p,editing:w}}let c;try{c=await R()}catch(e){a.innerHTML=`${g("recipes")}<div class="panel"><p class="muted">加载失败：${t(e.message)}</p></div>`,v();return}const M=c.cfg,I=c.refs,G=c.recipes,s=c.editing,S=M.categories??[],o="sandwich",h=await V(l).catch(()=>[]),T=I.reusableModels??[],H=((C=(P=I.platingContainers)==null?void 0:P[0])==null?void 0:C.id)??"Plate",j=d.map(e=>`<option value="${t(e.setName)}" ${y(l,e.setName)}>${t(e.levelSetNameZH||e.setName)}</option>`).join(""),L=S.map(e=>`<option value="${t(e.id)}" ${y((s==null?void 0:s.category)??o,e.id)}>${t(e.zh||e.id)}</option>`).join("")+(S.some(e=>e.id===o)?"":`<option value="${o}" ${y((s==null?void 0:s.category)??o,o)}>🥪 三明治</option>`),A=e=>h.map(i=>`<option value="${t(i.id)}" ${y(e,i.id)}>${t(i.nameZh||i.id)}</option>`).join(""),z=T.map(e=>`<option value="${t(e.guid)}" data-id="${t(e.id)}">${t(e.nameZh||e.id)}</option>`).join(""),q=(s==null?void 0:s.fillingIds)??[],_=(s==null?void 0:s.sauceIds)??[];a.innerHTML=`
    ${g("recipes")}
    <div class="panel">
      <div class="panel-head">
        <h2>🥪 三明治组装工作台</h2>
        <div class="muted">CustomRecipeOptionalSandwichSO（toast + 夹心 + 酱料）</div>
      </div>
      <div class="cc-grid" style="margin-bottom:12px">
        <label class="m-field">关卡集
          <select id="sw-set" class="rl-select">${j}</select>
        </label>
        <label class="m-field">分类
          <select id="sw-category" class="rl-select">${L}</select>
        </label>
      </div>
      <div class="cc-grid">
        <label class="m-field">标识 id（字母数字下划线）
          <input id="sw-id" class="rl-select" value="${t((s==null?void 0:s.id)??"")}" ${s?"disabled":""} placeholder="如 Web_Sandwich_Ham">
        </label>
        <label class="m-field">中文名
          <input id="sw-zh" class="rl-select" value="${t((s==null?void 0:s.nameZh)??"")}" placeholder="如 火腿三明治">
        </label>
        <label class="m-field">英文名
          <input id="sw-en" class="rl-select" value="${t((s==null?void 0:s.nameEn)??"")}" placeholder="Ham Sandwich">
        </label>
        <label class="m-field">分数
          <input id="sw-score" type="number" class="rl-select" value="${(s==null?void 0:s.score)??80}">
        </label>
        <label class="m-field">容量（夹心槽）
          <input id="sw-capacity" type="number" min="1" class="rl-select" value="${(s==null?void 0:s.sandwichCapacity)??8}">
        </label>
        <label class="m-field">Toast 底层（必填）
          <select id="sw-toast" class="rl-select">
            <option value="">— 选择食材 —</option>
            ${A(s==null?void 0:s.toastId)}
          </select>
        </label>
        <label class="m-field">装盘模型（可选）
          <select id="sw-model" class="rl-select">
            <option value="">— 不指定 —</option>
            ${z}
          </select>
        </label>
      </div>

      <details open style="margin-top:12px">
        <summary><b>夹心（optionalSOs）</b></summary>
        <div class="cc-item-grid" style="max-height:240px;overflow:auto">
          ${h.map(e=>`<label class="m-field" style="display:flex;gap:6px;align-items:center"><input type="checkbox" class="sw-filling" value="${t(e.id)}" ${k(q,e.id)}> ${t(e.nameZh||e.id)}</label>`).join("")}
        </div>
      </details>

      <details style="margin-top:12px">
        <summary><b>酱料（sauceSOs）</b></summary>
        <div class="cc-item-grid" style="max-height:200px;overflow:auto">
          ${h.map(e=>`<label class="m-field" style="display:flex;gap:6px;align-items:center"><input type="checkbox" class="sw-sauce" value="${t(e.id)}" ${k(_,e.id)}> ${t(e.nameZh||e.id)}</label>`).join("")}
        </div>
      </details>

      <div style="margin-top:16px;display:flex;gap:8px;align-items:center">
        <button type="button" class="m-btn primary" id="sw-save">${s?"保存修改":"创建三明治"}</button>
        ${s?`<a class="m-btn" href="${f(l)}">新建</a>`:""}
        <span id="sw-status" class="muted"></span>
      </div>

      <h3 style="margin-top:24px">已有三明治</h3>
      <div class="cc-item-grid">
        ${G.filter(e=>e.optionalKind==="sandwich").map(e=>`
          <a class="m-card" href="${f(l,e.id)}" style="text-decoration:none">
            <b>${t(e.nameZh||e.id)}</b><br>
            <span class="muted">${t(e.id)} · ${e.score} 分</span>
          </a>`).join("")||'<p class="muted">暂无</p>'}
      </div>
    </div>`,v();const x=document.getElementById("sw-status"),m=(e,i=!0)=>{x&&(x.textContent=e),O(e,i)};(N=document.getElementById("sw-set"))==null||N.addEventListener("change",e=>{const i=e.target.value;location.assign(f(i))}),(Z=document.getElementById("sw-save"))==null||Z.addEventListener("click",async()=>{try{const e=document.getElementById("sw-id").value.trim();if(!e)return m("请填写标识 id。",!1);if(!/^[A-Za-z_][A-Za-z0-9_]*$/.test(e))return m("标识 id 仅允许英文字母/数字/下划线，且不能以数字开头。",!1);const i=document.getElementById("sw-toast").value;if(!i)return m("请选择 Toast 底层食材。",!1);const p=Array.from(document.querySelectorAll(".sw-filling:checked")).map($=>$.value),r=Array.from(document.querySelectorAll(".sw-sauce:checked")).map($=>$.value),w=Number(document.getElementById("sw-score").value)||0,u=Number(document.getElementById("sw-capacity").value)||8,K=document.getElementById("sw-category").value||o,X=document.getElementById("sw-model").value,B={setName:l,assetPath:s==null?void 0:s.assetPath,recipeName:(s==null?void 0:s.recipeName)||e,nameZh:document.getElementById("sw-zh").value.trim()||e,nameEn:document.getElementById("sw-en").value.trim()||e,category:K,score:w,type:"Composite",compositionIds:[],cookingStepId:"",cookingStepIconId:"",platingStepId:H,mixingIconId:"",modelPrefabId:X,cookingProgress:1,mixingProgress:1,modelScale:1,modelRotationX:0,modelRotationY:0,modelRotationZ:0,modelPositionX:0,modelPositionY:0,modelPositionZ:0,modelPivotX:0,modelPivotY:0,modelPivotZ:0,optionalKind:"sandwich",toastGuid:i,fillingGuids:p,sauceGuids:r,ingredientContainerCapacity:u,ingredientModelSoGuids:[],ingredientModelGuids:[],sauceInPlateModelGuids:[],sauceOnToastModelGuids:[],wrongSauceInPlateGuid:"",wrongSauceOnToastGuid:""};s?await W(B):await F(B),m(s?"已保存。":"已创建。"),location.assign(f(l,s?s.id:e))}catch(e){ee(e)}})}export{ae as renderSandwichMakerView};
