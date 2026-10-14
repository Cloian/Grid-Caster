"""Build an offline, self-contained audition page and candidate bundle."""
from pathlib import Path
import base64, html, json, zipfile, wave
import numpy as np
ROOT=Path(__file__).resolve().parent
manifest=json.loads((ROOT/"asset-manifest.json").read_text())
data={"groups":manifest["groups"],"assets":[]}
for a in manifest["assets"]:
    path=ROOT/a["file"]
    with wave.open(str(path),"rb") as w:
        y=np.frombuffer(w.readframes(w.getnframes()),"<i2").astype(float)/32768
    step=max(1,len(y)//120)
    peaks=[round(float(np.max(np.abs(y[i:i+step]))),4) for i in range(0,len(y),step)][:120]
    data["assets"].append({**a,"audio":"data:audio/wav;base64,"+base64.b64encode(path.read_bytes()).decode(),"wave":peaks})
content=r"""<!doctype html>
<html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Grid-Caster · 효과음 후보 64</title>
<style>
:root{color-scheme:dark;--bg:#101719;--panel:#192426;--line:#35464a;--text:#ecede5;--muted:#a6b9b8;--cyan:#82d6cc;--gold:#dcc18b}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:15px/1.65 system-ui,-apple-system,"Apple SD Gothic Neo",sans-serif}
main{max-width:1160px;margin:auto;padding:36px 24px}h1{margin:0;font-size:30px;letter-spacing:-1px}p{color:var(--muted);margin:8px 0 20px}
header{display:flex;align-items:start;justify-content:space-between;gap:20px}.badge{font-size:12px;color:var(--gold);letter-spacing:1.5px}
button{font:inherit;cursor:pointer;background:#263638;color:var(--text);border:1px solid var(--line);padding:9px 13px;border-radius:8px}
button:hover{border-color:var(--cyan)}button:focus-visible,a:focus-visible,input:focus-visible{outline:2px solid var(--cyan);outline-offset:3px}
button.active,button.playing{background:#294f4d;border-color:var(--cyan);color:#e4fff9}.tabs{display:flex;gap:7px;flex-wrap:wrap;margin:20px 0}
.toolbar{display:flex;gap:9px;align-items:center;flex-wrap:wrap;background:var(--panel);padding:15px;border-radius:12px;border:1px solid var(--line)}
.toolbar label{color:var(--muted);margin-left:auto}input[type=range]{width:110px;vertical-align:middle;accent-color:var(--cyan)}
.category{display:flex;justify-content:space-between;align-items:center;margin:28px 0 12px}.category h2{font-size:21px;margin:0}
#status{color:var(--cyan);font-size:14px;min-height:25px;margin:8px 0}
.grid{display:grid;grid-template-columns:1fr 1fr;gap:14px}.card{padding:18px;border:1px solid var(--line);background:var(--panel);border-radius:12px}
.card.selected{border-color:var(--gold)}.card-top{display:flex;justify-content:space-between;align-items:center}
.id{color:var(--cyan);font-size:13px;font-weight:700;letter-spacing:1px}.card h3{font-size:19px;margin:2px 0}
.duration{color:var(--muted);font-size:13px}.wave{height:34px;display:flex;gap:2px;align-items:center;opacity:.7;margin:15px 0}
.wave i{background:var(--cyan);flex:1;min-height:2px;border-radius:1px}.card.selected .wave i{background:var(--gold)}
.actions{display:flex;gap:7px;align-items:center;flex-wrap:wrap}.actions .choose{margin-left:auto;color:var(--gold)}
.meta{color:var(--muted);font-size:12px;margin-top:10px}.selection{margin-top:28px;padding:18px;border:1px solid var(--line);border-radius:12px}
.selection h2{font-size:18px;margin:0 0 8px}.chips{display:flex;gap:8px;flex-wrap:wrap}.chip{background:#243436;border-radius:7px;padding:6px 10px}
.selection-actions{display:flex;gap:8px;margin-top:15px;flex-wrap:wrap}footer{color:var(--muted);font-size:12px;margin:28px 0}a{color:var(--cyan)}
@media(max-width:720px){main{padding:24px 16px}header{display:block}.grid{grid-template-columns:1fr}.toolbar label{margin-left:0}h1{font-size:26px}.tabs button{font-size:13px;padding:8px}.toolbar button{font-size:14px}}
</style>
<main>
<header><div><div class="badge">GRID-CASTER · SOUND AUDITION V2</div><h1>효과음 후보 64개</h1><p>8종류 × 각 8개. 후보마다 재료와 동작 구성을 다르게 만들었습니다.</p></div><button id="stop">모두 정지</button></header>
<nav class="tabs" aria-label="효과음 종류" id="tabs"></nav>
<div class="toolbar"><button id="groupPlay">현재 종류 8개 비교</button><button id="chosenPlay">선택한 조합 듣기</button><span class="duration">연속 비교는 각 후보를 2회씩 재생합니다.</span><label>음량 <input id="volume" aria-label="음량" type="range" min="0" max="1" step=".05" value=".7"></label></div>
<div id="status" role="status" aria-live="polite">종류를 고르고 재생을 누르세요. 자동 재생하지 않습니다.</div>
<div class="category"><h2 id="categoryTitle"></h2><span class="duration">숫자 1–8: 재생 · Esc: 정지</span></div>
<section class="grid" id="cards" aria-label="후보"></section>
<section class="selection"><h2>선택한 후보</h2><div id="selections" class="chips"></div><div class="selection-actions"><button id="copy">결과 복사</button><button id="export">선택 저장</button><button id="clear">선택 초기화</button></div></section>
<footer>녹음 소스 라이브러리(CC0)와 직접 합성한 레이어를 편집해 만든 청취 시안입니다. 게임에는 아직 연결하지 않았습니다.<br>
소스: <a href="https://kenney.nl/assets/rpg-audio" target="_blank" rel="noreferrer">Kenney RPG Audio</a> ·
<a href="https://kenney.nl/assets/impact-sounds" target="_blank" rel="noreferrer">Kenney Impact Sounds</a> ·
<a href="https://opengameart.org/content/fire-crackling/" target="_blank" rel="noreferrer">AntumDeluge Fire Crackling</a><br>
파일 안에 오디오가 포함되어 있어 인터넷 없이 사용할 수 있습니다. 선택 결과는 이 브라우저에 저장됩니다.</footer>
</main>
<script id="data" type="application/json">__DATA__</script>
<script>
const DATA=JSON.parse(document.getElementById('data').textContent);
let current='M',token=0,selection={},releaseWait=null;
try{selection=JSON.parse(localStorage.getItem('gridcaster-sfx-v2')||'{}')}catch{}
const player=new Audio(); player.preload='none';player.volume=.7;
const el=id=>document.getElementById(id);
function message(t){el('status').textContent=t}
function stop(){token++;player.pause();player.currentTime=0;if(releaseWait){releaseWait(false);releaseWait=null}player.onended=null;player.onerror=null;document.querySelectorAll('.playing').forEach(x=>x.classList.remove('playing'))}
function label(a){return a.id+' · '+a.name}
function persist(){try{localStorage.setItem('gridcaster-sfx-v2',JSON.stringify(selection))}catch{}}
function selectedAssets(){return DATA.groups.flatMap(g=>DATA.assets.filter(a=>a.id===selection[g.id]))}
function renderSelections(){
 const a=selectedAssets();el('selections').innerHTML='';
 if(!a.length){el('selections').textContent='아직 선택한 후보가 없습니다.';return}
 a.forEach(x=>{const span=document.createElement('span');span.className='chip';span.textContent=x.category+': '+label(x);el('selections').append(span)})
}
async function playSequence(assets,repeats=1){
 stop();const mine=token;
 for(const a of assets){for(let r=0;r<repeats;r++){
  if(mine!==token)return;message(label(a)+(repeats>1?' · '+(r+1)+'/'+repeats:''));
  player.src=a.audio;player.volume=Number(el('volume').value);
  const done=new Promise(resolve=>{releaseWait=resolve;player.onended=()=>{releaseWait=null;resolve(true)};player.onerror=()=>{releaseWait=null;resolve(false)}});
  try{await player.play()}catch{message('재생 버튼을 다시 눌러 주세요.');return}
  const ok=await done;if(mine!==token)return;if(!ok){message('오디오를 재생하지 못했습니다.');return}
  await new Promise(resolve=>setTimeout(resolve,r<repeats-1?350:750));
 }}
 if(mine===token)message('재생 완료');
}
function switchGroup(id){
 stop();current=id;document.querySelectorAll('.tabs button').forEach(b=>b.classList.toggle('active',b.dataset.id===id));
 renderCards();message('각 후보를 듣고 사용할 후보 하나를 선택하세요.');
}
function renderCards(){
 const g=DATA.groups.find(g=>g.id===current);el('categoryTitle').textContent=g.title;el('cards').innerHTML='';
 DATA.assets.filter(a=>a.group===current).forEach(a=>{
  const c=document.createElement('article');c.className='card'+(selection[current]===a.id?' selected':'');
  c.innerHTML='<div class="card-top"><div><span class="id">'+a.id+'</span><h3>'+a.name+'</h3></div><span class="duration">'+a.duration_seconds.toFixed(2)+'초</span></div>';
  const wave=document.createElement('div');wave.className='wave';wave.setAttribute('aria-hidden','true');
  a.wave.filter((_,i)=>i%2===0).forEach(v=>{const x=document.createElement('i');x.style.height=Math.max(2,v*36)+'px';wave.append(x)});c.append(wave);
  const actions=document.createElement('div');actions.className='actions';
  [['재생',()=>playSequence([a])],['2회 듣기',()=>playSequence([a],2)]].forEach(([text,fn])=>{const b=document.createElement('button');b.textContent=text;b.addEventListener('click',fn);actions.append(b)});
  const choose=document.createElement('button');choose.className='choose';choose.textContent=selection[current]===a.id?'✓ 선택됨':'이 후보 선택';choose.setAttribute('aria-pressed',selection[current]===a.id?'true':'false');
  choose.onclick=()=>{selection[current]=a.id;persist();renderCards();renderSelections()};actions.append(choose);c.append(actions);
  const meta=document.createElement('div');meta.className='meta';const roles=[...new Set(a.layers.map(l=>l.role))];
  meta.textContent='재료 '+a.sources.length+'종 · 레이어 '+a.layers.length+'개';c.append(meta);el('cards').append(c);
 });
}
DATA.groups.forEach(g=>{const b=document.createElement('button');b.textContent=g.title;b.dataset.id=g.id;b.classList.toggle('active',g.id===current);b.onclick=()=>switchGroup(g.id);el('tabs').append(b)});
el('stop').onclick=()=>{stop();message('정지했습니다.')};
el('volume').oninput=()=>{player.volume=Number(el('volume').value)};
el('groupPlay').onclick=()=>playSequence(DATA.assets.filter(a=>a.group===current),2);
el('chosenPlay').onclick=()=>{const a=selectedAssets();if(!a.length){message('후보를 먼저 선택하세요.');return}playSequence(a,2)};
function selectionText(){return selectedAssets().map(a=>a.category+': '+a.id+' '+a.name).join('\n')}
el('copy').onclick=async()=>{const t=selectionText();if(!t){message('후보를 먼저 선택하세요.');return}try{await navigator.clipboard.writeText(t);message('선택 결과를 복사했습니다.')}catch{message(t.replaceAll('\n',' / '))}};
el('export').onclick=()=>{const obj=selectedAssets().map(a=>({id:a.id,category:a.category,name:a.name,file:a.file}));const url=URL.createObjectURL(new Blob([JSON.stringify(obj,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='gridcaster-sfx-selection.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000)};
el('clear').onclick=()=>{selection={};persist();renderCards();renderSelections();message('선택을 초기화했습니다.')};
document.addEventListener('keydown',e=>{if(e.key==='Escape'){stop();message('정지했습니다.')}else if(/^[1-8]$/.test(e.key)&&!['INPUT','TEXTAREA'].includes(e.target.tagName)){const a=DATA.assets.filter(a=>a.group===current)[Number(e.key)-1];playSequence([a])}});
renderCards();renderSelections();
window.audition={data:DATA,player,stop,switchGroup,playSequence,selection};
</script></html>"""
content=content.replace("__DATA__",json.dumps(data,ensure_ascii=False).replace("</","<\\/"))
(ROOT/"audition.html").write_text(content,encoding="utf-8")
print("HTML",len(content.encode()),"bytes;",len(data["assets"]),"embedded candidates")
