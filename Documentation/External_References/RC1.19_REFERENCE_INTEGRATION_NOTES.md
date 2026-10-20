# RC1.19 — Reference Integration Notes

This package adds a Unity Editor audit command and a license-aware integration matrix based on the supplied public repositories.

## Unity Editor audit

Open the project in Unity 2022.3.40f1, then run:

`Project Real Loser > External Reference Audit > Generate Project Report`

It writes `PRL_EXTERNAL_REFERENCE_AUDIT.txt` next to the Unity project's `Assets` folder. The report counts scenes, prefabs, materials, animation clips, Animator controllers, textures, audio and model assets; lists build-settings scenes; and checks missing scripts on prefab root objects.

This is static Editor inspection only. It does not compile scripts, open all scenes, enter Play Mode, test animation playback, validate shader assignments, or build/test Android. Nested prefab and scene instances still require review.

## What was deliberately not copied

No external gameplay code, model weights, datasets, pretrained AI checkpoints, sounds or assets were copied into the game in this pass. Reasons: license/weight terms differ across repositories, some are non-commercial research-only, and integrating entire controllers or animation stacks without compiling/testing can create conflicting input and movement systems. The project now includes a concrete integration plan and a third-party notice template so future imports can be tracked.
