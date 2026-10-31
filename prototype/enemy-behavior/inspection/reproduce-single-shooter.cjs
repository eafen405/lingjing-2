// Deterministic diagnostic at the real observe -> decide -> step seam; no test framework.
// A lone, visible shooter must not cause idle/retreat when output is zero and safe approach exists.
const assert=require('node:assert/strict');
const builder=require('../build-fairy-response.prototype.cjs');
const planner=require('../fairy-planner.prototype.js');
const lab=builder.loadLab();
const argument=(name,fallback)=>{const i=process.argv.indexOf('--'+name);return i<0?fallback:process.argv[i+1];};
const strategy=argument('strategy','phase'),ticks=Number(argument('ticks','240'));
const world=lab.scenario('fairy-projectiles',1);
world.enemies=[world.enemies.find(e=>e.kind==='shooter')];
const controller=planner.create(strategy),startPlayer={...world.player.pos},startEnemy={...world.enemies[0].pos};
const startDistance=lab.dist(startPlayer,startEnemy);
const first=planner.decide(lab.observe(world),controller,lab);
const progress=c=>startDistance-lab.dist(c.path.at(-1),startEnemy);
const selected=first.decision.candidates.find(c=>c.id===first.decision.chosenId);
const safeApproaches=first.decision.candidates.filter(c=>c.damage===0&&progress(c)>.5);
const failures=[];
if(first.decision.attack===0&&safeApproaches.length&&progress(selected)<.2){
  failures.push('Zero-output choice idles/retreats despite a safe closing candidate.');
}
let held,minDistance=startDistance;
const decisions=[];
for(let i=0;i<ticks&&world.player.hp>0&&world.enemies.length;i++){
  if(world.tick%6===0){
    const o=lab.observe(world),r=planner.decide(o,controller,lab),d=r.decision;
    const leastDamage=Math.min(...d.candidates.map(c=>c.damage));
    const bestAttack=Math.max(...d.candidates.filter(c=>c.damage===leastDamage).map(c=>c.attack));
    if(d.predictedDamage!==leastDamage)failures.push('Approach overrode minimum predicted damage at tick '+world.tick);
    if(bestAttack-d.attack>1e-6)failures.push('Approach overrode available output at tick '+world.tick);
    if(d.approachActive&&bestAttack>1e-6)failures.push('Approach activated despite nonzero output at tick '+world.tick);
    held={move:r.move,spin:planner.spin(o,r.move,lab)};
    decisions.push({tick:world.tick,move:r.move,id:d.chosenId,attack:d.attack,approach:d.approach,approachActive:d.approachActive,predictedDamage:d.predictedDamage,distance:lab.dist(world.player.pos,world.enemies[0].pos)});
  }
  lab.step(world,held);
  if(world.enemies.length)minDistance=Math.min(minDistance,lab.dist(world.player.pos,world.enemies[0].pos));
}
if(ticks>=240&&['phase','phase-near'].includes(strategy)){
  if(world.stats.kills!==1||world.stats.damageDealt!==lab.C.enemyHp)failures.push('The lone shooter was not eliminated in four seconds.');
  if(world.stats.damage!==0)failures.push('Safe approach must preserve zero actual damage in this pinned scenario.');
}
const result={sourceHashes:builder.sourceHashes(),strategy,ticks:world.tick,startDistance,startPlayer,startEnemy,first:{move:first.move,id:first.decision.chosenId,attack:first.decision.attack,damage:first.decision.predictedDamage,progress:progress(selected)},safeApproaches:safeApproaches.map(c=>({id:c.id,move:c.move,damage:c.damage,attack:c.attack,progress:progress(c)})),actual:{damage:world.stats.damage,damageDealt:world.stats.damageDealt,kills:world.stats.kills,minDistance,player:world.player.pos,enemies:world.enemies.map(e=>({kind:e.kind,hp:e.hp,pos:e.pos,state:e.state}))},decisions,failures};
const output=argument('out',null);if(output)require('node:fs').writeFileSync(require('node:path').resolve(__dirname,output),JSON.stringify(result,null,2));
console.log(JSON.stringify({...result,decisions:process.argv.includes('--trace')?decisions:decisions.filter((d,i)=>i===0||i===6||i===decisions.length-1)},null,2));
assert.equal(failures.length,0,failures.join(' '));
