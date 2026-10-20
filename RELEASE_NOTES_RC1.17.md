# RC1.17 — Authored Animation & Texture Pipeline

This release addresses the two specific art gaps called out in RC1.16.

- Adds 27 real texture PNGs: eight detailed seamless PBR base-color/normal/mask sets plus concrete-panel and camouflage base-color atlases.
- Adds a Unity Editor asset pipeline that creates five original keyframed generic-rig AnimationClip assets on project open and before Unity builds: idle, walk, aim/fire, reload, and takedown.
- Adds runtime animation selection for spawned enemies and normalizes the GLB character root node to a stable `RigRoot` path across all 24 character models.
- Updates runtime materials to load the included texture sets and assign supported base-color, normal, metallic/AO/smoothness properties.
- Adds Editor validation menu for clips and PBR texture sets.

## Validation

Passed in this environment: PNG decoding/dimensions, texture seam check, GLB rig-root/path audit for 24 unique characters, static C# delimiter check, Resources path/name consistency, and ZIP integrity.

Not verified here: Unity Editor compilation, import warnings, actual AnimationClip generation/serialization, shader-specific mask interpretation, in-game animation pose quality, lighting response, or Android performance. Open Unity and run `Real Loser → Art Pipeline → Generate Authored Character Clips and Configure Textures`, then `Validate Authored Clips and Texture Sets` before runtime testing.
