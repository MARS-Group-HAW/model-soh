# Carleton MARS evacuation route optimization

Closed-loop search for shorter campus clearance (`evac_end_s`) by choosing
**lot→exit destinations** and **spawn start times**, then feeding each MARS
result into the next proposal.

Existing scenarios **01–12** are unchanged. Feedback candidates live only under:

- `resources/schedules/fb_candidates/`
- `configs/fb_candidates/`
- `results/fb_candidates/` (sims + `leaderboard.csv` + `history.jsonl`)

The older fraction-proxy tool `scripts/optimize_exit_assignment.py` still
exists for open proxy ranking of P5/P6/P7 SW splits; prefer the feedback loop
below for real clearance search.

---

## Feedback loop (recommended)

`scripts/optimize_evac_feedback.py` runs a true **propose → sim → analyze →
mutate** loop. No Optuna/scipy required (stdlib hill-climb + random restarts).
Mutations are biased by the latest run’s `remaining_by_lot` / `clearance_by_lot_s`
(stuck or last-clearing lots get redirected or delayed).

### Search space (only these knobs)

1. **Destinations** — each of P1–P7 → Hogs Back Plaza (SW) or Brewer (NE).  
   Defaults: P1/P2 SW, P3/P4 NE; all lots may flip.
2. **Start times** — one-shot `HH:MM,HH:MM,-1` from  
   `{06:01, 06:30, 07:00, 07:30, 08:00}`. Never `interval=1`; never exact `06:00`.

### Objective

| Outcome | Score |
|---------|--------|
| Full clear (`remaining==0`) | `evac_end_s` (minimize) |
| Incomplete | `horizon + remaining*1000` (worse than any clear) |

Only full clear counts as success. `endPoint = startPoint + horizon`
(default horizon **14400** s → `2021-10-11T10:00:00` from `06:00`).

### Run

From `CarletonDrivingBox/` (activate `.venv` if you use it):

```bash
# Overnight closed loop (hours per iteration)
python scripts/optimize_evac_feedback.py --iterations 20 --horizon 14400

# Start from scenario-10-style seed (P6→SW)
python scripts/optimize_evac_feedback.py --iterations 10 --seed s10

# Continue from fb leaderboard best
python scripts/optimize_evac_feedback.py --iterations 10 --resume --seed best

# Wiring test only (fake metrics, no MARS)
python scripts/optimize_evac_feedback.py --dry-run --iterations 8 --rng-seed 1
```

Each iteration:

1. Writes `fb_<dest7>_<time7>` schedule + config under `*/fb_candidates/`
2. Runs `dotnet` MARS sim + `analyze_run.py`
3. Updates `results/fb_candidates/leaderboard.csv`
4. Prints `best_so_far`, current score, and what changed
5. Proposes the next candidate from stuck/late-lot feedback

Candidate name example: `fb_MMBBBNB_0000000`  
(`M`/`B` = Hogs Back Plaza/Brewer for P1…P7; time digits `0`…`4` index the start-time grid).

### Seeds

| `--seed` | Meaning |
|----------|---------|
| `s01` | All early; P1/P2 SW, P3–P7 NE (default) |
| `s10` / `s12` | Binary stand-in: P6→SW, others default, all early (no P7 50/50 split) |
| `best` | Resume from current fb leaderboard best |

Promote a winner to a future scenario **13+** only after it **beats scenario 12
clearance** in a real (non-`--dry-run`) sim — never overwrite 01–12.

### Re-analyze / materialize

```bash
python scripts/optimize_evac_feedback.py --analyze-only fb_MMBBBNB_0000000
python scripts/optimize_evac_feedback.py --write-only s10 --horizon 14400
```

---

## Lessons from hand-tuned scenarios

| Plan | Assignment idea | Effect |
|------|-----------------|--------|
| **01** | P1/P2 → SW; P3–P7 → NE | NE corridor overloaded (esp. P6/P7) |
| **10** | P6 → SW | Moves 900 cars off NE onto SW |
| **12** | P6 → SW + P7 50/50 | Best early one-shot clearance |
| Heavy SW | Large SW share (e.g. P5 + half P7 + delays) | Overloads Hogs Back Plaza / SW; gridlock |

Exit coordinates (off-campus sinks; clearance = campus gate leave, not dest arrival):

- **Hogs Back Plaza / SW:** `45.367764, -75.702286` (888 Meadowlands Dr E)
- **Brewer / NE:** `45.387983, -75.690183`

---

## Legacy proxy optimizer (optional)

`scripts/optimize_exit_assignment.py` ranks **P5/P6/P7 SW fractions** with a
fast demand-balance **filter** (not a clearance guarantee). Opt-candidate
configs still use a short **10000 s** screening horizon. Use it only to
shortlist fraction splits; prove clearance with real sims / the feedback loop.

```bash
python scripts/optimize_exit_assignment.py --optimize --write-top 10
python scripts/optimize_exit_assignment.py --eval-sim opt_p6sw1_p7sw05_p5sw0
```

Artifacts: `*/opt_candidates/` (separate from `fb_candidates/`).

---

## Dependencies

Feedback loop needs **stdlib + existing project scripts** only. Optuna /
scipy / sklearn are **not** installed and **not** required. If you later want
TPE, install Optuna into `.venv` only (`pip install optuna`) and wire it as an
alternate proposer — keep the same objective and leaderboard schema.

---

## What `SOHBigEventBox` (Barclays Arena) does

Arena box name: **`SOHBigEventBox`** (agents/logic under `SOHModel/BigEvent/`).

It models post-event egress with CSV-driven staged multimodal release and
explicit source→destination rows. There is **no** automated lot→exit
optimizer. Carleton scenarios 10–12 already hand-tune destinations; this
branch formalizes that search with a real feedback loop over MARS clearance.
