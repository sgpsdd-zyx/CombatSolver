"""Exact small examples for quota semantics, not a CombatSolver gameplay benchmark."""

from dataclasses import dataclass, asdict, replace
import json


@dataclass(frozen=True)
class Card:
    name: str
    cost: int
    damage: int = 0
    block: int = 0
    vulnerable: int = 0


@dataclass(frozen=True)
class State:
    hp: int
    enemy_hp: int
    energy: int
    hand: tuple[Card, ...]
    block: int = 0
    vulnerable: int = 0
    own_damage: int = 0
    team_damage: int = 0
    hp_lost: int = 0
    actions: tuple[str, ...] = ()


def hit(state, damage, own):
    actual = min(state.enemy_hp, damage * (3 if state.vulnerable else 2) // 2)
    return replace(state, enemy_hp=state.enemy_hp - actual,
                   own_damage=state.own_damage + (actual if own else 0),
                   team_damage=state.team_damage + actual)


def play(state, index):
    card = state.hand[index]
    assert state.enemy_hp > 0 and state.hp > 0 and card.cost <= state.energy
    result = replace(state, energy=state.energy - card.cost,
                     hand=state.hand[:index] + state.hand[index + 1:],
                     block=state.block + card.block,
                     vulnerable=state.vulnerable + card.vulnerable,
                     actions=state.actions + (card.name,))
    return hit(result, card.damage, True)


def prefixes(root):
    result = [root]
    if root.enemy_hp <= 0 or root.hp <= 0:
        return result
    for i, card in enumerate(root.hand):
        if card.cost <= root.energy:
            result.extend(prefixes(play(root, i)))
    return result


def finish(state, incoming, peer_attacks=()):
    if state.hp <= 0:
        return state
    for attack in peer_attacks:
        if state.enemy_hp <= 0:
            break
        state = hit(state, attack, False)
    damage = min(state.hp, max(0, incoming - state.block)) if state.enemy_hp > 0 else 0
    return replace(state, hp=state.hp - damage, hp_lost=state.hp_lost + damage)


def best_qualified(states, quota, metric="own_damage"):
    qualified = [s for s in states if s.hp > 0 and getattr(s, metric) >= quota]
    return min(qualified, key=lambda s: (s.hp_lost, -s.team_damage, len(s.actions), s.actions)) if qualified else None


def describe(s):
    return None if s is None else {k: v for k, v in asdict(s).items() if k != "hand"}


def main():
    results = []
    strike = Card("strike", 1, damage=10)
    guard = Card("guard", 1, block=5)
    root = State(6, 20, 2, (strike, guard))
    sequences = prefixes(root)
    real = best_qualified([finish(s, 8) for s in sequences], 10)
    fake = best_qualified([finish(s, 8) for s in prefixes(replace(root, enemy_hp=10))], 10)
    assert real.hp == 3 and "guard" in real.actions
    assert fake.hp == 6 and fake.actions == ("strike",)
    quota_prefix = next(s for s in sequences if s.actions == ("strike",))
    assert quota_prefix.hp == 6 and finish(quota_prefix, 8).hp == 0
    results.append({"case": "quota_is_not_victory", "root": describe(root),
                    "quota": 10, "real_hp_end_phase": describe(real),
                    "scaled_enemy_hp": describe(fake),
                    "stopping_on_quota_prefix": describe(quota_prefix),
                    "same_prefix_after_real_enemy": describe(finish(quota_prefix, 8)),
                    "claim": "A virtual quota needs the real enemy response; scaling HP changes the optimal bundle."})

    bash = Card("team_vulnerable", 1, vulnerable=1)
    own = Card("own_attack", 1, damage=8)
    peer = (10, 10)
    support_root = State(30, 100, 1, (bash, own))
    support_sequences = prefixes(support_root)
    active = [finish(s, 0, peer) for s in support_sequences]
    inactive = [finish(s, 0, ()) for s in support_sequences]
    direct = best_qualified(active, 8)
    team = max(active, key=lambda s: s.team_damage)
    no_peer = max(inactive, key=lambda s: s.team_damage)
    assert direct.actions == ("own_attack",) and direct.team_damage == 28
    assert team.actions == ("team_vulnerable",) and team.team_damage == 30
    assert no_peer.actions == ("own_attack",) and no_peer.team_damage == 8
    results.append({"case": "direct_quota_rejects_team_support", "peer_attacks": peer,
                    "direct_quota_choice": describe(direct), "team_choice": describe(team),
                    "already_ended_peer_choice": describe(no_peer),
                    "claim": "Direct damage quotas can reject a better team result; support value depends on teammate opportunity."})

    # This is a finite outcome table, not simulated combat or a reachable production state.
    points = [(0, 0), (20, 0), (35, 2), (50, 6), (60, 15), (45, 8)]
    frontier = sorted((d, l) for d, l in points
                      if not any(d2 >= d and l2 <= l and (d2 > d or l2 < l) for d2, l2 in points))
    selected = {}
    for quota in (20, 35, 40, 50, 55):
        candidates = [(d, l) for d, l in frontier if d >= quota]
        selected[quota] = min(candidates, key=lambda p: (p[1], -p[0]))
    assert frontier == [(20, 0), (35, 2), (50, 6), (60, 15)]
    assert selected[40] == selected[50] == (50, 6)
    results.append({"case": "one_frontier_multiple_quotas", "all_points_damage_loss": points,
                    "frontier_damage_loss": frontier, "selections": selected,
                    "claim": "For the same frozen outcome set, one Pareto frontier answers many quota queries; no additional simulator calls are needed."})

    # The oracle is deliberately incomplete: its timeout says nothing about infeasibility.
    feasible_damage = (20, 60, 90)
    probes = []
    low, high = 0, 101
    while high - low > 1:
        quota = (low + high) // 2
        witness = next((d for d in feasible_damage if d >= quota), None) if quota != 50 else None
        status = "Feasible" if witness is not None else "Unknown"
        probes.append({"quota": quota, "status": status, "witness": witness})
        if witness is not None:
            low = quota
        else:
            high = quota
    assert low == 49 and max(feasible_damage) == 90
    results.append({"case": "unknown_cannot_lower_binary_upper_bound", "probes": probes,
                    "incorrect_binary_result": low, "known_feasible_damage": max(feasible_damage),
                    "claim": "Exact feasibility is monotone; an incomplete search result is not a valid infeasibility certificate."})

    print(json.dumps({"scope": "Two exact one-turn abstract card enumerations and two finite mathematical examples. Not a C# search, native simulation, teammate distribution, or performance comparison.",
                      "case_count": len(results), "results": results}, indent=2))


if __name__ == "__main__":
    main()
