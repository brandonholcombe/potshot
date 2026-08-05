# M2c — Networked combat: weapons, damage, respawn, bots, scores

## Author: fable-primary
## Status: Complete (2026-08-04) — combat E2E green in-process (fire, damage, score, respawn); deployed to tow-c1; live client verified

Shooting becomes server-authoritative on the live kodloki server.
Offline mode stays intact (network components guard themselves).

## Design

1. **Server-authoritative firing**: the Fire/AimWorldPos already flow in
   TankMoveData. NetworkTank's replicate passes the sample to a new
   `NetworkWeapon` component — but ONLY the server actually fires
   (`IsServerStarted` guard inside the replicate; clients skip — no
   predicted projectiles in this slice, shells appear at server
   authority ~RTT/2 late, acceptable at friends-latency; local visual
   tracer is an M5 feel task if wanted).
   NetworkWeapon wraps the existing WeaponController state machine
   (cooldown/ammo/equip logic reused — not duplicated).
2. **Networked projectiles**: `ProjectileNet.prefab` = Projectile +
   NetworkObject + FishNet NetworkTransform (server-owned, no client
   ownership). Server Instantiate + ServerManager.Spawn on fire;
   Projectile damage path runs server-side only when networked
   (IsServer guard — reviewer to verify the cleanest check for a
   non-NetworkBehaviour; likely a small NetworkProjectile shim).
   Despawn via ServerManager.Despawn. Ricochet/AoE/self-damage rules
   unchanged (same Projectile code).
3. **Health/death/respawn**: `NetworkHealth` on TankNet — Health as a
   SyncVar (HUD reads it); server applies Damageable damage, on death
   despawns... NO — reuse FfaGameMode-style respawn: server deactivates
   + reactivates via NetworkObject (reviewer: verify SetActive sync vs
   despawn/respawn preference in FishNet for temporary death; pick the
   idiomatic one). Kill credit (victim, killer) captured server-side.
4. **Server bots**: PlayerSpawner (rename NetGameMode) also spawns
   N bot TankNets (BotBrain AddComponent server-side; IsController
   makes the server author their replicate — verified m2b). Bot count:
   fill to 4 total, despawn extras as humans join (simple version:
   fixed 3 bots this slice).
5. **Scores + round state**: `NetFfaState` (on the hub or a spawned
   singleton NetworkObject): SyncDictionary<int, int> conn→kills (or
   SyncList of structs), round timer SyncVar. Server logic reuses
   FfaGameMode rules (kill +1, self -1, round end/reset); DevHud +
   FfaGameMode OnGUI read synced values client-side. (Full parity —
   intermission freeze etc. — server-side; clients render state only.)
6. **Pickups**: server-only PickupPad logic (pads exist in scenes;
   in networked sessions pads run on the server, pickup prefab becomes
   ProjectilePickupNet? — simplest: pads only act server-side and
   consumption applies to NetworkWeapon; the pickup visual becomes a
   NetworkObject spawn). If this bloats the slice, defer pickups to a
   follow-up task — reviewer judgment call.
7. **Tests** (in-process host, batchmode): fire → networked projectile
   exists + damages a second spawned tank server-side; death → respawn
   restores health/default weapon via network path; bot tank spawns and
   fires server-side; score increments on kill. Offline suite stays
   green (regression).
8. Deploy: rebuild server image, push :dev + sha, rollout restart on
   tow-c1, re-run the headless E2E (authenticated + spawned), THEN a
   fire-across-the-wire E2E if scriptable (client fires via scripted
   input — headless client can set its own InputSource? PlayerTankInput
   claims first; scripted override possible via a -potshotAutofire dev
   arg — nice-to-have, reviewer judgment).

## Risks

- Biggest API risk: SyncVar/SyncDictionary syntax in FishNet 4.7.2
  (SyncVar<T> readonly field + .Value pattern?) and NetworkTransform
  component defaults — reviewer MUST verify against PackageCache source.
- Projectile spawn rate (MG 10/s × players) × NetworkTransform sync —
  fine at 10 players/30 Hz, but confirm NetworkTransform send settings.
- Death-by-deactivation vs despawn semantics with prediction on the
  dead object (replicate on a deactivated NetworkObject?).
