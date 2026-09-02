const TEETH=[18,17,16,15,14,13,12,11,21,22,23,24,25,26,27,28,48,47,46,45,44,43,42,41,31,32,33,34,35,36,37,38];
function renderToothChart(root){
 if(!root||root.dataset.ready==='1')return;
 root.dataset.ready='1';root.classList.add('tooth-chart');root.setAttribute('role','group');
 if(!root.getAttribute('aria-label'))root.setAttribute('aria-label','Dental chart');
 for(const n of TEETH){const b=document.createElement('button');b.type='button';b.className='tooth-chart__tooth';b.dataset.tooth=String(n);b.setAttribute('aria-pressed','false');b.setAttribute('aria-label',`Tooth ${n}`);b.innerHTML=`<span aria-hidden="true">🦷</span><small>${n}</small>`;b.addEventListener('click',()=>{const v=b.getAttribute('aria-pressed')!=='true';b.setAttribute('aria-pressed',String(v));root.dispatchEvent(new CustomEvent('toothchange',{bubbles:true,detail:{tooth:n,selected:v}}));});root.appendChild(b);}
}
function initToothCharts(){document.querySelectorAll('[data-tooth-chart]').forEach(renderToothChart);}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',initToothCharts);else initToothCharts();
export{TEETH,renderToothChart,initToothCharts};
