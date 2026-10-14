const fs = require('node:fs');
const {pathToFileURL} = require('node:url');
const {chromium} = require('/Users/gimdong-yun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
const root=__dirname;
for(const name of ['comparison.png','comparison-120px-at-32ppu.png']) if(fs.existsSync(root+'/'+name)) throw new Error('Output already exists: '+name);
const browser=await chromium.launch({headless:true,executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'});
try{
const page=await browser.newPage({viewport:{width:1560,height:1040},deviceScaleFactor:1});
await page.goto(pathToFileURL(root+'/comparison.html').href);
await page.waitForFunction(()=>Array.from(document.images).every(i=>i.complete&&i.naturalWidth>0));
await page.screenshot({path:root+'/comparison.png',fullPage:true});
await page.locator('[data-scale="3.75"]').click();
await page.screenshot({path:root+'/comparison-120px-at-32ppu.png',fullPage:true});
console.log(JSON.stringify({loadedImages:await page.locator('img').count(),pageTitle:await page.title(),output:['comparison.png','comparison-120px-at-32ppu.png']}));
}finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});
