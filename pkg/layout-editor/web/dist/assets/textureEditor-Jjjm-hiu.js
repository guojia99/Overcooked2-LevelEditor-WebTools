import{Q as z,am as D,j as r,S as R}from"./version-d1gpIcOw.js";import{fetchTemplateMeshBuffer as M,previewLocalModel as j}from"./recipeModelPreview-BXzDUsOz.js";const a=1024,F="#c06205";function U(o){return String(o??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function q(o){const s=o.toDataURL("image/png"),i=s.indexOf(",");return i>=0?s.slice(i+1):s}function S(o,s){return new Promise((i,l)=>{o.toBlob(t=>{if(!t){l(new Error("贴图导出失败。"));return}i(new File([t],s,{type:"image/png"}))},"image/png")})}function _(o){var E,y,h,w,I,L,B,k;const s=o.templateId??"FriedFishCake";let i=o.initialColor??F;z("🎨 夹心贴图编辑器",`<p class="modal-hint">产出一张 ${a}×${a} PNG 贴图，贴到模板网格
      <code>${U(s)}.fbx</code> 上。纯色即可用（官方夹心贴图本身就是纯色）；
      也可上传图片或用画笔涂改。「🔍 预览模型」直接在浏览器渲染当前贴图效果，不写盘。</p>
     <div class="tex-editor">
       <div class="tex-canvas-wrap">
         <canvas id="tex-canvas" width="${a}" height="${a}"></canvas>
       </div>
       <div class="tex-tools">
         <div class="tex-row"><span class="muted">主色</span>
           <input type="color" id="tex-color" value="${U(i)}">
           ${r("填充整张","small",{id:"tex-fill"})}
         </div>
         <div class="tex-row"><span class="muted">上传图片</span>
           <input type="file" id="tex-upload" accept="image/png,image/jpeg,image/webp" class="rl-select">
         </div>
         <div class="tex-row"><span class="muted">画笔</span>
           <input type="range" id="tex-size" min="4" max="256" value="48">
           <span class="muted small" id="tex-size-val">48 px</span>
         </div>
         <div class="tex-row">
           ${r("↩ 撤销","small",{id:"tex-undo",title:"撤销上一笔（最多 20 步）"})}
           ${r("清空为主色","small",{id:"tex-clear"})}
           ${r("🔍 预览模型","small",{id:"tex-preview"})}
         </div>
         <p class="muted small">提示：在左侧画布上按住拖动即可涂抹；橡皮 = 把画笔色设成主色再涂。</p>
       </div>
     </div>`,`${D()}${r("使用这张贴图","primary",{id:"tex-ok"})}`),(E=document.querySelector(".modal-panel"))==null||E.classList.add("wide");const l=document.getElementById("tex-canvas"),t=l.getContext("2d"),v=document.getElementById("tex-size"),N=document.getElementById("tex-size-val"),x=document.getElementById("tex-color"),m=[],O=20,p=()=>{m.push(t.getImageData(0,0,a,a)),m.length>O&&m.shift()},u=e=>{t.fillStyle=e,t.fillRect(0,0,a,a)};u(i),x.addEventListener("input",()=>{i=x.value}),(y=document.getElementById("tex-fill"))==null||y.addEventListener("click",()=>{p(),u(i)}),(h=document.getElementById("tex-clear"))==null||h.addEventListener("click",()=>{p(),u(i)}),(w=document.getElementById("tex-undo"))==null||w.addEventListener("click",()=>{const e=m.pop();e&&t.putImageData(e,0,0)}),v.addEventListener("input",()=>{N.textContent=`${v.value} px`}),(I=document.getElementById("tex-upload"))==null||I.addEventListener("change",e=>{var $;const n=($=e.target.files)==null?void 0:$[0];if(!n)return;const c=URL.createObjectURL(n),d=new Image;d.onload=()=>{URL.revokeObjectURL(c),p(),u(i);const C=Math.max(a/d.width,a/d.height),b=d.width*C,T=d.height*C;t.drawImage(d,(a-b)/2,(a-T)/2,b,T)},d.onerror=()=>{URL.revokeObjectURL(c),alert("图片解码失败，请换一张 PNG/JPG。")},d.src=c});let g=!1;const f=e=>{const n=l.getBoundingClientRect();return{x:(e.clientX-n.left)/n.width*a,y:(e.clientY-n.top)/n.height*a}},P=e=>{const{x:n,y:c}=f(e);t.lineTo(n,c),t.stroke()};l.addEventListener("pointerdown",e=>{p(),g=!0,l.setPointerCapture(e.pointerId),t.strokeStyle=i,t.lineWidth=Number(v.value),t.lineCap="round",t.lineJoin="round",t.beginPath();const{x:n,y:c}=f(e);t.moveTo(n,c),t.lineTo(n,c),t.stroke()}),l.addEventListener("pointermove",e=>{g&&P(e)}),l.addEventListener("pointerup",e=>{g=!1,l.releasePointerCapture(e.pointerId)}),(L=document.getElementById("tex-preview"))==null||L.addEventListener("click",()=>{(async()=>{const e=document.getElementById("tex-preview");e&&(e.disabled=!0,e.textContent="加载中…");try{const n=await M(s),c=await S(l,o.fileName);await j({title:`夹心模型预览 · ${s}`,buffer:n,fileName:`${s}.fbx`,textures:[c]})}catch(n){alert(n.message||"预览失败。")}finally{e&&(e.disabled=!1,e.textContent="🔍 预览模型")}})()}),(B=document.querySelector("[data-cancel]"))==null||B.addEventListener("click",()=>R()),(k=document.getElementById("tex-ok"))==null||k.addEventListener("click",()=>{(async()=>{try{const e=await S(l,o.fileName);o.onDone({base64:q(l),file:e,color:i}),R()}catch(e){alert(e.message||"贴图导出失败。")}})()})}export{a as TEXTURE_SIZE,_ as openTextureEditor};
