# Project Real Loser RC1.10 — Rig Name Compatibility Fix

## Change
- Fixed `EnemyProceduralPose` bone-name matching for the actual shipped GLB rig names (`UpperArmL`, `UpperArmR`, `UpperLegL`, `UpperLegR`).
- Added name normalization for common underscore, hyphen, dot, and space variants.
- Avoided treating `Neck` as `Head`, allowing a distinct `Head` node to be selected where present.

## Why it matters
The previous pose fallback recognized `Spine`, `Chest`, and `Head`, but did not match the project's no-separator arm and leg names. As a result, the fallback could appear to animate only the upper body/head rather than the limbs. This patch makes the existing rig's limb names eligible for procedural gait and combat pose.

## Validation
- Static source checks and ZIP integrity are run for this package.
- Unity compilation, actual imported GLB bone hierarchy, animation quality, and Android runtime remain unverified because Unity Editor/device runtime is unavailable in this environment.
- This is a compatibility improvement, not authored animation clips or motion capture.
