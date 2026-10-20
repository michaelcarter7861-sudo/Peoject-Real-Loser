# Project Real Loser RC1.11 — Weapon Gameplay Pass

## Changes
- Replaced the old noise-only weapon fire method with a centralized hitscan damage path.
- Added per-weapon magazine/reserve counts, fire-rate cooldown, reload timing, and an explicit reload action.
- Shotguns now fire a small spread of pellets; hitscan damage falls off with range.
- Melee/stealth weapons use a short-range frontal overlap check and trigger a non-lethal takedown on a valid enemy.
- Trap damage and enemy damage are handled centrally by the weapon system; the HUD no longer applies a second independent damage path.
- Added a reload button and live ammunition/reload status to the mobile HUD.

## Validation performed
- Static source checks and ZIP archive integrity checks only.
- Mission JSON validation is run against the ten mission data files.

## Not verified
- Unity compilation, physics hit registration, actual weapon/model alignment, Android touch layout, and runtime behavior. These require opening the project in the intended Unity version and testing in Play Mode/device.

## Known scope limits
- This is a hitscan gameplay pass, not a complete animation-driven weapon system. Reload animation, shell ejection, muzzle flash, sound mixing, recoil tuning, ballistic projectile simulation, weapon pickup/inventory persistence, and enemy firing/damage behavior still need implementation and runtime validation.
