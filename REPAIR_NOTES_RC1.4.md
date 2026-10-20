# RC1.4 — Critical HTML Runtime Repair

## Applied
- Fixed the two falsy-zero bugs reported in the HTML raycaster: a valid floor tile (`0`) is no longer interpreted as a wall.
- Replaced unbounded enemy spawn rejection sampling with deterministic candidate collection, shuffle, and bounded placement so mission generation cannot hang on a crowded map.
- Added separate mobile portrait and landscape HUD layouts; the movement pad remains visible in landscape and action buttons use a three-column compact layout.
- Synced the corrected standalone HTML to the Android WebView asset.

## Checks
- JavaScript syntax check is required and included in the package report.
- This does not make the HTML prototype equivalent to the Unity 3D campaign. It still uses a 2D grid raycaster, procedural layouts, rectangle enemy sprites, and does not load GLB/WAV or mission JSON.
- Unity compile/play mode and real mobile browser tests have not been run in this environment.
