// 사용자의 브라우저와 분리된 Chrome 터치 에뮬레이션. iPhone/Safari 실기기 결과가 아니다.
import { createRequire } from 'node:module';
import fs from 'node:fs/promises';
import path from 'node:path';
const require=createRequire(import.meta.url);
const { chromium }=require(process.argv[3]||'playwright');
const output=path.resolve(process.argv[4]||'Builds/MobileWeb/verification');
await fs.mkdir(output,{recursive:true});
const browser=await chromium.launch({executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
const context=await browser.newContext({viewport:{width:390,height:844},deviceScaleFactor:2,isMobile:true,hasTouch:true,userAgent:'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1'});
const page=await context.newPage();
const logs=[],errors=[],checks=[];
page.on('console',message=>{logs.push(message.text());if(message.type()==='error')errors.push(message.text());});
page.on('pageerror',error=>errors.push(error.message));
async function snapshot(){
  const before=logs.length;
  await page.evaluate(()=>instance.SendMessage('GameHUD','ReportPlaytestState'));
  const limit=Date.now()+10000;
  while(Date.now()<limit){const item=logs.slice(before).find(item=>item.includes('MOBILE_PLAYTEST_STATE '));if(item)return JSON.parse(item.slice(item.indexOf('MOBILE_PLAYTEST_STATE ')+22));await new Promise(resolve=>setTimeout(resolve,50));}
  throw new Error('No runtime state snapshot');
}
async function capture(name){await page.screenshot({path:path.join(output,name+'.png')});}
function check(result,name){if(!result)throw new Error(name);checks.push('PASS '+name);console.log('PASS',name);}
async function tap(x,y){await page.touchscreen.tap(x,y);await page.waitForTimeout(1000);return snapshot();}
// 읽기 전용 화면 좌표로 실제 UI를 터치한다. DPR/브라우저 높이 차이를 반영한다.
async function tapUi(name){const s=await snapshot();const t=s.ui.find(t=>t.name===name);if(!t)throw new Error('No visible UI: '+name);const box=await page.locator('#game').boundingBox();return tap(box.x+t.x*box.width/s.screenWidth,box.y+t.y*box.height/s.screenHeight);}
async function tapBoard(dx,dy){const s=await snapshot();const box=await page.locator('#game').boundingBox();return tap(box.x+(s.playerScreenX+dx*s.tilePixels)*box.width/s.screenWidth,box.y+(s.playerScreenY-dy*s.tilePixels)*box.height/s.screenHeight);}
async function waitState(predicate){const limit=Date.now()+10000;let state;do{state=await snapshot();if(predicate(state))return state;await page.waitForTimeout(200);}while(Date.now()<limit);throw new Error('State timeout: '+JSON.stringify(state));}
try{
  await page.goto(process.argv[2]||'http://127.0.0.1:8765');
  await page.getByRole('button',{name:'터치하여 시작'}).tap();
  await page.waitForFunction(()=>typeof instance!=='undefined'&&instance,{timeout:180000});
  await page.waitForTimeout(8000);
  await capture('01-start');
  if(process.argv[5]!=='visual-only'){
    let state=await snapshot();check(state.startSelection&&state.worldTurns===0,'start trait selection visible, world frozen');
    await tap(195,210);state=await waitState(s=>!s.startSelection&&s.rewardSelection);await capture('trait-tap');check(!state.startSelection&&state.rewardSelection,'touch selects trait and opens starter reward');
    await capture('02-starter-reward');
    state=await tap(318,140);check(state.rewardSelection&&!state.canAct,'reward details keeps selection and world frozen');
    await capture('03-details');
    state=await tap(195,734);check(state.rewardSelection,'close details preserves reward');
    state=await tap(195,215);check(!state.rewardSelection&&state.canAct,'touch reward starts combat');
    await capture('04-gameplay');
    const start=state;
    check(!state.ui.some(t=>t.name==='Cancel'),'idle screen has no persistent cancel button');
    check(state.board[2]>=state.screenWidth*0.98,'full board uses nearly all portrait width');
    for(const name of ['Build','Relic','Menu']){const t=state.ui.find(t=>t.name===name);check(t.y+t.height/2<=state.board[1],'secondary control outside board: '+name);}
    state=await tapUi('Special');check(state.action==='None'&&state.worldTurns===0,'unready special button cannot attack');
    state=await tapUi('Attack');check(state.action==='Attack'&&!state.preview,'attack button selects aim only');
    state=await tapBoard(4,0);check(state.preview&&state.worldTurns===0,'board touch previews without advancing turn');
    const cancel=state.ui.find(t=>t.name==='Cancel');
    const special=state.ui.find(t=>t.name==='Special');
    check(cancel.y-cancel.height/2>=state.board[1]+state.board[3]&&cancel.y+cancel.height/2<=special.y-special.height/2,'cancel stays outside board and does not overlap action button');
    await capture('05-attack-preview');
    state=await tapUi('Cancel');check(!state.preview&&state.worldTurns===0,'cancel is free');
    await tapUi('Attack');await tapBoard(4,0);state=await tapUi('Attack');
    await page.waitForTimeout(800);state=await snapshot();
    check(state.worldTurns===1&&!state.preview&&state.canAct,'confirm advances exactly one world turn');
    await tapUi('Attack');state=await snapshot();check(state.worldTurns===1,'second action button tap without preview cannot duplicate action');
    state=await tapUi('Build');check(state.drawerOpen&&!state.canAct,'build drawer blocks board');
    await capture('06-build-drawer');
    state=await tap(260,350);check(state.worldTurns===1,'drawer tap cannot advance board');
    await tapUi('Close');state=await waitState(s=>!s.drawerOpen&&s.canAct);check(!state.drawerOpen&&state.canAct,'drawer closes without click-through');
    state=await tapUi('Menu');check(state.paused&&!state.canAct,'touch menu pauses');
    await capture('07-pause');
    state=await tapUi('ContinueButton');check(!state.paused&&state.canAct,'touch continue resumes');
    await tapUi('Move');state=await tapBoard(0,1);
    check(state.preview&&state.x===start.x&&state.y===start.y,'movement touch previews a neighboring tile');
    check(state.movePreviewVisible&&state.pendingX===state.x&&state.pendingY===state.y+1,'selected move has ghost at exact landing tile');
    check(state.caption.includes('위 1칸'),'direction is plain-language rather than coordinates');
    await capture('selected-move');
    await tapUi('Move');state=await waitState(s=>s.worldTurns===2&&s.canAct);
    check(state.x===start.x&&state.y===start.y+1&&state.worldTurns===2,'touch move executes one tile and one world turn');
    await page.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,value:true});document.dispatchEvent(new Event('visibilitychange'));});
    state=await snapshot();check(state.paused&&state.worldTurns===2,'synthetic hidden-tab event invokes pause hook without a turn');
    await page.evaluate(()=>{delete document.hidden;document.dispatchEvent(new Event('visibilitychange'));});
    check((await snapshot()).paused,'returning to foreground does not auto-resume');
    state=await tapUi('ContinueButton');check(!state.paused,'resume after hidden-tab simulation is explicit');
    await page.evaluate(()=>document.dispatchEvent(new Event('visibilitychange')));
    check((await snapshot()).worldTurns===2,'foreground visibility event does not advance turn');
    await page.setViewportSize({width:375,height:667});await page.waitForTimeout(700);await capture('08-small-portrait');
    await page.setViewportSize({width:390,height:660});await page.waitForTimeout(1000);await capture('09-inapp-height');
    state=await snapshot();
    for(const name of ['Move','Attack','Special']){const t=state.ui.find(t=>t.name===name);check(t.y-t.height/2>=state.board[1]+state.board[3]&&t.y+t.height/2<=state.screenHeight,'in-app height keeps action outside board and on screen: '+name);}
    await page.setViewportSize({width:844,height:390});await page.waitForTimeout(700);
    check(await page.locator('#rotate').isVisible(),'landscape requests portrait orientation');
    await page.setViewportSize({width:390,height:844});await page.waitForTimeout(700);
    check((await snapshot()).worldTurns===2,'resize does not advance turn');
    check(start.hp===10,'new run retains original health');
  }
  await fs.writeFile(path.join(output,'browser-log.json'),JSON.stringify({engine:'Chrome mobile emulation, not Safari/iPhone',checks,logs,errors},null,2));
  if(errors.length)throw new Error(errors.join('\n'));
}finally{
  await fs.writeFile(path.join(output,'browser-log.json'),JSON.stringify({engine:'Chrome mobile emulation, not Safari/iPhone',checks,logs,errors},null,2));
  await browser.close();
}
