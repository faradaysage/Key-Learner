# Third-party game assets

Source files and licenses verified on **2026-09-13**. The actual local files, rather than pack marketing counts, are recorded below. The original MonoGame content remains in `Content/` with its existing notices.

## Required distributed credit

- **Bird - Animated** by **Panther-One** (PantherOne), [OpenGameArt source](https://opengameart.org/content/bird-animated), licensed under [Creative Commons Attribution 3.0 Unported](https://creativecommons.org/licenses/by/3.0/). Converted from the original Blender model to FBX, restored its original diffuse texture, renamed the existing flight clip, and adapted Unity materials/pivot/scale. Original mesh, UVs, rig and animation preserved.
- **Coral Reef Kit** by **MiniPoly**, [bundle source](https://poly.pizza/bundle/Coral-Reef-Kit-ghN8EmbYa6), [creator](https://poly.pizza/u/MiniPoly), licensed under [Creative Commons Attribution 3.0 Unported](https://creativecommons.org/licenses/by/3.0/), via Poly Pizza. Six FBXs supplied manually by the user. Exact bundle attribution and license were confirmed directly by the user on 2026-09-13. Original embedded color/emission/roughness/metallic atlases extracted without modification; Unity materials, pivots, scale and scene composition adapted.

The credits above must accompany distributed builds and be available from the parent interface. A copy of this ledger and individual license notices ship in `Assets/StreamingAssets/Notices/`. The CC0 creators below are also credited voluntarily.

## Imported source inventory

Every imported path below starts with `UnityPort/Assets/ThirdParty/`. No scripts, installers, arbitrary shaders, Unity packages, or executable archive content were imported. Only selected FBX source sets, necessary textures and license notices were retained. Additional car/road/building source pieces remain available for future minigames; only the current catalog's subset becomes runtime prefabs.

| Creator / pack | Verified version and source | Local contents | License / imported path |
| --- | --- | --- | --- |
| Quaternius — Stylized Nature MegaKit Standard | July 2024 Standard; [creator](https://quaternius.com/packs/stylizednaturemegakit.html), [creator-uploaded archive](https://opengameart.org/content/stylized-nature-megakit) | 68 distinct Unity FBXs, 20 original textures. Twenty tree meshes across common, pine, twisted and dead families; shrubs, grass, flowers, rocks, paths and mushrooms. The paid 116-model edition was not downloaded. | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/), `QuaterniusNature/` |
| Quaternius — Animated Fish Pack | April 2018; [creator](https://quaternius.com/packs/animatedfish.html), [creator-uploaded archive](https://opengameart.org/content/animated-fish) | Seven original rigged FBXs: Dolphin, Whale, Shark, Manta ray and Fish1–3. Original swim clips and distinct material slots retained. | CC0 1.0, `QuaterniusFish/` |
| Quaternius — Animated Cute Fish Pack | February 2020; [creator](https://quaternius.com/packs/cutefish.html), [creator-linked public folder](https://drive.google.com/drive/folders/1QK5NCgIxUZ1cYGzVwaNfcfEiQhvczqUB) | Twelve selected species: BlueTang, ButterflyFish, Clownfish, CoralGrouper, Goldfish, Koi, MandarinFish, MoorishIdol, ParrotFish, Puffer, RoyalGramma, Tetra. Original bones and clips present; gameplay selects Swimming_Normal. Attack, death and out-of-water clips are not played. The advertised 52 models include props and are not 52 imported species. | CC0 1.0, `QuaterniusCuteFish/` |
| Quaternius — Farm Buildings Pack | September 2018; [creator](https://quaternius.com/packs/farmbuildings.html), [creator-linked public folder](https://drive.google.com/drive/folders/1gdZ39vcLML_ULU5sHirkKQ-gEkk458Ak) | Nine selected FBXs: Barn, BigBarn, ChickenCoop, Fence, Silo, TowerWindmill, WaterTower, Well, Windmill. Original material colors retained. | CC0 1.0, `QuaterniusFarm/` |
| Kenney — Car Kit | 3.1, creator notice dated 2026-04-02; [source](https://kenney.nl/assets/car-kit) | 50 FBX source pieces including vehicles, separate wheels and debris; one original color atlas. Runtime selection uses nine vehicle silhouettes, including the race car. | CC0 1.0, `KenneyCars/` |
| Kenney — City Kit Suburban | 2.0, creator notice dated 2025-04-23; [source](https://kenney.nl/assets/city-kit-suburban) | 40 FBXs including 21 house designs, fences, paths and planting props; original color atlas plus three palette variants. | CC0 1.0, `KenneySuburban/` |
| Kenney — City Kit Commercial | 2.1, creator notice dated 2025-07-21; [source](https://kenney.nl/assets/city-kit-commercial) | 41 FBXs including 14 buildings, five skyscrapers, awnings and lower-detail variants; original atlas plus two palette variants. | CC0 1.0, `KenneyCommercial/` |
| Kenney — City Kit Roads | 2.1, creator notice dated 2026-08-18; [source](https://kenney.nl/assets/city-kit-roads) | 95 FBX pieces and original color atlas. Runtime props include modeled lamps, signs, barriers and street furniture. The continuous playable road remains generated to preserve existing road rules. | CC0 1.0, `KenneyRoads/` |
| Panther-One — Bird - Animated | 2013-07-29 source, downloaded 2026-09-13; [source](https://opengameart.org/content/bird-animated) | One converted FBX, original feather texture, original 30-frame rigged flight clip. Reviewed against two additional public candidates before selection. | CC BY 3.0, `PantherOneBird/` |
| MiniPoly — Coral Reef Kit | User supplied on 2026-09-12; user license confirmation 2026-09-13; [source](https://poly.pizza/bundle/Coral-Reef-Kit-ghN8EmbYa6) | Six source FBXs, CoralReefSet1–6. Each embeds the same 1000px atlas set; extracted original bytes retained and one shared material uses the color/emission atlas. | CC BY 3.0, `MiniPolyCoral/` |
| Mohabins — Seaweed | User supplied on 2026-09-12; [source](https://poly.pizza/m/oYxUdpyc4u) | One FBX. Its missing external palette was recovered from the original PNG embedded in the user's companion GLB. | CC0 1.0, `MohabinsSeaweed/` |

All CC0 notices were read from the downloaded pack/folder, except Seaweed whose CC0 status is recorded in the user approved source list. No Poly Pizza page was scraped or downloaded automatically. The user-supplied Quaternius Mushroom archive was also inspected; the corresponding Mushroom_Common source is already included in the creator's verified Nature Standard pack, so it is not imported twice.

## Archive provenance

All automatically downloaded source files came from the creators' published public download links. No authentication, paid upgrade, session token, download restriction bypass, or third-party executable was used. Public Drive requests accessed only the explicitly shared creator files. Individual Drive downloads and source hashes are listed in `UnityPort/Assets/ThirdParty/ASSET_MANIFEST.json`.

| Source archive | SHA-256 |
| --- | --- |
| `stylized_nature_megakitstandard.zip` | `298f6732b872e4cf7b30e6e7abf9641c7f6dc6b326df37ac089533ed7e3d58c9` |
| `animated_fish_quaternius.zip` | `c56a4bf3468e900ed9d881037334152dc203f93b656373347c8a7fc3dedb1855` |
| `kenney_car-kit.zip` | `fac7dacac5c7874348cf19729af3ef205f3d366493edaf0a827d93f4fdf3d0c4` |
| `kenney_city-kit-suburban_20.zip` | `5869c35cf30b1c87bdb2d197b6d325eebadd2ef08ea27f04797e8e08d77a9a39` |
| `kenney_city-kit-commercial_2.1.zip` | `f8b09b081c2bb88bcc126e2dec1cb40fd0dad7e7e591b6c26aaefe96fb35276b` |
| `kenney_city-kit-roads.zip` | `22058af3d68173a7cf9bda9f0e243a8cef6bd68168c302ebc76327063849674e` |
| User `Coral Reef Kit.undefined-zip.zip` | `79bf98b20382256599d70510d6ed0c6dd158aab5ee76f408aeca54b60a8c303e` |
| User `Seaweed by Mohabins - oYxUdpyc4u.zip` | `8513185b24f951d7d607da9af637eaa024535ea1d82c407d2013208dade5f1fe` |
| Original `BirdPantherOne.blend` | `d60aae323997138fb273605063c363d5a78899a583e5d679e32b026024c2d3c9` |

## Adaptation and verification

`ContentBuilder.Build()` creates a small explicit content library and stable prefab/material paths using Unity Editor APIs. It preserves original meshes/UVs and source material colors, maps original atlases, supplies appropriate leaf cutouts and double-sided foliage, removes incidental source lights/cameras/colliders, shifts a wrapper pivot, measures static geometry and baked skinned vertices across locomotion frames, and configures looped Legacy Animation. The six coral display rows are adapted into 35 intact organism prefabs, retaining their authored geometry and atlas color variants. It never runs archive code. Game-specific generated adaptations live under `Assets/KeyLearner/Generated` and `Assets/KeyLearner/Resources/World`; source assets remain separate. Rebuilding retains Unity GUIDs at the same paths.

Blender 4.3.2 was discovered locally and used with `--disable-autoexec` for inspection, original image-byte extraction and the one bird FBX conversion. No build depends on runtime `.blend` importing or Blender installation. Source inspection verified original dolphin/whale swim clips, all cute-fish clip names, all nature material mappings, coral embedded atlases and the seaweed GLB palette. The Unity import report in `artifacts/unity/content-import-report.txt` records final bounds and selected runtime clip durations after the Editor builder runs. See the migration verification report for gameplay-camera evidence and measured performance; source acquisition alone is not a visual acceptance claim.

Procedural geometry is reserved for terrain, continuous roads, water, mathematical objects, letter targets and effects. Recognizable bird, dolphin, marine animals, reef, seaweed, trees, buildings, cars and roadside props use the credited real models.
