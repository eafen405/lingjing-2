// One-off browser observation of the throwaway model; not a test framework.
const fs=require('fs'),path=require('path'),os=require('os');
const {spawn}=require('child_process');const {pathToFileURL}=require('url');
const output=__dirname,file=path.resolve(__dirname,'../enemy-behavior.prototype.html'),port=19763;
const profile=fs.mkdtempSync(path.join(os.tmpdir(),'lingjing-enemy-lab-'));
const edge=spawn('C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',['--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--remote-debugging-address=127.0.0.1','--remote-debugging-port='+port,'--user-data-dir='+profile,'about:blank'],{stdio:'ignore',windowsHide:true});
const delay=ms=>new Promise(r=>setTimeout(r,ms));let socket,id=0;const pending=new Map(),exceptions=[];
function call(method,params={}){return new Promise((resolve,reject)=>{const n=++id,timer=setTimeout(()=>{pending.delete(n);reject(Error('CDP timeout '+method));},60000);pending.set(n,{resolve:v=>{clearTimeout(timer);resolve(v);},reject:e=>{clearTimeout(timer);reject(e);}});socket.send(JSON.stringify({id:n,method,params}));});}
async function evaluate(expression){const r=await call('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw Error(JSON.stringify(r.exceptionDetails));return r.result.value;}
async function shot(name){const r=await call('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(output,name),Buffer.from(r.data,'base64'));}
(async()=>{
 let ready=false;for(let i=0;i<80;i++){try{const r=await fetch(`http://127.0.0.1:${port}/json/version`);if(r.ok){ready=true;break;}}catch{}await delay(100);}if(!ready)throw Error('Temporary browser not ready');
 const target=await(await fetch(`http://127.0.0.1:${port}/json/new?about:blank`,{method:'PUT'})).json();socket=new WebSocket(target.webSocketDebuggerUrl);await new Promise((r,j)=>{socket.addEventListener('open',r,{once:true});socket.addEventListener('error',j,{once:true});});
 socket.addEventListener('message',ev=>{const m=JSON.parse(ev.data);if(m.id&&pending.has(m.id)){const h=pending.get(m.id);pending.delete(m.id);m.error?h.reject(Error(JSON.stringify(m.error))):h.resolve(m.result);}else if(m.method==='Runtime.exceptionThrown')exceptions.push(m.params.exceptionDetails);});
 await call('Page.enable');await call('Runtime.enable');await call('Emulation.setDeviceMetricsOverride',{width:1440,height:1100,deviceScaleFactor:1,mobile:false});await call('Page.navigate',{url:pathToFileURL(file).href});
 for(let i=0;i<100;i++){if(await evaluate("document.readyState==='complete' && typeof EnemyLab!=='undefined' && document.getElementById('stats').children.length>0"))break;if(i===99)throw Error('Page did not initialize');await delay(100);}
 const interactions=await evaluate(`(()=>{const rows=[];for(const scene of EnemyLab.scenes){document.querySelector('[data-scene="'+scene.id+'"]').click();for(let i=0;i<4;i++){document.querySelector('[data-guide="'+i+'"]').click();if(world.sceneId!==scene.id||world.guideIndex!==i)throw Error('Guide did not reset');if(!document.querySelector('#enemy-table tbody tr'))throw Error('State not rendered');rows.push({scene:scene.id,guide:i,tick:world.tick,hp:world.player.hp});}}return rows;})()`);
 const observations=await evaluate(`(()=>{
 const result={};let s=EnemyLab.create('melee');EnemyLab.advance(s,60);result.stationaryMelee=EnemyLab.summary(s);
 s=EnemyLab.guide('melee',3,{lock:'late',slots:'limited'});result.sidestepMelee=EnemyLab.summary(s);
 s=EnemyLab.guide('navigation',3,{lock:'late',slots:'limited'});result.navigation=EnemyLab.summary(s);
 s=EnemyLab.guide('dash-wall',3,{lock:'late',slots:'limited'});result.dashWall=EnemyLab.summary(s);
 s=EnemyLab.create('commitment');EnemyLab.advance(s,110);result.stationaryStriker=EnemyLab.summary(s);
 s=EnemyLab.guide('commitment',3,{lock:'late',slots:'limited'});result.sidestepStriker=EnemyLab.summary(s);
 s=EnemyLab.guide('cover',3,{lock:'late',slots:'limited'});result.cover=EnemyLab.summary(s);
 s=EnemyLab.guide('zone',3,{lock:'late',slots:'limited'});result.zone=EnemyLab.summary(s);
 s=EnemyLab.create('unreachable');EnemyLab.advance(s,240);result.unreachable=EnemyLab.summary(s);
 result.crowd=[];for(const slots of ['limited','independent']){s=EnemyLab.create('mixed',{lock:'late',slots});let maxBusy=0;for(let t=0;t<180;t++){EnemyLab.step(s,t>=60&&t<90?{x:-1,y:0}:{x:0,y:0});maxBusy=Math.max(maxBusy,s.enemies.filter(EnemyLab.busy).length);}result.crowd.push({slots,maxBusy,...EnemyLab.summary(s)});}
 const a=EnemyLab.guide('mixed',3,{lock:'late',slots:'limited'}),b=EnemyLab.guide('mixed',3,{lock:'late',slots:'limited'});result.sameActionSequenceEqual=JSON.stringify(EnemyLab.summary(a))===JSON.stringify(EnemyLab.summary(b));
 return result;})()`);
 await evaluate(`document.querySelector('[data-scene="commitment"]').click();document.querySelector('[data-guide="1"]').click();`);await shot('striker-locked.png');
 await evaluate(`document.querySelector('[data-scene="navigation"]').click();document.querySelector('[data-guide="1"]').click();document.getElementById('show-field').click();`);await shot('navigation.png');
 await evaluate(`document.querySelector('[data-scene="mixed"]').click();document.querySelector('[data-guide="1"]').click();`);await shot('mixed.png');
 const controls=await evaluate(`(()=>{const o={};document.getElementById('one-tick').click();o.oneTick=world.tick;document.getElementById('quarter').click();o.quarter=world.tick;document.querySelector('[data-walk="1,0"]').click();o.walk=world.tick;const n=world.enemies.length;for(const kind of ['tail','striker','shooter','dropper'])document.querySelector('[data-spawn="'+kind+'"]').click();o.added=world.enemies.length-n;document.querySelector('#enemy-table tbody tr:last-child').click();o.selected=world.selected;const l=document.getElementById('lock-mode');l.value='early';l.dispatchEvent(new Event('change',{bubbles:true}));o.lock={tick:world.tick,value:world.options.lock};const p=document.getElementById('slots-mode');p.value='independent';p.dispatchEvent(new Event('change',{bubbles:true}));o.slots={tick:world.tick,value:world.options.slots};return o;})()`);
 await evaluate(`document.querySelector('[data-scene="commitment"]').click();document.getElementById('play').click();document.dispatchEvent(new KeyboardEvent('keydown',{code:'KeyW',bubbles:true}));`);await delay(250);const keyboard=await evaluate(`(()=>{document.dispatchEvent(new KeyboardEvent('keyup',{code:'KeyW',bubbles:true}));document.getElementById('play').click();return {tick:world.tick,y:world.player.pos.y,running};})()`);
 await call('Emulation.setDeviceMetricsOverride',{width:760,height:1024,deviceScaleFactor:1,mobile:false});const narrow=await evaluate('({width:innerWidth,scrollWidth:document.documentElement.scrollWidth})');
 if(exceptions.length)throw Error('Runtime exceptions: '+JSON.stringify(exceptions));
 const result={page:pathToFileURL(file).href,sourceSha256:require('crypto').createHash('sha256').update(fs.readFileSync(file)).digest('hex'),interactions,observations,controls,keyboard,narrow,runtimeExceptions:exceptions,screenshots:['striker-locked.png','navigation.png','mixed.png']};fs.writeFileSync(path.join(output,'browser-observations.json'),JSON.stringify(result,null,2));console.log(JSON.stringify({guides:interactions.length,observations,controls,keyboard,narrow,runtimeExceptions:exceptions},null,2));
})().catch(e=>{console.error(e.stack||e);process.exitCode=1;}).finally(async()=>{try{if(socket?.readyState===WebSocket.OPEN)await call('Browser.close');}catch{}socket?.close();edge.kill();});
