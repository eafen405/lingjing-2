'use strict';

// 一次性逻辑原型：合法观察下，有限恒定输入候选能否兼顾避伤、刀环输出与顺路拾取？
// EnemyLab 是唯一世界规则；不复制碰撞、敌人状态机、刀刃命中或刷新规则。
// Interface: create('linear'|'phase'|'greedy' = 'phase') -> opaque controller;
// decide(observation, controller, lab) -> {move, decision}; spin(observation, move, lab) -> +/-1.
// 每场/重置后重建 controller。decide 可逐 tick 调用；每 6 tick 才重算，
// decision.tick 始终是实际规划起点。返回 decision 按只读使用，move 是独立副本。
// path/predictedPath 含起点和逐 tick 玩家位置；attack 是固定参考旋向 +1 的时间积分，
// 安全集输出全零时，approach 用起终点到最近命中环带的几何距离减少量决胜。
// pickup 只记实际消耗数量；pickupValue 按低血有效回血、真实加刀、总有效回血分层决胜。
// 能量/满刀/满血不提供道具引导；有限候选/前瞻不保证实际无伤，也不声称全局最优。
const FairyPlanner = (() => {
  const INTERVAL = 6, HORIZON = 60, GREEDY_HORIZON = 15;
  const DIRECTIONS = 16, MAX_CANDIDATES = 22, MAX_GUIDES = 4;
  const STEP_BUDGET = MAX_CANDIDATES * HORIZON;
  const ATTACK_EPSILON = 1e-6, APPROACH_EPSILON = 1e-6, CONTINUITY_EPSILON = 1e-12;
  const PICKUP_COMPONENTS = ['urgentHealing', 'blades', 'healing'];
  const copy = p => ({x:p.x, y:p.y});
  const sub = (a, b) => ({x:a.x-b.x, y:a.y-b.y});
  const compareId = (a, b) => a.id < b.id ? -1 : a.id > b.id ? 1 : 0;

  function create(strategy = 'phase') {
    if (!['linear', 'phase', 'greedy', 'phase-short', 'phase-near'].includes(strategy)) {
      throw new RangeError('Unknown FairyPlanner strategy: ' + strategy);
    }
    return {strategy, nextTick:0, move:{x:0, y:0}, decision:null};
  }

  // 确定性 governor：完整轨迹预算；不按墙钟或模型速度截断。
  function governor(strategy) {
    const ticks = strategy === 'greedy' || strategy === 'phase-short' ? GREEDY_HORIZON : HORIZON;
    const scoreTicks = strategy === 'phase-near' ? GREEDY_HORIZON : ticks;
    const limit = Math.min(MAX_CANDIDATES, Math.floor(STEP_BUDGET / ticks));
    return {ticks, scoreTicks, guides:Math.min(MAX_GUIDES, limit - DIRECTIONS - 2)};
  }

  function targets(o, lab, count) {
    const p = o.player.pos;
    const near = (a, b) => a.distance - b.distance || compareId(a, b);
    const enemies = o.enemies.filter(e => e.hp > 0).map(e => {
      let outward = lab.unit(sub(p, e.pos));
      if (Math.hypot(outward.x, outward.y) < 1e-9) outward = {x:1, y:0};
      const pos = lab.add(e.pos, lab.mul(outward, lab.C.bladeRadius));
      return {id:'enemy:' + e.id, pos, distance:lab.dist(p, pos)};
    }).sort(near);
    const items = o.items.map(item => ({
      id:'item:' + item.id, pos:item.pos, distance:lab.dist(p, item.pos),
      benefit:lab.itemBenefit(o.player, item)
    })).filter(target => PICKUP_COMPONENTS.some(key => target.benefit[key] > 0))
      .sort((a, b) => {
        for (const key of PICKUP_COMPONENTS) {
          const difference = b.benefit[key] - a.benefit[key];
          if (difference) return difference;
        }
        return near(a, b);
      });
    // 先各给敌人/道具最多两个引导名额，空缺由另一类补齐。不按安全余量筛候选。
    const chosen = enemies.slice(0, 2).concat(items.slice(0, 2));
    const extra = enemies.slice(2).concat(items.slice(2)).sort(near);
    for (const target of extra) {
      if (chosen.length >= count) break;
      chosen.push(target);
    }
    return chosen.slice(0, count);
  }

  function guidedMove(o, target, lab) {
    const grid = o.grid, origin = o.player.pos, radius = o.player.r;
    if (lab.clear(grid, origin, target, radius)) return lab.unit(sub(target, origin));
    // 只从公开静态图建立自己的 A*；每个引导最多一次 search，总共最多四次。
    const goal = lab.cell(grid, target);
    const tree = lab.search(grid, lab.cell(grid, origin), goal);
    const route = lab.backtrace(grid, tree, goal).reverse();
    for (let i = route.length - 1; i >= 0; i--) {
      if (lab.dist(origin, route[i]) > 1e-6 && lab.clear(grid, origin, route[i], radius)) {
        return lab.unit(sub(route[i], origin));
      }
    }
    return null;
  }

  function candidates(o, lab, guideCount) {
    const out = [];
    for (let i = 0; i < DIRECTIONS; i++) {
      const angle = i * 2 * Math.PI / DIRECTIONS;
      out.push({id:'dir:' + String(i).padStart(2, '0'), move:{x:Math.cos(angle), y:Math.sin(angle)}});
    }
    out.push({id:'idle', move:{x:0, y:0}});
    out.push({id:'current', move:lab.unit(o.player.vel)});
    for (const target of targets(o, lab, guideCount)) {
      const move = guidedMove(o, target.pos, lab);
      if (move && Math.hypot(move.x, move.y) > 1e-9) out.push({id:target.id, move});
    }
    return out;
  }

  // Geometric distance to a blade-hittable band, not distance to an enemy's centre.
  // No extra simulation or enemy-specific safety discount; navigation remains unchanged.
  function attackGap(world, lab) {
    let gap = Infinity;
    for (const enemy of world.enemies) {
      if (enemy.hp <= 0) continue;
      gap = Math.min(gap, Math.max(0,
        Math.abs(lab.dist(world.player.pos, enemy.pos) - lab.C.bladeRadius) - enemy.r));
    }
    return gap === Infinity ? 0 : gap;
  }

  function forecast(o, candidate, model, ticks, lab, scoreTicks = ticks) {
    // 每个候选新建动态 state，grid 由 fromObservation 只读共享；默认禁刀、无未来刷新。
    const world = lab.fromObservation(o, model);
    const startDamage = world.stats.damage, startPickups = world.stats.pickups;
    const path = [copy(world.player.pos)];
    let attack = 0, prefixDamage = 0;
    for (let i = 1; i <= ticks; i++) {
      lab.step(world, candidate.move);
      path.push(copy(world.player.pos));
      // 死亡后不再累积虚构输出；仍保留同样数量的 step 调用与路径采样。
      if (world.player.hp > 0 && i <= scoreTicks) attack += lab.attackRate(world, 1) * lab.C.dt;
      if (i === INTERVAL) prefixDamage = Math.max(0, world.stats.damage - startDamage);
    }
    return {
      id:candidate.id, move:copy(candidate.move),
      damage:Math.max(0, world.stats.damage - startDamage), prefixDamage, attack,
      approach:attackGap(o, lab) - attackGap(world, lab),
      pickup:Math.max(0, world.stats.pickups - startPickups),
      pickupValue:{urgentHealing:world.stats.urgentHealing, blades:world.stats.bladesAdded, healing:world.stats.healing}, path
    };
  }

  function choose(scored, previous) {
    // 分层筛集合，避免带 epsilon 的 sort comparator 不满足传递性。
    const damage = Math.min(...scored.map(c => c.damage));
    let pool = scored.filter(c => c.damage === damage);
    const attack = Math.max(...pool.map(c => c.attack));
    pool = pool.filter(c => attack - c.attack <= ATTACK_EPSILON);
    // 只补零输出的进展信号：安全/实际输出不让位，不把普通避弹延续成永久逃跑。
    const approach = Math.max(...pool.map(c => c.approach));
    const approachActive = attack <= ATTACK_EPSILON && approach > APPROACH_EPSILON;
    if (approachActive) pool = pool.filter(c => approach - c.approach <= APPROACH_EPSILON);
    // 道具只在安全与输出（含零输出接敌）相当后决胜；无效能量球的消耗次数不参加排序。
    for (const key of PICKUP_COMPONENTS) {
      const value = Math.max(...pool.map(c => c.pickupValue[key]));
      pool = pool.filter(c => c.pickupValue[key] === value);
    }
    const continuity = c => -((c.move.x - previous.x) ** 2 + (c.move.y - previous.y) ** 2);
    const closest = Math.max(...pool.map(continuity));
    pool = pool.filter(c => closest - continuity(c) <= CONTINUITY_EPSILON);
    return {chosen:pool.sort(compareId)[0], approachActive};
  }

  function result(controller) {
    return {move:copy(controller.move), decision:controller.decision};
  }

  function decide(observation, controller, lab) {
    const old = controller.decision;
    if (old && observation.tick >= old.tick && observation.tick < controller.nextTick) {
      return result(controller);
    }
    const budget = governor(controller.strategy);
    // greedy = linear + 0.25s 近视，仅作综合对照，不用于 phase vs linear 单因素结论。
    // linear/phase 除 fromObservation 的 model 参数外，共用全部候选、时域和评价规则。
    const model = ['phase', 'phase-short', 'phase-near'].includes(controller.strategy) ? 'phase' : 'linear';
    const previous = !old || observation.tick < old.tick ? lab.unit(observation.player.vel) : controller.move;
    const scored = candidates(observation, lab, budget.guides)
      .map(candidate => forecast(observation, candidate, model, budget.ticks, lab, budget.scoreTicks));
    const {chosen, approachActive} = choose(scored, previous);
    controller.move = copy(chosen.move);
    controller.nextTick = observation.tick + INTERVAL;
    controller.decision = {
      tick:observation.tick, chosenId:chosen.id,
      safeCount:scored.filter(c => c.damage === 0).length,
      predictedDamage:chosen.damage, prefixDamage:chosen.prefixDamage,
      attack:chosen.attack, approach:chosen.approach, approachActive,
      pickup:chosen.pickup, pickupValue:{...chosen.pickupValue},
      candidates:scored, predictedPath:chosen.path
    };
    return result(controller);
  }

  function spin(observation, chosenMove, lab) {
    const current = observation.player.spin === -1 ? -1 : 1;
    if (!observation.enemies.length || observation.player.hp <= 0) return current;
    // 只考察已经选好的移动；旋向得分不回传移动排序，实际最短切换由 world 负责。
    const world = lab.fromObservation(observation, 'phase');
    const move = copy(chosenMove);
    let positive = 0, negative = 0;
    for (let i = 0; i < INTERVAL; i++) {
      lab.step(world, move);
      if (world.player.hp > 0) {
        positive += lab.attackRate(world, 1) * lab.C.dt;
        negative += lab.attackRate(world, -1) * lab.C.dt;
      }
    }
    if (positive > negative + ATTACK_EPSILON) return 1;
    if (negative > positive + ATTACK_EPSILON) return -1;
    return current;
  }

  return Object.freeze({create, decide, spin});
})();

if (typeof module !== 'undefined') module.exports = FairyPlanner;
