// Inspect the served visualize preview, including its sandboxed iframe.
// Usage: bundled node Tools/graybox/queen-smoke.cjs http://127.0.0.1:8767/
const { chromium } = require(process.env.QUEEN_PLAYWRIGHT_PATH || '/Users/gimdong-yun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs = require('fs');
const path = require('path');
(async () => {
  const browser = await chromium.launch({headless:true, executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'});
  try {
    const page = await browser.newPage({viewport:{width:780,height:1250}}), errors=[];
    page.on('pageerror',e=>errors.push(e.message));
    await page.goto(process.argv[2] || 'http://127.0.0.1:8767/');
    const frame=page.frames().find(f=>f.parentFrame());
    await frame.waitForFunction(()=>window.QueenGraybox);
    const report=await frame.evaluate(()=>{
      const E=window.QueenGraybox.Engine,checks=[];
      const check=(name,condition,detail)=>checks.push({name,pass:!!condition,detail});
      for(const type of ['rook','bishop','knight','none']) for(const level of type==='none'?[1]:[1,2,3]) for(const pattern of ['single','relay']) {
        const s=E.initial(pattern,type,level),route=E.routeFor(s);
        check('무피격 경로 '+pattern+' '+type+' Lv.'+level,!!route,route);
      }
      let s=E.initial();s.player={x:3,y:6};check('룩은 중간 벽 통과 불가',!E.movement(s,{x:3,y:4}).ok);
      s.mobility='knight';check('나이트 빈 L자 착지 가능',E.movement(s,{x:4,y:4}).ok);
      s=E.initial();check('룩 긴 대각선 불가',!E.movement(s,{x:3,y:12}).ok);
      s.mobility='bishop';check('비숍 긴 직선 불가',!E.movement(s,{x:5,y:12}).ok);
      s=E.initial();check('첫 근위병에서 사격 정지',E.ray(s,{x:5,y:0}).target.type==='guard');
      s.charge=1;const hit=E.advance(s,{type:'attack',to:{x:5,y:0}}).state;
      check('직격 충전 행동당 한 번',hit.charge===2);
      check('공격 뒤 적 단계 한 번',hit.turn===1&&hit.step===1&&hit.guards.find(g=>g.id===1).y===9);
      const miss=E.advance(s,{type:'attack',to:{x:5,y:12}}).state;check('빗나감 충전 없음',miss.charge===1&&miss.turn===1);
      s.charge=0;check('미준비 특성은 행동을 쓰지 않음',!E.advance(s,{type:'special',to:{x:5,y:0}}).ok);
      s=E.initial();s.player={x:6,y:6};s.guards=[];s.bossHP=6;
      const win=E.advance(s,{type:'special',to:{x:6,y:0}}).state;
      check('큰 보스 직격 한 번·처치 후 공격 종료',win.bossHP===0&&win.end==='win'&&win.turn===1&&win.hp===12);
      s=E.initial();s=E.advance(s,{type:'wait'}).state;s=E.advance(s,{type:'wait'}).state;
      check('첫 전장 공격 두 번째 행동 뒤',s.hp===9&&s.step===2);
      s=E.advance(s,{type:'wait'}).state;check('둘째 공격 세 번째 행동 뒤',s.hp===6&&s.step===3);
      check('여왕 광역은 근위병도 처치',s.guards.length===0);
      s=E.initial('zone');const zone=JSON.stringify(s.zone);
      s=E.advance(s,{type:'move',to:{x:6,y:10}}).state;check('예고 위치 고정',JSON.stringify(s.zone)===zone);
      s=E.initial();s.guards=[{id:1,x:5,y:9,hp:3,dx:0,dy:1}];
      s=E.advance(s,{type:'wait'}).state;check('근위병 정면 공격과 이동은 하나만',s.hp===11&&s.guards[0].y===9);
      check('템플릿의 모든 안전 칸은 전장·벽·여왕과 충돌하지 않음',E.TEMPLATES.every(t=>E.validAreas(t.A)&&E.validAreas(t.B)));
      check('템플릿 A·B 공통 안전 칸 없음',E.TEMPLATES.every(t=>!E.overlap(t.A,t.B)));
      const coverage=[];
      for(const [pattern,type] of [['relay','rook'],['relay','bishop'],['relay','knight'],['relay','none'],['single','none']]) {
        const unsupported=[];let count=0;
        for(let y=0;y<13;y++)for(let x=0;x<13;x++) {
          const q=E.initial(pattern,type,3);q.guards=[];q.player={x,y};q.round=2;
          if(E.inQueen(q.player)||E.WALLS.some(w=>w.x===x&&w.y===y))continue;
          count++;const next=E.nextRound(q);
          if(next.end||!E.routeFor(next,true))unsupported.push({x,y});
        }
        coverage.push({pattern,type,count,unsupported});
        check('빈 전장 모든 합법 위치의 변경 구역·반격 경로 '+pattern+' '+type,!unsupported.length,unsupported);
      }
      const zoneChanges=[];
      for(const pattern of ['relay','single']) for(const type of ['rook','bishop','knight','none']) for(const level of type==='none'||type==='knight'?[1]:[1,2,3]) for(const seed of [173821,17,991]) {
        let q=E.initial(pattern,type,level,seed),ok=true,shot=true,displaced=true,locked=true,valid=true;
        const footprints=[],timings=[];
        for(let i=0;i<12;i++) {
          const plan=E.routeFor(q,true);if(!plan){ok=false;break;}
          const warning=JSON.stringify([q.A,q.B]);
          for(const a of plan){q=E.advance(q,a).state;locked&&=JSON.stringify([q.A,q.B])===warning;}
          shot&&=E.hasQueenShot(q);ok&&=q.hp===12&&!q.end;
          // Finishing in B (or single A) is safe now, but camping there must fail at the next A.
          const previous=JSON.parse(JSON.stringify(pattern==='relay'?q.B:q.A));
          while(q.step!==0&&!q.end)q=E.advance(q,{type:'wait'}).state;
          const started=performance.now();q=E.nextRound(q);timings.push(performance.now()-started);
          displaced&&=!E.overlap(previous,q.A);
          valid&&=E.validAreas(q.A)&&E.validAreas(q.B)&&!E.overlap(q.A,q.B);
          const camp=E.advance(E.advance(q,{type:'wait'}).state,{type:'wait'}).state;
          displaced&&=camp.hp<q.hp;
          footprints.push(JSON.stringify([q.A,q.B]));
          if(q.end){ok=false;break;}
        }
        const label=pattern+' '+type+' Lv.'+level+' seed '+seed;
        check('변경 구역 무피격·반격·고정·대기 실패 '+label,ok&&shot&&displaced&&locked&&valid);
        check('12회 순환 배치 다양성 '+label,new Set(footprints).size>=8,{unique:new Set(footprints).size});
        zoneChanges.push({pattern,type,level,seed,round:q.round,unique:new Set(footprints).size,maxWarningMs:Math.round(Math.max(...timings)),ok,shot,displaced,locked,valid});
      }
      const cycles=[];
      for(const type of ['rook','bishop','knight','none']) {
        let q=E.initial('relay',type,3);
        for(let i=0;i<8;i++) {
          const plan=E.routeFor(q);if(!plan){cycles.push({type,i,error:'no route'});break;}
          for(const a of plan) q=E.advance(q,a).state;
          q=E.advance(q,{type:'wait'}).state;q=E.nextRound(q);
          cycles.push({type,round:q.round,layout:q.layout,hp:q.hp});
        }
        check('여덟 순환 경로 '+type,q.hp===12&&q.round===9);
      }
      const fights=[];
      for(const type of ['rook','bishop','knight']) {
        let q=E.initial('relay',type,3);
        for(let i=0;i<100&&!q.end;i++) {
          let chosen=null;
          if(q.step===0||q.step===3) {
            for(const [dx,dy] of [[0,-1],[1,-1],[1,0],[1,1],[0,1],[-1,1],[-1,0],[-1,-1]]) {
              const to={x:q.player.x+dx,y:q.player.y+dy},r=E.ray(q,to);
              if(!r.target||!['queen','guard'].includes(r.target.type))continue;
              const action={type:q.charge===3?'special':'attack',to},out=E.advance(q,action);
              if(out.ok&&out.state.hp===q.hp&&(out.state.end==='win'||E.routeFor(out.state))) {chosen=action;break;}
            }
          }
          if(!chosen) chosen=E.routeFor(q)?.[0]||{type:'wait'};
          q=E.advance(q,chosen).state;
          if(q.step===0&&!q.end)q=E.nextRound(q);
        }
        fights.push({type,end:q.end,hp:q.hp,turn:q.turn,bossHits:q.bossHits});
        check('모형 보스 처치 가능 '+type,q.end==='win');
      }
      return {checks,coverage,zoneChanges,cycles,fights,templateCount:E.TEMPLATES.length};
    });
    await frame.locator('[data-action="restart"]').click();
    const turn=(await frame.evaluate(()=>window.QueenGraybox.getState())).turn;
    await page.waitForTimeout(550);
    report.checks.push({name:'입력 없는 시간에는 턴 정지',pass:(await frame.evaluate(()=>window.QueenGraybox.getState())).turn===turn});
    await frame.locator('[data-action="attack"]').click();
    await frame.locator('[data-control="x"]').selectOption({value:'5'});await frame.locator('[data-control="y"]').selectOption({value:'0'});
    report.checks.push({name:'선택·조준은 턴을 쓰지 않음',pass:(await frame.evaluate(()=>window.QueenGraybox.getState())).turn===turn});
    await frame.locator('[data-action="confirm"]').click();
    report.checks.push({name:'확정 버튼 행동 한 번',pass:(await frame.evaluate(()=>window.QueenGraybox.getState())).turn===turn+1});
    await frame.locator('[data-action="restart"]').click();
    const clickPoint=await frame.locator('[data-view="board"]').evaluate(el=>{const r=el.getBoundingClientRect(),cell=(r.width-36)/13;return {x:18+4.5*cell,y:18+9.5*cell};});
    await frame.locator('[data-view="board"]').click({position:clickPoint});
    report.checks.push({name:'보드 클릭 좌표 선택',pass:(await frame.evaluate(()=>window.QueenGraybox.getSelection())).x===4&&(await frame.evaluate(()=>window.QueenGraybox.getState())).turn===0});
    await frame.locator('[data-action="confirm"]').press('Enter');
    report.checks.push({name:'키보드 확정 이동 한 번',pass:(await frame.evaluate(()=>window.QueenGraybox.getState())).turn===1&&(await frame.evaluate(()=>window.QueenGraybox.getState())).player.x===4});
    await frame.locator('[data-action="confirm"]').press('Escape');
    report.checks.push({name:'취소는 행동을 소비하지 않음',pass:(await frame.evaluate(()=>window.QueenGraybox.getState())).turn===1});
    await frame.locator('[data-control="pattern"]').selectOption({value:'zone'});
    report.checks.push({name:'비교 패턴 변경·전투 초기화',pass:(await frame.evaluate(()=>window.QueenGraybox.getState())).pattern==='zone'&&(await frame.evaluate(()=>window.QueenGraybox.getState())).turn===0});
    await frame.locator('[data-control="pattern"]').selectOption({value:'relay'});
    await frame.evaluate(()=>{const api=window.QueenGraybox;for(const a of api.Engine.routeFor(api.getState(),true))api.perform(a);api.perform({type:'wait'});});
    const dir=path.resolve('docs/verification'),capture=path.join(dir,'captures');fs.mkdirSync(capture,{recursive:true});
    await page.screenshot({path:path.join(capture,'queen-graybox-desktop-2026-10-12.png'),fullPage:true});
    report.layouts=[];
    for(const width of [736,320]) {
      await page.setViewportSize({width,height:1400});await page.waitForTimeout(150);
      report.layouts.push(await frame.evaluate(()=>({width:innerWidth,scrollWidth:document.documentElement.scrollWidth,overflow:document.documentElement.scrollWidth>innerWidth,boardWidth:document.querySelector('.qg-board').getBoundingClientRect().width})));
    }
    await page.screenshot({path:path.join(capture,'queen-graybox-narrow-2026-10-12.png'),fullPage:true});
    report.errors=errors;report.ok=report.checks.every(c=>c.pass)&&!errors.length&&report.layouts.every(v=>!v.overflow);
    fs.writeFileSync(path.join(dir,'queen-graybox-2026-10-12.json'),JSON.stringify(report,null,2)+'\n');
    console.log(JSON.stringify({ok:report.ok,checks:report.checks.length,failed:report.checks.filter(c=>!c.pass),templateCount:report.templateCount,fights:report.fights,errors,layouts:report.layouts,maxWarningMs:Math.max(...report.zoneChanges.map(r=>r.maxWarningMs))}));if(!report.ok)process.exitCode=1;
  } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
