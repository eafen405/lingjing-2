// Deterministic isolated game-rule probes at the real observe -> decide -> step seam.
// Geometry stress cases are explicitly synthetic inputs, not ordinary gameplay runs.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const builder=require('../build-fairy-response.prototype.cjs'),planner=require('../fairy-planner.prototype.js'),lab=builder.loadLab();
const argument=(name,fallback)=>{const i=process.argv.indexOf('--'+name);return i<0?fallback:process.argv[i+1];};
const idle={move:{x:0,y:0},spin:1},close=(a,b)=>Math.abs(a-b)<1e-8;
function contactWorld(kind){
  const s=lab.scenario(kind==='shooter'?'fairy-single-shooter':'fairy-zones',1);
  s.enemies=[s.enemies.find(e=>e.kind===kind)];s.bullets=[];s.zones=[];s.items=[];s.schedule=[];s.disableBlades=true;
  const e=s.enemies[0];e.pos={x:s.player.pos.x-.5,y:s.player.pos.y};e.old={...e.pos};e.vel={x:0,y:0};e.state='recover';e.age=0;e.duration=100000;
  return s;
}
function snapshot(s){return {tick:s.tick,hp:s.player.hp,damage:s.stats.damage,hits:s.stats.hits,rangedContacts:s.stats.rangedContacts,position:{...s.player.pos},velocity:{...s.player.vel},enemies:s.enemies.map(e=>({id:e.id,kind:e.kind,hp:e.hp})),suicides:s.stats.suicides,invulnerableUntil:s.player.invulnerableUntil};}
const failures=[],checks=[],probes=[];
function check(name,ok,detail){checks.push({name,ok,detail});if(!ok)failures.push(name);}
function relocatePlayer(s,pos){s.player.pos={...pos};s.player.old={...pos};s.player.vel={x:0,y:0};}
function advance(s,ticks,command=idle){for(let i=0;i<ticks;i++)lab.step(s,command);}
for(const kind of ['shooter','dropper']){
  const s=contactWorld(kind),start={...s.player.pos},o=lab.observe(s),frozen=JSON.stringify(o),choice=planner.decide(o,planner.create('phase'),lab);
  const idleCandidate=choice.decision.candidates.find(c=>c.id==='idle');
  lab.step(s,idle);const first=snapshot(s);probes.push({kind,start,predictedIdleDamage:idleCandidate.damage,first});
  check(kind+' first contact damages exactly once',first.damage===lab.C.rangedContact&&first.hits===1&&first.hp===100-lab.C.rangedContact,first);
  check(kind+' contact moves Eous outward by configured distance',close(lab.dist(start,s.player.pos),lab.C.hurtKnockback)&&s.player.pos.x>start.x,first.position);
  check(kind+' body survives with unchanged HP',first.enemies.length===1&&first.enemies[0].hp===lab.C.enemyHp&&first.suicides===0,first.enemies);
  check(kind+' forecast includes contact',idleCandidate.damage===lab.C.rangedContact,{predicted:idleCandidate.damage});
  check(kind+' protection lasts within user range',first.invulnerableUntil-first.tick===lab.C.hurtInvulnerability&&lab.C.hurtInvulnerability>=30&&lab.C.hurtInvulnerability<=60,first);
  for(const model of ['phase','linear']){
    const f=lab.fromObservation(o,model);lab.step(f,idle);
    check(kind+' '+model+' first-step world matches execution',JSON.stringify(snapshot(f))===JSON.stringify(first),snapshot(f));
  }
  check(kind+' forecasts leave public observation unchanged',JSON.stringify(o)===frozen);
  lab.step(s,idle);check(kind+' one-shot displacement has no residual velocity',close(s.player.pos.x,first.position.x)&&close(s.player.pos.y,first.position.y)&&s.stats.damage===lab.C.rangedContact,snapshot(s));
  relocatePlayer(s,start);lab.step(s,idle);check(kind+' re-entry during protection cannot retrigger hit or kick',s.stats.hits===1&&close(s.player.pos.x,start.x),snapshot(s));
  relocatePlayer(s,{x:start.x+3,y:start.y});while(s.tick<s.player.invulnerableUntil)lab.step(s,idle);
  check(kind+' expiry alone does not cause damage after separation',s.stats.hits===1,snapshot(s));
  relocatePlayer(s,start);lab.step(s,idle);check(kind+' re-entry after expiry hits again',s.stats.hits===2&&s.stats.damage===2*lab.C.rangedContact,snapshot(s));
}
// A wall keeps the bodies overlapping, distinguishing protection from merely being pushed clear.
{
  const s=lab.scenario('fairy-contact-wall',1);lab.step(s,idle);const first=snapshot(s),o=lab.observe(s),forecasts=['phase','linear'].map(m=>lab.fromObservation(o,m));
  check('wall clips kick to the player footprint',s.player.pos.x<=14-s.player.r&&s.player.pos.x>13.63&&lab.clear(s.grid,{x:13.63,y:9.5},s.player.pos,s.player.r),first);
  advance(s,lab.C.hurtInvulnerability-1);for(const f of forecasts)advance(f,lab.C.hurtInvulnerability-1);
  check('persistent overlap stays one hit through protected final tick',s.tick===first.invulnerableUntil-1&&s.stats.hits===1,snapshot(s));
  for(const [i,f]of forecasts.entries())check('protected reconstruction '+i+' retains absolute deadline',f.player.invulnerableUntil===first.invulnerableUntil&&f.stats.damage===0&&close(f.player.pos.x,s.player.pos.x),snapshot(f));
  lab.step(s,idle);for(const f of forecasts)lab.step(f,idle);
  check('persistent overlap hits exactly at expiry',s.tick===first.invulnerableUntil&&s.stats.hits===2&&s.stats.rangedContacts===2,snapshot(s));
  for(const [i,f]of forecasts.entries())check('protected reconstruction '+i+' repeats at same expiry',f.stats.damage===lab.C.rangedContact&&f.player.invulnerableUntil===s.player.invulnerableUntil&&close(f.player.pos.x,s.player.pos.x),snapshot(f));
  probes.push({kind:'wall',first,atExpiry:snapshot(s)});
}
{
  const s=contactWorld('shooter'),e=s.enemies[0];s.enemies.push({...e,id:e.id+1,kind:'dropper',pos:{x:13.5,y:9},old:{x:13.5,y:9},cooldowns:[...e.cooldowns]});
  lab.step(s,idle);check('simultaneous bodies cause one hit and stable outward kick',s.stats.hits===1&&s.stats.rangedContacts===1&&s.enemies.length===2&&close(s.player.pos.x,13+lab.C.hurtKnockback),snapshot(s));
}
function addBullet(s){s.bullets.push({pos:{...s.player.pos},vel:{x:0,y:0},life:100,born:s.tick-1,owner:99});}
function addZone(s){s.zones.push({x:s.player.pos.x-.1,y:s.player.pos.y-.1,w:.2,h:.2,expiry:s.tick+1,born:s.tick,owner:99});}
{
  const s=contactWorld('shooter');addBullet(s);addZone(s);lab.step(s,idle);
  check('contact protection covers same-tick projectile and zone',s.stats.damage===lab.C.rangedContact&&s.stats.hits===1&&s.bullets.length===0&&s.zones.length===0,snapshot(s));
  addBullet(s);addZone(s);lab.step(s,idle);check('protection covers later projectile and zone',s.stats.damage===lab.C.rangedContact&&s.stats.hits===1,snapshot(s));
  s.enemies=[];while(s.tick<s.player.invulnerableUntil-1)lab.step(s,idle);addBullet(s);addZone(s);lab.step(s,idle);
  check('projectile at expiry starts common protection before later zone',s.stats.damage===lab.C.rangedContact+9&&s.stats.hits===2,snapshot(s));
  while(s.tick<s.player.invulnerableUntil-1)lab.step(s,idle);addZone(s);lab.step(s,idle);
  check('zone at next expiry starts common protection',s.stats.damage===lab.C.rangedContact+9+14&&s.stats.hits===3&&s.player.invulnerableUntil===s.tick+lab.C.hurtInvulnerability,snapshot(s));
}
{
  const s=contactWorld('shooter');s.enemies[0].hp=0;lab.step(s,idle);check('dead remote body cannot damage',s.stats.damage===0&&s.enemies.length===0,snapshot(s));
  const dead=contactWorld('dropper');dead.player.hp=0;lab.step(dead,idle);check('dead player cannot trigger combat or advance',dead.tick===0&&dead.stats.damage===0,snapshot(dead));
  const fatal=contactWorld('shooter');fatal.player.hp=2;fatal.items=[{id:1,kind:'heal',pos:{...fatal.player.pos},value:20}];lab.step(fatal,idle);
  check('fatal contact cannot be cancelled by same-tick healing',fatal.player.hp===0&&fatal.stats.healing===0&&fatal.items.length===1,snapshot(fatal));
}
{
  const s=contactWorld('shooter');s.enemies[0].pos={...s.player.pos};lab.step(s,idle);
  check('coincident centres have a finite deterministic fallback kick',Number.isFinite(s.player.pos.x)&&Number.isFinite(s.player.pos.y)&&close(s.player.pos.x,13+lab.C.hurtKnockback),snapshot(s));
}
// Tangential crossing with normal 4-unit/s movement: neither endpoint overlaps.
{
  const s=contactWorld('shooter'),e=s.enemies[0];e.pos={x:13,y:9};relocatePlayer(s,{x:13-1/30,y:9+e.r+s.player.r-.0001});s.player.vel={x:4,y:0};
  const before={...s.player.pos},naturalEnd={x:before.x+4/60,y:before.y},sum=e.r+s.player.r;
  check('sweep fixture endpoints are both separated',lab.dist(before,e.pos)>sum&&lab.dist(naturalEnd,e.pos)>sum,{before,naturalEnd});
  lab.step(s,{move:{x:1,y:0},spin:1});check('relative sweep catches contact between endpoints',s.stats.damage===lab.C.rangedContact,snapshot(s));
}
// Synthetic fast traversal distinguishes blade-first kill from body-first contact.
{
  const s=contactWorld('shooter');s.disableBlades=false;relocatePlayer(s,{x:10,y:9});s.player.vel={x:180,y:0};s.enemies[0].pos={x:13,y:9};s.enemies[0].hp=10;
  lab.step(s,{move:{x:1,y:0},spin:1});check('earlier blade kill suppresses later body event',s.stats.kills===1&&s.stats.damage===0&&s.enemies.length===0,{...snapshot(s),kills:s.stats.kills});
  const later=contactWorld('shooter');later.disableBlades=false;later.player.vel={x:240,y:0};later.enemies[0].hp=10;
  lab.step(later,{move:{x:1,y:0},spin:1});check('earlier body event is not erased by later blade kill',later.stats.damage===lab.C.rangedContact&&later.stats.kills===1&&later.enemies.length===0,{...snapshot(later),kills:later.stats.kills});
}
for(const kind of ['tail','striker']){
  const s=contactWorld('shooter');s.enemies[0].kind=kind;lab.step(s,idle);
  check(kind+' existing contact semantics remain',s.stats.damage===(kind==='tail'?lab.C.tailContact:lab.C.strikerContact)&&s.stats.suicides===1&&s.enemies.length===0&&s.stats.rangedContacts===0,snapshot(s));
}
for(const kind of ['tail','striker','bullet','zone']){
  const s=contactWorld('shooter'),start={...s.player.pos};
  if(kind==='tail'||kind==='striker')s.enemies[0].kind=kind;else{s.enemies=[];if(kind==='bullet')addBullet(s);else addZone(s);}
  const o=lab.observe(s);lab.step(s,idle);
  for(const model of ['phase','linear']){const f=lab.fromObservation(o,model);lab.step(f,idle);check(kind+' '+model+' common hurt forecast matches execution',JSON.stringify(snapshot(f))===JSON.stringify(snapshot(s)),snapshot(f));}
  check(kind+' uses common hurt protection',s.player.invulnerableUntil===s.tick+45,snapshot(s));
  check(kind+' uses common outward displacement',close(lab.dist(start,s.player.pos),.8),snapshot(s));
}
{
  const s=contactWorld('shooter');s.enemies=[];addBullet(s);addBullet(s);s.bullets[0].vel={x:-1,y:0};const start={...s.player.pos};lab.step(s,idle);
  check('multiple projectiles share one global hurt window',s.stats.damage===9&&s.stats.hits===1&&s.bullets.length===0,snapshot(s));
  check('projectile kick follows incoming travel direction',close(s.player.pos.x,start.x-.8)&&close(s.player.pos.y,start.y),snapshot(s));
  const z=contactWorld('shooter');z.enemies=[];z.zones=[{x:12,y:7.5,w:2,h:2,expiry:1,born:0,owner:99}];lab.step(z,idle);
  check('zone kick points outward from marked area centre',z.stats.damage===14&&close(z.player.pos.x,13)&&close(z.player.pos.y,9.8),snapshot(z));
  const mixed=contactWorld('shooter'),e=mixed.enemies[0];mixed.enemies.push({...e,id:2,cooldowns:[...e.cooldowns],pos:{...e.pos},old:{...e.old}});mixed.enemies[0].kind='tail';lab.step(mixed,idle);
  check('melee hurt protects against following remote body in same tick',mixed.stats.damage===lab.C.tailContact&&mixed.stats.hits===1&&mixed.stats.rangedContacts===0&&mixed.enemies.length===1&&mixed.enemies[0].kind==='shooter',snapshot(mixed));
}
for(const seed of [1,2,1000]){const s=lab.scenario('fairy-contact-wall',seed);check('wall fixture starts outside wall for seed '+seed,lab.clear(s.grid,s.player.pos,s.player.pos,s.player.r),{seed,position:s.player.pos});}
const result={sourceHashes:builder.sourceHashes(),configuration:{damage:lab.C.rangedContact,invulnerabilityTicks:lab.C.hurtInvulnerability,knockback:lab.C.hurtKnockback},probes,checks,failures};
const output=argument('out',null);if(output)fs.writeFileSync(path.resolve(__dirname,output),JSON.stringify(result,null,2));
console.log(JSON.stringify(result,null,2));assert.equal(failures.length,0,failures.join('; '));
