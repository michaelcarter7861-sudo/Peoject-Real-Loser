# Project Real Loser — External Repository Integration Matrix

Reviewed: 2026-10-20. This is a practical engineering review of repository landing pages, README files and available license statements, not a full source-by-source security/legal audit.

## Integration decisions

| Repository | Useful for PRL | Recommendation | Important caveat |
|---|---|---|---|
| [UniRig](https://github.com/VAST-AI-Research/UniRig) | Predict skeletons and skin weights for static character meshes | Use as an offline Blender/Python content pipeline; export rigged FBX/GLB, then validate humanoid bone mapping | MIT code does not automatically license all model weights/data. Current README says release is progressive and full checkpoints are not all available. Requires CUDA GPU (README states 8 GB VRAM minimum). |
| [MotionGPT3](https://github.com/OpenMotionLab/MotionGPT3) | Text-to-motion research, motion generation and prediction | Prototype animation offline; retarget generated motion to the PRL humanoid rig in Blender/Unity | Outputs are motion/SMPL-oriented and are not ready-to-use Unity `.anim` clips. MIT code; SMPL/SMPL-X, datasets and dependencies have separate terms. Tested on Python 3.11/PyTorch 2.0. |
| [GVHMR](https://github.com/zju3dv/GVHMR) | Recover human motion from video | Do not include in a distributable/commercial production pipeline without permission | Repository license restricts use to educational, research and non-profit purposes; derivative work must be open-source and commercial use is prohibited unless permission is obtained. |
| [Procedural Animation in Unity](https://github.com/anoushmistry/Procedural-Animation-in-Unity) | Animation Rigging package, Multi-Aim and Two-Bone IK patterns | Adapt the concepts for head tracking, hand-to-weapon alignment, foot placement and vault/cover interactions | MIT license; sample says some dynamic blending is in progress. Needs compatible humanoid rig and Animation Rigging package, not just copied scripts. |
| [TRELLIS](https://github.com/microsoft/TRELLIS) | Image/text-to-3D asset generation | Use offline to create static props, then decimate/UV-check/collider-bake for mobile | Majority of code/models MIT; some submodules have separate licenses. README indicates Linux-tested and NVIDIA GPU with at least 16 GB memory. |
| [TRELLIS.2](https://github.com/microsoft/TRELLIS.2) | Detailed 3D geometry and PBR texture generation | Evaluate for hero props and environment modules after license/dependency review | Repository README claims MIT for model/code, but dependencies have their own terms; confirm exact model-weight terms and pinned revision before production. Heavy CUDA pipeline, not an Android runtime dependency. |
| [Blender MCP](https://github.com/ahujasid/blender-mcp) | Automate Blender scene/asset authoring from prompts | Optional offline authoring tool; keep generated assets checked into the project, not an MCP runtime dependency | Review the repository's current LICENSE and TERMS_AND_CONDITIONS before redistributing or using commercially. Third-party integration; not a substitute for Blender QA. |
| [Blender AI MCP](https://github.com/PatrykIti/blender-ai-mcp) | Guided Blender scene automation concepts | Do not adopt this repository as the primary tool because its current page marks it discontinued | Use the maintained upstream Blender MCP or another maintained workflow after verifying licensing. |
| [Unity-MCP by IvanMurzak](https://github.com/IvanMurzak/Unity-MCP) | Unity Editor automation, asset inspection, test execution and build loops | Strong candidate for a developer workstation tool if the project owner installs/configures it locally | Apache-2.0; README/wiki says Unity 2022.3+. This environment has no running Unity Editor/MCP endpoint, so it cannot be activated here. |
| [Unity MCP by CoplayDev](https://github.com/CoplayDev/unity-mcp) | Unity Editor scene/asset/script/test/build automation | Alternative to IvanMurzak's tool; choose one bridge rather than installing both initially | MIT; supports Unity 2021.3 LTS through Unity 6 according to README. Requires a local Unity Editor and MCP client. |
| [NKRsys FPP CC](https://github.com/NeKoRoSYS/NKRsys-FPP-CC) | Lightweight non-Rigidbody FPS controller, crouch, slopes, camera and footsteps | Compare movement model and input handling; port selected behavior only after testing against PRL controller | The project is a Unity template using the new Input System. Keep one authoritative player controller to avoid competing movement/camera scripts. Verify license file before copying code. |
| [UnityAdvancedFPSCharacterController](https://github.com/unitycoder/UnityAdvancedFPSCharacterController) | Non-Rigidbody controller, event-based input, crouch, slopes, footstep audio and save-system patterns | Use as a code reference for a controlled player-controller refactor | New Input System and its event model must be reconciled with PRL's mobile controls; do not run two controllers simultaneously. Preserve attribution/license terms. |
| [AudioLDM2](https://github.com/haoheliu/audioldm2) | Generate original SFX from text prompts | Generate sounds offline and import approved WAVs into Unity; do not ship Python/PyTorch inference inside Android game | Repository has separate model/code/dependency terms; verify exact current license and model-card restrictions. Some published AudioLDM2 materials are CC BY-NC-SA 4.0, which is not suitable for commercial use without permission. |
| [Godot FPS template](https://github.com/kinifi/Godot-FPS-Template) | Conceptual reference for input, movement and state separation | Learn patterns only; do not port engine-specific GDScript into Unity | Godot scenes/scripts are not Unity-compatible. The exact supplied URL did not resolve during this review; verify the repository URL before using it. |

## What “open source” permits

- A public GitHub repository is not automatically open source. Check for an explicit license.
- MIT/BSD generally permit reuse and modification subject to license/notice requirements.
- Apache-2.0 permits reuse with its notice, license and patent provisions.
- GPL-family licenses may impose source-distribution obligations on covered derivative works; review carefully before combining with a closed-source release.
- Non-commercial research licenses (such as GVHMR's) are not safe for commercial game production without written permission.
- Model weights, datasets, pretrained checkpoints, third-party libraries, sample assets and generated outputs can have terms different from the repository code.
- Keep `THIRD_PARTY_NOTICES.md` and record repository URL, commit hash, file paths copied, license text and modifications for any copied code.

## Recommended production workflow

1. **Unity automation:** Choose one Unity-MCP bridge and install it in the local Unity 2022.3 project. Use it for import/compile, scene inspection, Play Mode tests, screenshots and builds.
2. **Player controller:** Compare PRL's controller with NKRsys/UnityAdvancedFPS; consolidate into one controller, then test keyboard and mobile touch paths. Preserve PRL mission/UI APIs.
3. **Stealth AI:** Implement explicit perception state (FOV, occlusion, hearing/noise, last-known position, search timeout), alarms/cameras, and measurable detection tests. Avoid importing an unverified full AI stack.
4. **Character pipeline:** Use UniRig only as an offline rigging experiment. Validate joint hierarchy, weights, scale, forward axis and humanoid retargeting before approving any soldier asset.
5. **Animation:** Use Animation Rigging Two-Bone IK/Multi-Aim on top of proper base locomotion clips. MotionGPT3/GVHMR are offline research pipelines, not a drop-in runtime Animator.
6. **World art:** Generate a small representative batch with TRELLIS/TRELLIS.2; retopologize/decimate, bake maps, author LODs/colliders, then profile on target Android device.
7. **Audio:** Generate/record SFX offline, audition them, normalize levels, use platform-compatible compressed formats and spatial audio; license-check every model/output source.
8. **Release gates:** Unity compile with zero errors, all scenes open, all 10 missions complete/fail/restart correctly, no missing references, animation/texture validation, Android smoke test and frame/memory budget.

## Immediate engineering conclusion

The best next investment is **not** to copy entire projects. First establish a Unity Editor compile/test loop using one Unity-MCP bridge, then integrate narrowly scoped, license-cleared patterns. For PRL's current gaps, the highest-value order is: (1) Editor compilation/import verification, (2) one stable FPS controller + mobile input, (3) stealth perception and alarms, (4) real rigged animation + IK, (5) authored environment assets and PBR material assignment, (6) mission regression tests and Android profiling.
