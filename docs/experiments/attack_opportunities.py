"""Static geometry experiment, NOT Unity gameplay or a human enjoyment test.

13x13 walkable cells, an enclosing wall, eight directions, first enemy stops
each ray. Candidate reflection uses the wall-cell centre as its discrete
impact point, flips the normal component, and allows at most one bounce.
No homing, penetration, turn simulation, trait triggers, or damage balancing.
Run with Python standard library; runtime Assets are never changed.
"""
import argparse
import hashlib
import json
import random
from pathlib import Path

SIZE = 13
SEED = 20261001
DIRECTIONS = ((1, 0), (1, 1), (0, 1), (-1, 1),
              (-1, 0), (-1, -1), (0, -1), (1, -1))
CELLS = tuple((x, y) for x in range(1, SIZE + 1)
              for y in range(1, SIZE + 1))
VARIANTS = ("base", "damage_plus_one", "reflect_once", "fork_two", "fan_three")


def inside(cell):
    return 1 <= cell[0] <= SIZE and 1 <= cell[1] <= SIZE


def route(origin, direction, reflect=False):
    # Include wall cells so a mounted Rook is hit BEFORE wall reflection.
    x, y = origin
    dx, dy = DIRECTIONS[direction]
    bounced = False
    cells = []
    for _ in range(4 * (SIZE + 2)):
        x, y = x + dx, y + dy
        cell = (x, y)
        cells.append(cell)
        if inside(cell):
            continue
        if not reflect or bounced:
            return tuple(cells)
        if x < 1 or x > SIZE:
            dx = -dx
        if y < 1 or y > SIZE:
            dy = -dy
        bounced = True
    raise AssertionError("Unbounded ray")


ROUTES = {(p, d, r): route(p, d, r) for p in CELLS
          for d in range(8) for r in (False, True)}


def cast(origin, direction, enemies, reflect=False):
    floor = set()
    bounced = False
    for cell in ROUTES[origin, direction, reflect]:
        if cell in enemies:
            return {cell}, floor, bounced
        if inside(cell):
            # Current TurnProjectile checks the hit before RemoveFire callback.
            floor.add(cell)
        elif reflect and not bounced:
            bounced = True
    return set(), floor, bounced


def actions(origin, enemies, fire, variant):
    casts = [cast(origin, d, enemies, variant == "reflect_once") for d in range(8)]
    results = []
    for d in range(8):
        indices = ((d - 1) % 8, (d + 1) % 8) if variant == "fork_two" else (
            ((d - 1) % 8, d, (d + 1) % 8) if variant == "fan_three" else (d,))
        hits, floor = set(), set()
        for index in indices:
            hits.update(casts[index][0])
            floor.update(casts[index][1])
        results.append({"hits": hits, "fire": floor & fire,
                        "damage": len(hits) * (2 if variant == "damage_plus_one" else 1),
                        "bounced": any(casts[i][2] for i in indices)})
    return results


def best(results):
    # Fixed common proxy policy: damage, then fire cleared. Not a fun score.
    return max(results, key=lambda a: (a["damage"], len(a["fire"])))


def metrics(origin, enemies, fire, variant):
    options = actions(origin, enemies, fire, variant)
    chosen = best(options)
    reachable = set().union(*(a["hits"] for a in options))
    baseline_reachable = set().union(*(a["hits"] for a in actions(origin, enemies, fire, "base")))
    here = max(len(a["hits"]) for a in options)
    nearby = [p for dx, dy in DIRECTIONS
              if inside(p := (origin[0] + dx, origin[1] + dy)) and p not in enemies]
    # Static repositioning proxy only: enemies would move after a real move.
    setup_gain = any(max(len(a["hits"]) for a in actions(p, enemies, fire, variant)) > here
                     for p in nearby)
    return {
        "any_attack_target": bool(reachable),
        "best_unique_targets": len(chosen["hits"]),
        "best_damage_units": chosen["damage"],
        "multi_target_snapshot": len(chosen["hits"]) >= 2,
        "new_target_snapshot": bool(reachable - baseline_reachable),
        "new_reachable_targets": len(reachable - baseline_reachable),
        "distinct_nonempty_target_sets": len({frozenset(a["hits"]) for a in options if a["hits"]}),
        "static_neighbor_improves_target_count": setup_gain,
        "cleared_fire_best_damage_shot": len(chosen["fire"]),
        "best_damage_hit_uses_bounce": chosen["bounced"] and bool(chosen["hits"]),
    }


def make_layout(rng, count, kind):
    origin = rng.choice(CELLS)
    pool = [p for p in CELLS if p != origin]
    if kind == "uniform":
        enemies = set(rng.sample(pool, count))
    else:
        # Synthetic compact formations: clustered, NOT actual spawner layouts.
        centre = rng.choice(pool)
        rng.shuffle(pool)
        pool.sort(key=lambda p: max(abs(p[0] - centre[0]), abs(p[1] - centre[1])))
        enemies = set(pool[:count])
    # Synthetic bishop-like diagonal trails; not the actual bishop AI.
    fire = set()
    for _ in range(5):
        p = rng.choice(CELLS)
        dx, dy = rng.choice((DIRECTIONS[1], DIRECTIONS[3], DIRECTIONS[5], DIRECTIONS[7]))
        for step in range(4):
            q = (p[0] + step * dx, p[1] + step * dy)
            if inside(q):
                fire.add(q)
    return origin, enemies, fire


def invariant_tests():
    checks = []

    def check(value, label):
        assert value, label
        checks.append(label)

    check(route((3, 5), 0, True)[:12] == tuple((x, 5) for x in range(4, 15)) + ((13, 5),),
          "normal incidence retraces the incoming row")
    hits, _, bounced = cast((3, 5), 3, {(5, 13)}, True)
    check(hits == {(5, 13)} and bounced, "oblique reflection reaches an off-axis enemy")
    check(not cast((3, 5), 3, {(5, 13)})[0], "same target absent from the straight incoming ray")
    check(cast((3, 5), 3, {(2, 6), (5, 13)}, True)[0] == {(2, 6)},
          "first enemy blocks the bounce opportunity")
    check(cast((6, 7), 2, {(6, 14)}, True)[0] == {(6, 14)}
          and not cast((6, 7), 2, {(6, 14)}, True)[2], "mounted Rook hit before reflecting")
    check(cast((3, 5), 3, {(6, 14)}, True)[0] == {(6, 14)}
          and cast((3, 5), 3, {(6, 14)}, True)[2], "bank shot can reach a mounted Rook on a later wall")
    for p in CELLS:
        for d in range(8):
            trace = ROUTES[p, d, True]
            check(len(trace) <= 4 * (SIZE + 2), f"bounded ray {p}/{d}")
            check(all(0 <= x <= 14 and 0 <= y <= 14 for x, y in trace), f"inside enclosing walls {p}/{d}")
        direct = set().union(*(set(ROUTES[p, d, False]) & set(CELLS) for d in range(8))) - {p}
        bounce = set().union(*(set(ROUTES[p, d, True]) & set(CELLS) for d in range(8))) - {p}
        check(direct <= bounce, f"reflection retains base reach {p}")
        if p == (7, 7):
            check(direct == bounce, "exact centre has no new reachable floor cells")
        # Fan directions are rotated base rays; their union cannot add new cells.
        for variant in ("fork_two", "fan_three"):
            all_enemies = set(CELLS) - {p}
            reachable = set().union(*(a["hits"] for a in actions(p, all_enemies, set(), variant)))
            baseline = set().union(*(a["hits"] for a in actions(p, all_enemies, set(), "base")))
            check(reachable == baseline, f"fan union equals base rays {p}/{variant}")
    # Explicit shape semantics and hit/fire stopping behaviour.
    enemy = {(7, 9)}
    check(actions((7, 7), enemy, set(), "fork_two")[2]["hits"] == set(),
          "two symmetric branches replace the centre ray")
    check(actions((7, 7), enemy, set(), "fan_three")[2]["hits"] == enemy,
          "three branches retain the centre ray")
    check(cast((7, 7), 2, {(7, 9)})[1] == {(7, 8)},
          "fire callback floor excludes the first occupied target cell")
    return checks


def run(samples):
    checks = invariant_tests()
    groups = []
    for kind in ("uniform", "clustered"):
        for count in (3, 8, 12, 18):
            rng = random.Random(SEED + count * 100 + (10000 if kind == "clustered" else 0))
            totals = {v: {} for v in VARIANTS}
            for _ in range(samples):
                origin, enemies, fire = make_layout(rng, count, kind)
                for variant in VARIANTS:
                    for key, value in metrics(origin, enemies, fire, variant).items():
                        totals[variant][key] = totals[variant].get(key, 0) + value
            groups.append({"layout": kind, "enemy_count": count, "snapshots": samples,
                           "means": {v: {k: round(n / samples, 6) for k, n in vals.items()}
                                     for v, vals in totals.items()}})
    examples = []
    for name, p, enemies in (
        ("off_axis_bank_shot", (3, 5), {(5, 13)}),
        ("centre_corner_retrace", (7, 7), {(10, 9)}),
        ("two_flanks", (7, 7), {(5, 9), (9, 9)}),
        ("three_targets", (7, 7), {(5, 9), (7, 9), (9, 9)}),
    ):
        examples.append({"name": name, "player": p, "enemies": sorted(enemies),
                         "variants": {v: metrics(p, enemies, set(), v) for v in VARIANTS}})
    return {"kind": "static geometry proxy; NOT human fun, Unity integration, or win rate",
            "board_interior": SIZE, "seed": SEED, "snapshots_total": 8 * samples,
            "samples_per_group": samples, "checks_passed": len(checks),
            "script_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
            "candidate_shapes": {"reflect_once": "one wall-cell-centre specular bounce, no self damage",
                                 "fork_two": "replace the centre with +/-45-degree rays",
                                 "fan_three": "centre plus +/-45-degree rays"},
            "groups": groups, "examples": examples}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--samples", type=int, default=600)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.samples <= 0:
        parser.error("--samples must be positive")
    result = run(args.samples)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({k: result[k] for k in ("kind", "snapshots_total", "checks_passed")}, ensure_ascii=False))
