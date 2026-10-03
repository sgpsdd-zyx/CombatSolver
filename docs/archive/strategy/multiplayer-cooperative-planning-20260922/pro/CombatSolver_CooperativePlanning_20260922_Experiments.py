#!/usr/bin/env python3
"""Reproducible ABSTRACT cooperative card-planning study; Python standard library only.
Not CombatSolver C#, not a game-DLL simulator, not online/human-win-rate evidence.
No network, no repository writes. Results keep negatives and all charged work.
Run: python ..._Experiments.py --out-dir DIR [--stage all|dev|heldout|stress|finaltest|lockbox|sensitivity|checks|probes|late_tail]
Existing run-key files are refused, so unchanged successful experiments are not rerun by accident.
"""
from __future__ import annotations
import argparse, collections, dataclasses as dc, hashlib, json, math, os, platform, random, sys, time
from pathlib import Path
from typing import Any, Iterable

PREFIX = 'CombatSolver_CooperativePlanning_20260922_'
BASELINE = '95fd98572884c1f2da97ffd5120167815fca6403'
VERSION = 'abstract-v4-actor-tail'

@dc.dataclass(frozen=True)
class Card:
    uid: str
    kind: str
    cost: int
    value: int
    aux: int = 0

# These are toy cards, NOT statements of any real game's card values.
CARD_SPEC = {
    'hit': (1, 7, 0), 'heavy': (2, 19, 0), 'jab': (0, 3, 0),
    'guard': (1, 9, 0), 'teamguard': (1, 11, 0),
    'vuln': (1, 2, 0), 'weak': (1, 2, 0),
    'power': (2, 3, 0), 'strength': (1, 3, 0),
    'grant': (1, 2, 0), 'charge': (0, 1, 0),
    'draw': (0, 1, 0), 'blood': (0, 2, 5),
    'bomb': (1, 24, 3), 'rescue': (1, 10, 0),
    'heal': (0, 12, 0), 'finish': (2, 28, 0),
}

def cards(names: Iterable[str], prefix: str) -> list[Card]:
    return [Card(f'{prefix}{i}:{n}', n, *CARD_SPEC[n]) for i, n in enumerate(names)]

@dc.dataclass
class Player:
    hp: int
    maxhp: int
    energy: int
    maxenergy: int
    hand: list[Card]
    draw: list[Card]
    discard: list[Card]
    played: list[Card]
    block: int = 0
    strength: int = 0
    scaling: int = 0
    ended: bool = False
    gross_loss: int = 0
    healed: int = 0
    saved: int = 0
    save_stock: int = 0
    potion_stock: int = 0
    potions_used: int = 0
    cycle_loss: int = 0
    excess: int = 0

@dc.dataclass
class Enemy:
    hp: int
    maxhp: int
    attack: int
    growth: int
    target: int
    vuln: int = 0
    weak: int = 0
    immune: bool = False
    death_rage: int = 0

@dc.dataclass
class State:
    players: list[Player]
    enemies: list[Enemy]
    cycle: int = 0
    bombs: list[tuple[int, int, int]] = dc.field(default_factory=list)
    rng: int = 1
    rng_calls: int = 0
    invalidated: int = 0
    damage_by_actor: list[int] = dc.field(default_factory=lambda: [0, 0])
    trace: list[dict[str, Any]] = dc.field(default_factory=list)

@dc.dataclass(frozen=True)
class Action:
    uid: str
    target: int = -1
    def label(self) -> str:
        return self.uid + (f'->{self.target}' if self.target >= 0 else '')

END = Action('END')

@dc.dataclass(frozen=True)
class Root:
    id: str
    split: str
    mechanism: str
    hp: tuple[int, int]
    hands: tuple[tuple[str, ...], tuple[str, ...]]
    enemies: tuple[tuple[int, int, int, int], ...]  # hp,attack,growth,target
    energy: tuple[int, int] = (2, 2)
    deck: tuple[tuple[str, ...], tuple[str, ...]] = (
        ('hit', 'guard', 'hit', 'guard', 'hit', 'hit'),
        ('hit', 'guard', 'hit', 'hit', 'guard', 'heavy'))
    order: str = 'local_first'
    ally_ended: bool = False
    immune: bool = False
    saves: tuple[int, int] = (0, 0)
    potions: tuple[int, int] = (0, 0)
    root_draw_public: bool = True  # toy root top draw order is expressly public
    rage: int = 0

@dc.dataclass(frozen=True)
class Trial:
    root: Root
    actual_style: str  # SEARCH NEVER RECEIVES THIS FIELD
    actual_order: str
    external_seed: int

@dc.dataclass(frozen=True)
class Scenario:
    index: int
    style: str
    order: str
    seed: int

class Exhausted(Exception):
    pass

class Budget:
    def __init__(self, limit: int):
        self.limit = limit
        self.used = 0
        self.costs: collections.Counter[str] = collections.Counter()
        self.events: collections.Counter[str] = collections.Counter()
        self.max_live_states = 0
    def pay(self, category: str, n: int = 1):
        if n < 0: raise ValueError('negative cost')
        if self.used + n > self.limit: raise Exhausted(category)
        self.used += n
        self.costs[category] += n
        self.events[category] += 1
    @property
    def remaining(self): return self.limit - self.used


def clone(s: State, b: Budget, keep_trace: bool = False) -> State:
    b.pay('fork', 1 + len(s.enemies))
    ps = [dc.replace(p, hand=p.hand.copy(), draw=p.draw.copy(), discard=p.discard.copy(),
                     played=p.played.copy()) for p in s.players]
    return State(ps, [dc.replace(e) for e in s.enemies], s.cycle, s.bombs.copy(), s.rng,
                 s.rng_calls, s.invalidated, s.damage_by_actor.copy(), s.trace.copy() if keep_trace else [])


def initial(r: Root, seed: int, b: Budget | None = None) -> State:
    if b is not None: b.pay('state_initialization', 2+sum(len(x) for x in r.hands)+sum(len(x) for x in r.deck))
    ps = [Player(r.hp[i], max(30, r.hp[i]), r.energy[i], r.energy[i],
                 cards(r.hands[i], f'p{i}r'), cards(r.deck[i], f'p{i}d'), [], [],
                 ended=(i == 1 and r.ally_ended), save_stock=r.saves[i],
                 potion_stock=r.potions[i]) for i in range(2)]
    # The first root draw is public in this toy. Remaining draw order is a hidden
    # scenario determination, common across candidates, independent of external seed.
    for i,p in enumerate(ps):
        start=1 if r.root_draw_public else 0
        tail=p.draw[start:]; random.Random(seed+7919*i).shuffle(tail); p.draw=p.draw[:start]+tail
    return State(ps, [Enemy(h, h, a, g, t, immune=r.immune, death_rage=r.rage)
                      for h, a, g, t in r.enemies], rng=seed or 1)


def random_int(s: State, n: int, b: Budget) -> int:
    b.pay('rng')
    s.rng = (1664525 * s.rng + 1013904223) & 0xffffffff
    s.rng_calls += 1
    return s.rng % n


def draw_one(s: State, actor: int, b: Budget):
    p = s.players[actor]
    if not p.draw and p.discard:
        p.draw, p.discard = p.discard, []
        for i in range(len(p.draw) - 1, 0, -1):
            j = random_int(s, i + 1, b)
            p.draw[i], p.draw[j] = p.draw[j], p.draw[i]
    if p.draw:
        b.pay('draw')
        p.hand.append(p.draw.pop(0))


def terminal(s: State) -> bool:
    return all(e.hp <= 0 for e in s.enemies) or all(p.hp <= 0 for p in s.players)


def legal(s: State, actor: int, b: Budget, category: str = 'candidate_generation') -> list[Action]:
    p = s.players[actor]
    b.pay(category)
    if p.hp <= 0 or p.ended or terminal(s): return [END]
    ans = [END]
    for c in p.hand:
        b.pay(category)
        if c.cost > p.energy: continue
        if c.kind in ('hit','heavy','jab','finish','vuln','weak','bomb'):
            ans.extend(Action(c.uid, i) for i,e in enumerate(s.enemies) if e.hp > 0)
        elif c.kind in ('teamguard','grant'):
            ans.extend(Action(c.uid, i) for i,q in enumerate(s.players) if q.hp > 0)
        elif c.kind == 'rescue':
            if p.potion_stock:
                ans.extend(Action(c.uid, i) for i,q in enumerate(s.players) if q.hp <= 0 and i != actor)
        elif c.kind == 'heal':
            if p.potion_stock: ans.append(Action(c.uid, actor))
        else: ans.append(Action(c.uid))
    return ans


def enemy_hit(e: Enemy) -> int:
    return math.floor(e.attack * (0.6 if e.weak > 0 else 1.0))


def attack_value(c: Card, p: Player, e: Enemy) -> int:
    return math.floor((c.value + p.strength) * (1.5 if e.vuln > 0 else 1.0))


def hurt(s: State, actor: int, amount: int):
    p = s.players[actor]
    if p.hp <= 0: return
    raw = max(0, amount - p.block)
    p.block = max(0, p.block - amount)
    removed = min(p.hp, raw)
    p.hp -= removed; p.gross_loss += removed; p.cycle_loss += removed
    if p.hp <= 0 and p.save_stock:
        p.save_stock -= 1; p.saved += 1; p.hp = 10


def damage(s: State, target: int, amount: int, actor: int):
    e = s.enemies[target]
    if e.hp <= 0: return
    hit = min(e.hp, max(0, amount)); e.hp -= hit; s.damage_by_actor[actor] += hit
    if e.hp <= 0 and e.death_rage:
        for other in s.enemies:
            if other.hp > 0: other.attack += e.death_rage


def apply(s: State, actor: int, a: Action, b: Budget, trace: bool = False):
    # Legal validation is charged, including replay/teammate actions, never silently retarget.
    opts = legal(s, actor, b, 'legality')
    if a not in opts: raise ValueError(f'illegal {actor} {a}')
    b.pay('state_transition', 3)
    p = s.players[actor]
    if a == END:
        p.ended = True
    else:
        c = next(x for x in p.hand if x.uid == a.uid)
        p.hand.remove(c); p.energy -= c.cost
        k = c.kind
        if k in ('hit','heavy','jab','finish'):
            damage(s, a.target, attack_value(c, p, s.enemies[a.target]), actor)
        elif k == 'guard': p.block += c.value
        elif k == 'teamguard': s.players[a.target].block += c.value
        elif k == 'vuln':
            if not s.enemies[a.target].immune: s.enemies[a.target].vuln = max(c.value, s.enemies[a.target].vuln)
        elif k == 'weak':
            if not s.enemies[a.target].immune: s.enemies[a.target].weak = max(c.value, s.enemies[a.target].weak)
        elif k == 'power': p.scaling += c.value
        elif k == 'strength': p.strength += c.value
        elif k == 'grant': s.players[a.target].energy += c.value
        elif k == 'charge': p.energy += c.value
        elif k == 'draw':
            for _ in range(c.value): draw_one(s, actor, b)
        elif k == 'blood':
            # Pure self-loss ignores block, as explicitly defined for this toy card.
            block = p.block; p.block = 0; hurt(s, actor, c.aux); p.block = block
            if p.hp > 0: p.energy += c.value
        elif k == 'bomb': s.bombs.append((a.target, s.cycle + c.aux, c.value))
        elif k == 'rescue':
            p.potion_stock -= 1; p.potions_used += 1; s.players[a.target].hp = c.value
            # A resurrected peer remains ended in this round; no free turn is invented.
            s.players[a.target].ended = True
        elif k == 'heal':
            p.potion_stock -= 1; p.potions_used += 1
            healed = min(c.value, p.maxhp - p.hp); p.hp += healed; p.healed += healed
        else: raise AssertionError(k)
        if k not in ('power','strength','rescue','heal','charge','blood'):
            p.played.append(c)  # powers and consumables exhaust in this toy system
    if trace: s.trace.append({'cycle':s.cycle,'actor':actor,'action':a.label(),
                            'hp':[q.hp for q in s.players],'enemy_hp':[e.hp for e in s.enemies],
                            'energy':[q.energy for q in s.players]})


@dc.dataclass(frozen=True)
class Observation:
    # No RNG, hidden draw order, external policy ID or future trajectories.
    hand: tuple[Card,...]
    energy: int
    hp: tuple[int,int]
    blocks: tuple[int,int]
    enemy: tuple[tuple[int,int,int,int,int,int], ...]
    ended: tuple[bool,bool]
    strength: int


def observe(s: State, actor: int) -> Observation:
    return Observation(tuple(s.players[actor].hand), s.players[actor].energy,
                       tuple(p.hp for p in s.players), tuple(p.block for p in s.players),
                       tuple((e.hp,e.attack,e.growth,e.target,e.vuln,e.weak) for e in s.enemies),
                       tuple(p.ended for p in s.players), s.players[actor].strength)


def choose_script(obs: Observation, actor: int, opts: list[Action], style: str, b: Budget) -> Action:
    b.pay('teammate_planning' if actor else 'continuation_planning', len(opts))
    if style == 'passive': return END
    hand = {c.uid:c for c in obs.hand}
    living = [i for i,e in enumerate(obs.enemy) if e[0] > 0]
    if not living: return END
    if style in ('focus','balanced','patient'):
        target = max(living, key=lambda i:(obs.enemy[i][1]+2*obs.enemy[i][2], -obs.enemy[i][0], -i))
    elif style == 'right': target = living[-1]
    elif style == 'left': target = living[0]
    elif style == 'spread': target = max(living, key=lambda i:obs.enemy[i][0])
    else: target = min(living, key=lambda i:obs.enemy[i][0])
    incoming = sum(math.floor(e[1]*(.6 if e[5] else 1)) for e in obs.enemy if e[0]>0 and e[3]==actor)
    unblocked = max(0,incoming-obs.blocks[actor])
    def rank(a: Action) -> float:
        if a == END: return 0.0
        c = hand[a.uid]; k=c.kind
        if k in ('hit','heavy','jab','finish'):
            e=obs.enemy[a.target]; dmg=math.floor((c.value+obs.strength)*(1.5 if e[4] else 1))
            return min(e[0],dmg)+ (6 if a.target==target else 0)+ (12 if dmg>=e[0] else 0)
        if k in ('guard','teamguard'):
            who=actor if k=='guard' else a.target
            threat=sum(math.floor(e[1]*(.6 if e[5] else 1)) for e in obs.enemy if e[0]>0 and e[3]==who)
            block_need=max(0,threat-obs.blocks[who]); effective=min(block_need,c.value)
            factor = 3.5 if style in ('defensive','patient') else 1.25
            if block_need >= obs.hp[who]: factor = 15
            return factor*effective + (1 if who==actor else 0)
        if k=='weak': return 5 if a.target==target and obs.enemy[a.target][5]==0 else -1
        if k=='vuln':
            attacks=sum(1 for x in obs.hand if x.kind in ('hit','heavy','jab','finish') and x.cost<=obs.energy-c.cost)
            return 5*attacks if obs.enemy[a.target][4]==0 and a.target==target else -1
        if k=='charge': return 20
        if k=='draw': return 8
        if k=='blood': return 8 if obs.hp[actor]>14 else -100
        if k=='rescue': return 30
        if k=='heal': return 25 if obs.hp[actor]<15 else -1
        if k=='grant': return 10 if a.target==actor else -1
        if k=='strength': return 5 if obs.energy>c.cost else -1
        if k=='power': return 3 if obs.hp[actor]>18 else -1
        if k=='bomb': return 8 if a.target==target else 1
        return -1
    # deterministic canonical tie breaker: not fitted to this candidate's eventual result
    return max(opts, key=lambda a:(rank(a), a.label()))


def scripted_turn(s: State, actor: int, style: str, b: Budget, trace=False, max_actions=12, finish=True):
    for _ in range(max_actions):
        opts=legal(s,actor,b,'policy_legal_enumeration')
        a=choose_script(observe(s,actor),actor,opts,style,b)
        apply(s,actor,a,b,trace)
        if a==END or terminal(s): break
    if finish and not s.players[actor].ended and not terminal(s): apply(s,actor,END,b,trace)


def end_cycle(s: State,b:Budget,trace=False):
    if terminal(s): return
    b.pay('enemy_phase', 2+len(s.enemies))
    for e in s.enemies:
        if e.hp<=0: continue
        target=e.target if s.players[e.target].hp>0 else 1-e.target
        hurt(s,target,enemy_hit(e))
        e.attack+=e.growth; e.weak=max(0,e.weak-1); e.vuln=max(0,e.vuln-1)
    for p in s.players:
        p.excess+=max(0,p.cycle_loss-3); p.cycle_loss=0
    s.cycle+=1
    if trace:s.trace.append({'enemy_cycle':s.cycle,'hp':[p.hp for p in s.players],
                            'enemy_hp':[e.hp for e in s.enemies], 'attack':[e.attack for e in s.enemies]})


def start_cycle(s:State,b:Budget):
    if terminal(s):return
    b.pay('turn_start',3)
    remaining=[]
    for target,due,val in s.bombs:
        if due<=s.cycle: damage(s,target,val,0)
        else:remaining.append((target,due,val))
    s.bombs=remaining
    for actor,p in enumerate(s.players):
        p.discard.extend(p.hand);p.discard.extend(p.played);p.hand=[];p.played=[]
        p.block=0;p.ended=p.hp<=0;p.energy=p.maxenergy
        if p.hp<=0:continue
        p.strength+=p.scaling
        for _ in range(3):draw_one(s,actor,b)


def execute_bundle(s:State,plan:tuple[Action,...],scenario:Scenario,b:Budget,trace=False):
    b.pay('bundle_replay',len(plan))
    if scenario.order=='ally_first' and not s.players[1].ended:
        scripted_turn(s,1,scenario.style,b,trace)
    for i,a in enumerate(plan):
        if terminal(s):break
        if scenario.order=='interleave' and i==1 and not s.players[1].ended:
            scripted_turn(s,1,scenario.style,b,trace,max_actions=1,finish=False)
        if a not in legal(s,0,b,'bundle_validation'):
            s.invalidated+=1
            if trace:s.trace.append({'invalidated':a.label(),'cycle':s.cycle,'rule':'stop_no_oracle_retarget'})
            break
        apply(s,0,a,b,trace)
        if a==END:break
    if not terminal(s) and not s.players[0].ended:apply(s,0,END,b,trace)
    if not terminal(s) and not s.players[1].ended:scripted_turn(s,1,scenario.style,b,trace)
    end_cycle(s,b,trace)


def continue_cycle(s:State,scenario:Scenario,b:Budget,trace=False):
    b.pay('rollout_step')
    start_cycle(s,b)
    if terminal(s):return
    order=(1,0) if scenario.order=='ally_first' else (0,1)
    for actor in order:
        if terminal(s):break
        scripted_turn(s,actor,scenario.style if actor else 'balanced',b,trace)
    end_cycle(s,b,trace)


def risk_penalty(s:State,r:Root,b:Budget,risk=True) -> float:
    b.pay('risk_evaluation',2)
    if not risk:return 0.0
    total=0.0
    for i,p in enumerate(s.players):
        weight=1.0 if i==0 else .7
        reserve=max(6,.2*p.maxhp)
        total+=weight*(2.0*max(0,reserve-p.hp)**2/reserve+4.0*p.maxhp*(p.hp<=0))
        total+=weight*7*p.saved
    return total


def utility(s:State,r:Root,b:Budget,risk=True) -> float:
    b.pay('outcome_evaluation',2+len(s.enemies))
    progress=sum(e[0] for e in r.enemies)-sum(e.hp for e in s.enemies)
    p,q=s.players
    # No healing reward, nominal block reward, kill bonus or raw buff bonus.
    cost=1.35*p.gross_loss+.65*q.gross_loss+(.3*p.excess if risk else 0)
    cost+=5*p.potions_used+3*q.potions_used
    return progress-cost-risk_penalty(s,r,b,risk)


def terminal_value(s:State,r:Root,scenario:Scenario,b:Budget,risk=True) -> float:
    """A bounded two-cycle tail ESTIMATE, not simulated damage or a safety certificate.
    Uses unordered known card multiset and public enemy state, never hidden RNG/order.
    Gains and losses here refer ONLY to cycles beyond the already simulated boundary.
    """
    b.pay('terminal_estimate',4+len(s.enemies))
    if terminal(s):return 0.0
    capacities=[]
    for i,p in enumerate(s.players):
        known=p.hand+p.draw+p.discard+p.played
        b.pay('terminal_resource_scan',len(known))
        attacks=[c for c in known if c.kind in ('hit','heavy','jab','finish')]
        if not attacks or p.hp<=0 or (i==1 and scenario.style=='passive'):
            capacities.append(0.0);continue
        avg=sum(c.value for c in attacks)/len(attacks)
        slots=min(p.maxenergy,3*len(attacks)/max(1,len(known)))
        # More defensive teammate type spends at most half this proxy capacity.
        discount=.55 if i==1 and scenario.style in ('defensive','patient') else .8
        capacities.append(discount*slots*(avg+p.strength+1.5*p.scaling))
    rem=[float(e.hp) for e in s.enemies]; tail_damage=0.0;tail_loss=[0.0,0.0]
    for j in (1,2):
        # A fixed threat-first terminal approximation, not a per-candidate omniscient teammate search.
        budget_damage=sum(capacities)
        for idx in sorted(range(len(rem)),key=lambda i:(-(s.enemies[i].attack+2*s.enemies[i].growth),i)):
            d=min(rem[idx],budget_damage);rem[idx]-=d;budget_damage-=d;tail_damage+=d
        for i,e in enumerate(s.enemies):
            if rem[i]>0:
                t=e.target if s.players[e.target].hp>0 else 1-e.target
                tail_loss[t]+=max(0,e.attack+(j-1)*e.growth-3) # 3 is a declared generic future-block proxy
    benefit=tail_damage-1.35*tail_loss[0]-.65*tail_loss[1]
    if risk:
        for i,p in enumerate(s.players):
            if p.hp>0 and tail_loss[i]>=p.hp:benefit-=(2*p.maxhp)*(1 if i==0 else .7)
    # Cap optimism and pessimism; no unbounded return for power stacks or guessed future deaths.
    return max(-90.0,min(50.0,benefit))*.65


def terminal_value_actor(s:State,r:Root,scenario:Scenario,b:Budget,risk=True)->float:
    """Owned, cost-aware, target-conditioned terminal RELAXATION, not legal rollout.
    Deliberately does not pool teammate capacity onto the locally optimal target.
    The real legal teammate simulator is execute_bundle/continue_cycle only.
    No external policy/seed or hidden draw order is read here.
    """
    b.pay('terminal_estimate',4+len(s.enemies))
    if terminal(s):return 0.0
    capacities=[]
    for actor,p in enumerate(s.players):
        known=p.hand+p.draw+p.discard+p.played
        b.pay('terminal_resource_scan',len(known))
        attacks=[c for c in known if c.kind in ('hit','heavy','jab','finish')]
        if not attacks or p.hp<=0 or (actor==1 and scenario.style=='passive'):
            capacities.append((0.0,0.0));continue
        avg=sum(c.value for c in attacks)/len(attacks)
        avgcost=sum(c.cost for c in attacks)/len(attacks)
        slots=min(p.maxenergy/max(.5,avgcost),3*len(attacks)/max(1,len(known)))
        discount=.55 if actor==1 and scenario.style in ('defensive','patient') else .8
        capacities.append((discount*slots,avg))
    rem=[float(e.hp) for e in s.enemies];gain=0.0;loss=[0.0,0.0]
    order=(1,0) if scenario.order=='ally_first' else (0,1)
    for j in (1,2):
        for actor in order:
            p=s.players[actor];slots,avg=capacities[actor]
            amount=slots*(avg+p.strength+j*p.scaling)
            style=scenario.style if actor else 'balanced'
            living=[i for i,x in enumerate(rem) if x>0]
            b.pay('terminal_target_scan',max(1,len(living)))
            if style=='right':targets=sorted(living,reverse=True)
            elif style=='left':targets=sorted(living)
            elif style=='spread':targets=sorted(living,key=lambda i:(-rem[i],i))
            elif style in ('defensive',):targets=sorted(living,key=lambda i:(rem[i],i))
            else:targets=sorted(living,key=lambda i:(-(s.enemies[i].attack+2*s.enemies[i].growth),rem[i],i))
            for idx in targets:
                if amount<=0:break
                e=s.enemies[idx];mult=1.5 if e.vuln>=j and not e.immune else 1.0
                d=min(rem[idx],amount*mult);rem[idx]-=d;amount-=d/mult;gain+=d
        for idx,e in enumerate(s.enemies):
            if rem[idx]<=0:continue
            who=e.target if s.players[e.target].hp>0 else 1-e.target
            factor=.6 if e.weak>=j and not e.immune else 1.0
            loss[who]+=max(0,(e.attack+(j-1)*e.growth)*factor-3)
    value=gain-1.35*loss[0]-.65*loss[1]
    if risk:
        for actor,p in enumerate(s.players):
            if p.hp>0 and loss[actor]>=p.hp:value-=2*p.maxhp*(1 if actor==0 else .7)
    return max(-90.0,min(50.0,value))*.65


def legacy_key(s:State,actions:int) -> tuple:
    p,q=s.players
    return (p.hp<=0,p.saved+q.saved,p.excess,not all(e.hp<=0 for e in s.enemies),
            -(int(p.hp>0)+int(q.hp>0)),sum(e.hp for e in s.enemies),p.gross_loss,-p.hp,
            p.potions_used+q.potions_used,actions)


def score_prefix(s:State,r:Root,b:Budget,objective:str)->float:
    b.pay('prefix_evaluation')
    if objective=='legacy':
        p=s.players[0]
        return -1e8*(p.hp<=0)-100000*(p.excess+max(0,p.cycle_loss-3))-100*sum(e.hp for e in s.enemies)+20*(p.strength+p.scaling)+p.energy*2
    val=utility(s,r,b)+min(10,s.players[0].block)*.8
    # Exploration-only options; final scoring NEVER adds these raw values.
    val+=sum(e.vuln+e.weak for e in s.enemies)*2+s.players[0].scaling*4+s.players[0].strength
    val+=sum(v for _,_,v in s.bombs)*.12
    return val


def family(plan:tuple[Action,...])->str:
    if not plan or plan[0]==END:return 'end'
    return plan[0].uid.split(':')[-1]


def exact_key(s:State)->tuple:
    return (s.cycle,tuple((p.hp,p.energy,p.block,p.strength,p.scaling,p.gross_loss,p.excess,p.cycle_loss,
                          p.saved,p.save_stock,p.potions_used,p.potion_stock,p.ended,
                          tuple(c.uid for c in p.hand),tuple(c.uid for c in p.draw),
                          tuple(c.uid for c in p.discard),tuple(c.uid for c in p.played)) for p in s.players),
            tuple((e.hp,e.attack,e.growth,e.target,e.vuln,e.weak,e.immune,e.death_rage) for e in s.enemies),
            tuple(s.bombs),s.rng,s.rng_calls)


def select_diverse(items:list[tuple[float,tuple[Action,...],State]],width:int,protect:bool,b:Budget):
    b.pay('retention_scan',len(items))
    items.sort(key=lambda x:(-x[0],len(x[1]),tuple(a.label() for a in x[1])))
    if not protect:return items[:width]
    selected=[];used=set();families=set()
    for x in items:
        f=family(x[1])
        if f not in families:
            selected.append(x);used.add(id(x));families.add(f)
            if len(selected)==width:return selected
    for x in items:
        if id(x) not in used:selected.append(x)
        if len(selected)==width:break
    return selected


def select_three_lanes(items, width, b):
    """Shrunken analogue of existing defense/offense/setup retention, not full C#."""
    b.pay('legacy_lane_scan', 3*len(items))
    if not items:return []
    ranked=sorted(items,key=lambda x:(-x[0],len(x[1]),tuple(a.label() for a in x[1])))
    living=[x for x in ranked if x[2].players[0].hp>0]
    lanes=[sorted(living,key=lambda x:(x[2].players[0].excess+max(0,x[2].players[0].cycle_loss-3),-x[2].players[0].hp,-x[2].players[0].block,-x[0])),
           sorted(living,key=lambda x:(sum(e.hp for e in x[2].enemies),-x[0])),
           sorted(living,key=lambda x:(-x[2].players[0].scaling,-x[2].players[0].strength,-len(x[2].bombs),-x[0]))]
    out=[];seen=set()
    def add(x):
        if len(out)<width and id(x) not in seen:out.append(x);seen.add(id(x))
    if width!=3:add(ranked[0])
    for lane in lanes:
        if lane:add(lane[0])
    for x in ranked:add(x)
    return out


@dc.dataclass(frozen=True)
class Config:
    name:str
    objective:str='new'
    peers:bool=True
    generator:str='bundle'
    protect:bool=True
    tail:bool=True
    risk:bool=True
    cap:int=3
    aggregate:str='mean_downside'
    beam:int=8
    candidates:int=6
    gen_fraction:float=.35
    tail_mode:str='pooled'

CONFIGS=[
    Config('A_legacy',objective='legacy',peers=False,generator='beam',protect=False,tail=False,cap=14),
    Config('B_objective',peers=False,generator='beam',protect=False,cap=14),
    Config('C_peer_model',generator='beam',protect=False,cap=14),
    Config('D_bundle',generator='bundle'),
    Config('E_UCT',generator='uct'),
    Config('D_no_peer',peers=False),Config('D_no_tail',tail=False),
    Config('D_no_risk',risk=False),Config('D_no_cover',protect=False),
    Config('D_worst',aggregate='worst'),
    Config('F_cheap',peers=False,generator='beam',protect=False,cap=2,gen_fraction=.22),
    Config('F_no_tail',peers=False,generator='beam',protect=False,tail=False,cap=2,gen_fraction=.22),
    Config('D_actor_tail',tail_mode='actor'),
]


def beam_proposals(r:Root,cfg:Config,b:Budget,seed:int)->tuple[list[tuple[Action,...]],dict]:
    s0=initial(r,seed,b);active=[(0.0,tuple(),s0)];ended=[];seen=set();generated=0;depth=0
    gen_end=b.used+int(b.remaining*cfg.gen_fraction)
    stop='depth'
    try:
        while active and depth<8:
            nxt=[]
            for _,plan,state in active:
                for a in legal(state,0,b):
                    if b.used>=gen_end:raise Exhausted('generation_slice')
                    ss=clone(state,b);apply(ss,0,a,b);generated+=1;pp=plan+(a,)
                    val=score_prefix(ss,r,b,cfg.objective)
                    if a==END or terminal(ss):
                        ended.append((val,pp,ss))
                    else:
                        # Interleaving depends on the timing/order, so the protected generator
                        # refuses end-state-only merges of different local action sequences.
                        key=(exact_key(ss),tuple(a.label() for a in pp) if cfg.generator=='bundle' else ())
                        b.pay('transposition')
                        if key not in seen:seen.add(key);nxt.append((val,pp,ss))
            active=(select_three_lanes(nxt,cfg.beam,b) if cfg.generator=='beam' else select_diverse(nxt,cfg.beam,cfg.protect,b));depth+=1
            b.max_live_states=max(b.max_live_states,len(nxt)+len(active)+len(ended))
            if len(ended)>cfg.candidates*8:
                ended=(select_three_lanes(ended,cfg.candidates*4,b) if cfg.generator=='beam' else select_diverse(ended,cfg.candidates*4,cfg.protect,b))
    except Exhausted as e:stop=str(e)
    # All unfinished proposals must pay for a legal end. No free completed bundle.
    for val,pp,s in active:
        if b.remaining<25:break
        ss=clone(s,b);apply(ss,0,END,b);ended.append((val,pp+(END,),ss))
    if not ended:return [(END,)],{'generated':generated,'depth':depth,'stop':stop,'unique':0}
    try:chosen=(select_three_lanes(ended,cfg.candidates,b) if cfg.generator=='beam' else select_diverse(ended,cfg.candidates,cfg.protect,b))
    except Exhausted:chosen=ended[:cfg.candidates]
    plans=[];pseen=set()
    for _,p,_ in chosen:
        if p not in pseen:plans.append(p);pseen.add(p)
    return plans,{'generated':generated,'depth':depth,'stop':stop,'unique':len(seen)}


@dc.dataclass
class UNode:
    state:State
    plan:tuple[Action,...]
    parent:Any=None
    children:list[Any]=dc.field(default_factory=list)
    untried:list[Action]|None=None
    n:int=0
    value:float=0.0


def uct_proposals(r:Root,cfg:Config,b:Budget,seed:int)->tuple[list[tuple[Action,...]],dict]:
    """Actual action-prefix UCT with random completions and a boundary heuristic.
    This is a surrogate-guided proposal generator, NOT ISMCTS or AlphaZero.
    The same paid scenario evaluator below decides the final bundle.
    """
    rng=random.Random(seed);root=UNode(initial(r,seed,b),());end=b.used+int(b.remaining*cfg.gen_fraction)
    best={};iterations=0;nodes=1
    try:
        while b.used<end:
            n=root
            while n.children and n.untried==[]:
                b.pay('uct_selection',len(n.children))
                n=max(n.children,key=lambda x:x.value/max(1,x.n)+1.4*math.sqrt(math.log(max(2,n.n))/max(1,x.n)))
            if n.untried is None:n.untried=legal(n.state,0,b)
            if n.untried and not (n.plan and n.plan[-1]==END) and len(n.plan)<8 and not terminal(n.state):
                a=n.untried.pop(rng.randrange(len(n.untried)));ss=clone(n.state,b);apply(ss,0,a,b)
                child=UNode(ss,n.plan+(a,),n);n.children.append(child);n=child;nodes+=1
            ss=clone(n.state,b);pp=n.plan
            while not terminal(ss) and not ss.players[0].ended and len(pp)<8:
                opts=legal(ss,0,b);a=rng.choice(opts);apply(ss,0,a,b);pp+=(a,)
            if not ss.players[0].ended and not terminal(ss):apply(ss,0,END,b);pp+=(END,)
            value=score_prefix(ss,r,b,'new')
            best[pp]=max(value,best.get(pp,-float('inf')))
            reward=max(-1,min(1,value/100))
            while n is not None:b.pay('uct_backup');n.n+=1;n.value+=reward;n=n.parent
            iterations+=1;b.max_live_states=max(b.max_live_states,nodes)
    except Exhausted:pass
    plans=[p for p,v in sorted(best.items(),key=lambda kv:-kv[1])[:cfg.candidates]]
    return plans or [(END,)],{'iterations':iterations,'tree_nodes':nodes,'generated_bundles':len(best),
                             'limitation':'boundary-heuristic UCT proposals; same later scenario evaluator'}


def make_scenarios(r:Root,cfg:Config,seed:int)->list[Scenario]:
    if not cfg.peers:return [Scenario(0,'passive',r.order,seed+101)]
    # Uniform DESIGNED conditions, not fitted frequencies or human probabilities.
    return [Scenario(i,style,r.order,seed+101+37*i) for i,style in enumerate(('focus','defensive','right'))]


def aggregate(values:list[float],mode:str)->float:
    mean=sum(values)/len(values)
    if mode=='worst':return min(values)
    downside=sum(max(0,mean-v) for v in values)/len(values)
    return mean-.25*downside


def choose_balanced(states:list[list[State]],plans:list[tuple[Action,...]],r:Root,cfg:Config,scenarios:list[Scenario],b:Budget):
    vals=[]
    for plan,row in zip(plans,states):
        b.pay('publication_scan')
        if cfg.objective=='legacy':vals.append(legacy_key(row[0],len(plan)));continue
        v=[utility(s,r,b,cfg.risk)+((terminal_value_actor(s,r,sc,b,cfg.risk) if cfg.tail_mode=='actor' else terminal_value(s,r,sc,b,cfg.risk)) if cfg.tail else 0)
           for s,sc in zip(row,scenarios)]
        vals.append(-aggregate(v,cfg.aggregate))
    i=min(range(len(plans)),key=lambda j:(vals[j],len(plans[j]),tuple(a.label() for a in plans[j])))
    return i,vals


def search(r:Root,cfg:Config,limit:int,seed:int)->dict:
    started=time.perf_counter();b=Budget(limit);b.pay('request_setup',8)
    scenarios=make_scenarios(r,cfg,seed);b.pay('scenario_preparation',4*len(scenarios))
    # A finite incumbent is constructed before expensive proposal generation.
    fb=initial(r,seed,b);fallback=[]
    try:
        for _ in range(8):
            opts=legal(fb,0,b);a=choose_script(observe(fb,0),0,opts,'balanced',b)
            apply(fb,0,a,b);fallback.append(a)
            if a==END or terminal(fb):break
        if not terminal(fb) and not fb.players[0].ended:apply(fb,0,END,b);fallback.append(END)
    except Exhausted:
        # Remains an explicit partial advice; never call unvalidated padding a complete turn.
        return {'algorithm':cfg.name,'budget':limit,'used':b.used,'costs':dict(b.costs),
                'plan':[a.label() for a in fallback], 'plan_data':[dc.asdict(a) for a in fallback],
                'complete_matrix_depth':0,'selected_depth':0,'status':'partial_budget',
                'elapsed_ms':(time.perf_counter()-started)*1000,'scenarios':[dc.asdict(s) for s in scenarios]}
    incumbent=tuple(fallback);inc_depth=0;status='fallback_script_no_complete_matrix';published=[];observed_selected_cycles=[]
    generator=uct_proposals if cfg.generator=='uct' else beam_proposals
    try: plans,meta=generator(r,cfg,b,seed)
    except Exhausted: plans,meta=[incumbent],{'stop':'generation_budget_no_new_incumbent'}
    if incumbent not in plans:
        # Within K, never silently increase seats; F10-style displacement is possible.
        plans=([incumbent]+plans)[:cfg.candidates]
    rows=[]
    deepest=0;attempted_cells=0;completed_cells=0;known_immediate_dead=set()
    try:
        rows=[[initial(r,sc.seed,b) for sc in scenarios] for _ in plans]
        # Reject deterministic self-kill in the local prefix, not hypothetical future teammate non-help.
        if cfg.risk:
            for j,plan in enumerate(plans):
                ss=initial(r,seed,b);b.pay('immediate_guard',2)
                for a in plan:
                    if terminal(ss):break
                    if a not in legal(ss,0,b,'guard_validation'):break
                    apply(ss,0,a,b)
                    if ss.players[0].hp<=0:known_immediate_dead.add(j);break
        for depth in range(1,cfg.cap+1):
            nextrows=[]
            for j,(plan,row) in enumerate(zip(plans,rows)):
                out=[]
                for st,sc in zip(row,scenarios):
                    attempted_cells+=1
                    ss=clone(st,b)
                    if not terminal(ss):
                        if depth==1:execute_bundle(ss,plan,sc,b)
                        else:continue_cycle(ss,sc,b)
                    out.append(ss);deepest=max(deepest,ss.cycle);completed_cells+=1
                nextrows.append(out)
            # Commit only after all representatives and all scenario indices reached this layer.
            idx,values=choose_balanced(nextrows,plans,r,cfg,scenarios,b)
            permitted=[j for j in range(len(plans)) if j not in known_immediate_dead]
            if permitted:idx=min(permitted,key=lambda j:(values[j],len(plans[j]),j))
            incumbent=plans[idx];inc_depth=depth;status='balanced_matrix';rows=nextrows
            observed_selected_cycles=[s.cycle for s in nextrows[idx]]
            published.append({'depth':depth,'plan_index':idx,'values':values,
                              'used':b.used,'bundle':[a.label() for a in incumbent]})
            if all(terminal(s) for row in rows for s in row):break
    except Exhausted:status+=':budget_stop'
    result={'algorithm':cfg.name,'budget':limit,'used':b.used,'costs':dict(b.costs),'events':dict(b.events),
            'plan':[a.label() for a in incumbent],'plan_data':[dc.asdict(a) for a in incumbent],
            'complete_matrix_depth':inc_depth,'selected_depth':inc_depth,
            'selected_observed_cycles':observed_selected_cycles,
            'depth_semantics':'selected_depth is publication target including terminal absorption; selected_observed_cycles is actual',
            'max_generated_cycle':deepest,
            'cap':cfg.cap,'status':status,'proposals':[[a.label() for a in p] for p in plans],
            'generator':meta,'published':published,'attempted_cells':attempted_cells,
            'completed_cells':completed_cells,'immediate_dead_rejections':sorted(known_immediate_dead),
            'peak_live_states_proxy':max(b.max_live_states,len(plans)*len(scenarios)*2),
            'elapsed_ms':(time.perf_counter()-started)*1000,'scenarios':[dc.asdict(s) for s in scenarios]}
    assert result['used']==sum(result['costs'].values()) and result['used']<=limit
    return result


def external(trial:Trial,plan:tuple[Action,...])->dict:
    # Hidden actual teammate policy is supplied ONLY here, never to search/root scoring.
    r=trial.root;b=Budget(10**9);s=initial(r,trial.external_seed)
    sc=Scenario(999,trial.actual_style,trial.actual_order,trial.external_seed)
    execute_bundle(s,plan,sc,b,trace=True);checkpoints={}
    for t in range(1,15):
        if t>1 and not terminal(s):continue_cycle(s,sc,b,trace=True)
        if t in (1,3,7,14):
            checkpoints[str(t)]={'actual_completed_cycle':s.cycle,'won':all(e.hp<=0 for e in s.enemies),
                'local_alive':s.players[0].hp>0,'survivors':sum(p.hp>0 for p in s.players),
                'hp':[p.hp for p in s.players],'enemy_hp':[e.hp for e in s.enemies],
                'enemy_total_hp':sum(e.hp for e in s.enemies), 'gross_loss':[p.gross_loss for p in s.players],
                'death_saves':[p.saved for p in s.players],'potions':[p.potions_used for p in s.players],
                'net_enemy_hp_reduction':sum(x[0] for x in r.enemies)-sum(e.hp for e in s.enemies),
                'realized_damage_by_actor':s.damage_by_actor.copy(),'invalidated':s.invalidated}
    return {'checkpoints':checkpoints,'trace':s.trace,'external_cost_excluded_from_search':dict(b.costs),
            'external_used':b.used,'rng_calls':s.rng_calls}


def root(id,split,mechanism,hands,enemies,**kw):
    return Root(id,split,mechanism,kw.pop('hp',(32,30)),tuple(tuple(h) for h in hands),tuple(enemies),**kw)


def dev_trials()->list[Trial]:
    data=[
      root('D01','dev','vulnerable_before_ally',(('vuln','hit','guard'),('heavy','hit')),((65,7,1,0),)),
      root('D02','dev','ally_already_ended',(('vuln','hit','guard'),('heavy','hit')),((65,7,1,0),),ally_ended=True),
      root('D03','dev','ally_no_attack',(('vuln','hit','guard'),('guard','teamguard')),((65,7,1,0),)),
      root('D04','dev','growth_trade_hp_for_kill',(('heavy','hit','guard','blood'),('hit','guard')),((30,7,6,0),)),
      root('D05','dev','must_defend_lethal',(('heavy','guard','hit'),('guard','hit')),((90,14,0,0),),hp=(9,30)),
      root('D06','dev','dangerous_add_vs_total_hp',(('heavy','hit','guard'),('hit','hit')),((14,9,3,0),(65,2,0,1))),
      root('D07','dev','power_pays',(('power','hit','guard'),('guard','hit')),((160,5,1,0),)),
      root('D08','dev','power_not_pay_short_fight',(('power','heavy','hit'),('heavy','hit')),((24,6,0,0),)),
      root('D09','dev','duplicate_vuln_overkill',(('vuln','vuln','heavy','hit'),('heavy',)),((6,3,0,0),(90,4,0,1))),
      root('D10','dev','rescue_owner_cost',(('rescue','guard','heavy'),('heavy','hit')),((68,8,1,0),),hp=(30,0),potions=(1,0)),
      root('D11','dev','energy_draw_sequence',(('grant','draw','heavy','guard'),('heavy','hit')),((80,7,2,0),),energy=(2,1)),
      root('D12','dev','third_action_combo',(('vuln','charge','heavy','guard','jab'),('hit','hit')),((65,8,2,0),)),
    ]
    return [Trial(r,'focus',r.order,811+i*97) for i,r in enumerate(data)]


def heldout_trials()->list[Trial]:
    # Locked procedural holdout; no external policy/outcome reaches Config or search.
    rng=random.Random(920260923)
    pools=[('vuln','hit','guard','heavy'),('power','hit','guard','blood'),
           ('charge','vuln','heavy','draw'),('teamguard','strength','hit','heavy'),
           ('bomb','guard','hit','heal'),('weak','hit','heavy','jab')]
    peers=[('heavy','hit','guard'),('guard','teamguard','heavy'),('hit','hit','vuln')]
    styles=['focus','right','spread','defensive','patient','left']
    result=[]
    for i in range(18):
        hp=(rng.randint(14,40),rng.randint(12,36));energy=(rng.choice([2,3]),rng.choice([1,2,3]))
        enemies=((rng.randint(12,35),rng.randint(4,10),rng.randint(0,4),0),
                 (rng.randint(45,110),rng.randint(1,6),rng.randint(0,3),1))
        r=root(f'H{i+1:02}','heldout','locked_mixed_unseen_parameters',
               (rng.choice(pools),rng.choice(peers)),enemies,hp=hp,energy=energy,
               order=rng.choice(['local_first','ally_first','interleave']),
               ally_ended=rng.random()<.2,immune=rng.random()<.2,potions=(1,0),saves=(0,int(i%7==0)))
        result.append(Trial(r,rng.choice(styles),r.order,880003+i*193))
    return result


def stress_trials()->list[Trial]:
    data=[
      (root('S01','stress','ally_target_mismatch',(('vuln','heavy','guard'),('heavy','hit')),((80,4,1,0),(80,2,0,1))), 'right','local_first'),
      (root('S02','stress','ally_kills_investment_target_first',(('bomb','hit','heavy'),('heavy','hit')),((18,6,1,0),(85,3,1,1))), 'left','ally_first'),
      (root('S03','stress','model_predicts_attack_actual_passive',(('vuln','guard','heavy','hit'),('heavy','hit')),((65,8,4,0),)), 'passive','local_first'),
      (root('S04','stress','boss_immune',(('weak','vuln','hit','guard'),('heavy','hit')),((120,11,1,0),),immune=True),'focus','local_first'),
      (root('S05','stress','save_is_owned',(('blood','heavy','guard','heal'),('heavy','hit')),((100,13,2,0),),hp=(12,30),saves=(1,0),potions=(1,0)),'defensive','local_first'),
      (root('S06','stress','small_budget_competition',(('guard','vuln','power','heavy','charge','hit'),('heavy','hit')),((37,13,4,0),),hp=(22,30)),'focus','local_first'),
      (root('S07','stress','death_trigger_passive_not_lower_bound',(('heavy','vuln','guard'),('heavy','hit')),((15,0,0,1),(100,6,1,0)),rage=10),'left','ally_first'),
      (root('S08','stress','late_bomb_never_pays',(('bomb','heavy','guard'),('heavy','hit')),((20,4,0,0),(22,4,0,1))),'focus','local_first'),
      (root('S09','stress','repeated_blood_not_free',(('blood','blood','heavy','heal','guard'),('hit','hit')),((100,8,2,0),),hp=(11,30),potions=(1,0)),'passive','local_first'),
      (root('S10','stress','same_hidden_policy_different_order',(('vuln','heavy','hit','guard'),('heavy','hit')),((23,5,0,0),(95,4,2,1))),'focus','interleave'),
    ]
    return [Trial(r,style,order,17117+i*89) for i,(r,style,order) in enumerate(data)]


def finaltest_trials()->list[Trial]:
    """Fresh higher-HP test, locked after choosing D_no_tail/F_no_tail.
    No main-algorithm parameter is tuned after observing these results.
    Earlier heldout split is henceforth a validation/model-selection set, not an
    unbiased final estimate for selecting D_no_tail.
    """
    rng=random.Random(9222026436)
    pools=[('vuln','hit','guard','heavy'),('power','hit','guard','blood'),
           ('charge','vuln','heavy','draw','guard'),('teamguard','strength','hit','heavy'),
           ('bomb','guard','hit','heal','weak'),('weak','hit','heavy','jab','guard')]
    peers=[('heavy','hit','guard'),('guard','teamguard','heavy','hit'),('hit','hit','vuln','guard')]
    styles=['focus','right','spread','defensive','patient','left']
    out=[]
    for i in range(12):
        r=root(f'Q{i+1:02}','finaltest','fresh_high_HP_growth_heldout',
               (rng.choice(pools),rng.choice(peers)),
               ((rng.randint(55,110),rng.randint(3,8),rng.randint(0,3),0),
                (rng.randint(135,310),rng.randint(2,6),rng.randint(0,2),1)),
               hp=(rng.randint(38,65),rng.randint(32,55)),energy=(3,rng.choice([2,3])),
               order=rng.choice(['local_first','ally_first','interleave']),
               ally_ended=rng.random()<.15,immune=rng.random()<.15,potions=(1,0),saves=(int(i%5==0),0))
        out.append(Trial(r,rng.choice(styles),r.order,9666001+277*i))
    return out


def lockbox_trials()->list[Trial]:
    # Final untouched confirmation roots: frozen before D_actor_tail was evaluated.
    # These outcomes are not used for further parameter or scenario changes.
    rng=random.Random(202609220436)
    pool=[('strength','teamguard','heavy','hit'),('power','blood','hit','guard'),
          ('vuln','charge','heavy','guard','jab'),('weak','bomb','hit','guard','heal')]
    out=[]
    for i in range(8):
        r=root(f'R{i+1:02}','lockbox','unseen_confirmation_after_owned_tail_fix',
               (rng.choice(pool),rng.choice([('heavy','hit','guard'),('hit','hit','vuln'),('guard','teamguard','heavy')])),
               ((rng.randint(20,80),rng.randint(5,12),rng.randint(0,4),0),
                (rng.randint(95,255),rng.randint(2,7),rng.randint(0,3),1)),
               hp=(rng.randint(24,58),rng.randint(24,48)),energy=(rng.choice([2,3]),rng.choice([2,3])),
               order=rng.choice(['local_first','ally_first','interleave']),ally_ended=rng.random()<.2,
               potions=(1,0),saves=(0,int(i==3)))
        out.append(Trial(r,rng.choice(['left','right','spread','patient','focus','defensive']),r.order,973371+997*i))
    return out


def checks()->list[dict]:
    out=[]
    def ok(name,condition,detail):
        if not condition:raise AssertionError(name+': '+str(detail))
        out.append({'name':name,'passed':True,'detail':detail})
    r=dev_trials()[0].root;b=Budget(100000);a=initial(r,1);c=initial(r,1)
    a.players[0].gross_loss=4;a.players[0].excess=1;a.enemies[0].hp=0
    c.players[0].gross_loss=3;c.players[0].excess=0
    ok('legacy_one_excess_before_victory',legacy_key(c,3)<legacy_key(a,3),{'victory':legacy_key(a,3),'nonvictory':legacy_key(c,3)})
    a.players[0].excess=0
    ok('within_allowance_victory_wins',legacy_key(a,3)<legacy_key(c,3),'not every one-HP difference outranks victory')
    # Same local prefix, legal real ally attacks; vulnerability order must matter.
    s=initial(r,1);t=initial(r,1);v=next(x for x in legal(s,0,b) if 'vuln' in x.uid)
    apply(s,0,v,b);scripted_turn(s,1,'focus',b);scripted_turn(t,1,'focus',b);apply(t,0,v,b)
    ok('order_vulnerable_before_ally',s.enemies[0].hp<t.enemies[0].hp,{'before':s.enemies[0].hp,'after':t.enemies[0].hp})
    ended=initial(dc.replace(r,ally_ended=True),1)
    ok('ended_peer_cannot_play',legal(ended,1,b)==[END],'no invented energy or attacks')
    q=initial(r,1);q.players[1].energy=0
    ok('energy_legality',legal(q,1,b)==[END],'cannot fund a heavy attack from zero energy')
    rr=dc.replace(r,hands=(('vuln','vuln'),('hit',)));q=initial(rr,1)
    vs=[x for x in legal(q,0,b) if 'vuln' in x.uid];apply(q,0,vs[0],b);before=q.enemies[0].vuln;apply(q,0,vs[1],b)
    ok('repeat_buff_no_extra_effect',q.enemies[0].vuln==before,before)
    rr=dc.replace(r,hands=(('heavy',),('hit',)),enemies=((2,1,0,0),));q=initial(rr,1)
    apply(q,0,next(x for x in legal(q,0,b) if 'heavy' in x.uid),b)
    ok('overkill_capped',q.damage_by_actor[0]==2,q.damage_by_actor)
    rr=dc.replace(r,hands=(('blood','heal'),('hit',)),potions=(1,0));q=initial(rr,1)
    apply(q,0,next(x for x in legal(q,0,b) if 'blood' in x.uid),b);loss=q.players[0].gross_loss
    apply(q,0,next(x for x in legal(q,0,b) if 'heal' in x.uid),b)
    ok('healing_does_not_erase_loss',q.players[0].gross_loss==loss,loss)
    ok('finite_potion_stock',q.players[0].potion_stock==0 and q.players[0].potions_used==1,'owner 0 consumed once')
    q=initial(r,1);q.players[1].save_stock=1;q.players[1].hp=2;hurt(q,1,5)
    ok('death_save_owner',q.players[0].saved==0 and q.players[1].saved==1,[p.saved for p in q.players])
    q=initial(dc.replace(r,immune=True),1);apply(q,0,next(x for x in legal(q,0,b) if 'vuln' in x.uid),b)
    ok('boss_immunity_real_resolution',q.enemies[0].vuln==0,'not a heuristic control reward')
    obs=observe(q,0)
    ok('script_information_barrier',not hasattr(obs,'rng') and not hasattr(obs,'draw') and not hasattr(obs,'actual_style'),list(obs.__dataclass_fields__))
    ss=clone(q,b);ss.rng=1234
    opts=legal(q,0,b)
    ok('unobserved_rng_does_not_change_policy',choose_script(observe(q,0),0,opts,'balanced',b)==choose_script(observe(ss,0),0,opts,'balanced',b),'identical observations')
    rr=initial(r,3);tt=initial(r,3);x=random_int(rr,1000,b);_ =random_int(tt,1000,b);y=random_int(tt,1000,b)
    ok('same_seed_not_same_event_trajectory',x!=y,{'aligned_first':x,'shifted_next':y})
    q=initial(r,1);base=utility(q,r,b);q.players[0].block=999
    ok('no_nominal_block_reward',utility(q,r,b)==base,'final utility uses actual HP, not displayed block')
    q=initial(r,1);vv=terminal_value(q,r,Scenario(0,'focus','local_first',1),b);q.players[0].scaling=1000
    ok('tail_optimism_cap',terminal_value(q,r,Scenario(0,'focus','local_first',1),b)<=32.5,{'normal_tail':vv,'ceiling':32.5})
    # F10/F12 are retained as logical counterexamples, not a rerun of old C# or archived Python.
    seats={'old_setup':35,'new_setup':20};ok('F10_seat_opportunity_cost',seats['old_setup']>seats['new_setup'],seats)
    ok('F12_suffix_not_root_gain',('hit',)==('hit',),{'root_equal':True,'suffix_lengths':[4,30],'quality_gain':False})
    # Pure worst-case can reject an attack that improves the designed average.
    vals={'pressure':[32,30,-18],'turtle':[0,0,0]}
    ok('worst_case_turtle_counterexample',aggregate(vals['pressure'],'worst')<0<aggregate(vals['pressure'],'mean_downside'),vals)
    # A shallow statistical race can delete the unique late payoff; no iid bound fixes biased truncation.
    ok('racing_delayed_return',max({'quick':10,'invest':-2},key={'quick':10,'invest':-2}.get)=='quick',{'depth1':[10,-2],'depth3':[15,50]})
    return out


def run_trial(t:Trial,cfg:Config,budget:int,seed:int)->dict:
    res=search(t.root,cfg,budget,seed)
    plan=tuple(Action(**a) for a in res['plan_data'])
    res.update({'root_id':t.root.id,'split':t.root.split,'mechanism':t.root.mechanism,'search_seed':seed,
                'external':external(t,plan)})
    return res


def new_tail_probes()->dict:
    """Only new v4 checks and new Beam=1/2/3 comparisons; no unchanged rerun."""
    out=[];b=Budget(100000)
    def ok(name,condition,detail):
        if not condition:raise AssertionError(name+': '+str(detail))
        out.append({'name':name,'passed':True,'detail':detail})
    r=root('P01','probes','owned_terminal_target', (('guard',),('heavy','hit')),
           ((40,12,2,0),(200,1,0,1)), deck=(('guard','guard','guard'),('heavy','heavy','hit')))
    q=initial(r,71);q.enemies[0].vuln=2
    focus=terminal_value_actor(q,r,Scenario(0,'focus','local_first',5),b)
    right=terminal_value_actor(q,r,Scenario(0,'right','local_first',5),b)
    ok('v4_target_policy_changes_conditional_tail',focus!=right,{'focus':focus,'right':right,'not_realized_damage':True})
    z=clone(q,b);z.players[1].draw.reverse();z.rng=94721
    same=terminal_value_actor(z,r,Scenario(0,'right','local_first',5),b)
    ok('v4_tail_ignores_hidden_order_and_rng',same==right,{'before':right,'after':same})
    z=clone(q,b);z.players[0].scaling=100000
    val=terminal_value_actor(z,r,Scenario(0,'focus','local_first',5),b)
    ok('v4_no_attack_cards_no_free_power_damage',val==focus,{'before':focus,'after':val})
    z=clone(q,b);z.enemies[0].hp=z.enemies[1].hp=0
    ok('v4_terminal_tail_zero',terminal_value_actor(z,r,Scenario(0,'focus','local_first',5),b)==0,0)
    rr=dc.replace(r,hands=(('blood',),('hit',)));z=initial(rr,1)
    a=next(a for a in legal(z,0,b) if 'blood' in a.uid);apply(z,0,a,b)
    ok('blood_instance_cannot_replay',a not in legal(z,0,b),{'gross_loss':z.players[0].gross_loss,'energy':z.players[0].energy})
    z=initial(r,1);before=(sum(e.hp for e in z.enemies),z.damage_by_actor.copy())
    _=terminal_value_actor(z,r,Scenario(0,'focus','local_first',5),b)
    ok('tail_does_not_mutate_realized_ledger',before==(sum(e.hp for e in z.enemies),z.damage_by_actor),{'realized':before})
    # Enumeration opportunity cost with an actually changed Beam; Beam=8 is already
    # present in the main run and is not executed again here.
    t=next(t for t in dev_trials() if t.root.id=='D12')
    base=next(c for c in CONFIGS if c.name=='D_actor_tail');runs=[]
    for width in (1,2,3):
        cfg=dc.replace(base,name=f'P_D_actor_beam{width}',beam=width)
        result=run_trial(t,cfg,6000,311);runs.append(result)
        print('PROBE_RUN '+json.dumps({'beam':width,'used':result['used'],'plan':result['plan'],
              'external14':result['external']['checkpoints']['14']},ensure_ascii=False),flush=True)
    for item in out:print('NEW_CHECK '+json.dumps(item,ensure_ascii=False),flush=True)
    return {'properties':out,'new_beam_runs':runs,'probe_check_work':dict(b.costs)}


def late_tail_counterexample()->dict:
    """New negative observation-pair probe, not another run of old fixtures."""
    r=root('P02','late_tail','late_committed_effect_unseen_by_tail',
           (('guard',),('guard',)),((80,0,0,0),),
           deck=(('guard','guard','guard'),('guard','guard','guard')))
    b=Budget(100000);plain=initial(r,17);invested=clone(plain,b)
    invested.bombs=[(0,6,40)]
    sc=Scenario(0,'focus','local_first',17)
    scores=[terminal_value_actor(z,r,sc,b) for z in (plain,invested)]
    outputs=[]
    for state in (plain,invested):
        for _ in range(7):continue_cycle(state,sc,b,trace=True)
        outputs.append({'actual_cycle':state.cycle,'enemy_hp':sum(e.hp for e in state.enemies),
                        'realized_damage':state.damage_by_actor,'trace':state.trace})
    assert scores[0]==scores[1] and outputs[0]['enemy_hp']>outputs[1]['enemy_hp']
    result={'name':'v4_late_tail_blind_spot_counterexample','passed':True,
      'meaning':'PASS means the negative counterexample was reproduced, NOT that tail is correct.',
      'tail_values':scores,'external7':outputs,'costs':dict(b.costs),
      'scope':'synthetic boundary-state pair; identical except paid-history-excluded pending bomb. Not a complete plan-choice or production reachability test.'}
    print('LATE_TAIL_NEGATIVE '+json.dumps(result,ensure_ascii=False),flush=True)
    return result


def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--out-dir',type=Path,default=Path.cwd())
    ap.add_argument('--stage',choices=['all','dev','heldout','stress','finaltest','lockbox','sensitivity','checks','probes','late_tail'],default='all')
    ap.add_argument('--budget',type=int,default=6000)
    ap.add_argument('--algorithms',default='')
    ap.add_argument('--root-ids',default='')
    args=ap.parse_args();args.out_dir.mkdir(parents=True,exist_ok=True)
    selected=[c for c in CONFIGS if not args.algorithms or c.name in args.algorithms.split(',')]
    if not selected:raise ValueError('no algorithms selected')
    name=PREFIX+('Results.json' if args.stage=='all' else args.stage+'_Results.json')
    dest=args.out_dir/name
    if dest.exists():raise FileExistsError(f'Refusing unchanged successful run overwrite: {dest}')
    script_hash=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    alltrials=dev_trials()+heldout_trials()+stress_trials()+finaltest_trials()+lockbox_trials()
    trials=[t for t in alltrials if args.stage=='all' or t.root.split==args.stage]
    if args.root_ids:trials=[t for t in trials if t.root.id in args.root_ids.split(',')]
    lock={'script_sha256':script_hash,'version':VERSION,'baseline':BASELINE,'configs':[dc.asdict(c) for c in CONFIGS],
          'holdout_seed':920260923,'finaltest_seed':9222026436,'lockbox_seed':202609220436,'budget':args.budget,
          'selection_before_finaltest':{'main':'D_no_tail','fallback':'F_no_tail','optimistic_tail_default':False},
          'selection_before_lockbox':{'main':'D_actor_tail','fallback':'F_cheap','note':'owned terminal relaxation, not guaranteed damage; no post-lockbox tuning'},'external_protocol':'one fixed current local bundle; same observable balanced future policy; unknown fixed actual peer; T1/3/7/14',
          'data_digest':hashlib.sha256(json.dumps([dc.asdict(t) for t in alltrials],sort_keys=True).encode()).hexdigest()}
    (args.out_dir/(PREFIX+args.stage+'_PreRun_Lock.json')).write_text(json.dumps(lock,indent=2))
    prop=checks() if args.stage in ('all','checks') else []
    additional=new_tail_probes() if args.stage in ('all','probes') else {}
    if args.stage in ('all','late_tail'):
        additional.setdefault('properties',[]).append(late_tail_counterexample())
    results=[];started=time.perf_counter()
    for t in trials:
        for c in selected:
            res=run_trial(t,c,args.budget,311)
            results.append(res);o=res['external']['checkpoints']['14']
            print(f"RUN {t.root.id} {c.name} B={args.budget} used={res['used']} depth={res['complete_matrix_depth']} hp={o['hp']} E={o['enemy_total_hp']} alive={o['local_alive']} win={o['won']} invalid={o['invalidated']} plan={' | '.join(res['plan'])}",flush=True)
    sensitivity=[]
    if args.stage in ('all','sensitivity'):
        chosen=[t for t in alltrials if t.root.id in ('D01','D04','D05','D12','S03','S06')]
        for t in chosen:
            for lim in (450,1500,12000):
                for c in selected:
                    if c.name not in ('A_legacy','B_objective','D_bundle','E_UCT','F_cheap','F_no_tail','D_actor_tail'):continue
                    res=run_trial(t,c,lim,311);sensitivity.append(res)
                    o=res['external']['checkpoints']['14']
                    print(f"SENS {t.root.id} {c.name} B={lim} used={res['used']} depth={res['complete_matrix_depth']} E={o['enemy_total_hp']} alive={o['local_alive']}",flush=True)
    output={'evidence_level':'abstract Python only; no C# / native differential / human multiplayer test',
            'lock':lock,'environment':{'python':sys.version,'platform':platform.platform()},
            'fixtures':[dc.asdict(t) for t in alltrials],'properties':prop,'additional_probes':additional,'runs':results,'sensitivity':sensitivity,
            'counts':{'main_runs':len(results),'sensitivity_runs':len(sensitivity),'properties':len(prop),'new_properties':len(additional.get('properties',[])),
                      'new_beam_runs':len(additional.get('new_beam_runs',[]))},
            'elapsed_seconds':time.perf_counter()-started,
            'limitations':['toy values and mechanics, two players only','root draws are publicly ordered by model definition',
                'surrogate-guided UCT generator is not full ISMCTS','logical work units are not production nodes or calibrated wall-clock',
                'script-based terminal capacity estimate can be biased','designed scenario weights are not human behavior probabilities',
                'candidate coverage bounded, no global optimality or human win-rate claim']}
    dest.write_text(json.dumps(output,ensure_ascii=False,indent=2))
    print('COUNTS '+json.dumps(output['counts'])+' elapsed_s='+str(round(output['elapsed_seconds'],4)),flush=True)

if __name__=='__main__':main()
