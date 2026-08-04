# M0b — Unity project creation + QA harness

## Author: (assign on activation)
## Status: Blocked — waiting on Human task 001 (Unity license)

Remaining M0 work once the license is active:

1. `scripts/unity.sh`-equivalent createProject run for `game/` (the editor
   generates ProjectSettings/Packages; commit the project skeleton).
2. Headless install of the `linux-server` module:
   `"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub" -- --headless
   install-modules --version 6000.1.14f1 -m linux-server`
3. `ProjectConfigurator` editor script: 2D-ish physics settings (top-down,
   gravity off in XY plane), tags/layers, quality settings, company/product
   name (kodloki / Potshot).
4. QA harness:
   - Screenshot rig (`docs/agents.md` §"Seeing without eyes")
   - Test assemblies + a trivial EditMode test so `scripts/run-tests.sh`
     goes green end-to-end.
5. Update PROJECT_STATUS.md M0 checklist; move this doc to Active with an
   Author, get an independent review, then implement (review gate applies).
