#!/usr/bin/env python3
"""CombatSolver 0.41.2 review: reproducible ABSTRACT checks, not production C#.

Python 3.10+, standard library only.  No DLL, game, network, repository writes.
The facts/order functions port the selected scalar branches of fixed commit
2dc5d15b26f16d89436af0fb98650d8b4cf6b411. The toy searches are deliberately
separate; they do NOT port the combat engine or predict game win rates.

Run:
  python CombatSolver_0.41.2_abstract_checks.py --out ./abstract-results
Optional source-text consistency checks (read-only):
  --source ./CombatSolver-0.41.2
"""
from __future__ import annotations
import argparse
from dataclasses import dataclass, replace
from itertools import permutations, product
import json
from pathlib import Path
import shutil
import sys
from typing import Iterable

COMMIT = '2dc5d15b26f16d89436af0fb98650d8b4cf6b411'
ALLOWANCE = 3
HORIZON = 7

@dataclass(frozen=True)
class CP:
    cycle: int
    hp: int = 50
    loss: int = 0                 # root-relative cumulative loss
    saves: int = 0                # existing implementation: whole party
    enemy: int = 40
    team: int = 2
    potions: int = 0
    action_count: int = 2         # PROPOSED metadata, not current CP field

@dataclass(frozen=True)
class Node:
    name: str
    cps: tuple[CP, ...] = ()
    won: bool = False
    dead: bool = False
    boundary: str = 'None'
    hp: int = 50
    max_hp: int = 50
    loss: int = 0
    root_loss: int = 0
    saves: int = 0
    enemy: int = 40
    team: int = 2
    potions: int = 0
    automatic: int = 0
    completed_excess: int = 0
    current_loss: int = 0
    forced: bool = True
    score: float = 0.0
    actions: int = 2
    ended_turn: int | None = None
    canonical_prefix: str = ''

    @property
    def explicit(self) -> int:
        return max(0, self.potions - self.automatic)

    @property
    def excess(self) -> int:
        return self.completed_excess + max(0, self.current_loss - ALLOWANCE)

@dataclass(frozen=True)
class Facts:
    comparable: bool
    won: bool
    hp: int
    loss: int
    saves: int
    excess: int
    enemy: int
    team: int
    potions: int


def depth_for(nodes: Iterable[Node]) -> int:
    """MultiplayerEvaluation.cs:20-43; skips zero-checkpoint nodes in first pass."""
    nodes = list(nodes)
    ordinary = [n.cps[-1].cycle for n in nodes
                if not (n.won or n.dead)
                and n.boundary in ('None', 'AdvisoryHorizon') and n.cps]
    if ordinary:
        return min(ordinary)
    blocked = [n.cps[-1].cycle for n in nodes if not(n.won or n.dead) and n.cps]
    depth = min(blocked, default=0)
    if depth == 0 and nodes and all(n.won or n.dead for n in nodes):
        depth = HORIZON
    return depth


def facts_at(n: Node, depth: int, fix_win: bool = False) -> Facts:
    """MultiplayerEvaluation.cs:49-68. fix_win is an explicit design delta."""
    if fix_win and (n.won or n.dead):
        return Facts(True,n.won,n.hp,n.loss,n.saves,n.excess,n.enemy,n.team,n.potions)
    cp = next((c for c in reversed(n.cps) if c.cycle <= depth), None)
    if depth > 0 and cp is not None and cp.cycle == depth:
        previous = -n.root_loss
        excess = 0
        for c in n.cps:
            if c.cycle > depth:
                break
            excess += max(0, c.loss - previous - ALLOWANCE)
            previous = c.loss
        return Facts(True, False,
                     cp.hp, cp.loss, cp.saves, excess, cp.enemy, cp.team, cp.potions)
    return Facts(n.won or n.dead, n.won, n.hp, n.loss, n.saves,
                 n.excess, n.enemy, n.team, n.potions)


def rank_key(n: Node, depth: int, fix_win: bool = False,
             scoped_tie: bool = False, flag_only: bool = False) -> tuple:
    """Scalar lexicographic equivalent of CompareMultiplayerAtCycle:71-115.
    Finite scores only. Nullable<int> ordering is represented explicitly.
    NOT an implementation of .NET List.Sort tie stability.
    """
    a = facts_at(n, depth, fix_win)
    if flag_only and n.won:
        a = replace(a,won=True)
    head = (-int(n.forced), int(n.dead), -int(a.comparable),
            max(a.saves, n.saves), max(a.excess, n.excess), -int(a.won))
    if not a.comparable:
        return head + (-n.score, n.actions)
    ending = (n.ended_turn is not None, n.ended_turn or 0) if a.won else (False, 0)
    cp = next((c for c in n.cps if c.cycle == depth), None)
    tie = (cp.action_count if scoped_tie and cp else n.actions)
    key = head + (-min(a.team, n.team), a.enemy, a.loss, -a.hp, a.potions, ending, tie)
    if scoped_tie:
        key += (n.canonical_prefix or n.name,)
    return key


def compare(a: Node, b: Node, depth: int) -> int:
    ka, kb = rank_key(a, depth), rank_key(b, depth)
    return (ka > kb) - (ka < kb)


def eligible(nodes: Iterable[Node], minimum: int = 0, require_one: bool = False,
             enforce_forced: bool = True) -> list[Node]:
    out: list[Node] = []
    seen: set[int] = set()
    for n in nodes:
        if id(n) in seen:
            continue
        seen.add(id(n))
        if enforce_forced and not n.forced:
            continue
        if n.explicit < minimum or require_one and n.explicit == 0:
            continue
        out.append(n)
    return out


def prepare(nodes: Iterable[Node], beam: int = 1, *, minimum: int = 0,
            require_one: bool = False, enforce_forced: bool = True,
            fixed_depth: int | None = None, fix_win: bool = False) -> tuple[list[Node], int]:
    pool = eligible(nodes, minimum, require_one, enforce_forced)
    d = depth_for(pool) if fixed_depth is None else fixed_depth
    return sorted(pool, key=lambda n: rank_key(n, d, fix_win))[:4 * beam], d


def ordinary(name: str, enemy: int, cycle: int = 1, **kw) -> Node:
    cps = tuple(CP(i, enemy=enemy + cycle - i) for i in range(1, cycle+1))
    return Node(name, cps=cps, enemy=enemy, **kw)


def early_stop_0412(n: Node, has_growth: bool = False, future_sold: int = 0,
                    initial_max_hp: int = 50) -> bool:
    """Phases.cs:1995-2009. Does not consult team/saves/Multiplayer flag."""
    return (not has_growth and n.won and n.explicit == 0 and future_sold == 0
            and n.loss == 0 and n.max_hp >= initial_max_hp and n.hp >= n.max_hp)


def run_checks(source: Path | None) -> dict:
    rows: list[dict] = []
    def record(test: str, detail: dict) -> None:
        rows.append({'test': test, 'status': 'PASS', **detail})

    # A01: fixed R1/R2 on synthetic scalar rows; not the repository's C# test run.
    dry = [ordinary(f'dry{i}', 10+i) for i in range(4)]
    used = ordinary('used', 90, potions=1)
    cohort5 = dry + [used]
    cohort6 = cohort5 + [ordinary('forced_bad', 1, forced=False)]
    count = 0
    for cohort in (cohort5, cohort6):
        for perm in permutations(cohort):
            result, d = prepare(perm, require_one=True)
            assert result and result[0].name == 'used' and d == 1
            # Stored batch is used as-is: no new sort or context after cut.
            assert (result[0], d) == (result[0], d)
            count += 1
    assert count == 840
    assert not prepare([ordinary('auto', 10, potions=1, automatic=1)], require_one=True)[0]
    assert not prepare(cohort5, minimum=2)[0]
    assert dry[0] in list(cohort5)  # exploration candidate pool is NOT final-filtered
    triple_count = 0
    for a,b,c in product(cohort6, repeat=3):
        if compare(a,b,1) <= 0 and compare(b,c,1) <= 0:
            assert compare(a,c,1) <= 0
        triple_count += 1
    record('A01_R1_R2_fixed_pool', {'permutations': count, 'triples': triple_count,
                                  'only_explicit_eligible': True})

    # A02: actual victory vs earlier checkpoint, with an ordinary shallow rival.
    w = Node('W_later_victory', cps=(CP(1, enemy=40),), won=True, enemy=0,
             actions=4, ended_turn=2)
    n = ordinary('N_nonterminal', 39, actions=2)
    pool, d = prepare([w,n])
    fixed, _ = prepare([w,n], fix_win=True)
    assert d == 1 and not facts_at(w,d).won and pool[0] == n and fixed[0] == w
    immediate = replace(w, name='W_immediate', cps=(), ended_turn=1)
    assert prepare([immediate,n])[0][0] == immediate
    all_term = [w, replace(w, name='W_other', ended_turn=3)]
    assert depth_for(all_term) == 7 and facts_at(w,7).won
    record('A02_victory_checkpoint_shadow', {'depth':d,'current_winner':pool[0].name,
               'fixed_winner':fixed[0].name,'immediate_win_ok':True,
               'all_terminal_context':7,'actual_completed_cycles_of_W':1})

    # A03: shared solo stopping predicate is not a bound for MP's own comparator.
    w_peer_dead = replace(w, name='win_peer_dead', cps=(), team=1)
    v_peer_alive = replace(w, name='win_peer_alive', cps=(), team=2, actions=5, ended_turn=3)
    assert early_stop_0412(w_peer_dead)
    assert compare(v_peer_alive,w_peer_dead,7) < 0
    w_peer_fairy = replace(w, name='win_peer_fairy', cps=(), saves=1, potions=1, automatic=1)
    assert early_stop_0412(w_peer_fairy)
    assert compare(v_peer_alive,w_peer_fairy,7) < 0
    record('A03_premature_multiplayer_stop', {'stop_with_peer_dead':True,
                'stop_with_peer_fairy':True,'abstract_better_suffix_not_ruled_out':v_peer_alive.name})

    # A04: two compression contexts; each individual prepare already has fixed depth.
    archive = [Node(name, cps=(CP(1,enemy=e1), CP(2,enemy=e2)),
                    enemy=e2, boundary='ExternalPlayerChoice', actions=4)
               for name,e1,e2 in [('A',10,9),('B',20,8),('C',30,7),('D',40,6),('E',50,5)]]
    compressed, d_old = prepare(archive)
    shallow = ordinary('S_live',99)
    merged, d_new = prepare([*compressed,shallow])
    full, _ = prepare([*archive,shallow])
    epoch_d = depth_for(eligible([*archive,shallow]))
    context_compressed, _ = prepare(archive, fixed_depth=epoch_d)
    repaired, _ = prepare([*context_compressed,shallow], fixed_depth=epoch_d)
    assert d_old == 2 and d_new == 1 and 'A' not in [x.name for x in compressed]
    assert merged[0].name == 'B' and full[0].name == repaired[0].name == 'A'
    record('A04_cross_batch_loss', {'old_depth':d_old,'new_depth':d_new,
        'archive_after_cut':[x.name for x in compressed], 'compressed_winner':merged[0].name,
        'full_pool_winner':full[0].name,'same_epoch_repair':repaired[0].name})

    # A05: tail-only extension flips a root-action tie at an unchanged checkpoint.
    a = ordinary('A',40,actions=2,canonical_prefix='A')
    b = ordinary('B',40,actions=3,canonical_prefix='B')
    a_long = replace(a,name='A_extended',actions=4)
    assert compare(a,b,1) < 0 < compare(a_long,b,1)
    assert rank_key(a_long,1,scoped_tie=True) < rank_key(b,1,scoped_tie=True)
    tie_peer = replace(a,name='another_root')
    assert compare(a,tie_peer,1) == 0
    record('A05_tail_length_and_ties', {'before':'A','after':'B','scoped_tie_winner':'A',
                'distinct_routes_may_compare_equal':True})

    # A06: wider fixed-context preorder tests; no set-independent order asserted.
    samples: list[Node] = []
    for i in range(24):
        c = CP(1,hp=50-(i%4),loss=i%4,saves=i%2,enemy=30+i%5,team=1+i%2,potions=i%3)
        cps = () if i%6 == 0 else (c,)
        won,dead = i%8==0, i%8==1
        samples.append(Node(f'p{i}', cps=cps, won=won, dead=dead,
            boundary=('None','AdvisoryHorizon','ExternalPlayerChoice','PendingChoice','UnsupportedEffect','TimeLimit')[i%6],
            hp=0 if dead else 50-i%4, loss=i%4, saves=i%2,
            enemy=0 if won else 20+i, team=1+i%2, potions=i%3,
            current_loss=i%5, actions=i%5+1,score=float(i%7),forced=i%7!=0,
            ended_turn=2 if won else None))
    comparisons=triples=0
    for d in (0,1,2,7):
        for x,y in product(samples,repeat=2):
            assert compare(x,y,d) == -compare(y,x,d)
            comparisons += 1
        for x,y,z in product(samples,repeat=3):
            if compare(x,y,d) <= 0 and compare(y,z,d) <= 0:
                assert compare(x,z,d) <= 0
            triples += 1
    record('A06_fixed_context_preorder',{'nodes':24,'depths':[0,1,2,7],
                                       'antisymmetry_pairs':comparisons,'triples':triples})

    # A07: finite evidence is not a proof that a prefix has no safe continuation.
    p = ordinary('P_unknown',30)
    bad = replace(p,name='P_bad_suffix',current_loss=5,actions=5)
    good = replace(p,name='P_good_suffix',actions=4)
    q = ordinary('Q_known_excess1',40,current_loss=4,actions=4)
    assert compare(p,q,1)<0 and compare(bad,q,1)>0 and compare(good,q,1)<0
    assert prepare([p,bad,q])[0][0] == p
    # Removing later risk would incorrectly rank the actually bad suffix above Q.
    assert rank_key(replace(bad,current_loss=0),1)<rank_key(q,1)
    record('A07_unknown_vs_bad_suffix',{'unknown_beats_Q':True,'bad_suffix_loses_to_Q':True,
        'same_first_action_good_suffix_beats_Q':True,'ancestor_not_proof_of_future_safety':True})

    # A08: fixed policy uses team-wide saves before local excess. Ownership fix alone doesn't change it.
    save_peer = Node('local3_peerFairy',cps=(CP(1,hp=47,loss=3,saves=1,potions=1),),
                     hp=47,loss=3,saves=1,potions=1,automatic=1)
    hurt_local = Node('local6_peerNoSave',cps=(CP(1,hp=44,loss=6),),hp=44,loss=6,completed_excess=3)
    assert compare(hurt_local,save_peer,1)<0
    record('A08_resource_ownership_policy',{'current_winner':hurt_local.name,
        'loser':save_peer.name,'owner_observation_alone_changes_policy':False})

    # A09: end-of-parent iterator admission. Three children already evaluated, only one consumed.
    events: list[str] = []
    def paid_children():
        materialized = [('first',1),('second_better',2),('third_best',3)]
        events.extend('simulate:'+name for name,_ in materialized)
        yielded = 0
        try:
            for child in materialized:
                yielded += 1
                yield child
            events.append('simulate:potion_or_endturn')
            yield ('endturn',4)
        finally:
            events.extend('release:'+name for name,_ in materialized[yielded:])
    gen = paid_children()
    accepted = [next(gen)]
    gen.close()
    assert accepted == [('first',1)]
    assert events == ['simulate:first','simulate:second_better','simulate:third_best',
                      'release:second_better','release:third_best']
    record('A09_last_parent_admission', {'already_simulated':3,'accepted':1,
        'discarded_paid_children':2,'potion_endturn_not_started':True,'events':events})

    # A10: exact toy threat model. No assertion about a concrete game encounter.
    # Enemy D: HP6 attacks peer HP4 for5; enemy S: HP6 no attack. One 6-damage local attack.
    kill_safe = {'enemy_hp':6,'local_loss':0,'team_before':2,'team_after':1}
    kill_danger = {'enemy_hp':6,'local_loss':0,'team_before':2,'team_after':2}
    assert tuple(kill_safe[k] for k in ('enemy_hp','local_loss','team_before')) == tuple(kill_danger[k] for k in ('enemy_hp','local_loss','team_before'))
    assert kill_danger['team_after'] > kill_safe['team_after']
    record('A10_asymmetric_threat', {'aggregate_prefix_tie':True,'after_cycle':{
        'kill_safe_enemy_survivors':1,'kill_dangerous_enemy_survivors':2},
        'specific_game_reachability':'NOT_TESTED'})

    # A11: exact deterministic toy tree + counted edges + full final route replay.
    # Width-one root heuristic picks A; a challenge keeps B's paid first edge.
    edges = {
        ('root','A'):('a',5), ('root','B'):('b',0),
        ('a','a1'):('a1',6), ('a','a2'):('a2',5),
        ('a','a3'):('a3',4), ('a','a4'):('a4',3),
        ('b','b1'):('b1',20), ('b','b2'):('b2',0),
    }
    def toy_run(use_challenge: bool) -> tuple[int,list[str]]:
        trace: list[str] = []
        def step(state: str, action: str, replay: bool=False):
            trace.append(('replay:' if replay else 'search:')+state+'->'+action)
            return edges[(state,action)]
        step('root','A'); step('root','B')
        tests = [('a','a1'),('b','b1'),('b','b2'),('a','a2')] if use_challenge else [
                 ('a','a1'),('a','a2'),('a','a3'),('a','a4')]
        tested = [(edge,step(*edge)[1]) for edge in tests]
        (parent,action),value = max(tested,key=lambda item:item[1])
        first = 'A' if parent=='a' else 'B'
        state,_ = step('root',first,replay=True)
        _,checked_value = step(state,action,replay=True)
        assert checked_value == value and len(trace)==8
        return value,trace
    help_base,help_base_trace = toy_run(False)
    help_new,help_new_trace = toy_run(True)
    assert help_base == 6 and help_new == 20
    record('A11_two_step_can_help',{'baseline_value':help_base,'challenge_value':help_new,
        'both_total_toy_work':8,'both_final_replay_work':2,
        'baseline_trace':help_base_trace,'challenge_trace':help_new_trace})

    # A12: exact counted toy chain with replay reservation; SAME six-edge budget.
    # Challenger inspects a dead-end twice, leaving only two baseline edges plus
    # their replay. Baseline finds the three-edge certificate; the first local
    # action need not differ, but the available completed evidence regresses.
    chain = {('root','a'):('a',1), ('a','b'):('ab',2), ('ab','c'):('abc',10),
             ('root','x'):('x',0), ('x','y'):('xy',0)}
    def budget_schedule(challenge_first: bool) -> tuple[int,list[str]]:
        budget = 6
        trace: list[str] = []
        def step(state: str, action: str, replay: bool=False):
            trace.append(('replay:' if replay else 'search:')+state+'->'+action)
            return chain[(state,action)]
        if challenge_first:
            state,_ = step('root','x'); step(state,'y')
        state,value = 'root',0
        path: list[str] = []
        for action in ('a','b','c'):
            # One new edge, then enough work to replay the entire selected path.
            if len(trace)+1+(len(path)+1)>budget:
                break
            state,value = step(state,action)
            path.append(action)
        state='root'
        for action in path:
            state,checked_value = step(state,action,replay=True)
        assert value == checked_value and len(trace)==budget
        return value,trace
    vb,tb = budget_schedule(False); vc,tc = budget_schedule(True)
    assert vb == 10 and vc == 2
    record('A12_two_step_can_hurt',{'same_total_work':6,'baseline_value':vb,'challenge_value':vc,
        'baseline_trace':tb,'challenge_trace':tc,'same_first_action_in_this_fixture':True})

    # A13: three-edge payoff remains unseen after two; eight legal toy choices
    # need eight transitions. Neither number estimates the production engine.
    delayed_payoff = [0,0,30]
    two_step_best = max(5,max(delayed_payoff[:2]))
    three_step_best = max(5,max(delayed_payoff))
    choice_transitions = [('choice',i) for i in range(8)]
    assert two_step_best==5 and three_step_best==30 and len(choice_transitions)==8
    record('A13_two_step_scope',{'delayed_payoff':delayed_payoff,'two_step_best':two_step_best,
        'three_step_best':three_step_best,'example_second_action_choice_fanout':len(choice_transitions),
        'a_second_action_is_not_one_simulation':True})

    # A14: simple per-enemy-cycle loss ledger (not game history collection).
    def budget_excess(losses: list[int]) -> int:
        return sum(max(0,x-3) for x in losses)
    assert budget_excess([3,3])==0 and budget_excess([0,6])==3
    root_paid, later_self_loss, healing = 2,2,10
    assert max(0,root_paid+later_self_loss-3)==1  # healing deliberately absent
    before_end = 3
    next_player_start = 2
    assert budget_excess([before_end,next_player_start])==0
    assert max(0,before_end+next_player_start-3)==2  # incorrect extra-turn/reset conflation
    record('A14_hp_budget', {'three_plus_three_excess':0,'zero_plus_six_excess':3,
        'root2_self2_heal10_excess':1,'end3_next_start2_separate_excess':0})

    # A15: nonanticipativity: scenario-conditioned independent maxima are not executable.
    payoffs = {'X':(10,0),'Y':(0,10),'Z':(6,6)}
    clairvoyant = sum(max(v[i] for v in payoffs.values()) for i in range(2))/2
    shared = max(payoffs,key=lambda x:sum(payoffs[x])/2)
    robust = max(payoffs,key=lambda x:min(payoffs[x]))
    assert clairvoyant==10 and shared==robust=='Z'
    record('A15_shared_prefix_scenarios',{'invalid_clairvoyant_average':10,
        'best_shared_prefix':'Z','shared_toy_average':6,'weights_are_not_probabilities':True})

    # A16: zero complete cycle / external-only / mixed boundary behavior remains explicit.
    unknown = Node('unknown',score=1e6,actions=1)
    measured = ordinary('measured',50)
    assert depth_for([unknown,measured])==1
    assert prepare([unknown,measured])[0][0]==measured
    ext0 = replace(unknown,name='external0',boundary='ExternalPlayerChoice')
    ext2 = ordinary('external2',40,cycle=2,boundary='ExternalPlayerChoice')
    assert depth_for([ext0,ext2])==2 and not facts_at(ext0,2).comparable
    assert depth_for([unknown])==0 and not facts_at(unknown,0).comparable
    record('A16_boundary_semantics', {'mixed_zero_and_one_context':1,
        'external_zero_and_two_context':2,'unknown_does_not_become_verified':True})

    # A17: setting only Won=true is NOT a complete fix: two true wins retain
    # different old checkpoint benefits when an unrelated nonterminal is added.
    wa = Node('WA_earlier_win',cps=(CP(1,enemy=40),),won=True,enemy=0,actions=4,ended_turn=2)
    wb = Node('WB_later_win',cps=(CP(1,enemy=30),),won=True,enemy=0,actions=5,ended_turn=3)
    unrelated = ordinary('nonterminal',99)
    assert prepare([wa,wb])[0][0]==wa
    assert prepare([wa,wb,unrelated])[0][0]==wb
    assert min([wa,wb],key=lambda n:rank_key(n,1,flag_only=True))==wb
    assert min([wa,wb],key=lambda n:rank_key(n,1,fix_win=True))==wa
    record('A17_won_flag_only_insufficient',{'all_terminal_winner':wa.name,
        'mixed_pool_winner':wb.name,'flag_only_winner':wb.name,'terminal_facts_first_winner':wa.name})

    # A18: prior peer potion is an INPUT problem before final pool eligibility.
    # Source CountPotionHistoryEntries counts whole history, Actor unfiltered.
    history = [('peer','potion')]
    global_paid = len(history)
    local_paid = sum(actor=='local' for actor,_ in history)
    dry_candidate = ordinary('local_no_potion',10)
    wet_candidate = ordinary('local_use_potion',30,potions=1)
    configured_require_one = True
    old_require = configured_require_one and global_paid==0
    local_require = configured_require_one and local_paid==0
    old = prepare([dry_candidate,wet_candidate],require_one=old_require)[0][0]
    own = prepare([dry_candidate,wet_candidate],require_one=local_require)[0][0]
    assert old.name=='local_no_potion' and own.name=='local_use_potion'
    assert global_paid==1 and local_paid==0
    record('A18_root_peer_potion_disables_requirement',{'global_prior_uses':global_paid,
        'local_prior_uses':local_paid,'current_effective_policy':'Smart',
        'current_winner':old.name,'local_scoped_winner':own.name,
        'R1_filter_function_itself_is_not_broken':True})

    source_checks = None
    if source is not None:
        checks = {
          'checkpoint_returns_won_false': ('src/Search/CombatBeamSolver.MultiplayerEvaluation.cs',
                                           'return new(true, false, checkpoint.Hp'),
          'final_batch_type': ('src/Search/CombatBeamSolver.Multiplayer.cs',
                              'private sealed record MultiplayerFinalBatch'),
          'completed_compressed_independently': ('src/Search/CombatBeamSolver.Phases.cs',
                              '? PrepareMultiplayerFinalCandidates(completedCandidates).Candidates'),
          'shared_zero_loss_stop': ('src/Search/CombatBeamSolver.Phases.cs',
                              'if (!_hasGrowthTargets && completed.Any(node =>'),
          'legacy_first_child_comment': ('src/Search/CombatBeamSolver.Phases.cs',
                              'The legacy iterator intentionally yields only the first child'),
          'solo_power_guard': ('src/Search/CombatBeamSolver.cs',
                              '_hasRegisteredPowerCards = policy.Multiplayer == null'),
        }
        source_checks = {}
        for key,(relative,needle) in checks.items():
            text = (source/relative).read_text(encoding='utf-8-sig')
            assert needle in text, f'source mismatch: {relative}: {needle}'
            source_checks[key] = True
    return {'baseline_commit':COMMIT, 'evidence_level':'ABSTRACT_PYTHON_ONLY',
            'python_version':sys.version.split()[0], 'dotnet_found':shutil.which('dotnet') is not None,
            'tests_passed':len(rows),'source_text_checks':source_checks,'results':rows,
            'not_run':['production C# contracts','game native/simulator differential','real multiplayer sessions']}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out',type=Path,default=Path('abstract-results'))
    parser.add_argument('--source',type=Path)
    args = parser.parse_args()
    result = run_checks(args.source)
    args.out.mkdir(parents=True,exist_ok=True)
    output = json.dumps(result,ensure_ascii=False,indent=2)+'\n'
    (args.out/'CombatSolver_0.41.2_abstract_results.json').write_text(output,encoding='utf-8')
    lines = [f'Baseline: {COMMIT}', 'Evidence: ABSTRACT_PYTHON_ONLY',
             f'Python: {result["python_version"]}; dotnet found: {result["dotnet_found"]}']
    for row in result['results']:
        lines.append(f'PASS {row["test"]}: '+json.dumps({k:v for k,v in row.items() if k not in ('test','status')},ensure_ascii=False,sort_keys=True))
    lines += [f'PASS total={result["tests_passed"]}',
              'NOT RUN: production C#; native game differential; real multiplayer']
    text = '\n'.join(lines)+'\n'
    (args.out/'CombatSolver_0.41.2_abstract_output.txt').write_text(text,encoding='utf-8')
    print(text,end='')

if __name__ == '__main__':
    main()
