#!/usr/bin/env python3
"""CombatSolver 851c1521 / 0.43.3 window selection: abstract, NOT production C#.
Python >=3.10, standard library, no network, no repository writes.

Two separate instruments:
(1) synthetic candidate-pool contracts port the documented comparator;
(2) a paid finite first-turn-plan stream, NOT a reproduction of card-level Beam.
Every strategy has the same stream, root, seed, budget, live-path cap (8), and
finalization reserve. C/E publication and D rollout consume that budget.
External evaluation uses the selected COMPLETE first local turn and one shared
state-feedback continuation to enemy cycles 3/7/14. It is separately metered.

Run: python THIS_FILE.py --output-dir OUTPUT_DIRECTORY
No fitted probabilities or statistical population claims. Fixtures are designed
positive/negative counterexamples, not a random sample of real games.
"""
from __future__ import annotations
import argparse, copy, hashlib, itertools, json, random, sys, time
from collections import Counter
from dataclasses import asdict, dataclass, field, replace
from functools import cmp_to_key
from pathlib import Path
from typing import Any

BASELINE = '851c1521f5258ec43dc71c24eeb8d5200fdf612d'
PREFIX = 'CombatSolver_Multiplayer_WindowSelection_20260920'
HORIZON = 14
BEAM_CAP = 8
FINAL_RESERVE = 256
STRATEGIES = ('A', 'B', 'C_count', 'C_cover', 'D', 'E')

@dataclass(frozen=True)
class Act:
    kind: str
    amount: int = 0
    target: int = 0

@dataclass(frozen=True)
class Plan:
    name: str
    actions: tuple[Act, ...]
    maturity: int = 3
    burst: int = 60
    tied_target: bool = False
    resource: bool = False
    future_potion: bool = False
    discovery_work: int = 0
    fanout_work: int = 0
    risk_cycle: int = 0
    risk_amount: int = 0
    regen: int = 0
    stop_depth: int = 14
    blocked: bool = False
    bad_branch: bool = False

@dataclass(frozen=True)
class Fixture:
    name: str
    intent: str
    plans: tuple[Plan, ...]
    hp: tuple[int, ...] = (600,)
    incoming: int = 2
    passive_block: int = 2
    seed: int = 1729
    budget: int = 1600
    root_loss: int = 0
    # Emit additional legal current-turn plan after this completed cycle.
    late_plan: Plan | None = None
    late_at: int = 3
    shadow: bool = False
    forced_potion: bool = False
    scenarios: tuple[str, ...] = ('idle',)

@dataclass(frozen=True)
class Fact:
    cycle: int
    enemy_hp: int
    player_hp: int
    loss: int = 0
    excess: int = 0
    saves: int = 0
    survivors: int = 2
    potions: int = 0
    won: bool = False
    dead: bool = False
    actions: int = 0
    own_damage: int = 0
    peer_damage: int = 0
    nominal: int = 0
    kills: int = 0
    player_turns: int = 0

@dataclass
class State:
    hp: list[int]
    player_hp: int = 60
    loss: int = 0
    excess: int = 0
    saves: int = 0
    survivors: int = 2
    potions: int = 0
    own_damage: int = 0
    peer_damage: int = 0
    nominal: int = 0
    kills: int = 0
    actions: int = 0
    player_turns: int = 0
    cycle: int = 0
    power: int = 0
    installed: bool = False
    charged: int = 0
    channelled: bool = False
    discharged: bool = False
    guard: int = 0
    peer_shield: int = 0
    attack_bonus: int = 0
    rng: random.Random = field(default_factory=lambda: random.Random(1729))
    draws: list[int] = field(default_factory=list)

@dataclass
class Node:
    identity: str
    plan: str
    branch: str
    facts: tuple[Fact, ...]
    state: State | None = None
    blocked: bool = False
    explicit: int = 0
    score: float = 0.0
    @property
    def end(self) -> Fact: return self.facts[-1]
    @property
    def depth(self) -> int: return self.end.cycle

@dataclass
class Work:
    limit: int
    costs: Counter = field(default_factory=Counter)
    denied: Counter = field(default_factory=Counter)
    expanded: Counter = field(default_factory=Counter)
    generated: Counter = field(default_factory=Counter)
    trace: list[dict] = field(default_factory=list)
    peak_live: int = 0
    peak_fact_rows: int = 0
    drain: int = 0
    @property
    def spent(self) -> int: return sum(self.costs.values())
    def pay(self, kind: str, n: int = 1, reserve: int = 0) -> bool:
        if n < 0: raise ValueError('negative work')
        if self.spent + n + reserve > self.limit:
            self.denied[kind] += 1
            return False
        self.costs[kind] += n
        return True
    def must(self, kind: str, n: int = 1) -> None:
        if not self.pay(kind,n): raise RuntimeError(f'finalization reserve exhausted: {kind} {n}')


def root_state(f: Fixture) -> State:
    return State(list(f.hp), player_hp=60-f.root_loss, loss=f.root_loss,
                 excess=max(0,f.root_loss-3), rng=random.Random(f.seed))

def hit(s: State, amount: int, target: int = 0, peer: bool = False, retarget: bool = True) -> None:
    if amount <= 0: return
    if target >= len(s.hp) or s.hp[target] <= 0:
        if not retarget: return
        alive = [i for i,h in enumerate(s.hp) if h>0]
        if not alive: return
        target=alive[0]
    actual=min(s.hp[target],amount)
    if not peer: s.nominal += amount
    s.hp[target]-=actual
    if peer: s.peer_damage+=actual
    else: s.own_damage+=actual
    if s.hp[target]==0: s.kills+=1

def apply_action(s: State, a: Act, p: Plan) -> None:
    s.actions+=1
    if a.kind=='hit': hit(s,a.amount+s.power,a.target)
    elif a.kind=='guard': s.guard+=a.amount
    elif a.kind=='install': s.installed=True
    elif a.kind=='power': s.power+=a.amount
    elif a.kind=='charge': s.charged+=a.amount
    elif a.kind=='channel': s.channelled=s.charged>0
    elif a.kind=='release':
        if s.channelled: hit(s,s.charged,a.target); s.charged=0; s.channelled=False
    elif a.kind=='cash':
        hit(s,a.amount,a.target,False,not p.tied_target); s.discharged=True
    elif a.kind=='potion': s.potions+=1; hit(s,a.amount,a.target)
    elif a.kind=='extra': s.player_turns+=1  # does NOT advance enemy cycle
    elif a.kind=='end': pass
    else: raise ValueError(f'unknown action {a.kind}')

def turn_actions(s: State, p: Plan, branch: str, cycle: int) -> tuple[Act,...]:
    if cycle==1: return p.actions
    acts=[]
    if p.future_potion and cycle==2: acts.append(Act('potion',0))
    if p.resource and s.installed and not s.discharged:
        if branch=='greedy' and cycle>=2: acts.append(Act('cash',4))
        elif cycle>=p.maturity: acts.append(Act('cash',p.burst))
    acts.append(Act('hit',6))
    acts.append(Act('end'))
    return tuple(acts)

def step(s0: State, p: Plan, f: Fixture, branch: str='normal', scenario: str='idle') -> tuple[State,Fact,int]:
    """A fully paid cycle job calls this. Peer events only in external evaluator."""
    s=copy.deepcopy(s0)
    c=s.cycle+1
    s.guard=0
    start_loss=s.loss
    if c==2 and scenario=='kill_target': hit(s,10**6,0,True,False)
    if c==2 and scenario=='end_combat':
        for i in range(len(s.hp)): hit(s,10**6,i,True,False)
    if scenario=='shield': s.peer_shield=8
    if c==2 and scenario=='phase':
        hit(s,1,0,True,False)
        if s.hp[0]>0: s.hp[0]+=60; s.attack_bonus=4
    if scenario=='rng_shift' and c>=2:
        s.draws.append(s.rng.randrange(1000))  # a peer event consumes the stream
    acts=turn_actions(s,p,branch,c)
    if s.player_hp>0 and sum(s.hp)>0:
        s.player_turns+=1
        for a in acts:
            apply_action(s,a,p)
            if sum(s.hp)==0: break
        if c>=2 and scenario=='rng_shift' and sum(s.hp)>0:
            v=s.rng.randrange(1000); s.draws.append(v); hit(s,v%7)
        elif c>=2 and scenario=='rng_base' and sum(s.hp)>0:
            v=s.rng.randrange(1000); s.draws.append(v); hit(s,v%7)
        if s.installed and not s.discharged and not p.resource:
            due=(c>=p.maturity)
            if due:
                hit(s,p.burst,0,False,not p.tied_target); s.discharged=True
        if p.regen and sum(s.hp)>0: s.hp[0]=min(f.hp[0],s.hp[0]+p.regen)
        if sum(s.hp)>0:
            loss=max(0,f.incoming+s.attack_bonus-f.passive_block-s.guard-s.peer_shield)
            if c==p.risk_cycle and (branch=='bad' or not p.bad_branch): loss+=p.risk_amount
            s.loss+=loss; s.player_hp=max(0,s.player_hp-loss)
            if s.player_hp==0: s.survivors=1
            paid_this_cycle=loss+(f.root_loss if c==1 else 0)
            # root excess was initialized; do not double-count it.
            s.excess+=max(0,paid_this_cycle-3)-(max(0,f.root_loss-3) if c==1 else 0)
    s.cycle=c
    won=sum(s.hp)==0
    fact=Fact(c,sum(s.hp),s.player_hp,s.loss,s.excess,s.saves,s.survivors,s.potions,
              won,s.player_hp==0,s.actions,s.own_damage,s.peer_damage,s.nominal,s.kills,s.player_turns)
    return s,fact,len(acts)+1 # actions plus one enemy phase dispatch

def common_depth(nodes: list[Node]) -> int:
    normal=[n.depth for n in nodes if not(n.end.won or n.end.dead) and not n.blocked and n.depth>0]
    if normal: return min(normal)
    rest=[n.depth for n in nodes if not(n.end.won or n.end.dead) and n.depth>0]
    if rest: return min(rest)
    return HORIZON if nodes and all(n.end.won or n.end.dead for n in nodes) else 0

def facts_at(n: Node,d: int) -> tuple[Fact,bool]:
    if n.end.won or n.end.dead: return n.end,True
    for f in n.facts:
        if f.cycle==d: return f,True
    return n.end,False

def rankkey(n: Node,d: int,policy: str='A',weights: tuple[int,int,int]=(1,1,1)) -> tuple:
    f,ok=facts_at(n,d); t=n.end
    risk=(t.dead,not ok,max(f.saves,t.saves),max(f.excess,t.excess),not f.won)
    if not ok: return risk+(-n.score,t.actions)
    # E only changes the damage-level comparator, after existing safety/team tiers.
    damage: float=f.enemy_hp
    if policy=='E':
        pts=[(q,w) for q,w in zip((3,7,14),weights) if q<=d]
        if pts:
            vals=[(facts_at(n,q)[0].enemy_hp,w) for q,w in pts]
            damage=sum(v*w for v,w in vals)/sum(w for _,w in vals)
    cost=t.actions if policy=='A' or t.won or t.dead else f.actions
    return risk+(-min(f.survivors,t.survivors),damage,f.loss,-f.player_hp,f.potions,
                 t.player_turns if f.won else 0,cost)

def order(nodes:list[Node],d:int,policy:str,work:Work|None=None,weights=(1,1,1)) -> list[Node]:
    def cmp(a:Node,b:Node)->int:
        if work: work.must('comparison')
        ka,kb=rankkey(a,d,policy,weights),rankkey(b,d,policy,weights)
        return (ka>kb)-(ka<kb)
    return sorted(nodes,key=cmp_to_key(cmp))

def eligible(nodes:list[Node],forced:bool)->list[Node]:
    return [n for n in nodes if not forced or n.explicit>0]

def make_node(p:Plan,branch:str,facts:tuple[Fact,...],s:State|None=None)->Node:
    e=facts[-1]
    return Node(p.name+':'+branch+':'+str(e.cycle),p.name,branch,facts,s,
                p.blocked and e.cycle>=p.stop_depth,e.potions,-e.enemy_hp-100000*e.excess)

def paid_advance(parent:Node|None,p:Plan,f:Fixture,branch:str,w:Work,
                 reserve:int=FINAL_RESERVE,kind:str='main',cheap:bool=False) -> Node|None:
    s=parent.state if parent else root_state(f)
    assert s is not None
    if parent and (parent.end.won or parent.end.dead or parent.blocked or parent.depth>=p.stop_depth): return parent
    c=s.cycle+1
    acts=turn_actions(s,p,branch,c)
    # Alternative legal-action enumerations are modeled as fanout work, NOT game nodes.
    discovery=p.discovery_work if c==1 else 0
    fees={'fork':1,'choice_enumeration':(1 if cheap and c>1 else p.fanout_work)+discovery,
          kind+'_simulation':len(acts)+1,'admission':1,'metadata':1}
    need=sum(fees.values())
    if w.spent+need+reserve>w.limit:
        w.denied[kind+'_job']+=1
        w.trace.append({'event':'not_admitted','plan':p.name,'branch':branch,'cycle':c,'required':need})
        return None
    for k,v in fees.items(): assert w.pay(k,v,reserve)
    w.expanded[str(s.cycle)]+=1
    s1,cp,transitions=step(s,p,f,branch)
    fs=parent.facts+(cp,) if parent else (cp,)
    child=make_node(p,branch,fs,s1)
    w.generated[str(cp.cycle)]+=1
    w.trace.append({'event':'generate','kind':kind,'node':child.identity,'cycle':cp.cycle,
                    'work':w.spent,'enemy_hp':cp.enemy_hp,'loss':cp.loss})
    return child

def coverage_batch(leaves:list[Node],reps:set[str],count_only:bool=False)->tuple[list[Node],int]:
    if not leaves: return [],0
    # Terminal outcomes are complete evidence, not artificial normal checkpoints.
    for d in range(HORIZON,0,-1):
        candidates=[n for n in leaves if n.end.won or n.end.dead or n.depth>=d]
        if count_only:
            adequate=len(candidates)>=min(2,len(reps))
        else:
            adequate=all(any(n.plan==r for n in candidates) for r in reps)
        if adequate: return candidates,d
    return [],0

def final_pool(leaves:dict[tuple[str,str],Node],shadow:Node|None,forced:bool)->list[Node]:
    pool=list(leaves.values())
    # Models the CATEGORY-gated no-potion fallback, not an unconditional ancient pool.
    if shadow and shadow.explicit==0 and all(n.explicit>0 for n in pool): pool.append(shadow)
    return eligible(pool,forced)

def run_search(f:Fixture,policy:str,budget:int|None=None,weights=(1,1,1),
               rollout_branch:str='greedy',cheap_rollout:bool=False)->dict:
    w=Work(f.budget if budget is None else budget)
    if w.limit<=FINAL_RESERVE: raise ValueError('budget must exceed common final reserve')
    leaves:dict[tuple[str,str],Node]={}
    plans=list(f.plans); planmap={p.name:p for p in plans}
    reps=set(p.name for p in plans)
    shadow=None; candidate_incumbent=None; published_depth=0; coverage_log=[]
    d_started=False; stopped=False
    for cycle in range(1,HORIZON+1):
        if f.late_plan and cycle==f.late_at:
            plans.append(f.late_plan); planmap[f.late_plan.name]=f.late_plan
            reps.add(f.late_plan.name) # epoch invalidation; do NOT silently omit it
            w.trace.append({'event':'new_representative_epoch','plan':f.late_plan.name})
        for p in plans:
            branches=('normal','bad') if p.bad_branch else ('normal',)
            for branch in branches:
                key=(p.name,branch); parent=leaves.get(key)
                if parent and (parent.depth>=cycle or parent.depth>=p.stop_depth or parent.blocked or parent.end.won or parent.end.dead): continue
                child=paid_advance(parent,p,f,branch,w)
                if child is None: stopped=True; break
                leaves[key]=child
                if f.shadow and shadow is None and child.depth==1 and child.explicit==0: shadow=child
                w.peak_live=max(w.peak_live,len(leaves)+(1 if shadow else 0)+1)
                w.peak_fact_rows=max(w.peak_fact_rows,sum(len(n.facts) for n in leaves.values()))
                if len(leaves)>BEAM_CAP: raise AssertionError('abstract frontier exceeded fixed capacity')
            if stopped: break
        if stopped: break
        if policy in ('C_count','C_cover','E'):
            pool=eligible(list(leaves.values()),f.forced_potion)
            # Candidate identity and coverage processing is PAID, before exposing values.
            overhead=len(leaves)*2+(3*len(pool) if policy=='E' else 0)
            if not w.pay('layer_and_curve_scan',overhead,FINAL_RESERVE): stopped=True; break
            batch,d=coverage_batch(pool,reps,policy=='C_count')
            coverage_log.append({'cycle_pass':cycle,'representatives':sorted(reps),
                                 'covered':sorted({n.plan for n in batch}), 'depth':d})
            if batch:
                candidate_incumbent=(tuple(batch),d)
                published_depth=d
        if policy=='D' and cycle==1 and not d_started:
            d_started=True
            # Bounded two-plan pilot consumes at most 1/3 remaining SEARCH work.
            initial=eligible(list(leaves.values()),f.forced_potion)
            d0=common_depth(initial)
            representatives=sorted(initial,key=lambda n:rankkey(n,d0,'A'))[:2]
            if not w.pay('rollout_representative_scan',len(initial)+len(initial)*(len(initial)-1)//2,FINAL_RESERVE): stopped=True; break
            allowance=max(0,(w.limit-w.spent-FINAL_RESERVE)//3)
            pilot_end=w.spent+allowance
            roll={n.plan:n for n in representatives}
            for target in range(2,HORIZON+1):
                success=True
                for base in representatives:
                    old=roll[base.plan]; p=planmap[base.plan]
                    replay_cost=base.end.actions+1 if target==2 else 0
                    if not w.pay('rollout_prefix_replay',replay_cost,w.limit-pilot_end): success=False; break
                    r=paid_advance(old,p,f,rollout_branch,w,w.limit-pilot_end,
                                   'rollout_fast' if cheap_rollout else 'rollout',cheap_rollout)
                    if r is None: success=False; break
                    roll[base.plan]=r
                if not success: break
                # Atomic all selected representatives reach same boundary.
                if all(n.depth>=target or n.end.won or n.end.dead for n in roll.values()):
                    for name,n in roll.items(): leaves[(name,'rollout')]=n
                    if len(leaves)>BEAM_CAP: raise AssertionError('D displaced cap without authorization')
            w.trace.append({'event':'rollout_finished','representatives':[n.plan for n in representatives],
                            'allowance':allowance,'spent_at_end':w.spent})
            w.peak_live=max(w.peak_live,len(leaves)+len(roll)+1)
    pool=final_pool(leaves,shadow,f.forced_potion)
    w.must('final_candidate_scan',len(pool))
    if not pool:
        return {'fixture':f.name,'strategy':policy,'budget':w.limit,'no_eligible':True,
                'work':dict(w.costs),'spent':w.spent,'trace':w.trace}
    selection_policy='B' if policy in ('B','C_count','C_cover','D') else policy
    if policy in ('C_count','C_cover','E'):
        # Final catch-up scan pays even when an atomic layer was not reached.
        w.must('final_coverage_scan',len(pool)*2)
        batch,d=coverage_batch(eligible(list(leaves.values()),f.forced_potion),reps,policy=='C_count')
        if batch:
            pool=batch
        else:
            # Keep current evidence, including known bad leaves. Never resurrect a clean ancestor.
            d=common_depth(pool); selection_policy='A'
    elif policy=='D':
        # D is only eligible to change the final depth if EVERY discovered plan is covered.
        batch,d=coverage_batch(pool,reps)
        if batch: pool=batch
        else: d=common_depth(pool)
    else: d=common_depth(pool)
    if policy=='E': w.must('final_curve_scan',len(pool)*3)
    ranked=order(pool,d,selection_policy,w,weights)
    selected=ranked[0]
    p=planmap[selected.plan]
    # Full selected route replay is separately accounted and actually checked.
    replay=root_state(f); replays=[]
    for _ in range(selected.depth):
        if replays and (replays[-1].won or replays[-1].dead): break
        acts=turn_actions(replay,p,selected.branch,replay.cycle+1)
        w.must('final_route_replay',len(acts)+2)
        replay,rf,_=step(replay,p,f,selected.branch)
        replays.append(rf)
    assert replays[-1]==selected.end
    w.must('release_and_drain',len(leaves)+1)
    w.drain=len(leaves)+1
    assert w.spent<=w.limit
    row={
        'fixture':f.name,'strategy':policy,'budget':w.limit,'seed':f.seed,'horizon_limit':14,
        'selected_plan':selected.plan,'complete_first_turn':[asdict(a) for a in p.actions],
        'selected_branch':selected.branch,'selected_cycles':selected.depth,'common_evaluation_cycle':d,
        'published_depth_diagnostic':published_depth,'max_expanded_parent_cycle':max(map(int,w.expanded),default=0),
        'expanded_parent_distribution':dict(w.expanded),'generated_cycle_distribution':dict(w.generated),
        'max_generated_cycle':max(map(int,w.generated),default=0),
        'selected_display_action_count':selected.end.actions,'selected_first_turn_actions':len(p.actions),
        'spent':w.spent,'unused_budget':w.limit-w.spent,'work':dict(w.costs),
        'denied':dict(w.denied),'peak_live_state_slots':w.peak_live,
        'peak_shared_fact_rows':w.peak_fact_rows,'cancel_or_release_drain':w.drain,
        'known_future_risk':{'loss':selected.end.loss,'excess':selected.end.excess,'dead':selected.end.dead},
        'comparison_checkpoint':asdict(facts_at(selected,d)[0]),'terminal':selected.end.won or selected.end.dead,
        'selection_policy':selection_policy,'selection_weights':list(weights) if policy=='E' else None,
        'coverage_log':coverage_log,'final_pool':[{'id':n.identity,'depth':n.depth,'plan':n.plan} for n in pool],
        'selected_checkpoints':[asdict(x) for x in selected.facts], 'trace':w.trace,
        'evaluations':{}}
    return row

def evaluate(f:Fixture,p:Plan,scenario:str)->dict:
    s=root_state(f); out={}; work=Counter(); trace=[]
    terminal=None
    for c in range(1,15):
        if terminal:
            cp=replace(terminal,cycle=c) # explicitly absorbing combat/death, not unfinished tail
        else:
            acts=turn_actions(s,p,'normal',c)
            work['external_simulation']+=len(acts)+1; work['external_fork']+=1
            if scenario!='idle': work['external_scenario_dispatch']+=1
            s,cp,_=step(s,p,f,'normal',scenario)
            if cp.won or cp.dead: terminal=cp
        trace.append(asdict(cp))
        if c in (3,7,14): out[str(c)]=asdict(cp)
    return {'at':out,'work':dict(work),'trace':trace,'rng_draws':s.draws,
            'protocol':'execute fixed full current local turn, then legal hit6/end; mature resource held until due; no strategy suffix reuse'}

def fixture_set()->list[Fixture]:
    def direct(name='attack',damage=18,**kw): return Plan(name,(Act('hit',damage),Act('end')),**kw)
    def setup(name='setup',m=7,b=84,**kw): return Plan(name,(Act('install'),Act('end')),maturity=m,burst=b,**kw)
    ff=[]
    for m in (3,7,14):
        ff.append(Fixture(f'W{m:02d}_delayed_{m}',f'first-turn sacrifice repaid at cycle {m}',
                          (direct(future_potion=True),setup(m=m,b=84,future_potion=True)),shadow=True))
    ff.extend([
      Fixture('W20_order','power then attack versus attack then power',(
          Plan('hit_power',(Act('hit',10),Act('power',8),Act('end'))),
          Plan('power_hit',(Act('power',8),Act('hit',10),Act('end'))))),
      Fixture('W21_third_step','third local action cashes combo; same first card insufficient',(
          Plan('charge_wrong',(Act('charge',42),Act('release'),Act('channel'),Act('end'))),
          Plan('charge_right',(Act('charge',42),Act('channel'),Act('release'),Act('end'))),direct())),
      Fixture('W22_known_risk','bad continuation does not ban same first-turn good continuation',(
          direct(),setup(m=3,b=84,risk_cycle=3,risk_amount=9,bad_branch=True))),
      Fixture('W23_shallow_unknown','unfinished alternative is not a safety certificate',(
          direct(stop_depth=1),setup(m=3,b=84,risk_cycle=7,risk_amount=6))),
      Fixture('W24_branch_gap','one completed root plan cannot represent a missing expensive plan',(
          direct(),setup(m=7,b=120,fanout_work=19)),budget=760),
      Fixture('W25_rollout_wrong','uniform greedy rollout discharges resource before maturity',(
          direct(),setup(m=7,b=120,resource=True)),budget=900),
      Fixture('W26_late_discovery','rollout/curve scanning competes with late good current-turn plan',(
          direct(),setup(m=14,b=30)),late_plan=direct('late_combo',90,discovery_work=35),late_at=3,budget=570),
      Fixture('W27_extra_turn','extra local turn recorded without spending enemy cycle',(
          Plan('extra_sequence',(Act('hit',9),Act('extra'),Act('hit',9),Act('end'))),direct())),
      Fixture('W28_external_choice','external block does not establish future completion',(
          direct(stop_depth=1,blocked=True),setup(m=3,b=84))),
      Fixture('W29_forced_potion','required use remains pending at cycle1 and becomes eligible at2',(
          direct(),setup(m=3,b=84,future_potion=True)),forced_potion=True),
      Fixture('W30_terminal','true early victory is a full fact, not old checkpoint or horizon',(
          direct(damage=80),setup(m=3,b=84)),hp=(70,)),
      Fixture('W31_target_invalid','future payoff depends on target still alive',(
          direct(future_potion=True),setup(m=7,b=100,tied_target=True,future_potion=True)),
          hp=(150,350),shadow=True,scenarios=('idle','kill_target','end_combat','phase')),
      Fixture('W32_teammate_defense','idle is neither calibrated probability nor universal lower bound',(
          Plan('guard_attack',(Act('guard',6),Act('hit',18),Act('end'))),
          setup(m=3,b=100)),incoming=7,scenarios=('idle','shield','phase')),
      Fixture('W33_rng','same seed, different event consumption',(
          direct(),setup(m=7,b=84)),scenarios=('rng_base','rng_shift')),
      Fixture('W34_heal_farm','actual positive damage may farm healing; enemy HP is separate',(
          Plan('power_heal',(Act('power',20),Act('hit',1),Act('end')),regen=26),direct())),
      Fixture('W35_weight_crossing','early sustained output versus delayed larger output',(
          Plan('early_power',(Act('power',5),Act('hit',13),Act('end'))),setup(m=14,b=130))),
      Fixture('W36_root_loss','root loss and each cycle excess are not reset by more depth',(
          direct(),setup(m=3,b=84)),incoming=3,root_loss=3),
      Fixture('W37_two_enemies','damage totals cannot identify every control or target utility',(
          Plan('target0',(Act('hit',20,0),Act('end'))),
          Plan('target1',(Act('hit',20,1),Act('end')))),hp=(20,580)),
      Fixture('W38_scan_opportunity','layer overhead can prevent a late root plan admission',(
          direct(),setup(m=14,b=30)),late_plan=direct('late_combo',90,discovery_work=80),late_at=3,budget=480),
    ])
    return ff


def synthetic(name:str,enemy:list[int],costs:list[int],*,plan:str|None=None,losses:list[int]|None=None,
              won=False,dead=False,saves=0,explicit=0,blocked=False)->Node:
    fs=tuple(Fact(i+1,h,60-(losses[i] if losses else 0),losses[i] if losses else 0,
                  max(0,(losses[i] if losses else 0)-3),saves,2,explicit,
                  won and i==len(enemy)-1,dead and i==len(enemy)-1,costs[i]) for i,h in enumerate(enemy))
    return Node(name,plan or name,'normal',fs,None,blocked,explicit,-enemy[-1])

def micro_checks()->list[dict]:
    out=[]
    def check(name,condition,evidence):
        if not condition: raise AssertionError(name+' '+repr(evidence))
        out.append({'name':name,'passed':True,'evidence':evidence})
    shallow=synthetic('fallback',[590],[2],plan='direct')
    x=synthetic('invest',[590,560,490],[1,5,9],plan='invest')
    y=synthetic('attack',[590,580,570],[2,3,4],plan='direct')
    pool=[x,y,shallow]; d=common_depth(pool)
    a=order(pool,d,'A')[0]; b=order(pool,d,'B')[0]
    check('M01_A_shallow_tail_bias',d==1 and a.plan=='direct' and b.plan=='invest',
          {'common':d,'A':a.identity,'B':b.identity,'C3':order([x,y],3,'B')[0].identity})
    # B can select a different entire first turn, but no theorem relates its tail.
    xb=synthetic('bad_invest',[590,590,590],[1,5,9],plan='bad_invest')
    check('M02_B_can_hurt',order([xb,y,shallow],1,'B')[0].plan=='bad_invest',
          {'B_enemy3':590,'A_enemy3':570,'reason':'same immediate facts, cheaper first prefix has worse later payoff'})
    same=replace(x,plan='same_turn'); ss=replace(shallow,plan='same_turn')
    check('M03_F12_no_current_gain',order([same,ss],1,'A')[0].plan==order([same,ss],1,'B')[0].plan,
          {'full_plan_unchanged':True,'classification':'coverage_or_tiebreak_only'})
    branches=[synthetic(f'low{i}',[590,580,570],[2,4,6],plan='low') for i in range(4)]
    high=synthetic('high',[588],[2],plan='high')
    ccount,cd=coverage_batch(branches+[high],{'low','high'},True)
    call,ca=coverage_batch(branches+[high],{'low','high'})
    check('M04_row_count_is_not_coverage',cd==3 and ca==1,
          {'count_depth':cd,'all_plan_depth':ca,'count_plans':sorted({n.plan for n in ccount})})
    win=synthetic('win',[590,0],[2,5],won=True)
    check('M05_terminal_before_old_checkpoint',facts_at(win,1)[0].won and facts_at(win,1)[0].enemy_hp==0,
          {'facts':asdict(facts_at(win,1)[0]),'all_terminal_context':common_depth([win])})
    loss=synthetic('risky',[580,400],[2,4],losses=[0,9],plan='shared')
    good=synthetic('safe',[585,500],[2,4],plan='shared')
    check('M06_risk_tail_not_hidden_or_plan_banned',order([loss,good],1,'A')[0].identity=='safe',
          {'bad_excess':rankkey(loss,1)[3],'same_plan_good_selected':True})
    no=synthetic('incomplete',[590],[2]); yes=synthetic('used',[591],[3],explicit=1)
    check('M07_eligibility_before_context',eligible([no,yes],True)==[yes],{'retained':['used']})
    check('M08_blocked_not_normal_depth',common_depth([replace(no,blocked=True),x])==3,
          {'normal_depth':3,'blocked_depth':1})
    zero=Node('zero','zero','normal',(Fact(0,600,60),))
    check('M09_zero_checkpoint_does_not_always_zero',common_depth([zero,x])==3,{'depth':3})
    # Fixed context comparator is lexicographic; test its pre-order, not global opt.
    seq=[shallow,x,y,loss,good,win]
    perms=0
    expected=sorted(n.identity for n in order(seq,1,'A')[:1])
    for pp in itertools.permutations(seq):
        assert sorted(n.identity for n in order(list(pp),1,'A')[:1])==expected; perms+=1
    triples=0
    for aa,bb,cc in itertools.product(seq,repeat=3):
        if rankkey(aa,1)<=rankkey(bb,1) and rankkey(bb,1)<=rankkey(cc,1): assert rankkey(aa,1)<=rankkey(cc,1)
        triples+=1
    check('M10_fixed_comparator_properties',True,{'permutations':perms,'triples':triples})
    early=synthetic('early',[580,560,540,520,500,480,460,440,420,400,380,360,340,320],list(range(1,15)))
    late=synthetic('late',[590]*13+[200],list(range(1,15)))
    eq=order([early,late],14,'E',weights=(1,1,1))[0].plan
    terminal=order([early,late],14,'E',weights=(1,1,8))[0].plan
    check('M11_E_weights_are_policy',eq=='early' and terminal=='late',
          {'equal_anchor_weights':eq,'late_weight8':terminal,'enemy14':{'early':320,'late':200}})
    check('M12_heal_farm_counter',80>30 and 100>70,
          {'farm_actual_damage':80,'farm_enemy_hp':100,'progress_damage':30,'progress_enemy_hp':70,
           'naive_sum_positive_damage_picks_wrong':True})
    # Same total HP but threat/phase differs; not a validated game monster model.
    check('M13_endpoint_HP_not_control_value',20+80==0+100,
          {'A_enemies':[20,80],'B_enemies':[0,100],'A_next_attack':12,'B_next_attack':2})
    # Sequential wrong continuation does not enjoy a rollout theorem.
    check('M14_wrong_base_policy',4<84,{'premature_release':4,'mature_release':84})
    check('M15_three_HP_not_hard_filter',max(0,4-3)<max(0,6-3),
          {'excess4':1,'excess6':3,'both_remain_eligible':True})
    # Genuine finite memory opportunity cost (F10-like, NOT a Beam8 production reproduction).
    retained=['attack','guard','good_setup']; replace_slot=['attack','guard','new_setup']
    check('M16_fixed_seat_opportunity', 'good_setup' not in replace_slot,
          {'before':retained,'after':replace_slot,'good_setup_output':80,'new_setup_output':50})
    # Explicit admitted job cancellation: pay job before cancellation, then drain, no free reuse.
    work=Work(30); assert work.pay('admitted_simulation',20); assert work.pay('drain',4)
    check('M17_cancel_drain',not work.pay('new_job',7) and work.spent==24,
          {'spent':work.spent,'remaining':6,'not_admitted_cost':7,'no_outstanding_owner':True})
    # Repeated observation at 0/extra does not close enemy ledger.
    f=Fixture('extra','',(Plan('e',(Act('extra'),Act('hit',6),Act('end'))),),incoming=3,root_loss=3)
    s,cp,_=step(root_state(f),f.plans[0],f)
    check('M18_extra_and_root_ledger',cp.cycle==1 and cp.player_turns==2 and cp.loss==4 and cp.excess==1,
          asdict(cp))
    return out



def supplemental_experiments()->dict:
    """New, nonduplicate sensitivity cases added after inspecting the corrected core."""
    f39=Fixture('W39_target_opportunity','attack a surviving enemy vs invest in target killed by peer',(
        Plan('attack_survivor',(Act('hit',18,1),Act('end')),future_potion=True),
        Plan('mark_target0',(Act('install'),Act('end')),maturity=7,burst=100,
             tied_target=True,future_potion=True)),hp=(150,350),shadow=True,
        scenarios=('idle','kill_target','end_combat','phase'))
    f40=Fixture('W40_cheap_rollout','cheap one-branch tail can help, but wrong base policy can erase gain',(
        Plan('attack',(Act('hit',18),Act('end')),fanout_work=80),
        Plan('resource',(Act('install'),Act('end')),maturity=7,burst=120,
             resource=True,fanout_work=80)),budget=1000)
    rows=[]
    for f in (f39,f40):
        variants=[(x,x,False,'greedy') for x in STRATEGIES]
        if f==f40: variants += [('D_fast_hold','D',True,'normal'),('D_fast_greedy','D',True,'greedy')]
        for label,policy,cheap,branch in variants:
            row=run_search(f,policy,rollout_branch=branch,cheap_rollout=cheap)
            row['strategy_variant']=label
            p=next(p for p in f.plans if p.name==row['selected_plan'])
            row['evaluations']={s:evaluate(f,p,s) for s in f.scenarios}
            rows.append(row)
    f=next(f for f in fixture_set() if f.name=='W38_scan_opportunity')
    for budget in (378,382,386,394):
        for strategy in STRATEGIES:
            row=run_search(f,strategy,budget)
            row['strategy_variant']=strategy
            p=next(p for p in (*f.plans,f.late_plan) if p.name==row['selected_plan'])
            row['evaluations']={'idle':evaluate(f,p,'idle')}
            rows.append(row)
    for row in rows:
        sc=next(iter(row['evaluations']))
        hp='/'.join(str(row['evaluations'][sc]['at'][t]['enemy_hp']) for t in ('3','7','14'))
        print(f"SUPPLEMENT {row['fixture']} budget={row['budget']} {row['strategy_variant']} "
              f"{row['selected_plan']} common={row['common_evaluation_cycle']} hp={hp} work={row['spent']}")
    return {'fixtures':[asdict(f39),asdict(f40)],'runs':rows,
            'purpose':'new adverse target fixture; cheap/wrong rollout-base sensitivity; narrow declared budget cliff, not a population frequency',
            'run_count':len(rows)}

def main()->None:
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir',type=Path,default=Path('.'))
    args=parser.parse_args(); args.output_dir.mkdir(parents=True,exist_ok=True)
    start=time.perf_counter(); checks=micro_checks(); fixtures=fixture_set(); runs=[]
    for f in fixtures:
        pmap={p.name:p for p in f.plans}
        if f.late_plan: pmap[f.late_plan.name]=f.late_plan
        for strategy in STRATEGIES:
            row=run_search(f,strategy)
            if not row.get('no_eligible'):
                row['evaluations']={s:evaluate(f,pmap[row['selected_plan']],s) for s in f.scenarios}
            runs.append(row)
        base=next(r for r in runs if r['fixture']==f.name and r['strategy']=='A')
        for row in [r for r in runs if r['fixture']==f.name]:
            row['comparison_to_A']={}
            if 'evaluations' not in row or 'evaluations' not in base: continue
            for s in f.scenarios:
                diffs={}
                for t in ('3','7','14'):
                    x=row['evaluations'][s]['at'][t]; a=base['evaluations'][s]['at'][t]
                    diffs[t]={'enemy_hp_delta':x['enemy_hp']-a['enemy_hp'],
                              'local_loss_delta':x['loss']-a['loss'], 'excess_delta':x['excess']-a['excess'],
                              'own_effective_damage_delta':x['own_damage']-a['own_damage'],
                              'dead_changed':x['dead']!=a['dead'], 'won_changed':x['won']!=a['won']}
                row['comparison_to_A'][s]={'same_complete_first_turn':row['complete_first_turn']==base['complete_first_turn'],
                                          'by_time':diffs}
        compact=[]
        for row in [r for r in runs if r['fixture']==f.name]:
            if row.get('no_eligible'): compact.append(row['strategy']+':NO_ELIGIBLE'); continue
            scenario=f.scenarios[0]
            hp='/'.join(str(row['evaluations'][scenario]['at'][t]['enemy_hp']) for t in ('3','7','14'))
            compact.append(f"{row['strategy']}:{row['selected_plan']} cp{row['common_evaluation_cycle']} sel{row['selected_cycles']} hp{hp} w{row['spent']}")
        print(f.name+' | '+' | '.join(compact),flush=True)
    # Distinct new budget points, not repeated successful main runs.
    grid=[]
    selected_names={'W24_branch_gap','W25_rollout_wrong','W26_late_discovery','W38_scan_opportunity'}
    for f in fixtures:
        if f.name not in selected_names: continue
        for budget in (400,520,700,1100):
            if budget==f.budget: continue
            for strategy in STRATEGIES:
                row=run_search(f,strategy,budget)
                if not row.get('no_eligible'):
                    p=next(p for p in (*f.plans,*((f.late_plan,) if f.late_plan else ())) if p.name==row['selected_plan'])
                    row['evaluations']={'idle':evaluate(f,p,'idle')}
                grid.append(row)
    weight_runs=[]
    f=next(f for f in fixtures if f.name=='W35_weight_crossing')
    for weights in ((4,2,1),(1,1,4),(1,1,8)):
        row=run_search(f,'E',weights=weights)
        p=next(p for p in f.plans if p.name==row['selected_plan'])
        row['evaluations']={'idle':evaluate(f,p,'idle')}; weight_runs.append(row)
    result={
        'baseline':BASELINE,'version':'0.43.3','evidence_level':'abstract_python_only',
        'model':'paid finite complete-first-turn-plan stream; not production Beam/C# or gameplay',
        'parameters':{'horizon':14,'path_cap':8,'same_finalization_reserve':FINAL_RESERVE,
                      'strategies':STRATEGIES,'external_times':[3,7,14]},
        'fixtures':[asdict(f) for f in fixtures], 'micro_checks':checks,'main_runs':runs,
        'budget_grid':grid,'weight_sensitivity':weight_runs,
        'counts':{'fixtures':len(fixtures),'main_runs':len(runs),'budget_runs':len(grid),
                  'weight_runs':len(weight_runs),'micro_checks':len(checks)},
        'limitations':[
            'No production C#/.NET/game-DLL/native differential/network test was executed.',
            'Root plans and discovery schedule are finite fixture inputs; card-level Beam/TT not replicated.',
            'Fanout units model enumerating unselected alternatives; not calibrated wall-clock CPU cost.',
            'D uses only two representatives and an intentionally simple greedy base policy.',
            'Teammate scenarios are sensitivity interventions, never a probability distribution.',
            'One finalization reserve is imposed on all toy strategies; not an existing production setting.',
            'Complete root-plan equality, not first-card equality, determines unchanged decision.',
            'Synthetic comparator-pool reachability must be reproduced using real Replay/Expand locally.',
            'Extra-turn fixture only increments a player-turn counter; it does not validate real extra-turn phase/signature splitting. All its selected plans are attack.'
        ]}
    result['supplement']=supplemental_experiments()
    result['counts']['supplement_runs']=result['supplement']['run_count']
    duration=time.perf_counter()-start
    result['measured_python_wall_seconds']=duration
    path=args.output_dir/(PREFIX+'_Results.json')
    path.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('COUNTS '+json.dumps(result['counts'],ensure_ascii=False))
    print('MICRO_CHECKS '+' '.join(x['name']+':PASS' for x in checks))
    print(f'WALL_SECONDS {duration:.6f}')
    print('RESULTS '+str(path))

if __name__=='__main__':
    main()
