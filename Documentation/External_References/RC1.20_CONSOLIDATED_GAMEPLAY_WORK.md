# Project Real Loser RC1.20 — Consolidated Gameplay Engineering

## Implemented in this pass

- Enemy perception now requires three checks: visibility-adjusted range, horizontal field-of-view cone, and a physics line-of-sight raycast. A solid wall/door/terrain/prop between an enemy and the player blocks detection; the enemy's own colliders are ignored.
- Enemies retain the player's last known position for `combatMemorySeconds` (default 5.5 seconds). If line of sight is lost, combat/flank actors move toward the last known location and then investigate it instead of instantly having perfect tracking.
- Sight reacquisition no longer restarts the combat/flank action timer on every frame. This previously prevented those timers from naturally expiring and caused repeated state transitions.
- New static release checks assert these perception and combat-memory invariants.

## Repository use / license boundary

This pass uses the previously reviewed repository documentation to guide architecture; it does not vendor any third-party source, model checkpoints, datasets, or media. That avoids silently importing research-only/non-commercial assets or creating competing movement/input systems. Before code is copied from any repository, record its exact commit, LICENSE, dependency licenses, attribution/notice obligations, and any separate model-weight/dataset terms.

## Recommended next integration sequence

1. **Unity-MCP bridge (choose one):** configure CoplayDev Unity MCP or IvanMurzak Unity-MCP against the local Unity 2022.3.40f1 editor. Do not install both initially. Use it to import, compile, open scenes, run Play Mode smoke tests, capture screenshots and build Android. The bridge itself has not been installed or run in this environment.
2. **FPS controller comparison:** compare movement/aim/lean/input against UnityAdvancedFPSCharacterController and NKRsys FPP CC. Keep one authoritative player controller; selectively port features only after license review and keyboard + touch testing.
3. **Animation:** use Unity Animation Rigging Two-Bone IK and Multi-Aim patterns with existing locomotion. Do not treat MotionGPT3 or GVHMR outputs as drop-in Unity animation clips; they require offline conversion/retargeting and their data/model terms need review.
4. **Art generation:** use TRELLIS/TRELLIS.2 only as an offline asset-generation experiment on a compatible Linux/NVIDIA workstation. Inspect topology, UVs, materials, scale, pivots, colliders, LODs and mobile performance before import. UniRig is an offline rigging experiment, not a replacement for Unity rig validation.
5. **Audio:** audition generated cues in-game, normalize levels, compress for mobile, and review the exact model/dataset/license terms before distribution.

## Required release gates still outstanding

- Open the project in Unity 2022.3.40f1 and compile with zero errors.
- Open every build-settings scene and check missing scripts/references, lighting, materials, colliders and camera placement.
- Play through all ten missions: objective completion/failure, checkpoint/restart, extraction, stealth alarms, enemy LOS and combat damage.
- Validate actual character animation playback/retargeting and all texture/material slots.
- Build and smoke-test Android ARM64 on a real device; profile frame time, memory and thermal behavior.

## Verification scope

The Python static release validator can catch source delimiter errors, mission-data issues and asset-container structure. It is not a C# compiler and does not prove Unity Play Mode or Android behavior. Unity Editor and Android device verification were unavailable during this pass, so no successful Unity compile, Play Mode run or APK build is claimed.
