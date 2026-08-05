# Net — First-life speed burst after scene loads

## Author: fable-primary
## Status: Not Started

Brandon (2026-08-04): "tank speed is out of control on the first life or
something." Hypothesis: scene-load tick-debt catch-up — the client now
connects from the MENU and immediately receives the Lobby global scene
load (and another swap at match start); during the load the tick clock
accrues debt, then the client burns catch-up ticks in a few frames and
the predicted tank replays them at render speed (a 1-2 s turbo burst).
Later lives don't load scenes → normal. Server truth never exceeds spec.

## Investigation + fix (reviewer: verify in 4.7.2 source)

1. Confirm mechanics: TimeManager's frame-tick accumulation — is there a
   cap (maximum frame ticks / delta clamp)? What happens to _elapsedTime
   across a multi-second main-thread scene load; do queued ticks run in
   a burst on resume? Cite the accumulation loop.
2. Identify the sanctioned mitigation:
   - a TimeManager setting (max ticks per frame / maximum delta)?
   - client-side: does FishNet reset timing on scene load END for
     late-join scene sync (SceneManager interplay)?
   - or must we clamp manually (e.g., on OnLoadEnd client-side, discard
     accrued time — is there an API like TimeManager reset, or a
     recommended pattern in demos/docs comments)?
   Choose the approach that does NOT cause tick desync with the server
   (the server keeps ticking during client loads — the client MUST
   catch up somehow or stay behind; the right fix likely caps the
   VISIBLE burst, e.g. limited ticks/frame so catch-up spreads over a
   second, or FishNet's own timing adjustment absorbs it — verify how
   clients resync tick after long stalls).
3. Repro test: in-process host (real flow), measure max GRAPHICAL
   displacement per frame for the owned tank during the 3 s after (a)
   warmup spawn and (b) match-start swap; assert bounded by
   ~topSpeed*boostMult*frameDelta*safety. Must fail before the fix,
   pass after.
4. Also verify the smoother's teleport threshold (5 u) interaction: a
   catch-up burst moving >5 u in a tick window teleports the graphical —
   maybe the burst LOOKS worse because smoothing disengages; the fix
   choice may interact.
5. Deploy client (+server if the fix touches shared timing settings).

## Risks

- Wrong fix = permanent tick lag or rubber-banding. The reviewer's
  source citations on FishNet's own catch-up/resync design are the
  deliverable; do not invent a timing hack it already solves.
