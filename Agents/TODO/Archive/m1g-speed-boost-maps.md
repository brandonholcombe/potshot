# M1g — Speed bump, boost mechanic, PNG-drawn maps

## Author: fable-primary
## Status: Complete (2026-08-04) — gate 40/40; 3 maps imported (connectivity 100%/98%/92%), overheads agent-verified vs drawings; boost + 7 u/s live

Three playtest-driven requests (Brandon, 2026-08-04): faster tanks, a
boost key with cooldown, and real maps generated from the hand-drawn PNGs
in `Human-Input-Maps/`.

## 1. Speed bump

TankSpec topSpeed 6 → 7, accel 15 → 17.5 (preserves the 0.4 s reach that
playtested well). Update gameplay.md feel targets and the tests that pin
6/15 (EditMode spec asserts + PlayMode speed asserts).

## 2. Boost

- `TankInputSample.Boost` (bool — part of the M2 replicate payload).
- TankSpec: boostMultiplier 1.8, boostDuration 1.0 s, boostCooldown 4 s
  (cooldown starts when boost fires).
- **BoostState struct** (activeLeft, cooldownLeft) passed `ref` into
  `TankMotor.Step` — boost timers are predicted state and must live in
  the pure tick path, NOT in MonoBehaviour fields (M2 reconciliation
  replays Step). During boost both speed cap and accel scale by the
  multiplier.
- TankController owns the BoostState field, passes it by ref, exposes it
  read-only for the HUD (DevHud shows BOOST READY / cooldown seconds).
- PlayerTankInput: Left Shift or Space. BotBrain: boost when far from
  target (dist > preferredRangeMax + 6) and cooldown ready.
- Tests: boosted speed ≈ topSpeed×1.8 during the window; decays back to
  topSpeed after; second boost blocked until cooldown elapses.

## 3. PNG maps (MapImporter, editor)

`Potshot.EditorTools.MapImporter.BuildAllMaps()`: every PNG in
`Human-Input-Maps/` → `Assets/Scenes/Maps/<slug>.unity`, fully populated
(ground, walls, water, bridges, spawns, pads, bots, camera, FfaGameMode).

Pixel semantics (from reading Brandon's three PNGs):
- near-white → open ground
- brown/tan strokes → cliff walls (height 2.5). Connected components
  under ~200 px are DISCARDED — this erases the dashed strokes, so
  dashed tunnel regions become passable gaps (that is the tunnel).
- gray strokes (low saturation, mid value) → fort/cover walls (height 1.5)
- blue → water: visual quad + low collider (height 0.3) — blocks tanks,
  shells at y≈0.45 fly over. No pathing into water.
- orange/solid rects over water → bridges (water suppressed there)
- dark red circles → spawn markers (component centroids). Fewer than 4
  markers → supplement with auto-picked open cells far from other spawns
  (forts map has zero markers → all auto).

Pipeline: Texture2D.LoadImage (no import settings needed) → downsample to
0.6 u grid at fixed world width 48 u → classify cells → greedy run-length
merge wall cells into box colliders (perf: dozens of boxes, not thousands
of cubes) → place 4 pads on open cells far from spawns → instantiate
player + 3 bots + FfaGameMode (spawn list from markers/auto) + follow
camera + sun. Deterministic for a given PNG.

Map access in the dev build: Builder includes DevArena + all map scenes;
PlaytestHotkeys F1..Fn loads scenes by build index. Source PNGs get a
`docs/assets.md` ledger row (author: Brandon, original work).

## Verification

- EditMode: new feel numbers; per-map spot checks via an importer test
  that classifies the real PNGs and asserts: ≥1 wall region, ≥4 spawns,
  water present only in the rivers map, tunnels (open path) exist in the
  cliffs map where dashes were.
- PlayMode: boost tests as above (batchmode gate).
- Visual: RenderProbe of each generated map — agent compares against the
  source PNGs for silhouette match; captures committed for Brandon.

## Risks

- Hand-drawn stroke thickness varies — classification thresholds tuned
  against the actual PNGs, asserted by the importer tests.
- Tunnel gaps must fit a tank (~2.5 u clearance); if a drawn tunnel
  rasterizes too narrow, widen pass: dilate open cells inside discarded-
  dash bounding boxes by 1 cell.
- Scene count in build settings grows — Builder scene list stays explicit.
