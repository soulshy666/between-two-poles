const {chromium}=require('C:/Users/周雨杰/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const path=require('path');
(async()=>{
 const browser=await chromium.launch({headless:true,channel:'msedge'});
 const page=await browser.newPage({viewport:{width:820,height:720},deviceScaleFactor:1});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('file:///'+path.join(__dirname,'preview.html').replace(/\\/g,'/'));
 const f=page.frameLocator('iframe');
 await f.locator('[data-ready="true"]').waitFor();
 await f.locator('#btp-astronaut-design').screenshot({path:path.join(__dirname,'review-three.png')});
 for(const view of ['front','back','game']){
  await f.locator('[data-view="'+view+'"]').click();
  await f.locator('#btp-astronaut-design').screenshot({path:path.join(__dirname,'review-'+view+'.png')});
 }
 await f.locator('[data-pixel]').check();
 await f.locator('#btp-astronaut-design').screenshot({path:path.join(__dirname,'review-pixel.png')});
 await page.setViewportSize({width:360,height:720});
 await f.locator('[data-view="three"]').click();
 await f.locator('#btp-astronaut-design').screenshot({path:path.join(__dirname,'review-mobile.png')});
 const frame=page.frames().find(x=>x!==page.mainFrame());
 const check=await frame.evaluate(()=>({scroll:document.documentElement.scrollWidth,width:innerWidth,caption:document.querySelector('[data-caption]').textContent,parts:window.AstronautDesign.parts.length,pixel:document.querySelector('[data-pixel]').checked}));
 console.log(JSON.stringify({errors,check},null,2));
 await browser.close();if(errors.length)process.exitCode=1;
})();
