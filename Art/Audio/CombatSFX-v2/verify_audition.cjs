const fs=require('fs');
const path=require('path');
const {pathToFileURL}=require('url');
const {chromium}=require('/Users/gimdong-yun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
 const root=__dirname;
 const browser=await chromium.launch({headless:true,executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'});
 const page=await browser.newPage({viewport:{width:1280,height:1000},deviceScaleFactor:1});
 const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(pathToFileURL(path.join(root,'audition.html')).href);
 const decoded=await page.evaluate(async()=>{
  const ctx=new AudioContext({sampleRate:48000});
  const results=[];
  for(const a of window.audition.data.assets){
   const b64=a.audio.split(',')[1];
   const bytes=Uint8Array.from(atob(b64),c=>c.charCodeAt(0));
   const buffer=await ctx.decodeAudioData(bytes.buffer);
   if(buffer.numberOfChannels!==1||Math.abs(buffer.duration-a.duration_seconds)>.002)throw Error(a.id+' decode mismatch');
   results.push({id:a.id,duration:buffer.duration});
  }
  await ctx.close();return results;
 });
 if(decoded.length!==64)throw Error('expected 64 audio candidates');
 if(await page.locator('.tabs button').count()!==8)throw Error('tabs missing');
 if(await page.locator('.card').count()!==8)throw Error('cards missing');
 await page.locator('.card').nth(2).getByText('이 후보 선택',{exact:true}).click();
 await page.locator('.tabs button').getByText('쇠뇌 장전',{exact:true}).click();
 await page.locator('.card').nth(4).getByText('이 후보 선택',{exact:true}).click();
 await page.locator('.card').first().getByText('재생',{exact:true}).click();
 await page.waitForTimeout(70);
 const playing=await page.evaluate(()=>!window.audition.player.paused&&window.audition.player.currentTime>0);
 if(!playing)throw Error('individual playback failed');
 await page.getByRole('button',{name:'모두 정지',exact:true}).click();
 const stopped=await page.evaluate(()=>window.audition.player.paused);
 if(!stopped)throw Error('stop failed');
 await page.reload();
 const saved=await page.locator('#selections').innerText();
 if(!saved.includes('M3')||!saved.includes('C5'))throw Error('selection persistence failed');
 await page.evaluate(()=>{window.audition.switchGroup('M');});
 await page.screenshot({path:path.join(root,'audition-preview.png'),fullPage:true});
 await page.evaluate(()=>{localStorage.removeItem('gridcaster-sfx-v2')});
 if(errors.length)throw Error(errors.join('\n'));
 const report={passed:true,decoded_candidates:decoded.length,categories:8,cards_per_category:8,individual_playback:playing,stop:stopped,selection_persistence:true,browser_errors:errors};
 fs.writeFileSync(path.join(root,'browser-qa.json'),JSON.stringify(report,null,2));
 console.log(JSON.stringify(report,null,2));
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});

