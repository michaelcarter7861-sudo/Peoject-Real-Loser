# RC1.9 Changelog

- Added optional procedural posing fallback for skeletal enemy models that have recognizable bone names but lack animation clips.
- Hooked the fallback into runtime enemy spawning.
- Added explicit documentation of limitations and verification status.
- Re-ran Anti-Cheat v3 mission data validation for all ten missions: 10/10 passed.

This update does not add authored animation clips and does not prove runtime visual correctness. Unity Editor/device validation is still required.
