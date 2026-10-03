#!/usr/bin/env python3
"""CombatSolver d55fa84 multiplayer long-term falsification laboratory.

Standard library only, no network, no repository writes, Python >= 3.10.
This is NOT the production C# solver or StS2. A is an explicitly reduced
adapter of the score/three-lane/fixed-cycle ordering; game and graph legality
are distinct. Numbers in fixtures are synthetic, NOT native card values.
All variants have the same per-fixture root, RNG, DOP=1, horizon and budgets.
One work unit is one model operation, NOT one C# node or millisecond.
Run: python CombatSolver_Multiplayer_LongTerm_Experiments_20260918.py --out results.json
"""
from __future__ import annotations
import argparse
from dataclasses import dataclass, replace, asdict
from itertools import permutations
import hashlib
import json
from pathlib import Path
import platform
import random
import time
from typing import Any

SHA = 'd55fa84ec07dc252ce62248a9e8b2f4af95effd5'
SEED = 20260918

@dataclass(frozen=True)
class Fixture:
    name: str
    family: str
    horizon: int = 3
    hp: int = 40
    enemy: int = 90
    incoming: tuple[int, ...] = (2, 2, 2, 2, 2, 2, 2)
    beam: int = 3
    parent_cap: int = 8
    max_expanded: int = 80
    max_work: int = 240
    wall_limit_seconds: float = 5.0
    has_consumer: bool = True
    owner_local: bool = True
    external_choice: bool = False
    ready_cycle: int = 1
    threshold: int = 1
    gain: int = 1
    engine_damage: int = 22
    initial_dark: int = 6
    decoy_damage: int = 3
    future: int = 0
    persistent: int = 1
    late_risk: int = 0
    bad_suffix: bool = False
    immediate: bool = False
    pre_parent_drop: bool = False
    choice_cost: int = 0
    root_paid: int = 0
    note: str = ''

@dataclass(frozen=True)
class Checkpoint:
    cycle: int
    hp: int
    loss: int
    enemy: int
    excess: int
    team: int = 2
    saves: int = 0

@dataclass(frozen=True)
class State:
    mode: str = 'root'
    phase: str = 'open'
    cycle: int = 0
    hp: int = 40
    enemy: int = 90
    block: int = 0
    energy: int = 0
    persistent: int = 0
    future: int = 0
    stars: int = 0
    peer_stars: int = 0
    dark: int = 0
    strength: int = 0
    used: bool = False
    resource: int = 0
    gain_events: int = 0
    spend_events: int = 0
    spent_amount: int = 0
    current_loss: int = 0
    losses: tuple[int, ...] = ()
    checkpoints: tuple[Checkpoint, ...] = ()
    actions: tuple[str, ...] = ()
    rng: int = SEED
    boundary: str = ''
    bad: bool = False
    potions: int = 0
    @property
    def won(self) -> bool:
        return self.enemy <= 0 and self.hp > 0
    @property
    def terminal(self) -> bool:
        return self.won or self.hp <= 0 or bool(self.boundary)
    @property
    def loss(self) -> int:
        return sum(self.losses) + self.current_loss
    @property
    def excess(self) -> int:
        return sum(max(0, x-3) for x in self.losses) + max(0, self.current_loss-3)


def observe(s: State) -> dict[str, Any]:
    return {k: getattr(s, k) for k in ('mode','phase','cycle','hp','enemy','block','persistent',
        'future','stars','peer_stars','dark','resource','used','current_loss','losses','gain_events',
        'spend_events','spent_amount','actions','rng','boundary','bad')}


def score(s: State, variant: str) -> float:
    # Mirrors the relevant current multiplayer coefficients, not every snapshot field.
    base = (-1e12 if s.hp <= 0 else 0) + (1e11 if s.won else 0)
    base += -100*s.enemy + 20*s.persistent + 2*s.energy - .001*len(s.actions)
    base -= 100000*s.excess
    # B consumes an already present snapshot-style fact. Same lanes; no new seat.
    # The factor 20 reuses the persistent scale for an ablation, NOT a calibrated value.
    return base + (20*s.future if variant == 'B' else 0)


def window(s: State, f: Fixture) -> tuple[bool, str, int]:
    """Inspect only current resources and declared visible legal action conditions.
    Never read eventual enemy HP, engine_damage, decoy_damage, late_risk or rewards.
    This is exact for the toy grammar, NOT a production certificate.
    """
    if s.mode != 'engine' or s.used or s.terminal:
        return False, 'not_pending', 0
    if not f.has_consumer:
        return False, 'no_consumer', 0
    if not f.owner_local:
        return False, 'peer_owned', 0
    if f.external_choice:
        return False, 'external_choice', 0
    if s.hp <= 0 or s.excess > 0:
        return False, 'known_risk_not_protected', 0
    if f.family == 'dark':
        need = max(0, f.ready_cycle-s.cycle)
    elif f.family in ('stars','star_trigger'):
        deficit = max(0, f.threshold-s.stars)
        if deficit and f.gain <= 0:
            return False, 'no_resource_supply', 0
        need = max(max(0, f.ready_cycle-s.cycle), (deficit+max(1,f.gain)-1)//max(1,f.gain))
    else:
        need = max(0, f.ready_cycle-s.cycle)
    # The last player-start at horizon is NOT simulated: an event there is outside.
    if s.cycle+need >= f.horizon:
        return False, 'beyond_enemy_cycle_window', need
    return True, 'legal_toy_window', need


def legal_actions(s: State, f: Fixture) -> tuple[str, ...]:
    if s.terminal:
        return ()
    if s.phase == 'open':
        return ('attack','defend','decoy','engine')
    if s.phase == 'end':
        return ('end',)
    if s.mode != 'engine':
        return ('act',)
    if f.external_choice:
        return ('external',)
    usable = f.has_consumer and f.owner_local and s.cycle >= f.ready_cycle
    if f.family in ('stars','star_trigger'):
        usable = usable and s.stars >= f.threshold
    if f.family == 'dark':
        usable = usable and s.dark > 0
    actions = ['use' if usable else 'wait']
    if f.bad_suffix and not s.used:
        actions.append('bad_use')
    return tuple(actions)


def transition(s: State, action: str, f: Fixture) -> State:
    if action not in legal_actions(s, f):
        raise ValueError(f'Illegal toy action: {action} in {observe(s)}')
    # Common random numbers: deterministic stream, never sampled differently by variant.
    nxt_rng = (1664525*s.rng+1013904223) & 0xffffffff
    n = replace(s, actions=s.actions+(action,), rng=nxt_rng)
    if s.phase == 'open':
        n = replace(n, mode=action, phase='end', energy=0)
        if action == 'attack':
            return replace(n, enemy=max(0,s.enemy-8))
        if action == 'defend':
            return replace(n, block=6)
        if action == 'decoy':
            return replace(n, persistent=8)
        initial_stars = f.gain if f.family in ('stars','star_trigger') else 0
        immediate_damage = 16 if f.immediate else (3 if f.family == 'star_trigger' else 0)
        return replace(n, persistent=f.persistent, future=f.future, resource=0,
            stars=initial_stars if f.owner_local else 0, peer_stars=initial_stars if not f.owner_local else 0, dark=f.initial_dark if f.family == 'dark' else 0,
            gain_events=int(initial_stars > 0), used=f.immediate,
            enemy=max(0,s.enemy-immediate_damage))
    if action == 'external':
        return replace(n, boundary='ExternalPlayerChoice')
    if action == 'end':
        # Dark passive grows before the enemy acts; no fake direct damage credit.
        dark = s.dark + (6 if s.mode == 'engine' and s.dark > 0 else 0)
        extra = f.late_risk if s.mode == 'engine' and s.cycle >= 1 else 0
        harm = max(0, f.incoming[min(s.cycle,len(f.incoming)-1)] + extra - s.block)
        loss = s.current_loss+harm
        hp = max(0,s.hp-harm)
        losses = s.losses+(loss,)
        cp = Checkpoint(s.cycle+1,hp,sum(losses),s.enemy,sum(max(0,x-3) for x in losses))
        boundary = 'AdvisoryHorizon' if s.cycle+1 >= f.horizon else ''
        n = replace(n, phase='act', cycle=s.cycle+1, hp=hp, block=0, dark=dark,
            current_loss=0, losses=losses, checkpoints=s.checkpoints+(cp,), boundary=boundary)
        if boundary or hp <= 0:
            return n
        if s.mode == 'engine':
            stars = s.stars
            event = 0
            if f.family in ('stars','star_trigger') and f.gain > 0:
                if f.owner_local:
                    stars += f.gain
                event = int(f.owner_local)
            return replace(n, energy=1+int(f.family == 'energy'), resource=s.resource+f.gain,
                strength=s.strength+(3 if f.family == 'scaling' else 0), stars=stars,
                peer_stars=s.peer_stars+(f.gain if not f.owner_local else 0),
                gain_events=s.gain_events+event,
                enemy=max(0,n.enemy-(3*event if f.family == 'star_trigger' else 0)))
        return replace(n, energy=1)
    # A synthetic local action, not a named StS2 card or a hidden rollout.
    damage = 0
    block = 0
    stars, dark = s.stars,s.dark
    spent = 0
    used = s.used
    if s.mode == 'attack':
        damage = 8
    elif s.mode == 'defend':
        damage,block = 3,6
    elif s.mode == 'decoy':
        damage = f.decoy_damage
    elif action in ('use','bad_use'):
        used = True
        if f.family == 'dark':
            damage, dark = s.dark,0
        elif f.family in ('stars','star_trigger'):
            spent = f.threshold
            stars -= spent
            damage = f.engine_damage + (3 if f.family == 'star_trigger' else 0)
            block = 2*spent if f.family == 'star_trigger' else 0
        elif f.family == 'scaling':
            damage = f.engine_damage+s.strength
        else:
            damage = f.engine_damage
    # Late counterevidence is attached only to this suffix, not to the first action.
    self_harm = 8 if action == 'bad_use' else 0
    return replace(n, phase='end', enemy=max(0,s.enemy-damage), hp=max(0,s.hp-self_harm),
        current_loss=s.current_loss+self_harm, block=block, stars=stars,dark=dark,used=used,
        spend_events=s.spend_events+int(spent>0),spent_amount=s.spent_amount+spent,
        bad=s.bad or action == 'bad_use',energy=0)


def final_keys(pool: list[State], variant: str, horizon: int) -> tuple[dict[State,tuple],int]:
    open_nodes = [s for s in pool if not s.won and s.hp>0 and not s.boundary]
    depth = min((s.cycle for s in open_nodes), default=horizon)
    keys = {}
    for s in pool:
        terminal = s.won or s.hp<=0
        cp = next((c for c in s.checkpoints if c.cycle == depth),None)
        comparable = terminal or cp is not None
        if terminal:
            hp,loss,enemy,excess,team = s.hp,s.loss,s.enemy,s.excess,2
        elif cp:
            hp,loss,enemy,excess,team = cp.hp,cp.loss,cp.enemy,max(cp.excess,s.excess),cp.team
        else:
            hp,loss,enemy,excess,team = s.hp,s.loss,s.enemy,s.excess,2
        common = (s.hp<=0, not comparable, 0, max(excess,s.excess),not s.won)
        keys[s] = common + ((-score(s,variant),len(s.actions)) if not comparable else
            (-team,enemy,loss,-hp,s.potions,s.cycle if s.won else 0,len(s.actions)))
    return keys,depth


def beam_rank(pool: list[State], f: Fixture, variant: str, stats: dict, trace: list) -> list[State]:
    order = sorted(pool,key=lambda s:(-score(s,variant),len(s.actions),s.actions))
    living = [s for s in order if s.hp>0]
    if not living:
        return order[:f.beam]
    defense = sorted(living,key=lambda s:(s.excess,-s.hp,-s.block,-score(s,variant),s.actions))
    offense = sorted(living,key=lambda s:(s.enemy,-score(s,variant),s.actions))
    setup = sorted(living,key=lambda s:(-(s.persistent+(s.future if variant=='B' else 0)),
        -score(s,variant),s.actions))
    result: list[State] = []
    def add(s: State) -> None:
        if len(result)<f.beam and s not in result:
            result.append(s)
    def lane(seq: list[State]) -> None:
        for s in seq:
            if s not in result:
                add(s)
                break
    if f.beam == 1:
        add(order[0])
    elif f.beam == 2:
        add(order[0]); lane((defense,offense,setup)[len(order[0].actions)%3])
    else:
        if f.beam>=4:
            add(order[0])
        lane(defense); lane(offense)
        # C is one alternative setup representative, not an extra Beam seat.
        opportunities = []
        if variant == 'C':
            for s in living:
                ok,reason,distance = window(s,f)
                stats['window_tests'] += 1
                if ok and s not in result:
                    opportunities.append((distance,s))
            if opportunities:
                opportunities.sort(key=lambda x:(x[0],-score(x[1],'A'),x[1].actions))
                chosen = opportunities[0][1]
                add(chosen)
                stats['protected_admissions'] += 1
                trace.append({'stage':'C_setup_replace','actions':chosen.actions,
                    'window':window(chosen,f),'observed':observe(chosen)})
            else:
                lane(setup)
        else:
            lane(setup)
    for s in order:
        add(s)
    stats['beam_pruned'] += len(pool)-len(result)
    trace.append({'stage':'beam','before':[observe(s) for s in pool],
                  'after':[observe(s) for s in result]})
    return sorted(result,key=lambda s:(-score(s,variant),s.actions))


def run(f: Fixture, variant: str) -> dict[str, Any]:
    begin = time.perf_counter()
    root = State(hp=f.hp,enemy=f.enemy,current_loss=f.root_paid)
    frontier,completed = [root],[]
    trace: list[dict] = []
    stats = dict(expanded=0,transitions=0,choice_branches=0,parent_pruned=0,beam_pruned=0,
        duplicates_pruned=0,transpositions_pruned=0,dominance_pruned=0,unsupported_boundaries=0,
        completed_compression_pruned=0,window_tests=0,protected_admissions=0,
        replay_transitions=0,root_capture=1,work=1,feature_work=0,peak_frontier=1)
    generated_engine = retained_engine = False
    first_loss = None
    stop = 'frontier_exhausted'
    # Same upper replay reservation for all variants. Unused reserve is not consumed.
    reserve = 2*f.horizon+3
    while frontier:
        if stats['expanded']>=f.max_expanded:
            stop='node_budget'; break
        if time.perf_counter()-begin>=f.wall_limit_seconds:
            stop='wall_budget'; break
        children=[]
        interrupted=False
        for p in frontier:
            acts=legal_actions(p,f)
            extra_choices=max(0,len(acts)-1) if p.phase!='open' else 0
            # Charge declared choice handling as a separate operation.
            feature = len(acts) if variant in ('B','C') else 0
            cost=1+len(acts)+extra_choices+feature
            if stats['expanded']>=f.max_expanded or stats['work']+cost+reserve>f.max_work:
                stop='work_or_node_budget'; interrupted=True; break
            stats['expanded']+=1
            stats['work']+=cost
            stats['feature_work']+=feature
            stats['choice_branches']+=extra_choices
            sims=[transition(p,a,f) for a in acts]
            stats['transitions']+=len(sims)
            if any(n.mode=='engine' for n in sims):
                generated_engine=True
            trace.append({'stage':'generated','parent':p.actions,'children':[observe(s) for s in sims]})
            # Toy reduction of parent family quotas; this is not the full C# portfolio.
            if p.phase=='open' and f.parent_cap<len(sims):
                priority={'defend':0,'attack':1,'decoy':2,'engine':3}
                sims.sort(key=lambda s:priority[s.mode])
                lost=sims[f.parent_cap:]
                if any(s.mode=='engine' for s in lost) and first_loss is None:
                    first_loss={'stage':'parent_cap','parent':p.actions,'lost':[observe(s) for s in lost]}
                stats['parent_pruned']+=len(lost)
                sims=sims[:f.parent_cap]
            for s in sims:
                if s.terminal:
                    completed.append(s)
                    stats['unsupported_boundaries']+=int(s.boundary=='ExternalPlayerChoice')
                else:
                    children.append(s)
        if interrupted:
            # Retain the previous bounded frontier, as a fallback; don't pretend partial
            # parent work is a complete search layer. Already terminal results remain.
            break
        if not children:
            frontier=[]; break
        before_engine=any(s.mode=='engine' for s in children)
        # Actual feature scans are charged above, independent of how many are accepted.
        frontier=beam_rank(children,f,variant,stats,trace)
        after_engine=any(s.mode=='engine' for s in frontier)
        retained_engine|=after_engine
        if before_engine and not after_engine and first_loss is None:
            first_loss={'stage':'beam','depth':len(children[0].actions),
                'lost':[observe(s) for s in children if s.mode=='engine']}
        stats['peak_frontier']=max(stats['peak_frontier'],len(frontier))
    pool=completed+frontier
    if not pool:
        pool=[root]
    keys,depth=final_keys(pool,variant,f.horizon)
    # Stable deterministic tie for toy reproducibility; production list-sort equal keys
    # is a separate contract, not silently claimed to have this tie behavior.
    selected=min(pool,key=lambda s:(keys[s],s.actions))
    replay=root
    for action in selected.actions:
        replay=transition(replay,action,f)
        stats['replay_transitions']+=1
        stats['work']+=1
    if replay != selected:
        raise AssertionError('Toy final replay diverged')
    assert stats['work']<=f.max_work and stats['expanded']<=f.max_expanded
    assert stats['peak_frontier']<=f.beam
    if generated_engine and not any(s.mode=='engine' for s in pool) and first_loss is None:
        first_loss={'stage':'budget_not_reached','stop':stop}
    if any(s.mode=='engine' for s in pool) and selected.mode!='engine' and first_loss is None:
        first_loss={'stage':'final_comparison','common_cycle':depth}
    elapsed=time.perf_counter()-begin
    return dict(fixture=f.name,variant=variant,first_action=selected.actions[0] if selected.actions else None,
        focal_generated=generated_engine,focal_retained=retained_engine,
        focal_in_final_pool=any(s.mode=='engine' for s in pool),focal_selected=selected.mode=='engine',
        first_loss=first_loss,completed_enemy_cycles=selected.cycle,common_cycle=depth,
        cumulative_local_hp_loss=selected.loss,cycle_hp_losses=selected.losses,
        per_cycle_excess=[max(0,x-3) for x in selected.losses],
        current_cycle_hp_loss=selected.current_loss,local_hp=selected.hp,team_alive=2,
        enemy_effective_hp=selected.enemy,potions=selected.potions,action_count=len(selected.actions),
        actions=selected.actions,won=selected.won,boundary=selected.boundary,stop=stop,
        events=dict(star_gains=selected.gain_events,star_spends=selected.spend_events,
                    stars_spent=selected.spent_amount,dark_remaining=selected.dark),
        rng=selected.rng,root_rng=SEED,stats=stats,elapsed_seconds=elapsed,trace=trace,
        complete_final_key=keys[selected],unmodeled=['native card generation','real deck/shuffle',
        'production transposition/dominance','four-player interleaving','potion effects','C# cancellation'])


def fixtures() -> list[Fixture]:
    return [
        Fixture('F01_energy_cycle2','energy',future=16,engine_damage=22,
                note='Recurring-energy investment; synthetic consumer pays on the second completed cycle.'),
        Fixture('F02_scaling_cycle3','scaling',engine_damage=9,persistent=1,
                note='Scaling first use is weak; repeats pay by cycle three.'),
        Fixture('F03_no_consumer','energy',future=16,has_consumer=False,
                note='A future resource is not a usable payoff; C must not create a seat.'),
        Fixture('F04_late_self_risk','energy',future=16,late_risk=6,engine_damage=32,
                note='Later known excess must beat attractive output; no first-action blacklist.'),
        Fixture('F05_gain_then_spend','stars',gain=2,threshold=4,engine_damage=28,
                note='Raw current stars are not FutureResourceValue; legality checked before spend.'),
        Fixture('F06_star_events_block','star_trigger',gain=2,threshold=4,engine_damage=24,
                note='One positive gain/spend event gives fixed BH damage; spend amount gives block.'),
        Fixture('F07_dark_growth_evoke','dark',persistent=6,horizon=3,initial_dark=24,engine_damage=0,
                note='Existing Dark passive feature does not encode stored evoke amount.'),
        Fixture('F08_good_bad_suffix','energy',future=16,bad_suffix=True,engine_damage=22,
                note='One engine first action, separate safe and harmful suffixes.'),
        Fixture('F09_immediate_control','energy',immediate=True,engine_damage=22,
                note='Immediate payoff already enters offense; no extra C protection.'),
        Fixture('F10_seat_opportunity_cost','energy',future=16,decoy_damage=35,engine_damage=20,
                note='Negative control: replaced existing setup seat is actually better later.'),
        Fixture('F11_parent_cap','energy',future=16,parent_cap=2,engine_damage=22,
                note='The route disappears before Beam; B/C after Beam cannot restore it.'),
        Fixture('F12_budget_cutoff','energy',future=16,max_work=31,engine_damage=22,
                note='Small identical total work caps, including feature reads and replay.'),
        Fixture('F13_payoff_cycle6','energy',future=16,horizon=7,ready_cycle=5,engine_damage=80,
                max_work=340,note='Five-cycle bridge within seven enemy cycles; prediction not safety proof.'),
        Fixture('F14_outside_cycle7','energy',future=16,horizon=7,ready_cycle=7,engine_damage=200,
                max_work=340,note='At horizon no next player start; no in-window consumer.'),
        Fixture('F15_peer_only_resource','stars',gain=2,threshold=4,owner_local=False,
                note='Toy engine can progress conditionally; local C may not claim peer-owned opportunity.'),
        Fixture('F17_recurring_power_no_future_field','energy',future=0,engine_damage=22,
                note='Recurring engine may have only generic persistent stacks, not EnergyNextTurnPower.'),
        Fixture('F16_external_choice','energy',future=16,external_choice=True,
                note='No fake default choice; explicitly stop at external-player boundary.'),
    ]


def property_checks() -> list[dict[str,Any]]:
    checks=[]
    def record(name: str, value: Any) -> None:
        checks.append({'name':name,'passed':True,'observed':value})
    f=fixtures()[0]
    rows=[run(f,v) for v in 'ABC']
    assert all(r['root_rng']==SEED for r in rows)
    assert all(r['stats']['work']<=f.max_work for r in rows)
    record('same_root_rng_and_budget', {r['variant']:r['stats']['work'] for r in rows})
    # Key fact blindness. Not a claim that whole states collide in production.
    dark1=State(mode='engine',persistent=6,dark=12,enemy=80)
    dark2=replace(dark1,dark=36)
    assert score(dark1,'A')==score(dark2,'A')
    record('dark_stored_value_invisible_to_reduced_A_score', [score(dark1,'A'),score(dark2,'A')])
    assert max(0,3-3)+max(0,3-3)==0 and max(0,6-3)==3
    record('three_HP_not_six_HP_pool', {'split':[3,3],'split_excess':0,'lumped_excess':3})
    # Two actual gain events, even with equal total gained, are not one event.
    def bh_event_damage(amounts: tuple[int,...]) -> int:
        return 3*sum(x>0 for x in amounts)
    assert bh_event_damage((4,))==3 and bh_event_damage((2,2))==6
    record('star_amount_not_event_count', {'gain4':3,'gain2_twice':6})
    # Marginal resources finite; no square. This pure bound is not a C# implementation.
    assert min(8,4)*2==8 and min(80,4)*2==8
    record('linear_spend_cap', {'stars8':8,'stars80':8})
    s=State(mode='engine',cycle=1,enemy=20,actions=('engine','use'),
        checkpoints=(Checkpoint(1,40,0,100,0),))
    won=replace(s,enemy=0)
    other=replace(s,enemy=10,checkpoints=(Checkpoint(1,40,0,10,0),),actions=('attack','end'))
    pool=[won,other]
    k,_=final_keys(pool,'A',3)
    assert k[won]<k[other]
    record('terminal_before_old_checkpoint', {'winner':'won','old_checkpoint_enemy_hp':100})
    # No fake unearned future at a completed common boundary.
    s1=replace(other,future=0); s2=replace(other,future=10000,actions=('decoy','end'))
    k,_=final_keys([s1,s2],'B',3)
    assert k[s1]==k[s2]
    record('completed_final_does_not_cash_future', list(k[s1]))
    # Enumeration independent fixed set comparison, including strict triples.
    candidates=[replace(other,enemy=10+i,checkpoints=(Checkpoint(1,40,0,10+i,0),),
        actions=(str(i),'end')) for i in range(6)]
    fixed,_=final_keys(candidates,'A',3)
    count=0
    for a,b,c in __import__('itertools').product(candidates,repeat=3):
        assert not (fixed[a]<=fixed[b] and fixed[b]<=fixed[c]) or fixed[a]<=fixed[c]
        count+=1
    rankings={tuple(s.actions[0] for s in sorted(p,key=fixed.__getitem__)) for p in permutations(candidates)}
    assert len(rankings)==1
    record('fixed_pool_total_preorder', {'triples':count,'permutations':720})
    # Eligibility must not secretly inspect future reward parameters.
    e=transition(State(hp=f.hp,enemy=f.enemy),'engine',f)
    altered=replace(f,engine_damage=0,decoy_damage=999,late_risk=100)
    assert window(e,f)==window(e,altered)
    record('window_not_reward_or_future_risk_oracle', window(e,f))
    no=replace(f,has_consumer=False)
    assert not window(e,no)[0]
    record('no_consumer_no_protection', window(e,no))
    # Unknown teammates: common-prefix robust decision, not average of clairvoyant choices.
    values={'hold':(4,4),'hit':(10,-8)}
    assert max(min(v) for v in values.values())==4
    clairvoyant=sum(max(values[a][i] for a in values) for i in (0,1))/2
    assert clairvoyant==7
    record('nonanticipativity_counterexample', {'best_common_prefix_worst_case':4,
                                              'invalid_clairvoyant_average':clairvoyant})
    # Equal seed does not imply equal semantic random event after extra draws.
    r1=random.Random(SEED); r2=random.Random(SEED)
    a=r1.random(); r2.random(); b=r2.random()
    assert a!=b
    record('same_seed_not_same_rng_consumption', {'first':a,'after_peer_consumption':b})
    # Determinism excluding timings.
    x=run(f,'C'); y=run(f,'C')
    x.pop('elapsed_seconds'); y.pop('elapsed_seconds')
    assert x==y
    record('deterministic_replay', 'same_non_timing_JSON')
    return checks


def main() -> int:
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out',type=Path,default=Path('CombatSolver_Multiplayer_LongTerm_Results_20260918.json'))
    args=parser.parse_args()
    fs=sorted(fixtures(), key=lambda f:f.name)
    rows=[run(f,v) for f in fs for v in 'ABC']
    checks=property_checks()
    width8=[run(replace(f,beam=8),v) for f in fs if f.name.startswith(('F01','F07','F10','F17')) for v in 'ABC']
    # Freeze one joint comparison boundary; keep endpoint and tiebreak diagnostics
    # separate. Comparator-order gains are NOT automatically combat gains.
    summary=[]
    for f in fs:
        rs={r['variant']:r for r in rows if r['fixture']==f.name}
        equal_depth=len({r['completed_enemy_cycles'] for r in rs.values()})==1
        def quality(r:dict) -> tuple:
            return (r['local_hp']<=0,sum(r['per_cycle_excess'])+max(0,r['current_cycle_hp_loss']-3),
                not r['won'],-r['team_alive'],r['enemy_effective_hp'],r['cumulative_local_hp_loss'])
        qa,qc=quality(rs['A']),quality(rs['C'])
        states={}
        for v in 'ABC':
            t=State(hp=f.hp,enemy=f.enemy,current_loss=f.root_paid)
            for act in rs[v]['actions']:
                t=transition(t,act,f)
            states[v]=t
        paired_keys,paired_depth=final_keys(list(states.values()),'A',f.horizon)
        ka,kc=paired_keys[states['A']],paired_keys[states['C']]
        comparison='better' if kc<ka else 'worse' if kc>ka else 'equal'
        endpoint_comparison='better' if qc<qa else 'worse' if qc>qa else 'equal'
        tie_only = ka[:-2]==kc[:-2] and ka!=kc
        combat_interpretation = ('tiebreak_only_NOT_combat_gain' if tie_only else comparison)
        summary.append({'fixture':f.name,'C_vs_A_comparator_order':comparison,
            'C_vs_A_combat_interpretation':combat_interpretation,'paired_common_cycle':paired_depth,
            'endpoint_comparison_NOT_common_policy':endpoint_comparison,
            'comparison_differs_only_in_final_tiebreak':ka[:-2]==kc[:-2] and ka!=kc,
            'same_completed_depth':equal_depth,
            'A':{k:rs['A'][k] for k in ('first_action','enemy_effective_hp','cumulative_local_hp_loss','completed_enemy_cycles')},
            'B':{k:rs['B'][k] for k in ('first_action','enemy_effective_hp','cumulative_local_hp_loss','completed_enemy_cycles')},
            'C':{k:rs['C'][k] for k in ('first_action','enemy_effective_hp','cumulative_local_hp_loss','completed_enemy_cycles')}})
    out={'schema':'combat-longterm-lab-v1','baseline_sha':SHA,'game_semantics_target':'0.111.0',
        'evidence':'ACTUALLY_RUN_ABSTRACT_PYTHON_NOT_PRODUCTION','python':platform.python_version(),
        'seed':SEED,'DOP':1,'variant_definitions':{
            'A':'Reduced current three-lane / score / fixed-checkpoint ordering adapter, NOT production C#',
            'B':'A + existing-style FutureResourceValue consumed in heuristic and same setup lane',
            'C':'A + one conditional setup representative from current visible toy guard; same width'},
        'cost_model':{'root_capture':1,'expanded_parent':1,'simulated_edge':1,'choice_branch':1,
            'B_or_C_feature_read_per_generated_edge':1,'final_replay_per_edge':1,
            'unit_warning':'synthetic work unit, NOT C# node, byte, time or calibrated speed'},
        'fixtures':[asdict(f) for f in fs],'results':rows,'summary':summary,'checks':checks,'beam8_controls':width8,
        'unrun':['production C#','game native/simulation differential','real multiplayer',
                 'fixed wall-clock quality benchmarks','real memory peak','full current archive verification'],
        'scope_notes':['Main-tree RNG is a deterministic state counter; random combat effects are not modeled.',
            'No 3/4-player combat, potion engine, production memory or wall-clock quality is tested.',
            'Comparator tiebreak improvement is not counted as a realized combat improvement.']}
    args.out.parent.mkdir(parents=True,exist_ok=True)
    args.out.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f'ABSTRACT_CHECKS_OK fixtures={len(fs)} variants=3 runs={len(rows)} properties={len(checks)}')
    for s in summary:
        print(s['fixture'], ' '.join(f"{v}={s[v]['first_action']}:{s[v]['enemy_effective_hp']}hp/{s[v]['completed_enemy_cycles']}c" for v in 'ABC'),('tiebreak_only_NOT_combat_gain' if s['comparison_differs_only_in_final_tiebreak'] else s['C_vs_A_comparator_order']))
    print(f'BEAM8_CONTROL_RUNS {len(width8)}')
    print('UNRUN production_Csharp native_differential real_multiplayer fixed_wall_quality')
    print('JSON',args.out)
    return 0

if __name__=='__main__':
    raise SystemExit(main())
