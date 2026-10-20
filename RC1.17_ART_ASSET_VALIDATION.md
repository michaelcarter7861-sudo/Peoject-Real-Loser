# RC1.17 Art Asset Validation

## Passed static checks

- 27 PNG texture assets decode successfully at 512x512.
- Eight full PBR texture sets are present: Concrete, DesertSand, OliveFabric, BrushedSteel, WeaponSteel, WeatheredWood, Foliage, Rubber. Each set has BaseColor, Normal, and Mask images.
- Three additional base-color atlases are present: ConcretePanels, DesertCamo, UrbanCamo.
- 24/24 unique character GLB files use the expected top-level character rig root and the bone naming convention used by the animation clips.
- Runtime loader normalizes that top-level root to `RigRoot`; five clip definitions target that stable path.
- Character spawn code uses the authored-clip playback component rather than the old procedural-only pose component.
- All C# files pass a basic delimiter-balance check; ZIP integrity passes.

## Editor/runtime checks still required

Unity was not installed in the authoring environment. Therefore this package does not claim that Unity has compiled the scripts, imported the maps, serialized the `.anim` files, or rendered the poses. The Editor automation generates the clips when the project opens and before a build. In Unity, run:

1. `Real Loser → Art Pipeline → Generate Authored Character Clips and Configure Textures`
2. `Real Loser → Art Pipeline → Validate Authored Clips and Texture Sets`
3. Play a representative enemy from each major model family; inspect walk, aim/fire, reload, takedown, material response and Android frame time.

The animation curves are original keyframed clips authored for the shared 15-bone generic rig, not motion-capture data. These are baseline gameplay clips rather than cinematic or archetype-specific final animation.
