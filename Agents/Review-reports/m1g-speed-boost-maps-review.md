# Review: m1g-speed-boost-maps.md

## Reviewer: sonnet-reviewer
## Task: Agents/TODO/Active/m1g-speed-boost-maps.md (author: fable-primary)

Verified against the actual code and by Reading all three PNGs in
`Human-Input-Maps/`. Verdict at the end.

## Findings

1. **BoostState by-ref: approved shape.** `ref` through `TankMotor.Step` is
   fine — Step already mutates the Rigidbody, so a mutated-state signature is
   consistent, and serialization is unaffected either way (the struct itself
   is the data; FishNet reconcile payloads embed its two floats next to
   pos/rot/velocity regardless of how Step touches it). Requirements: the
   struct must be plain/blittable (two floats, no references) in the Runtime
   assembly, and TankController's exposure must NOT be read-only-only — the
   task says "exposes it read-only for the HUD", but the M2 reconcile path
   must also be able to *restore* BoostState before replaying Step. Make it a
   public field (or property with setter); HUD reads it, network writes it.
   State in the doc: M2 reconcile data = pose + velocity + BoostState.

2. **Boost trigger edge semantics: unspecified — decide now.** Pick
   HELD-trigger (boost fires whenever `input.Boost && cooldownLeft <= 0`):
   deterministic, no extra state, and a bare bool in the replicate payload.
   Feel implication to document in gameplay.md: a held Shift re-fires boost
   automatically at every cooldown expiry. If playtests dislike that, edge
   detection must live *inside* BoostState (a `prevHeld` bool replayed with
   it), never in MonoBehaviour fields — note that in the doc as the fallback.

3. **InputFrozen must also zero Boost.** `TankController.FixedUpdate`
   currently zeroes only `Move`/`Fire` when frozen. Without zeroing
   `sample.Boost`, a held Shift during intermission fires boost into the
   freeze, burning the cooldown so the player respawns with it spent. Zero
   Boost in the freeze block; timers keep ticking in Step (correct — they're
   dt-driven, no Time.* leakage, purity preserved).

4. **Speed-number test updates verified.** TankMotorTests pins 6f at lines
   55 and 63 (and comments at 68/84); TankSpecTests pins 6f/0.4 s. At 7/17.5:
   95% reach = 6.65/17.5 = 0.38 s (window 0.3–0.5 still valid); stop =
   (7−0.2)/17.5 ≈ 0.39 s ≤ 0.6 ✓. Wall test: 7/60 ≈ 0.117 u/step, and even
   boosted (12.6 u/s → 0.21 u/step) cannot tunnel a 1 u wall; the 4.6
   threshold (front face 4.5) stays valid. Update the stale "5.7/15" and
   "(6-0.2)/15" comments too. Add a boosted wall assert only if cheap.

5. **The drawings are OUTLINES, not fills — the classification as written is
   wrong for the rivers map.** In `forts bridges and rivers.png` the river is
   two thin blue *bank lines* with white between them; "blue → water" yields
   two 1-cell water strips and a driveable white channel down the middle of
   the river. Bridges are orange outline rects and forts are gray outlines
   (hollow double walls) too. The importer needs a fill step: flood-fill from
   the image border; enclosed white regions bounded by a feature's strokes
   become that feature (water between blue banks and under nothing else;
   gray/orange closed outlines filled solid). The task must state this.

6. **Cliffs map: hollow-ring vs filled-plateau must be an explicit decision,
   and I judge the drawing intends FILLED.** The dashed strokes cross
   *through* the blob interiors (classic dashed = hidden passage); the intent
   reads as solid raised cliffs with tunnels carved along the dash paths.
   The task's approach (discard dashes, strokes-as-walls) gives hollow rooms
   instead — and worse, where the solid outline runs continuously under a
   dash crossing (the left blob's south edge looks continuous), removing
   dashes opens NO gap and the tunnel dead-ends. Required: (a) state the
   interpretation in the doc — fill closed brown outlines solid, then carve
   open corridors along dilated dash-component paths (≥ tank clearance
   ~2.5 u); (b) the EditMode tunnel assert MUST be a pathability check —
   flood-fill open cells and assert the regions on either side of each dash
   corridor are connected — not "some open cells exist", which proves nothing.

7. **~200 px discard threshold: valid ONLY at source resolution — the
   pipeline order makes it ambiguous.** The doc's pipeline (downsample →
   classify) implies component analysis on the 0.6 u grid (48 u / 0.6 =
   80 cells wide; ~8.6 source px per cell for the 685 px PNG). At cell
   resolution a large blob outline is only ~100–150 cells — *under* 200, so
   everything would be discarded; and any-pixel downsampling welds dashes
   (gaps ~10–20 px ≈ 1–2 cells) into the solid outline before counting.
   Required: run component labeling + dash discard at source-pixel
   resolution, before downsampling. At source resolution the numbers check
   out (dashes ≈ 50–120 px, outlines in the thousands), but make the
   threshold data-derived (discard components < ~10% of the largest) and
   assert in the importer test that both classes exist on the real PNGs.

8. **Spawn markers: counts verified.** Cliffs and Clifside each have exactly
   2 dark-red circles; forts has 0 — "fewer than 4 → auto-supplement" covers
   all three, but the doc implies only forts needs autos; both cliff maps
   need 2 each. Auto spawn/pad picking must be deterministic per SceneBuilder
   convention: farthest-point argmax over a fixed row-major cell scan with a
   fixed tie-break, no UnityEngine.Random / no seeds from time. Also verify
   orientation with a test anchor: Texture2D pixel (0,0) is bottom-left after
   LoadImage, so pixel y maps directly to +z — assert the cliffs map's two
   spawn markers land top-right/bottom-left in world (a mirrored importer is
   the classic failure here).

9. **Water collider 0.3: two accepted risks to document.** (a) Mortar arcs
   descend below 0.3 only near impact, so cross-river lobs mostly work, but
   shells targeted at/near water detonate on the water surface — acceptable,
   arguably good; document it. (b) A 0.3-high curb hit at 7 (or 12.6
   boosted) u/s is a ride-up risk for a box-collider rigidbody; the rotation
   constraints help but add a PlayMode or probe check that a tank driven at
   the river is blocked, or raise the collider to ~0.35 (top must stay under
   shell height 0.45).

10. **F-keys and scenes: fine with two notes.** F1..Fn avoids the existing
    Alpha1–4 weapon binds in PlaytestHotkeys. LoadScene(Single) rebuilds
    everything, and each generated map carries its own FfaGameMode, camera,
    and player (DevHud/PlaytestHotkeys ride the Tank prefab) — self-
    sufficient. But `Builder.BuildMacDev` currently hardcodes only DevArena;
    the doc's "Builder includes all map scenes" needs the scene list order
    pinned (DevArena = index 0, maps alphabetical) so F-key mapping is
    stable. BotBrain's static `_stagger` survives loads — harmless.

11. **Run-length box merging: correct.** Greedy per-row runs exactly cover
    wall cells; on an 80×~100 grid that is at most a few hundred boxes —
    within budget. Optional: extend runs vertically into rectangles.

## Verdict

**Approve with changes.** Required before implementation: fill semantics for
outlined features (5), explicit cliffs interpretation + pathability test (6),
source-resolution component discard (7), deterministic auto-placement +
orientation assert (8), InputFrozen zeroes Boost (3), writable BoostState
exposure + held-trigger decision documented (1, 2). Items 4, 9–11 are
verified-fine or documentation-level.
