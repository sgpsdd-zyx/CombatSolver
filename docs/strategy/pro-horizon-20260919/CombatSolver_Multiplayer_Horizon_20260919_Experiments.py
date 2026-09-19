#!/usr/bin/env python3
"""Bounded, standard-library abstract research, NOT the CombatSolver C# engine.

Run: python CombatSolver_Multiplayer_Horizon_20260919_Experiments.py --out result.json
Optional: --legacy-script PATH executes the unchanged archived 20260918 Python
experiment in a subprocess and records its results (not production C# evidence).
All simulation is fictional, deterministic, and explicitly specified below.
One macro transition normally completes one enemy cycle; real card branching,
C# cost accounting, transpositions, asynchronous workers and game semantics are
NOT reproduced. No network, game process, repository write, or third-party lib.
"""
from __future__ import annotations
import argparse
from collections import Counter
from dataclasses import asdict, dataclass, replace
from functools import cmp_to_key
import hashlib
import itertools
import json
from pathlib import Path
import platform
import random
import subprocess
import sys
import time
from typing import Any, Callable

PREFIX = "CombatSolver_Multiplayer_Horizon_20260919"
SHA = "3ccac172dd55ad4a8d97074b7129cbf4155add65"
FINAL_RESERVE = 384  # common reservation, NOT extra work and NOT a C# setting
POLICIES = ("H3", "H5", "H7", "H9", "Adaptive", "Fallback7")
EXTERNAL_TIMES = (9, 12, 16)  # T12 primary; all times fixed before experiments

@dataclass(frozen=True)
class Action:
    name: str
    damage: int = 0
    block: int = 0
    rescue: int = 0
    due: int = 0
    reward: int = 0
    hint: int = 0  # observable stand-in; never a payout date or probability
    dependency: str = "independent"  # target, marker, rng
    potion: int = 0
    extra: bool = False
    self_loss: int = 0

@dataclass(frozen=True)
class Fixture:
    id: str
    description: str
    actions: tuple[Action, ...]
    enemies: tuple[int, ...] = (200,)
    hp: int = 40
    peer_hp: int = 40
    incoming: tuple[int, ...] = (2,)
    peer_incoming: tuple[int, ...] = ()
    ongoing_damage: int = 2
    root_paid: int = 0
    beam: int = 8
    max_nodes: int = 100
    max_work: int = 1000
    seed: int = 1
    require_potion: bool = False
    potion_available_cycle: int = 0
    external_choice_cycle: int = 0
    suffix_branch_cycle: int = 0
    scenarios: tuple[str, ...] = ("passive",)
    redundant_probes: tuple[str, ...] = ()

@dataclass(frozen=True)
class CP:
    cycle: int
    hp: int
    lost: int
    excess: int
    enemy: int
    team: int
    potions: int
    actions: int

@dataclass(frozen=True)
class State:
    cycle: int = 0
    local_turn: int = 1
    first: str = ""
    hp: int = 40
    peer_hp: int = 40
    enemies: tuple[int, ...] = (200,)
    losses: tuple[int, ...] = ()
    pending_loss: int = 0
    potions: int = 0
    saves: int = 0
    hint: int = 0
    marker: bool = True
    rng_index: int = 0
    extra_pending: bool = False
    mode: str = ""
    actions: tuple[str, ...] = ()
    cps: tuple[CP, ...] = ()
    boundary: str = ""
    won: bool = False
    terminal_at: int = 0
    events: tuple[str, ...] = ()

class Exhausted(RuntimeError):
    pass

class Work:
    def __init__(self, limit: int):
        if limit < FINAL_RESERVE + 30:
            raise ValueError("Budget too small for common publication reserve")
        self.limit = limit
        self.counts: Counter[str] = Counter()
        self.used = 0
        self.final = False

    def spend(self, kind: str, n: int = 1) -> None:
        ceiling = self.limit if self.final else self.limit - FINAL_RESERVE
        if n < 0:
            raise ValueError("Negative work")
        if self.used + n > ceiling:
            raise Exhausted(kind)
        self.used += n
        self.counts[kind] += n

    def sort(self, seq: list[State], keys: dict[State, tuple], kind: str) -> list[State]:
        def compare(a: State, b: State) -> int:
            self.spend(kind)
            return (keys[a] > keys[b]) - (keys[a] < keys[b])
        return sorted(seq, key=cmp_to_key(compare))


def root(f: Fixture) -> State:
    return State(hp=f.hp, peer_hp=f.peer_hp, enemies=f.enemies,
                 pending_loss=f.root_paid)


def action_by_name(f: Fixture, name: str) -> Action:
    return next(a for a in f.actions if a.name == name)


def loss(s: State) -> int:
    return sum(s.losses) + s.pending_loss


def excess(s: State) -> int:
    return sum(max(0, x - 3) for x in s.losses) + max(0, s.pending_loss - 3)


def team(s: State) -> int:
    return int(s.hp > 0) + int(s.peer_hp > 0)


def damage(enemies: list[int], amount: int, fixed: int | None = None) -> None:
    if fixed is not None:
        if enemies[fixed] > 0:
            enemies[fixed] = max(0, enemies[fixed] - amount)
        return
    # A fixed causal target policy: prefer the last living enemy. No oracle.
    for i in reversed(range(len(enemies))):
        if enemies[i] > 0:
            enemies[i] = max(0, enemies[i] - amount)
            return


def legal(s: State, f: Fixture) -> tuple[str, ...]:
    if s.won or s.hp <= 0 or s.boundary:
        return ()
    if not s.first:
        return tuple(a.name for a in f.actions)
    if s.extra_pending:
        return ("extra",)
    c = s.cycle + 1
    if f.suffix_branch_cycle == c:
        return ("cash", "charge")
    if f.potion_available_cycle == c and s.potions == 0:
        return ("advance", "potion")
    return ("advance",)


def transition(s: State, token: str, f: Fixture, scenario: str = "passive") -> State:
    """Pure toy transition; all hidden future events defined in the fixture.
    A root self-cost plus an extra local turn shares the SAME pending 3 HP ledger.
    External choices are censorship boundaries, not made-up successful cycles.
    """
    if s.won or s.hp <= 0 or s.boundary:
        raise ValueError("Cannot continue a terminal or blocked state")
    c = s.cycle + 1
    enemies = list(s.enemies)
    hp, peer, pending = s.hp, s.peer_hp, s.pending_loss
    potions, rng_index, marker = s.potions, s.rng_index, s.marker
    hint, mode, events = s.hint, s.mode, list(s.events)
    actions = s.actions + (token,)
    first = s.first or token
    a = action_by_name(f, first)
    if s.first and f.external_choice_cycle == c and not s.extra_pending:
        return replace(s, boundary="ExternalChoice", actions=actions,
                       events=s.events + (f"external_choice_before_cycle:{c}",))
    if not s.first:
        hp -= a.self_loss
        pending += a.self_loss
        damage(enemies, a.damage)
        potions += a.potion
        hint = a.hint
        peer += a.rescue
        events.append(f"root:{a.name}")
        if sum(enemies) == 0 or hp <= 0:
            return replace(s, first=first, hp=hp, peer_hp=peer,
                enemies=tuple(enemies), pending_loss=pending, potions=potions,
                hint=hint, actions=actions, won=sum(enemies) == 0 and hp > 0,
                terminal_at=0, events=tuple(events))
        if a.extra:
            return replace(s, first=first, hp=hp, peer_hp=peer,
                enemies=tuple(enemies), pending_loss=pending, potions=potions,
                hint=hint, actions=actions, extra_pending=True,
                local_turn=s.local_turn + 1, events=tuple(events))
    elif token == "extra":
        hp -= a.self_loss
        pending += a.self_loss
        events.append("extra_local_turn_no_enemy_cycle_yet")
    else:
        if c == 2:
            if scenario == "kill_target_2":
                enemies[0] = 0
                events.append("peer_kills_target_0")
            elif scenario == "consume_marker_2":
                marker = False
                events.append("peer_consumes_shared_enemy_marker_not_local_inventory")
            elif scenario == "rng_shift_2":
                r = random.Random(f.seed)
                tape = [r.random() for _ in range(rng_index + 1)]
                events.append(f"peer_rng:{rng_index}:{tape[-1]:.12f}")
                rng_index += 1
            elif scenario == "retarget_2":
                enemies[0] = 0
                if len(enemies) > 1:
                    enemies[-1] += 10
                events.append("peer_kill_target_and_trigger_second_enemy_resource")
        damage(enemies, f.ongoing_damage)
        if token == "potion":
            potions += 1
            events.append("local_explicit_potion")
        if token == "cash":
            damage(enemies, 8)
            mode = "cash"
        elif token == "charge":
            mode = "charge"
            hint = max(hint, 1)
        if mode == "charge" and c == 7:
            damage(enemies, 30)
            hint = 0
            events.append("suffix_charge_realized")
    if a.due == c:
        amount = a.reward
        if a.dependency == "target" and enemies[0] == 0:
            amount = 0
        if a.dependency == "marker" and not marker:
            amount = 0
        if a.dependency == "rng":
            r = random.Random(f.seed)
            v = [r.random() for _ in range(rng_index + 1)][-1]
            events.append(f"local_rng:{rng_index}:{v:.12f}")
            rng_index += 1
            if v >= 0.5:
                amount = 0
        damage(enemies, amount, 0 if a.dependency == "target" else None)
        hint = 0
        events.append(f"payout_cycle:{c}:damage:{amount}")
    # A victory before enemy attack is a full real terminal, not a fabricated CP.
    if sum(enemies) == 0 or hp <= 0:
        return State(s.cycle, s.local_turn, first, hp, peer, tuple(enemies),
            s.losses, pending, potions, s.saves, hint, marker, rng_index, False,
            mode, actions, s.cps, "", sum(enemies) == 0 and hp > 0,
            c, tuple(events))
    incoming = f.incoming[c - 1] if c <= len(f.incoming) else 0
    block = a.block if c == 1 else 0
    if scenario == "peer_block_root" and c == 1:
        block += 4
        events.append("peer_block_4_is_perturbation_not_search_assumption")
    paid = max(0, incoming - block)
    hp -= paid
    pending += paid
    if peer > 0:
        peer -= f.peer_incoming[c - 1] if c <= len(f.peer_incoming) else 0
    if hp <= 0:
        return State(s.cycle, s.local_turn, first, hp, peer, tuple(enemies),
            s.losses, pending, potions, s.saves, hint, marker, rng_index, False,
            mode, actions, s.cps, "", False, c, tuple(events))
    losses = s.losses + (pending,)
    cp = CP(c, hp, sum(losses), sum(max(0, x - 3) for x in losses),
            sum(enemies), int(hp > 0) + int(peer > 0), potions, len(actions))
    return State(c, s.local_turn + 1, first, hp, peer, tuple(enemies), losses,
                 0, potions, s.saves, hint, marker, rng_index, False, mode,
                 actions, s.cps + (cp,), "", False, 0, tuple(events))


def common_cycle(pool: list[State], cap: int, work: Work | None = None) -> tuple[int, str]:
    ordinary: list[int] = []
    other: list[int] = []
    for s in pool:
        if work:
            work.spend("common_cycle_scan")
        if s.won or s.hp <= 0:
            continue
        if not s.boundary and s.cps:
            ordinary.append(s.cps[-1].cycle)
        if s.cps and s.cps[-1].cycle > 0:
            other.append(s.cps[-1].cycle)
    if ordinary:
        return min(ordinary), "observed_min"
    if other:
        return min(other), "blocked_positive_fallback"
    if pool and all(s.won or s.hp <= 0 for s in pool):
        return cap, "terminal_only_sentinel_NOT_coverage"
    return 0, "no_complete_comparable_cycle"


def final_key(s: State, k: int, work: Work | None = None, aligned_tie: bool = False) -> tuple:
    if work:
        work.spend("final_key")
    cp = None
    for candidate in reversed(s.cps):
        if work:
            work.spend("checkpoint_scan")
        if candidate.cycle <= k:
            cp = candidate
            break
    terminal = s.won or s.hp <= 0
    comparable = terminal or (cp is not None and k > 0 and cp.cycle == k)
    if terminal or not comparable:
        hp, lost, enemy, alive, bottles = s.hp, loss(s), sum(s.enemies), team(s), s.potions
        bad = excess(s)
    else:
        hp, lost, enemy, alive, bottles = cp.hp, cp.lost, cp.enemy, min(cp.team, team(s)), cp.potions
        bad = max(cp.excess, excess(s))
    pre = (s.hp <= 0, not comparable, s.saves, bad, not s.won)
    tie = cp.actions if aligned_tie and cp and not terminal else len(s.actions)
    if not comparable:
        # Deliberately small proxy, not CombatSolver's actual score function.
        proxy = -sum(s.enemies) - 100 * excess(s) + s.hint
        return pre + (-proxy, tie)
    return pre + (-alive, enemy, lost, -hp, bottles, s.terminal_at if s.won else 0, tie)


def eligible(pool: list[State], f: Fixture, work: Work) -> list[State]:
    out: list[State] = []
    for s in pool:
        work.spend("eligibility_scan")
        if not f.require_potion or s.potions > 0:
            out.append(s)
    return out


def batch(pool: list[State], f: Fixture, cap: int, work: Work) -> tuple[list[State], int, str]:
    unique: list[State] = []
    seen: set[State] = set()
    for s in pool:
        work.spend("dedup_scan")
        if s not in seen:
            unique.append(s)
            seen.add(s)
    pool = eligible(unique, f, work)  # Current R1: full pool eligibility FIRST.
    k, kind = common_cycle(pool, cap, work)
    keys = {s: final_key(s, k, work) for s in pool}
    return work.sort(pool, keys, "final_sort_comparison")[:4 * f.beam], k, kind


def retain(pool: list[State], f: Fixture, work: Work) -> list[State]:
    """Three existing-style representatives inside Beam, never an extra seat.
    No final potion eligibility here: unfinished no-potion prefixes stay legal.
    This is a small adapter, not the full production retention pipeline.
    """
    keys: dict[State, tuple] = {}
    for s in pool:
        work.spend("retention_key")
        keys[s] = (s.hp <= 0, excess(s), -team(s), sum(s.enemies), loss(s), -s.hint, s.actions)
    order = work.sort(pool, keys, "retention_sort_comparison")
    if len(order) <= f.beam:
        return order
    lanes = [
        lambda s: (s.hp <= 0, excess(s), -s.hp, keys[s]),
        lambda s: (sum(s.enemies), keys[s]),
        lambda s: (-s.hint, keys[s]),
    ]
    result: list[State] = []
    if f.beam >= 4:
        result.append(order[0])
    for lane in lanes:
        lane_keys = {}
        for s in order:
            work.spend("lane_key_scan")
            lane_keys[s] = lane(s)
        ranked = work.sort(order, lane_keys, "lane_sort_comparison")
        for s in ranked:
            work.spend("lane_admission_scan")
            if s not in result:
                result.append(s)
                break
        if len(result) >= f.beam:
            break
    for s in order:
        work.spend("retention_fill_scan")
        if len(result) >= f.beam:
            break
        if s not in result:
            result.append(s)
    return result


def snapshot(s: State | None) -> dict | None:
    if s is None:
        return None
    return {"first": s.first, "cycles": s.cycle, "local_turn": s.local_turn,
        "hp": s.hp, "peer_hp": s.peer_hp, "enemy_hp": sum(s.enemies),
        "enemy_by_target": list(s.enemies), "losses": list(s.losses),
        "pending_loss": s.pending_loss, "excess": excess(s), "potions": s.potions,
        "won": s.won, "boundary": s.boundary, "actions": list(s.actions),
        "hint": s.hint, "rng_index": s.rng_index, "events": list(s.events),
        "checkpoints": [asdict(c) for c in s.cps]}


def solve(f: Fixture, policy: str) -> dict:
    started = time.perf_counter_ns()
    cap = 9 if policy == "Adaptive" else (7 if policy == "Fallback7" else int(policy[1:]))
    w = Work(f.max_work)
    active = [root(f)]
    completed: list[State] = []
    previous: list[State] = []
    max_parent = max_child = expanded = transitions = 0
    stop = "cap_or_terminal"
    trace: list[dict] = []
    gate_history: list[dict] = []
    last_winner: str | None = None
    gates_seen: set[int] = set()
    partial: list[State] = []
    # Negative control: redundant root MACRO rechecks consume the SAME ledger.
    # NOT an emulation of CSharp PreviousRoutes (which only replays local-turn
    # non-EndTurn actions). No seats or scores are added by these probes.
    try:
        for name in f.redundant_probes:
            w.spend("redundant_root_recheck", 5)
            _ = transition(root(f), name, f)
            transitions += 1
        while active:
            previous = active[:] if any(s.cycle > 0 for s in active) else []
            partial = []
            for parent in active:
                if expanded >= f.max_nodes:
                    raise Exhausted("node_cap")
                w.spend("parent_admission")
                expanded += 1
                max_parent = max(max_parent, parent.cycle)
                opts = legal(parent, f)
                for token in opts:
                    w.spend("legal_action_scan")
                    w.spend("fork_transition", 5)
                    child = transition(parent, token, f)
                    transitions += 1
                    max_child = max(max_child, child.cycle)
                    trace.append({"stage": "generated", "parent_cycles": parent.cycle,
                                  "child": snapshot(child), "work": w.used})
                    if child.won or child.hp <= 0 or child.boundary or child.cycle >= cap:
                        completed.append(child)
                    else:
                        partial.append(child)
            if partial:
                active = retain(partial, f, w)
            else:
                active = []
            # Like current phased design, completed compression is a separate batch.
            # Most primary fixtures do not fill 4B; the explicit microtest does.
            if len(completed) > 4 * f.beam:
                completed, _, _ = batch(completed, f, cap, w)
            if policy == "Adaptive" and active:
                d = min(s.cycle for s in active)
                if d in (3, 5, 7) and d not in gates_seen:
                    gates_seen.add(d)
                    # One existing search, no second search / future payout oracle.
                    pool = active + completed
                    ep = eligible(pool, f, w)
                    k, kk = common_cycle(ep, cap, w)
                    keys = {s: final_key(s, k, w) for s in ep}
                    ranked = w.sort(ep, keys, "adaptive_sort_comparison")
                    names: set[str] = set()
                    has_hint = False
                    for s in pool:
                        w.spend("adaptive_signal_scan")
                        names.add(s.first)
                        has_hint = has_hint or s.hint > 0
                    winner = ranked[0].first if ranked else None
                    changed = last_winner is not None and winner != last_winner
                    # Unsatisfied final potion policy is NOT proof no feasible future.
                    unresolved_eligibility = f.require_potion and not ep
                    extend = len(names) > 1 and (has_hint or changed or unresolved_eligibility)
                    gate_history.append({"cycle": d, "common": k, "kind": kk,
                        "prefixes": sorted(names), "hint": has_hint, "winner": winner,
                        "changed": changed, "unresolved_eligibility": unresolved_eligibility,
                        "extend": extend, "work": w.used})
                    last_winner = winner
                    if not extend:
                        stop = f"adaptive_stop_{d}"
                        break
            partial = []
    except Exhausted as exc:
        stop = str(exc)
    # Last completely retained cohort plus paid admitted children; no unknown
    # suffix is silently promoted to validated deep safety.
    pool = completed + active
    if stop not in ("cap_or_terminal",) and not stop.startswith("adaptive_stop"):
        pool += previous + partial
    pool = [s for s in pool if s.first]
    search_work = w.used
    w.final = True
    selected: State | None = None
    k, kind = 0, "empty"
    try:
        ranked, k, kind = batch(pool, f, cap, w)
        selected = ranked[0] if ranked else None
        if selected:
            replayed = root(f)
            for token in selected.actions:
                w.spend("final_prefix_replay", 5)
                replayed = transition(replayed, token, f)
            if replayed != selected:
                raise AssertionError("Final toy replay mismatch")
    except Exhausted as exc:
        raise AssertionError(f"Publication reservation insufficient: {f.id}/{policy}/{exc}") from exc
    assert w.used <= f.max_work
    assert expanded <= f.max_nodes
    return {"fixture": f.id, "policy": policy, "cap": cap,
        "max_expanded_parent_cycles": max_parent, "max_produced_cycles": max_child,
        "selected_cycles": selected.cycle if selected else None,
        "common_cycle": k, "common_kind": kind,
        "selected": snapshot(selected), "stop": stop,
        "expanded": expanded, "transitions": transitions,
        "work_limit": f.max_work, "work_used": w.used, "work_counts": dict(w.counts),
        "reserved_publication_ceiling": FINAL_RESERVE,
        "search_work_before_publication": search_work,
        "publication_and_replay_work": w.used - search_work,
        "reserve_unused": max(0, FINAL_RESERVE - (w.used - search_work)),
        "gates": gate_history, "trace": trace,
        "wall_ns_python_only": time.perf_counter_ns() - started}


def causal_tail(s: State, f: Fixture) -> str:
    """Same causal controller for every horizon and every root alternative.
    It observes current state, never the future perturbation or chosen solver H.
    """
    options = legal(s, f)
    if not options:
        raise ValueError("No continuation")
    if "extra" in options:
        return "extra"
    if "potion" in options and f.require_potion and s.potions == 0:
        return "potion"
    if "cash" in options:
        return "cash"
    return "advance"


def external(f: Fixture, first: str, scenario: str, target_cycle: int) -> dict:
    s = transition(root(f), first, f, scenario)
    count = 1
    while s.cycle < target_cycle and not s.won and s.hp > 0 and not s.boundary:
        s = transition(s, causal_tail(s, f), f, scenario)
        count += 1
        if count > 64:
            raise AssertionError("Unbounded judge")
    valid = not s.boundary and (not f.require_potion or s.potions > 0)
    # Deliberately excludes route depth, common cycle and action-count tiebreaks.
    # No probability averaging of scenarios. This is a constrained toy outcome.
    quality = (s.hp <= 0, s.saves, excess(s), not s.won, -team(s), sum(s.enemies),
               loss(s), -s.hp, s.potions, s.terminal_at if s.won else 0) if valid else None
    return {"scenario": scenario, "external_cycle": target_cycle,
            "evaluable": valid, "quality_no_depth_no_action_tie": quality,
            "state": snapshot(s), "offline_judge_transitions": count,
            "offline_judge_work_units": 5 * count,
            "not_available_to_search": True}


def attach_evaluation(f: Fixture, run: dict) -> None:
    run["external_evaluation"] = []
    if not run["selected"]:
        return
    for scenario in f.scenarios:
        for t in EXTERNAL_TIMES:
            run["external_evaluation"].append(external(f, run["selected"]["first"], scenario, t))


def fixtures() -> list[Fixture]:
    hit = Action("Hit", damage=20)
    guard = Action("Guard", block=4)
    cases = [
        Fixture("H01", "即时输出；2 HP 在目标内但仍按已支付扣血比较", (hit,)),
        Fixture("H02", "本机4点入伤必须先防；队友补防只作扰动", (hit, guard), incoming=(4,),
                scenarios=("passive", "peer_block_root")),
        Fixture("H03", "致命控制；不得用远端收益抵扣本机死亡", (hit, guard), hp=3, incoming=(3,)),
        Fixture("H04", "本机风险相同先救队友", (hit, Action("Rescue", rescue=4)),
                peer_hp=3, peer_incoming=(4,)),
    ]
    for i, due in enumerate((3, 5, 7, 9, 11), 5):
        cases.append(Fixture(f"H{i:02}", f"本机独立延迟收益在周期{due}兑现", (
            hit, Action("Invest", due=due, reward=60, hint=1))))
    cases += [
        Fixture("H10", "周期7依赖目标存活；队友提前击杀或改写敌方资源", (
            hit, Action("Mark", due=7, reward=60, hint=1, dependency="target")),
            enemies=(80, 120), scenarios=("passive", "kill_target_2", "retarget_2")),
        Fixture("H11", "共享敌方标记被队友消费；不是队友拿走本机星能", (
            hit, Action("Mark", due=7, reward=60, hint=1, dependency="marker")),
            scenarios=("passive", "consume_marker_2")),
        Fixture("H12", "同seed、不同事件消耗；周期5本机随机收益失效", (
            hit, Action("RandomInvest", due=5, reward=60, hint=1, dependency="rng")),
            scenarios=("passive", "rng_shift_2")),
        Fixture("H13", "同一个首动作，不同条件后缀；外部首动作质量相同", (
            Action("OnlyRoot", damage=10),), suffix_branch_cycle=3),
        Fixture("H14", "小节点预算到不了5/7/9，不把cap当coverage", tuple(
            [Action("Invest", due=7, reward=60, hint=1)] +
            [Action(f"Hit{i}", damage=20-i) for i in range(7)]), max_nodes=10),
        Fixture("H15", "外部选择阻断；不得填零损失或假设队友做出最佳选择", (hit, guard),
                external_choice_cycle=2),
        Fixture("H16", "额外本机回合不计周期；两次2HP自损共用3HP账本", (
            Action("ExtraCost", damage=30, extra=True, self_loss=2), hit), incoming=()),
        Fixture("H17", "真实胜利早于第一检查点；全终局K是哨兵", (
            Action("Win", damage=20),), enemies=(10,), incoming=()),
        Fixture("H18", "完整用药资格在周期4才可满足；中间前缀不能提前过滤", (hit, guard),
                require_potion=True, potion_available_cycle=4),
        Fixture("H19", "观测提示零但周期5有真实收益；自适应假阴性", (
            hit, Action("HiddenInvest", due=5, reward=60, hint=0))),
        Fixture("H20", "持续提示为正但无可兑现收益；自适应假阳性", (
            hit, Action("StaleSetup", hint=1))),
        Fixture("H21", "周期9依赖目标；加深到9可能使当前投资在扰动下更差", (
            hit, Action("LateMark", due=9, reward=60, hint=1, dependency="target")),
            enemies=(80,120), scenarios=("passive", "kill_target_2")),
        Fixture("H22", "根前已付2HP，治疗/新请求不能刷新额度", (hit, guard), root_paid=2,
                incoming=(2,)),
    ]
    return cases


def micro_checks() -> list[dict]:
    out: list[dict] = []
    def add(name: str, passed: bool, **details: Any) -> None:
        if not passed:
            raise AssertionError(name)
        out.append({"name": name, "passed": True, **details})
    def node(name: str, values: tuple[tuple[int,int],...], n: int = 3,
             losses: tuple[int,...] = (), won: bool = False, hp: int = 40,
             potions: int = 0) -> State:
        cps = tuple(CP(c,hp,0,0,e,2,potions,min(c,n)) for c,e in values)
        return State(cycle=values[-1][0] if values else 0, first=name, hp=hp,
            enemies=(0 if won else (values[-1][1] if values else 100),),
            losses=losses, potions=potions, won=won, cps=cps,
            actions=tuple(f"{name}:{i}" for i in range(n)))
    a = node("A", ((3,70),(7,60)))
    bs = [node(f"B{i}", ((3,85),(7,20))) for i in range(32)]
    c = node("C", ((3,95),))
    prior = sorted([a]+bs, key=lambda s:final_key(s,7))[:32]
    trimmed = min(prior+[c], key=lambda s:final_key(s,3))
    united = min([a]+bs+[c], key=lambda s:final_key(s,3))
    add("M01_known_joint_batch_4B_compression", trimmed.first.startswith("B") and united.first=="A",
        beam=8, completed_limit=32, local_common=7, joint_common=3,
        after_early_compression=trimmed.first, full_joint_choice=united.first,
        enemy_at_joint_after=85, enemy_at_joint_full=70,
        classification="abstract_candidate_counterexample_NOT_game_reachability")
    add("M02_common_cycle_can_cancel_deeper_gain",
        min([a,bs[0]],key=lambda s:final_key(s,7))==bs[0] and
        min([a,bs[0],c],key=lambda s:final_key(s,3))==a)
    p = node("same-prefix", ((3,50),), n=3)
    q = node("same-prefix", ((3,50),(7,20)), n=8)
    add("M03_F12_total_action_tie_not_combat_quality", final_key(p,3)<final_key(q,3)
        and final_key(p,3,aligned_tie=True)==final_key(q,3,aligned_tie=True),
        classification="tiebreak_only_NOT_combat_gain", selected_prefix_same=True)
    terminal = node("Win", ((1,99),), won=True)
    competitor = node("Hit", ((1,10),))
    add("M04_real_terminal_not_old_checkpoint", final_key(terminal,1)<final_key(competitor,1))
    bad = replace(q, losses=(0,0,0,6), hp=34)
    safe = replace(q, actions=q.actions+("alternative",))
    add("M05_known_bad_suffix_not_hidden_by_smaller_K", final_key(bad,3)>final_key(p,3)
        and final_key(safe,3)<final_key(bad,3), excess=excess(bad), same_first=bad.first==safe.first)
    f = Fixture("micro", "", (Action("x"),), require_potion=True)
    x = node("Potion", ((1,99),), potions=1)
    w = Work(5000); w.final=True
    selected,k,kind = batch([node(f"No{i}", ((1,1),)) for i in range(40)]+[x],f,7,w)
    add("M06_R1_eligibility_before_truncate", selected==[x], common=k)
    w=Work(5000)
    prefixes = retain([node("unused",((1,1),)),x],f,w)
    add("M07_unfinished_no_potion_prefix_retained", any(s.first=="unused" for s in prefixes))
    won0 = node("win0",(),won=True)
    k,kind=common_cycle([won0],9)
    add("M08_all_terminal_K_is_not_reached_depth",k==9 and won0.cycle==0,
        cap=9, selected_cycles=0, common=9, kind=kind)
    f2=Fixture("extra","",(Action("cost",extra=True,self_loss=2),),incoming=())
    s=transition(root(f2),"cost",f2); t=transition(s,"extra",f2)
    add("M09_extra_turn_same_ledger",s.cycle==0 and t.cycle==1 and excess(t)==1,
        intermediate=snapshot(s), complete=snapshot(t))
    r=random.Random(1); v1,v2=r.random(),r.random()
    add("M10_same_seed_different_event", v1<0.5<v2, seed=1, first=v1, second=v2)
    pool=[a,bs[0],c,p,bad,terminal]
    expected=min(pool,key=lambda s:final_key(s,3)).first
    n=0
    for perm in itertools.permutations(pool):
        assert min(perm,key=lambda s:final_key(s,3)).first==expected
        n+=1
    add("M11_fixed_K_permutations", n==720, permutations=n)
    triples=0
    for x,y,z in itertools.product(pool,repeat=3):
        kx,ky,kz=(final_key(v,3) for v in (x,y,z))
        assert not (kx<=ky and ky<=kz) or kx<=kz
        triples+=1
    add("M12_fixed_K_preorder",triples==216, triples=triples,
        warning="does_not_prove_invariance_when_batch_composition_changes_K")
    # Indistinguishable observations at the gate: no signal-only rule can infer
    # which deterministic future was omitted by the model or feature map.
    f3=Fixture("zero","",(Action("Invest",due=5,reward=60,hint=0),))
    f4=replace(f3,actions=(Action("Invest",hint=0),))
    a3=transition(root(f3),"Invest",f3); b3=transition(root(f4),"Invest",f4)
    for _ in range(2):
        a3=transition(a3,"advance",f3); b3=transition(b3,"advance",f4)
    add("M13_gate_observation_does_not_identify_tail", snapshot(a3)==snapshot(b3))
    blocked=replace(node("blocked",((1,1),)),boundary="ExternalChoice")
    deeper=node("deeper",((1,99),(2,98),(3,97)))
    kk,kind=common_cycle([blocked,deeper],7)
    add("M14_blocked_earlier_checkpoint_not_comparable_at_later_K",kk==3 and
        final_key(blocked,kk)>final_key(deeper,kk),common=kk)
    zero=State(first="unexpanded",actions=("unexpanded",),hp=40,enemies=(1,))
    kk,kind=common_cycle([zero,deeper],7)
    add("M15_missing_checkpoint_does_not_anchor_K_to_zero",kk==3 and
        final_key(zero,kk)>final_key(deeper,kk),common=kk)
    # Negative control for a cheaper but different proposed scope: a shallow
    # nonterminal rejected by normal frontier retention must not silently anchor
    # a later scope defined as the ACTUAL retained frontier plus completed pool.
    kk,kind=common_cycle([a]+bs,7)
    after_frontier=min([a]+bs,key=lambda x:final_key(x,kk))
    before_frontier=min([a]+bs+[c],key=lambda x:final_key(x,3))
    add("M16_rejected_shallow_anchor_changes_scope_not_universal_gain",
        kk==7 and after_frontier.first.startswith("B") and before_frontier.first=="A",
        retained_scope_common=kk, early_scope_choice=before_frontier.first,
        retained_scope_choice=after_frontier.first,
        implication="freeze actual retained frontier scope; pre-prune anchoring is a different policy")
    return out


def strip_timing(obj: Any) -> Any:
    if isinstance(obj,dict):
        return {k:strip_timing(v) for k,v in obj.items() if "wall_ns" not in k}
    if isinstance(obj,list):
        return [strip_timing(x) for x in obj]
    return obj


def main() -> int:
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--out",type=Path,default=Path(PREFIX+"_Results.json"))
    ap.add_argument("--legacy-script",type=Path)
    args=ap.parse_args()
    all_f=fixtures()
    results=[]
    for f in all_f:
        for policy in POLICIES:
            r=solve(f,policy);attach_evaluation(f,r);results.append(r)
        a=next(r for r in results if r["fixture"]==f.id and r["policy"]=="H7")
        b=next(r for r in results if r["fixture"]==f.id and r["policy"]=="Fallback7")
        aa=strip_timing(a);bb=strip_timing(b);aa.pop("policy");bb.pop("policy")
        assert aa==bb, "Fallback must be identity, not a hidden seventh algorithm"
    # Fixed, disclosed budget grid; retain EVERY point, not only a favorable one.
    stress=Fixture("BUDGET_GRID", "8 root options, setup pays at5; gate/replay costs compete", tuple(
        [Action("Invest",due=5,reward=60,hint=1)]+[Action(f"Hit{i}",damage=20-i) for i in range(7)]))
    grid=[]
    for budget in range(510,1961,25):
        f=replace(stress,max_work=budget)
        row={"work_limit":budget,"policies":{}}
        for p in ("H7","Adaptive","Recheck4_H7"):
            ff=replace(f,redundant_probes=("Hit0",)*4) if p=="Recheck4_H7" else f
            r=solve(ff,"H7" if p=="Recheck4_H7" else p)
            r["policy"]=p;attach_evaluation(ff,r)
            # Compact grid output, keep selected route and all costs/gates.
            r.pop("trace")
            row["policies"][p]=r
        grid.append(row)
    micros=micro_checks()
    legacy=None
    if args.legacy_script:
        legacy_path=args.legacy_script.resolve()
        if not legacy_path.is_file():
            raise FileNotFoundError(legacy_path)
        legacy_out=args.out.with_name(PREFIX+"_Legacy_Rerun.json")
        completed=subprocess.run([sys.executable,str(legacy_path),"--out",str(legacy_out)],
            text=True,capture_output=True,timeout=40,check=True)
        legacy={"script_sha256":hashlib.sha256(legacy_path.read_bytes()).hexdigest(),
                "exit_code":completed.returncode,"stdout":completed.stdout,
                "stderr":completed.stderr,"result_file":legacy_out.name,
                "evidence_level":"rerun_archived_Python_NOT_production_CSharp"}
    # Search is completed before root-action enumeration by this offline judge.
    # This oracle supplies regret ONLY for synthetic fixture evaluation, never gates.
    oracle=[]
    for f in all_f:
        for scenario in f.scenarios:
            alternatives=[external(f,a.name,scenario,12) for a in f.actions]
            evaluated=[a for a in alternatives if a["evaluable"]]
            best=min(evaluated,key=lambda e:e["quality_no_depth_no_action_tie"]) if evaluated else None
            oracle.append({"fixture":f.id,"scenario":scenario,"alternatives":alternatives,
                "best_first_under_common_tail":best["state"]["first"] if best else None,
                "not_a_human_probability_or_online_planner":True})
    output={"metadata":{"prefix":PREFIX,"source_commit":SHA,"python":platform.python_version(),
        "evidence_level":"standard_library_abstract_macro_cycle_model_only",
        "production_CSharp_run":False,"game_DLL_run":False,"real_network_run":False,
        "primary_external_cycle":12,"secondary_external_cycles":[9,16],
        "beam":8,"risk_budget_per_cycle":3,"main_policies":list(POLICIES),
        "final_reserve_common_not_added":FINAL_RESERVE,
        "cost_unit":"explicit operation tokens; NOT milliseconds or CSharp nodes",
        "external_judge_cost_is_separate_not_search_input":True,
        "adaptive":"immutable hard9; gates3/5/7; >=2prefixes AND (positive hint OR changed winner OR unresolved potion); all scans paid",
        "main_previous_routes": "empty; actual CSharp prefix reuse is not simulated",
        "limitations":["root/action/cycle macro adapter, not production baseline",
            "no production transposition, card/choice branching, turn-layer4/8 scheduler or parallel workers",
            "keys cached once per toy batch; CSharp comparator scans/actions per comparison differ",
            "toy loss totals include frozen root-paid amount; production keeps root-paid and new cumulative loss separate",
            "toy hints and consumers do not validate Dark/stars/native semantics",
            "no calibrated human distribution; no average win rate",
            "final replay uses common bounded reservation unlike current soft CSharp deadline"]},
        "fixtures":[asdict(f) for f in all_f],"runs":results,"budget_grid":grid,
        "micro_checks":micros,"external_oracle":oracle,"legacy_rerun":legacy}
    canonical=json.dumps(strip_timing(output),ensure_ascii=False,sort_keys=True,separators=(",",":"))
    # Legacy script reports timing internally; reproducibility hash is for NEW lab.
    stable=dict(output);stable["legacy_rerun"]=None
    output["deterministic_payload_sha256"]=hashlib.sha256(json.dumps(strip_timing(stable),
        ensure_ascii=False,sort_keys=True,separators=(",",":")).encode()).hexdigest()
    args.out.parent.mkdir(parents=True,exist_ok=True)
    args.out.write_text(json.dumps(output,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(f"SOURCE {SHA}")
    print(f"ABSTRACT_ONLY fixtures={len(all_f)} main_runs={len(results)} grid_runs={len(grid)*3} micro_checks={len(micros)}")
    print("fixture | H3 H5 H7 H9 Adaptive Fallback7 (first / selected cycles / K / work)")
    for f in all_f:
        values=[]
        for r in results:
            if r["fixture"]==f.id:
                name=r["selected"]["first"] if r["selected"] else "NO_ELIGIBLE"
                values.append(f'{name}/{r["selected_cycles"]}/{r["common_cycle"]}/{r["work_used"]}')
        print(f.id+" | "+" | ".join(values))
    print("PAYLOAD_SHA256 "+output["deterministic_payload_sha256"])
    print("JSON "+str(args.out))
    return 0

if __name__=="__main__":
    raise SystemExit(main())
