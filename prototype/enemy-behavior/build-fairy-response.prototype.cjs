// Throwaway builder: reuse the existing EnemyLab geometry/navigation/FSM, patch only experiment rules.
const fs=require('fs'),path=require('path'),vm=require('vm'),crypto=require('crypto');
const root=__dirname;
function makeModelSource(){
 // The prototype keeps one shared damage gate for real execution and forecasts.
 let src=fs.readFileSync(path.join(root,'enemy-behavior.prototype.html'),'utf8').match(/<script>([\s\S]*?)<\/script>/)[1];
 function replace(a,b){if(!src.includes(a))throw Error('Original EnemyLab changed: '+a.slice(0,90));src=src.replace(a,b);}
 replace("tailAim:20,tailActive:8,tailRecover:36,tailReach:1.1,tailWidth:.22,","tailContact:8,strikerContact:16,rangedContact:4,hurtInvulnerability:45,hurtKnockback:.8,bladeRadius:2,bladeOmega:2.5,bladeCount:4,bladeLimit:10,bladeDamage:10,bladeCooldown:6,enemyHp:60,spinCooldown:6,itemRadius:.2,healValue:20,lowHpThreshold:.4,");
 replace("function log(s,text,kind='note'){s.events.push", "function log(s,text,kind='note'){if(s.forecast)return;s.events.push");
 replace("['aim','locked','swing','dash','mark','zone']","['aim','locked','dash','mark','zone']");
 replace("if(e.kind==='tail')return d<=1.2&&clear(s.grid,e.pos,s.player.pos,C.enemyR);","if(e.kind==='tail')return false;");
 const oldDamage=src.split('\n').find(line=>line.includes('function damage('));
 replace(oldDamage,"  function damage(s,amount,source,direction=point(1,0)){const p=s.player;if(p.hp<=0||s.tick<p.invulnerableUntil)return false;p.hp=Math.max(0,p.hp-amount);s.stats.damage+=amount;s.stats.hits++;p.invulnerableUntil=s.tick+C.hurtInvulnerability;direction=unit(direction);s.hurtKick=len(direction)>1e-9?direction:point(1,0);log(s,source+' 命中，生命 -'+amount,'hit');return true;}");
 replace('damage(s,9,`钉枪 ${b.owner} 的弹丸`);','damage(s,9,`钉枪 ${b.owner} 的弹丸`,b.vel);');
 replace('if(hit)damage(s,14,`坠物 ${z.owner} 的落区`);','if(hit)damage(s,14,`坠物 ${z.owner} 的落区`,sub(s.player.pos,point(z.x+z.w/2,z.y+z.h/2)));');
 
 const grantStart="if(e.kind==='tail'){lockPoint(s,e);e.duration=C.tailAim;enter(e,'locked');e.reason='方向已锁定，短打准备';}else if(e.kind==='dropper')";
 replace(grantStart,"if(e.kind==='dropper')");
 replace("if(e.kind==='tail'){enter(e,'swing');e.duration=C.tailActive;e.reason='短打生效，一次动作最多命中一次';}else if(e.kind==='striker')","if(e.kind==='striker')");
 const swing=src.split('\n').find(l=>l.includes("if(e.state==='swing')"));replace(swing,'');
 replace("const hit=circleTime(sub(e.pos,s.player.old),sub(wanted,s.player.pos),point(0,0),e.r+C.playerR);if(!e.hitDone&&hit<=1&&hit<=wall){damage(s,16,`撞针 ${e.id} 的突刺`);e.hitDone=true;}","");
 replace("e.age++;const p=s.player.pos,d=dist(e.pos,p);e.old=copy(e.pos);",`e.age++;const p=s.player.pos,d=dist(e.pos,p);e.old=copy(e.pos);
    if(s.predictionModel==='linear'){
      const speed=e.kind==='striker'&&e.state==='dash'?C.strikerDashSpeed:e.kind==='tail'?C.tailSpeed:e.kind==='striker'?C.strikerSpeed:e.kind==='shooter'?C.shooterSpeed:C.dropperSpeed;
      let v=e.vel;if(len(v)>speed)v=mul(unit(v),speed);const wanted=add(e.pos,mul(v,C.dt));
      e.pos=e.kind==='dropper'?point(clamp(wanted.x,e.r,C.w-e.r),clamp(wanted.y,e.r,C.h-e.r)):clipped(s.grid,e.pos,wanted,e.r);e.vel=mul(sub(e.pos,e.old),1/C.dt);return;
    }
    if(e.kind==='tail'){
      e.state='approach';if(clear(s.grid,e.pos,p,e.r))e.path=[copy(p)];else e.path=flowPath(s,e);
      const target=nextPoint(s,e,e.path);if(target)moveActor(s,e,sub(target,e.pos),C.tailSpeed,false,true);else e.vel=point(0,0);
      e.reason=target?'沿共享路径接近，接触立即结算并自毁':'目标不可达';return;
    }`);
 replace("if(s.player.hp<=0)return s;s.tick++;s.input=unit(input);",`if(s.player.hp<=0)return s;s.tick++;
    if(input.move){if(input.spin&&input.spin!==s.player.spin&&s.tick-s.player.lastFlip>=C.spinCooldown){s.player.spin=input.spin;s.player.lastFlip=s.tick;}input=input.move;}
    s.input=unit(input);applySchedule(s);`);
 replace("for(const e of s.enemies)updateEnemy(s,e,requests);", "for(const e of s.enemies)updateEnemy(s,e,requests);resolveCombat(s);s.enemies=s.enemies.filter(e=>e.hp>0);");
 replace("if(available>0&&ready(s,e)){grant", "if(available>0&&e.hp>0&&ready(s,e)){grant");
 replace("return s;\n  }\n  function advance", "finishHurtKick(s);collectItems(s);s.stats.proxyIntegral+=attackRate(s,1)*C.dt;s.stats.coverageTicks+=s.enemies.filter(e=>Math.abs(dist(e.pos,s.player.pos)-C.bladeRadius)<=e.r).length;return s;\n  }\n  function advance");
 replace("function enemy(id,kind,x,y){return {id,kind,", "function enemy(id,kind,x,y){return {id,kind,hp:C.enemyHp,maxHp:C.enemyHp,cooldowns:Array(C.bladeCount).fill(0),");
 replace("r:C.playerR,hp:100}", "r:C.playerR,hp:100,maxHp:100,spin:1,phase:0,bladeCount:C.bladeCount,bladeLimit:C.bladeLimit,energy:0,lastFlip:-1000,invulnerableUntil:0}");
 replace("enemies:scene.enemies.map((e,i)=>enemy(i+1,...e)),bullets:[]", "enemies:scene.enemies.map((e,i)=>enemy(i+1,...e)),nextEnemyId:scene.enemies.length+1,nextItemId:1,items:[],schedule:[],anchor:null,forecast:false,disableBlades:false,bullets:[]");
 replace("damage:0,cancelled:0,wallStops:0}", "damage:0,cancelled:0,wallStops:0,damageDealt:0,bladeHits:0,kills:0,suicides:0,rangedContacts:0,...pickupStats(),spawned:0,proxyIntegral:0,coverageTicks:0}");
 replace("const id=s.enemies.reduce((n,e)=>Math.max(n,e.id),0)+1;", "const id=s.nextEnemyId++;");
 replace("function reduce(s,a){if(a.type==='scene')", "function reduce(s,a){if(a.type==='set-player')return setPlayer(s,a);if(a.type==='add-item'){addItem(s,a.kind);return s;}if(a.type==='scene')");
 const exported="return {C,names,labels,scenes,create,guide,step,advance,reduce,summary,busy,clear,clipped,point,dist,add,mul,unit,cell,center};";
 replace(exported, extension()+"\n  return {C,names,labels,scenes,create,guide,step,advance,reduce,summary,busy,clear,clipped,point,dist,add,mul,unit,cell,center,search,backtrace,scenario,studyScenes,observe,fromObservation,attackRate,spawnNear,itemNames,itemBenefit};");
 return src;
}
function extension(){return `
  // Same chronological blade/contact settlement for the real world and observation-derived forecasts.
  function resolveCombat(s){
    const p=s.player,oldPhase=p.phase;p.phase=(p.phase+p.spin*C.bladeOmega*C.dt)%(Math.PI*2);const events=[];
    for(const e of s.enemies){for(let j=0;j<e.cooldowns.length;j++)if(e.cooldowns[j]>0)e.cooldowns[j]--;
      if(e.hp>0){const t=circleTime(sub(e.old,p.old),sub(e.pos,p.pos),point(0,0),e.r+p.r);if(t<=1)events.push({t,type:0,e});}
      if(!s.disableBlades)for(let j=0;j<p.bladeCount;j++){
        if((e.cooldowns[j]||0)>0)continue;const a0=oldPhase+j*2*Math.PI/p.bladeCount,a1=p.phase+j*2*Math.PI/p.bladeCount;
        const a=add(p.old,point(Math.cos(a0)*C.bladeRadius,Math.sin(a0)*C.bladeRadius)),b=add(p.pos,point(Math.cos(a1)*C.bladeRadius,Math.sin(a1)*C.bladeRadius));
        const t=circleTime(sub(a,e.old),sub(b,e.pos),point(0,0),e.r);if(t<=1)events.push({t,type:1,e,j});
      }
    }
    events.sort((a,b)=>a.t-b.t||a.type-b.type||a.e.id-b.e.id||(a.j||0)-(b.j||0));
    for(const x of events){const e=x.e;if(e.hp<=0)continue;if(x.type===0){
        const playerAt=add(p.old,mul(sub(p.pos,p.old),x.t)),enemyAt=add(e.old,mul(sub(e.pos,e.old),x.t));
        let direction=sub(playerAt,enemyAt);
        if(len(direction)<1e-9)direction=sub(sub(e.pos,e.old),sub(p.pos,p.old));
        const amount=e.kind==='tail'?C.tailContact:e.kind==='striker'?C.strikerContact:C.rangedContact;
        const applied=damage(s,amount,names[e.kind]+' '+e.id+' 身体接触',direction);
        if(e.kind==='tail'||e.kind==='striker'){e.hp=0;s.stats.suicides++;log(s,names[e.kind]+' '+e.id+' 接触后自毁');}
        else if(applied)s.stats.rangedContacts++;
      }
      else if(!(e.cooldowns[x.j]>0)){const amount=Math.min(C.bladeDamage,e.hp);e.hp-=amount;e.cooldowns[x.j]=C.bladeCooldown;s.stats.damageDealt+=amount;s.stats.bladeHits++;if(e.hp<=0){s.stats.kills++;log(s,names[e.kind]+' '+e.id+' 被飞刀击杀');}}
    }
  }
  // Apply the one-shot kick at tick end: precomputed blade/contact sweeps remain valid.
   // The displacement uses wall sweep clipping, grants no blade hits along the kick,
   // and does not become a huge next-tick movement velocity.
   function finishHurtKick(s){
     const direction=s.hurtKick;s.hurtKick=null;if(!direction||s.player.hp<=0)return;
     const p=s.player,old=copy(p.pos);p.pos=clipped(s.grid,old,add(old,mul(direction,C.hurtKnockback)),p.r);p.vel=point(0,0);
     log(s,'伊埃斯震开 '+dist(old,p.pos).toFixed(2)+' 单位');
   }
   const itemNames={knife:'飞刀',heal:'血包',energy:'能量球'};
  function pickupStats(){return {pickups:0,knifePickups:0,healPickups:0,energyPickups:0,bladesAdded:0,healing:0,urgentHealing:0};}
  // One source of truth for collection effects and candidate guidance. Energy is consumed but never useful during this demo's takeover.
  function itemBenefit(p,item){
    const value={urgentHealing:0,blades:0,healing:0};if(p.hp<=0)return value;
    if(item.kind==='knife'&&p.bladeCount<p.bladeLimit)value.blades=1;
    if(item.kind==='heal'){value.healing=Math.max(0,Math.min(item.value,p.maxHp-p.hp));if(p.hp<p.maxHp*C.lowHpThreshold)value.urgentHealing=value.healing;}
    return value;
  }
  function collectItems(s){
    if(s.player.hp<=0)return;const left=[],p=s.player;
    for(const item of s.items){
      if(dist(item.pos,p.pos)>p.r+C.itemRadius||(item.kind==='knife'&&p.bladeCount>=p.bladeLimit)){left.push(item);continue;}
      const benefit=itemBenefit(p,item);s.stats.pickups++;
      if(item.kind==='knife'){
        p.bladeCount+=benefit.blades;s.stats.knifePickups++;s.stats.bladesAdded+=benefit.blades;
        for(const e of s.enemies)while(e.cooldowns.length<p.bladeCount)e.cooldowns.push(0);
        log(s,'拾取飞刀，刀数 '+p.bladeCount+'/'+p.bladeLimit);
      }else if(item.kind==='heal'){
        p.hp+=benefit.healing;s.stats.healPickups++;s.stats.healing+=benefit.healing;s.stats.urgentHealing+=benefit.urgentHealing;
        log(s,'拾取血包，有效回复 '+benefit.healing+' HP');
      }else if(item.kind==='energy'){s.stats.energyPickups++;log(s,'能量球已消耗，代行期间不回能（+0）');}
    }
    s.items=left;
  }
  function setPlayer(s,a){
    const p=s.player;if(Number.isFinite(a.hp))p.hp=clamp(a.hp,0,p.maxHp);
    if(Number.isFinite(a.bladeCount)){p.bladeCount=clamp(Math.floor(a.bladeCount),C.bladeCount,p.bladeLimit);for(const e of s.enemies){e.cooldowns=e.cooldowns.slice(0,p.bladeCount);while(e.cooldowns.length<p.bladeCount)e.cooldowns.push(0);}}
    log(s,'实验设置：HP '+p.hp+'/'+p.maxHp+'，飞刀 '+p.bladeCount+'/'+p.bladeLimit);return s;
  }
  function makeItem(id,kind,pos){return {id,kind,pos:copy(pos),value:kind==='heal'?C.healValue:kind==='knife'?1:0};}
  function addItem(s,kind){
    if(!itemNames[kind])return null;
    for(let k=0;k<16;k++){const angle=k*Math.PI/8,pos=add(s.player.pos,point(Math.cos(angle)*1.2,Math.sin(angle)*1.2));
      if(!fits(s.grid,pos,C.itemRadius)||!clear(s.grid,s.player.pos,pos,s.player.r)||s.items.some(i=>dist(i.pos,pos)<C.itemRadius*2))continue;
      const item=makeItem(s.nextItemId++,kind,pos);s.items.push(item);log(s,'实验投放'+itemNames[kind]+' '+item.id);return item.id;
    }
    log(s,'附近没有可用道具投放位置');return null;
  }
  function attackRate(s,referenceSpin=1){let rate=0;const p=s.player;for(const e of s.enemies){const q=sub(e.pos,p.pos),d=len(q);if(d<1e-9||Math.abs(d-C.bladeRadius)>e.r)continue;const tangent=point(-q.y/d,q.x/d),angular=dot(sub(e.vel,p.vel),tangent)/d;rate+=C.bladeDamage*p.bladeCount*Math.abs(referenceSpin*C.bladeOmega-angular)/(2*Math.PI);}return rate;}
  function spawnNear(s,kind,origin,angle=0){const id=s.nextEnemyId++;for(let k=0;k<24;k++){const a=angle+k*Math.PI/12,r=.9+.25*(k%3),p=add(origin,point(Math.cos(a)*r,Math.sin(a)*r));if(!fits(s.grid,p,C.enemyR)||s.enemies.some(e=>dist(e.pos,p)<e.r+C.enemyR+.05))continue;s.enemies.push(enemy(id,kind,p.x,p.y));s.stats.spawned++;return id;}return null;}
  function applySchedule(s){if(s.forecast)return;const left=[];for(const event of s.schedule){if(event.tick>s.tick){left.push(event);continue;}for(let j=0;j<event.kinds.length;j++)spawnNear(s,event.kinds[j],event.origin,event.angle+j*2*Math.PI/event.kinds.length);log(s,'召唤源周围生成新小怪对象');}s.schedule=left;}
  const studyScenes=[
     {id:'fairy-contact-shooter',label:'钉枪身体接触',description:'接触探针：暂停敌人招式与飞刀伤害，只观察身体扣血、震开和受伤保护；切手动后定步可隔离检查。',player:[13,9],walls:[],enemies:[['shooter',12.5,9]],contactProbe:true},
     {id:'fairy-contact-dropper',label:'坠物身体接触',description:'接触探针：空中路径不影响本轮身体投影接触规则；暂停招式与飞刀伤害，只观察受伤、震开和短暂保护。',player:[13,9],walls:[],enemies:[['dropper',12.5,9]],contactProbe:true},
     {id:'fairy-contact-wall',label:'墙边接触与保护',description:'接触探针：伊埃斯贴近右墙，震开受墙体限制。切手动停留，保护期内不连扣，保护结束仍重叠才再次受伤。',player:[13.63,9.5],walls:[[14,9]],enemies:[['shooter',13.13,9.5]],contactProbe:true},
    {id:'fairy-projectiles',label:'弹丸与射击',description:'两处射线压力，比较提前预测出手与只外推当前速度。',player:[13,9],walls:[],enemies:[['shooter',7,9],['shooter',19,11],['tail',13,13]]},
    {id:'fairy-single-shooter',label:'单钉枪与主动接敌',description:'只剩一只正常射击的钉枪，起距6；比较零输出时的安全接敌与持续逃跑。先看首次斜向接近，再看四秒实际击杀；弹丸判定不放宽。',player:[13,9],walls:[],enemies:[['shooter',7,9]]},
    {id:'fairy-dash',label:'预警转冲锋',description:'预警阶段静止不等于接下来一秒静止。',player:[13,9],walls:[],enemies:[['striker',10,9],['striker',15,12],['tail',17,8]]},
    {id:'fairy-zones',label:'空降固定落区',description:'按固定矩形生效时刻离场，同时寻找飞刀攻击机会。',player:[13,9],walls:[],enemies:[['dropper',15,11],['shooter',7,9],['tail',17,9]]},
    {id:'fairy-mixed',label:'混合职责',description:'接触自毁、弹丸、冲锋与空降使用同一世界结算。',player:[13,9],walls:[[10,5],[10,6],[16,11],[16,12]],enemies:[['tail',9,9],['striker',13,5],['shooter',7,5],['shooter',20,13],['dropper',18,7]]},
    {id:'fairy-corner',label:'墙边与通路',description:'近墙轨迹须按真实体积执行，不能用穿墙短线输出。',player:[13,9],walls:[[14,8],[14,9],[14,10],[14,11],[14,12]],enemies:[['striker',10,9],['shooter',8,12],['tail',17,9]]},
    {id:'fairy-long-warning',label:'三秒落区',description:'公开三秒落区位于一秒近场前瞻之外；观察滚动前瞻的实际边界。',player:[13,9],walls:[[16,8],[16,9],[16,10]],enemies:[['shooter',7,9],['tail',17,7]],longZone:true},
    {id:'fairy-summon',label:'Boss周边新增对象',description:'只验证召唤源周边新小怪接入，不是完整Boss战；出现前不向规划器提供未来刷新表。',player:[13,9],walls:[],enemies:[['shooter',7,9],['tail',12,13]],summon:true},
    {id:'fairy-pickup',label:'输出与顺路拾取',description:'实际加刀进入后续刀刃输出；低血与加刀收益仍不能抵消预测伤害，能量消耗不加选路分。',player:[13,9],walls:[],enemies:[['shooter',15,9]],loadout:{hp:70},pickup:true,items:[['heal',12.2,9],['knife',13,10],['energy',13,8]]},
    {id:'fairy-pickup-priority',label:'低血与40%边界',description:'无敌人时比较等距道具：30%生命先血包；切到40%后先飞刀。能量球不占引导名额。',player:[13,9],walls:[],enemies:[],loadout:{hp:30},items:[['heal',14.6,9],['knife',11.4,9],['energy',13,10.6]]},
    {id:'fairy-pickup-cap',label:'满刀与无效能量',description:'起点满10刀，脚下飞刀留地，能量球消耗但+0；切9刀后再前进一帧可补回10刀。',player:[13,9],walls:[],enemies:[],loadout:{bladeCount:10},items:[['knife',13,9],['energy',13,9]]},
    {id:'fairy-pickup-safety',label:'血包与危险落区',description:'低血血包在0.5秒后生效的落区内；安全仍先于输出和道具。观察首次规划，再看危险结束后的拾取。',player:[13,9],walls:[],enemies:[],loadout:{hp:30},items:[['heal',15.5,9],['knife',11.4,9]],dangerZone:{x:14,y:8,w:3,h:2,expiry:30,born:0,owner:0}}
  ];
  function random(seed){let n=seed>>>0;return ()=>{n=(1664525*n+1013904223)>>>0;return n/4294967296;};}
  function scenario(id,seed=1,options={lock:'late',slots:'independent'}){
    const spec=studyScenes.find(x=>x.id===id)||studyScenes[0],s=create('mixed',options),rnd=random(seed);s.sceneId=spec.id;s.grid=makeGrid(spec.walls.map(x=>[...x]));
    const jitter=seed===1?0:.4;s.player.pos=point(spec.player[0]+(rnd()-.5)*jitter,spec.player[1]+(rnd()-.5)*jitter);if(spec.contactProbe&&!fits(s.grid,s.player.pos,C.playerR))s.player.pos=point(spec.player[0],spec.player[1]);s.player.old=copy(s.player.pos);Object.assign(s.player,spec.loadout||{});
    s.enemies=spec.enemies.map((e,i)=>enemy(i+1,e[0],e[1]+(rnd()-.5)*jitter,e[2]+(rnd()-.5)*jitter));s.nextEnemyId=s.enemies.length+1;s.flow=null;s.flowAt=0;s.events=[];
    s.items=(spec.items||[]).map((item,i)=>makeItem(i+1,item[0],point(item[1],item[2])));s.nextItemId=s.items.length+1;
    if(spec.longZone)s.zones.push({x:10.5,y:6.5,w:5,h:5,expiry:180,born:0,owner:0});
    if(spec.dangerZone)s.zones.push({...spec.dangerZone});
    if(spec.summon){s.anchor=point(17,9);s.schedule=[{tick:90,origin:copy(s.anchor),kinds:['striker','shooter','tail'],angle:rnd()*Math.PI*2}];}
    if(spec.pickup){s.enemies[0].state='recover';s.enemies[0].duration=240;}
     if(spec.contactProbe){s.disableBlades=true;for(const e of s.enemies){e.state='recover';e.duration=100000;}}
    rebuildFlow(s,true);return s;
  }
  function observe(s){return {tick:s.tick,grid:s.grid,options:{...s.options},player:{pos:copy(s.player.pos),vel:copy(s.player.vel),r:s.player.r,hp:s.player.hp,maxHp:s.player.maxHp,spin:s.player.spin,phase:s.player.phase,bladeCount:s.player.bladeCount,bladeLimit:s.player.bladeLimit,energy:s.player.energy,invulnerableUntil:s.player.invulnerableUntil},enemies:s.enemies.map(e=>({id:e.id,kind:e.kind,pos:copy(e.pos),vel:copy(e.vel),r:e.r,hp:e.hp,state:e.state,age:e.age,duration:e.duration,dir:copy(e.dir),target:e.target?copy(e.target):null,waitSince:e.waitSince})),bullets:s.bullets.map(b=>({pos:copy(b.pos),vel:copy(b.vel)})),zones:s.zones.map(z=>({...z})),items:s.items.map(i=>({...i,pos:copy(i.pos)}))};}
  function fromObservation(o,model='phase'){
    const s={tick:o.tick,grid:o.grid,options:{...o.options},player:{id:0,...o.player,pos:copy(o.player.pos),old:copy(o.player.pos),vel:copy(o.player.vel),spin:1,phase:0,lastFlip:-1000},enemies:o.enemies.map(v=>({...enemy(v.id,v.kind,v.pos.x,v.pos.y),...v,pos:copy(v.pos),old:copy(v.pos),vel:copy(v.vel),dir:copy(v.dir),target:v.target?copy(v.target):null,path:[],navAt:0,stand:null})),nextEnemyId:1,nextItemId:Math.max(0,...o.items.map(i=>i.id))+1,items:o.items.map(i=>({...i,pos:copy(i.pos)})),schedule:[],anchor:null,forecast:true,disableBlades:true,predictionModel:model,bullets:o.bullets.map(b=>({pos:copy(b.pos),vel:copy(b.vel),life:C.bulletLife,born:o.tick-1,owner:0})),zones:o.zones.map(z=>({...z})),events:[],input:point(0,0),flow:null,flowAt:0,stats:{flowBuilds:0,astarCalls:0,attacks:0,shots:0,hits:0,damage:0,cancelled:0,wallStops:0,damageDealt:0,bladeHits:0,kills:0,suicides:0,rangedContacts:0,...pickupStats(),spawned:0,proxyIntegral:0,coverageTicks:0}};
    rebuildFlow(s,true);return s;
  }
`;}
function loadLab(){return vm.runInNewContext(makeModelSource()+'\n;EnemyLab',{Math,console});}
function sourceHashes(model=makeModelSource()){return {model:crypto.createHash('sha256').update(model).digest('hex'),planner:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,'fairy-planner.prototype.js'))).digest('hex')};}
function buildPage(){
 const model=makeModelSource(),planner=fs.readFileSync(path.join(root,'fairy-planner.prototype.js'),'utf8'),shell=fs.readFileSync(path.join(root,'fairy-page.prototype.html'),'utf8'),hashes=sourceHashes(model);
 const keys=new Set(),groups=[],configurations=[],ignoredBatches=[];
 for(const relative of ['fairy-results/contact/summary.json','fairy-results/engagement/summary.json','fairy-results/pickups/summary.json','fairy-results/summary.json','fairy-results/followup/summary.json','fairy-results/confirmation/summary.json']){
  const file=path.join(root,relative);if(!fs.existsSync(file))continue;const batch=JSON.parse(fs.readFileSync(file,'utf8'));
  if(batch.sourceHashes?.model!==hashes.model||batch.sourceHashes?.planner!==hashes.planner){ignoredBatches.push(relative);continue;}
  configurations.push({path:relative,configuration:batch.configuration});
  for(const g of batch.groups||[]){const key=g.scene+'|'+g.strategy;if(!keys.has(key)){keys.add(key);groups.push(g);}}
 }
 const data={groups,sourceHashes:hashes,configurations,ignoredBatches};
 const html=shell.replace('/* ENEMY_LAB_MODEL */',()=>model).replace('/* FAIRY_PLANNER */',()=>planner).replace('/* BATCH_RESULTS */',()=>JSON.stringify(data).replace(/</g,'\\u003c'));
 fs.writeFileSync(path.join(root,'fairy-response.prototype.html'),html);console.log('Built fairy-response.prototype.html; current-rule groups: '+groups.length);return html;
}
module.exports={makeModelSource,loadLab,sourceHashes,buildPage};if(require.main===module)buildPage();
