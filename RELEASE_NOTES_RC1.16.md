# RC1.16 — Consolidated Engineering

- Fixed multi-primitive GLB import and per-submesh material assignment.
- Added skinned mesh/bone-weight/bind-pose import for rigged GLBs.
- Added generated planar UV fallback and cached procedural surface textures for models with no authored UVs/textures.
- Fixed root collider bounds to include skinned renderers and use local-space bounds.
- Cached runtime GLB templates to avoid repeated full asset parsing.
- Added Android-safe Resources payloads for 80 GLBs, 34 WAVs, and 10 mission JSON files; removed blocking synchronous URL polling.
- Integrated M03 C4/fuel-tank alternative, M04 escort health and enemy-fire damage, M05 blast visuals, M06 technician disarm/replant loop, and M08 simplified drivable cargo truck.
- Static checks pass; Unity compilation and Android runtime remain unverified.
