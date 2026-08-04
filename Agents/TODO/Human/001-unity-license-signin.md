# Human task 001 — Activate Unity license

## Status: Open
## Estimated: 5–10 minutes

Unity 6000.1.14f1 is installed but has **no activated license**, so batchmode
(and therefore all agent work in `game/`) fails with:

> No valid Unity Editor license found. Please activate your license.

Agents installed Unity Hub headlessly at `/Applications/Unity Hub.app` —
the sign-in itself is browser-SSO and cannot be done by an agent.

## Steps

1. Open **Unity Hub** (first launch may prompt to move/verify the app).
2. Sign in with your Unity account (top-right avatar → Sign in).
3. When prompted (or under Settings → Licenses), add a **Personal license**.
4. That's it — no need to open any project.

## On completion

Append to `HUMAN_INPUT.log`:

```
2026-08-04 | Brandon | <minutes> | Unity Hub sign-in + Personal license activation
```

Then tell the agent "license done" — it will retry project creation and the
linux-server module install headlessly.

## Why agents can't do this

Unity account sign-in requires interactive browser SSO (with 2FA); the old
`-username/-password` CLI activation is deprecated. Manual `.alf`/`.ulf`
activation also requires a logged-in browser session.
