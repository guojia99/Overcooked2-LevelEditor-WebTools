import{H as S,I as R}from"./version-aGNr2fLU.js";import{fetchTemplateMeshBuffer as z,previewLocalModel as D}from"./recipeModelPreview-DicRs3vr.js";const a=1024,M="#c06205";function U(l){return String(l??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function F(l){const s=l.toDataURL("image/png"),o=s.indexOf(",");return o>=0?s.slice(o+1):s}function $(l,s){return new Promise((o,i)=>{l.toBlob(t=>{if(!t){i(new Error("贴图导出失败。"));return}o(new File([t],s,{type:"image/png"}))},"image/png")})}function G(l){var f,b,y,E,h,w,I,L;const s=l.templateId??"FriedFishCake";let o=l.initialColor??M;S("🎨 夹心贴图编辑器",`<p class="modal-hint">产出一张 ${a}×${a} PNG 贴图，贴到模板网格
      <code>${U(s)}.fbx</code> 上。纯色即可用（官方夹心贴图本身就是纯色）；
      也可上传图片或用画笔涂改。「🔍 预览模型」直接在浏览器渲染当前贴图效果，不写盘。</p>
     <div class="tex-editor">
       <div class="tex-canvas-wrap">
         <canvas id="tex-canvas" width="${a}" height="${a}"></canvas>
       </div>
       <div class="tex-tools">
         <div class="tex-row"><span class="muted">主色</span>
           <input type="color" id="tex-color" value="${U(o)}">
           <button type="button" class="m-btn small" id="tex-fill">填充整张</button>
         </div>
         <div class="tex-row"><span class="muted">上传图片</span>
           <input type="file" id="tex-upload" accept="image/png,image/jpeg,image/webp" class="rl-select">
         </div>
         <div class="tex-row"><span class="muted">画笔</span>
           <input type="range" id="tex-size" min="4" max="256" value="48">
           <span class="muted small" id="tex-size-val">48 px</span>
         </div>
         <div class="tex-row">
           <button type="button" class="m-btn small" id="tex-undo" title="撤销上一笔（最多 20 步）">↩ 撤销</button>
           <button type="button" class="m-btn small" id="tex-clear">清空为主色</button>
           <button type="button" class="m-btn small" id="tex-preview">🔍 预览模型</button>
         </div>
         <p class="muted small">提示：在左侧画布上按住拖动即可涂抹；橡皮 = 把画笔色设成主色再涂。</p>
       </div>
     </div>`,`<button type="button" class="m-btn" data-cancel>取消</button>
     <button type="button" class="m-btn primary" id="tex-ok">使用这张贴图</button>`),(f=document.querySelector(".modal-panel"))==null||f.classList.add("wide");const i=document.getElementById("tex-canvas"),t=i.getContext("2d"),p=document.getElementById("tex-size"),N=document.getElementById("tex-size-val"),g=document.getElementById("tex-color"),r=[],O=20,u=()=>{r.push(t.getImageData(0,0,a,a)),r.length>O&&r.shift()},m=e=>{t.fillStyle=e,t.fillRect(0,0,a,a)};m(o),g.addEventListener("input",()=>{o=g.value}),(b=document.getElementById("tex-fill"))==null||b.addEventListener("click",()=>{u(),m(o)}),(y=document.getElementById("tex-clear"))==null||y.addEventListener("click",()=>{u(),m(o)}),(E=document.getElementById("tex-undo"))==null||E.addEventListener("click",()=>{const e=r.pop();e&&t.putImageData(e,0,0)}),p.addEventListener("input",()=>{N.textContent=`${p.value} px`}),(h=document.getElementById("tex-upload"))==null||h.addEventListener("change",e=>{var k;const n=(k=e.target.files)==null?void 0:k[0];if(!n)return;const c=URL.createObjectURL(n),d=new Image;d.onload=()=>{URL.revokeObjectURL(c),u(),m(o);const B=Math.max(a/d.width,a/d.height),C=d.width*B,T=d.height*B;t.drawImage(d,(a-C)/2,(a-T)/2,C,T)},d.onerror=()=>{URL.revokeObjectURL(c),alert("图片解码失败，请换一张 PNG/JPG。")},d.src=c});let v=!1;const x=e=>{const n=i.getBoundingClientRect();return{x:(e.clientX-n.left)/n.width*a,y:(e.clientY-n.top)/n.height*a}},P=e=>{const{x:n,y:c}=x(e);t.lineTo(n,c),t.stroke()};i.addEventListener("pointerdown",e=>{u(),v=!0,i.setPointerCapture(e.pointerId),t.strokeStyle=o,t.lineWidth=Number(p.value),t.lineCap="round",t.lineJoin="round",t.beginPath();const{x:n,y:c}=x(e);t.moveTo(n,c),t.lineTo(n,c),t.stroke()}),i.addEventListener("pointermove",e=>{v&&P(e)}),i.addEventListener("pointerup",e=>{v=!1,i.releasePointerCapture(e.pointerId)}),(w=document.getElementById("tex-preview"))==null||w.addEventListener("click",()=>{(async()=>{const e=document.getElementById("tex-preview");e&&(e.disabled=!0,e.textContent="加载中…");try{const n=await z(s),c=await $(i,l.fileName);await D({title:`夹心模型预览 · ${s}`,buffer:n,fileName:`${s}.fbx`,textures:[c]})}catch(n){alert(n.message||"预览失败。")}finally{e&&(e.disabled=!1,e.textContent="🔍 预览模型")}})()}),(I=document.querySelector("[data-cancel]"))==null||I.addEventListener("click",()=>R()),(L=document.getElementById("tex-ok"))==null||L.addEventListener("click",()=>{(async()=>{try{const e=await $(i,l.fileName);l.onDone({base64:F(i),file:e,color:o}),R()}catch(e){alert(e.message||"贴图导出失败。")}})()})}export{a as TEXTURE_SIZE,G as openTextureEditor};
