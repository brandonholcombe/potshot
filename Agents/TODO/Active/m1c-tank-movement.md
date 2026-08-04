# M1c — Tank controller + dev arena (M1 slice a)

## Author: fable-primary
## Status: Complete (2026-08-04) — gate green (EditMode 5, PlayMode 6), Visual 2/2, driving screenshot agent-verified. Friction fix recorded in PrefabFactory

First gameplay slice: a driveable tank with the docs/gameplay.md feel
targets, in a generated dev arena, verified by physics asserts + screenshots.

## Decisions

- **Input**: legacy Input Manager (default axes; no package dependency).
  All input flows through a `ITankInput` abstraction (`Potshot.Core`):
  `Vector2 Move`, `Vector3 AimWorldPos`, `bool Fire` — implemented by
  `PlayerTankInput` (WASD + mouse), later `BotTankInput` (M1 bots),
  `ScriptedTankInput` (tests), and the network layer (M2).
- **Tank**: Rigidbody hull, physics in FixedUpdate. Arcade model:
  velocity-driven (not force-tuned) — accelerate toward `Move * topSpeed`
  with accel/decel rates; hull yaws toward move direction; independent
  turret child rotates toward `AimWorldPos` at fixed deg/s. Tuning values
  in a `TankSpec` ScriptableObject created by editor script
  (top speed 6 u/s, reach ~0.4 s, turret 360°/s — from docs/gameplay.md).
- **Prefab**: `PrefabFactory.CreateTankPrefab()` (editor) — primitives
  (box hull, cylinder turret, barrel), saved material assets, saved to
  `Assets/Resources/Prefabs/Tank.prefab` (Resources so runtime + PlayMode
  tests load it without AssetDatabase/build-settings).
- **Arena**: `SceneBuilder.BuildDevArena()` → `Assets/Scenes/DevArena.unity`
  (40x40 ground, perimeter walls, tank spawn, top-down camera at fixed
  offset following the tank).

## Verification

- EditMode: TankSpec values match gameplay.md feel targets.
- PlayMode (merge gate): spawn tank from Resources with ScriptedTankInput;
  assert (a) top speed within 5% of 6 u/s, (b) 0→95% speed in 0.3–0.5 s,
  (c) full stop below 0.2 u/s within 0.6 s of input release, (d) turret
  reaches a 90° aim change in ≤0.3 s. Wall collision: tank cannot leave
  the arena.
- Visual: RenderProbe of DevArena + play-mode capture after 1 s of scripted
  driving — agent Reads PNGs (tank visible, inside walls, camera framing).

## Risks

- Feel constants asserted with tolerances, not exact — physics timestep
  quantization. Tests document the tolerance rationale.
- Prefab-from-editor-script: use PrefabUtility.SaveAsPrefabAsset on a temp
  scene object, then destroy it — idempotent overwrite.
