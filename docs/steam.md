# Steam (M6)

Steam is the release target but nothing before M6 depends on it. Three
Steam-ready rules apply from day one:

1. Original branding only — **Potshot**, never any "Shell*" name (kChamp
   Games' marks: ShellShock Live, ShellShot Arena).
2. Transport-agnostic netcode (FishySteamworks swaps in beside Tugboat).
3. Steamworks SDK lives in an optional assembly (`Potshot.Steam.asmdef`),
   compiled out of non-Steam builds.

## Process facts (verified 2026-08)

- Steam Direct fee: **$100 per app**, recouped only at $1,000 adjusted gross.
- Identity + tax verification, then a **mandatory 30-day wait** after payment
  before release. Store page review ~3–7 days; build review a few days.
  Realistic first-signup → launch: **4–6 weeks**.
- Store page needs capsule art (multiple sizes), screenshots, trailer,
  description. Budget commissioned capsule art from the asset budget if
  needed (see `docs/assets.md`).

## M6 gate checklist

- [ ] Name vetting: deep Steam search, USPTO/EUIPO trademark search,
      domains/socials check for "Potshot"
- [ ] Steamworks partner account, identity + tax forms, $100 fee (HUMAN)
- [ ] App ID, depots, SteamPipe upload scripts
- [ ] FishySteamworks transport + Steam auth on the dedicated server
- [ ] Steam friends invite / join-on-friend flow
- [ ] Store assets + page, "Coming Soon" live ≥2 weeks before launch
- [ ] Beta branch for friends (replaces direct downloads from M5)

## What stays human

Partner account creation, identity/tax verification, payment, and final
store-page submission approvals are legally Brandon's. Everything else
(SteamPipe scripts, store copy drafts, asset uploads via steamcmd) is agent
work with supervision.
