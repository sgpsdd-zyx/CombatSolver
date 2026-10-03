"""A finite planning counterexample for resetting a contribution deadline."""

import itertools
import json


def plan(slots, remaining_damage):
    candidates = []
    for actions in itertools.product(("defend", "attack"), repeat=slots):
        damage = 10 * actions.count("attack")
        loss = 3 * actions.count("attack")
        if damage >= remaining_damage:
            # Equal total cost prefers the first round with less local damage.
            tie = tuple(0 if action == "defend" else 1 for action in actions)
            candidates.append((loss, tie, actions))
    return min(candidates)[2] if candidates else None


def main():
    rolling = []
    for step in range(3):
        chosen = plan(3, 20)
        rolling.append({"step": step + 1, "projected": chosen, "executed": chosen[0]})
    assert all(x["executed"] == "defend" for x in rolling)
    fixed = []
    remaining = 20
    for step in range(3):
        chosen = plan(3 - step, remaining)
        fixed.append({"step": step + 1, "projected": chosen, "executed": chosen[0]})
        if chosen[0] == "attack":
            remaining -= 10
    assert remaining == 0
    assert [x["executed"] for x in fixed] == ["defend", "attack", "attack"]
    print(json.dumps({
        "scope": "Exact finite scheduling example, not card or native game simulation. Attack causes 10 effective damage and 3 local HP loss; defend causes neither. Target is 20 within three rounds. No empirical win-rate claim.",
        "rolling_deadline": rolling, "rolling_damage": 0,
        "fixed_deadline": fixed, "fixed_damage": 20,
        "claim": "Each rolling plan meets its own projected quota, but repeated execution can postpone all progress. This is a counterexample, not a claim that every MPC policy procrastinates.",
    }, indent=2))


if __name__ == "__main__":
    main()
