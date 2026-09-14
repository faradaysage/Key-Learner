# Noncommercial asset register

**Builds containing the WildMesh animals are for noncommercial use. A commercial release needs a suitable separate license for these exact assets, or their removal/replacement.** The project's MIT source-code license does not relicense third-party art.

The user explicitly approved this exception on 2026-09-14. `NONCOMMERCIAL_ASSETS.json` is the machine-readable inventory. Keep both files with the distributed notices.

| Pack / creator | Current imported subset | License | Source / future licensing contact |
| --- | --- | --- | --- |
| ULTIMATE ANIMAL PACK, WildMesh 3D | Wolf mesh, source fast-walk animation, T_Wolf texture | [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/) | [Exact pack listing](https://sketchfab.com/3d-models/ultimate-animal-pack-100-animals-50-off-165f755e44fc477fbc4ee41085606dd1), [credit link](https://skfb.ly/pMR9W) |

The supplied download contains a large combined display mesh and one separately rigged wolf with howl/walk takes. The listing's “100 animals” title is not evidence that the download supplies 100 individually usable animated animals. Only the verified wolf is imported.

## Files and placement

- Source adaptations: `UnityPort/Assets/ThirdParty/WildMeshAnimals/Models/Wolf.fbx`, `Textures/Wolf_Diffuse.png`, `License.txt`.
- Generated prefab: `UnityPort/Assets/KeyLearner/Resources/World/WildMeshAnimals_Wolf.prefab`; material(s) under `Assets/KeyLearner/Generated/Materials/WildMeshAnimals_*`.
- Catalog category: `wolf`, registered in `ContentBuilder.cs`. Forest vignettes in `ExplorerWorldLife.cs` use it for Sky Speller and selected Letter Racer roadside areas.
- Conversion source: `scripts/convert-wildmesh-wolf.py`. Archive/source/output hashes are recorded in `Assets/ThirdParty/ASSET_MANIFEST.json`.

## Before a commercial release

1. Follow the exact pack listing and obtain terms covering these particular models, game distribution and the intended commercial use. A price or purchase receipt alone does not establish the scope of the license.
2. Record the applicable terms, date, covered files and a private evidence reference; keep receipts/personal information out of the public repository. Update this register and distributed notices.
3. Alternatively, replace the `wolf` category with commercially reusable art, update the two placement/import files, remove the restricted source/prefab/material files and regenerate the content catalog and asset manifest. The existing CC0 Quaternius animals are suitable fallback scenery.
4. Review every remaining entry in this register. Do not mark a build commercially cleared while any unresolved noncommercial art remains.

The CC BY polar bear and boat/shark assets have separate attribution requirements and are not part of this noncommercial exception.
