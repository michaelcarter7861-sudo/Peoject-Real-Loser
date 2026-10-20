# Project Real Loser RC1.9 — Character Pose Fallback

## Change
- Added `EnemyProceduralPose.cs`, a low-cost fallback posing component for imported skeletal enemy models.
- Runtime mission spawner attaches the component to spawned enemies.
- If the imported hierarchy contains recognizable head/spine/arm/leg bone names, the component applies subtle patrol scanning, movement gait, and a basic combat-ready upper-body pose.
- If suitable bones are not present, the component safely does nothing.

## Important limitation
This is not a replacement for authored walk/run/aim/fire/reload/hit/death animation clips. Bone naming and pose quality depend on the imported GLB hierarchy, and must be checked in Unity. It does not establish that all character models are animated correctly.

## Validation
- Source-level checks and ZIP integrity can be run on this package.
- Unity compilation, imported rig compatibility, visual pose quality, performance, and Android runtime remain unverified because Unity Editor/device runtime is unavailable in this environment.
