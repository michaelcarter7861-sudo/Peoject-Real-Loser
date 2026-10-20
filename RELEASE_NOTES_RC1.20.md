# Project Real Loser RC1.20 — Consolidated Gameplay AI Pass

Base: RC1.19 Reference Integration. Unity project target remains 2022.3.40f1.

## Changed
- Enemy visual perception now checks visibility-adjusted distance, FOV cone, and actual physics line of sight.
- Solid obstacles block detection; own actor colliders are skipped.
- Combat memory tracks the last known player location for a configurable period (default 5.5 seconds), then transitions to investigate/search behavior.
- Reacquiring sight no longer resets the combat/flank state timer every frame.
- Static release validator includes three checks for these AI invariants.
- Added `Documentation/External_References/RC1.20_CONSOLIDATED_GAMEPLAY_WORK.md` describing changes, repository/license boundaries, next integrations and remaining release gates.

## Validation performed
- `python3 validate_consolidated_release.py`: PASS, 34 static checks.
- `python3 validate_consolidated_mission_mechanics.py`: PASS, 17 contracts.
- `python3 validate_mission_objective_runtime.py`: PASS, mission objective static integration checks.
- All 32 C# files passed token-aware delimiter sanity checks.
- 80/80 GLB containers parsed structurally; 34 audio assets were previously structurally validated.

## Known limits
- No Unity Editor/compiler, Play Mode, Android device or APK build was available in this environment. Unity compilation/runtime/device tests are NOT claimed.
- The 80 GLBs report zero authored UV primitives and zero embedded textures; runtime loader's planar UV/material fallback is still relied on. This is acceptable for structural import tests but is a visible-art-quality gap that needs a deliberate UV/material baking pass.
- Full commercial redistribution review remains required for any third-party code, model weights, datasets or generated content before importing them.
