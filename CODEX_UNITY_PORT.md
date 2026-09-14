# MonoGame to Unity: full migration and visual upgrade

Carry out this implementation in the existing repository. Do not merely provide a plan or generate an empty Unity project.

## Objective and constraints

Port the complete existing MonoGame desktop learning-game suite to Unity with URP. Preserve the existing gameplay, rules, controls, progression, scoring, audio/narration intent, difficulty, and minigame selection flow. Preserve pure C# logic where practical. The existing implementation and repository instructions are authoritative for behavior; inspect them before changing anything.

This is a Windows laptop game used with a touchpad, including bird-flight, car-racing, dolphin/ocean-swimming, and simpler educational minigames. Do not introduce controls requiring simultaneous mouse buttons or difficult precision gestures. Do not turn the migration into a gameplay redesign.

The visual upgrade is a primary deliverable, not an optional final step. The current problems are crude stand-in geometry, sparse environments, and obvious repetition of the same tree, house, and other props. Replace these problems with suitable real assets, coherent materials, animation, and deliberately composed environments. A renderer change with the same sparse placeholders does not satisfy the task.

**Visual awe is an explicit acceptance criterion.** The explorer games should feel delightful and visually rich to a young child while remaining readable and performant. Do not choose a crude generated mesh, primitive approximation, or low-detail placeholder merely because it is faster for the agent. When a substantially better free, legally usable model/pack exists, prefer acquiring and integrating it. Spend time on silhouettes, animation, material response, lighting, environment density, landmarks, motion, particles, water/underwater treatment, and camera presentation.

The asset list below is a starting point, not a ceiling. Proactively search for additional high-quality free assets when the current game needs them, especially for hero/player models, animated animals, standout landmarks, environmental set dressing, and future minigames. Prefer sources that permit direct automated download without authentication. If the best legitimate source requires a normal user login or manual download, document the exact asset/page in `MANUAL_DOWNLOADS.md` and continue other work rather than silently substituting inferior final art.

Preserve the cheerful toon/stylized identity while improving quality. Keep learning targets and the player clear against the scenery. Do not add distracting visual noise to simple counting, shapes, letters, or arithmetic games.

## 1. Establish a safe baseline and discover local tooling

Inspect repository instructions, Git status, architecture, minigames, dependencies, assets, tests, and current build/run procedures. Preserve unrelated edits. Do not reset, delete, or overwrite the existing MonoGame implementation. Create the Unity implementation alongside it, preferably in `UnityPort/`, unless the repository already establishes a better structure. Do not push or publish anything without a separate request.

Capture baseline behavior and representative visuals when the existing game can run. Maintain a short migration checklist and update it as work proceeds.

### Architecture goal: make future minigames cheap to add

Treat this as a migration plus a careful architectural cleanup, not a line-for-line engine translation. Preserve behavior, save compatibility, input-safety invariants, and tested learning logic, while improving structure where Unity gives a cleaner equivalent.

Design the Unity side so future minigames can be added without editing a giant central switch or duplicating engine plumbing. Prefer:
- a small explicit minigame contract/base abstraction for lifecycle, input policy, presentation, pause/focus handling, and metadata;
- a data-driven catalog/registry for picker metadata, suggested ages, topics, movement type, scene/prefab association, and future discoverability;
- shared services for audio/speech, save/profile access, particles/rewards, camera effects, input/focus ownership, accessibility/gentle-motion settings, and asset/style lookup;
- engine-independent C# domain/state logic for learning rules, progression, scoring, pattern generation, and deterministic tests where practical;
- Unity-specific adapters/presentation around that logic rather than spreading `MonoBehaviour` dependencies through everything;
- reusable environment/prefab composition systems for explorer games so new worlds can reuse roads, vegetation, water, props, lighting, toon materials, and spawning rules without copy/paste.

Avoid speculative frameworks, service-locator sprawl, reflection-heavy plugin systems, or generic ECS rewrites that do not buy this project something concrete. The target is straightforward extensibility for adding child-driven minigames over time.

The user installed Unity Hub on Windows. Their installation screen showed Unity 6.6, Editor version `6000.6.0f1`, and the official Unity CLI `v1.0.0-beta.8`. Discover what is actually installed rather than assuming these versions or pinning to the earlier proposed 6.3 LTS. Do not install another Editor merely to match an old instruction.

Run the installed CLI's help and discovery commands, checking actual supported syntax:

```text
unity --version
unity --help
unity editors --installed
unity auth status
```

Use the installed command help as authoritative because the CLI is experimental. If the executable is not found, inspect the installation/PATH and try a fresh terminal or absolute executable path before reinstalling. Detect native Windows versus WSL and handle Windows executable invocation and paths correctly.

If authentication is needed, ask the user to complete `unity auth login` in their own browser. Never request or extract passwords, browser cookies, or session tokens. Do not assume CLI sign-in proves that the Editor license is activated; actually open and validate the Editor.

Create a Universal 3D/URP project compatible with the selected installed Editor. Keep dependencies minimal and pin compatible package versions.

Use the official Unity CLI and, where supported, the Unity Pipeline package to automate the running Editor. With the intended Unity project open and the terminal in that project's directory, check help and use:

```text
unity pipeline install
unity pipeline list
unity command
```

Wait for package import/compilation. Explicitly target this project when multiple Editors are open. Discover available Editor commands rather than inventing CLI operations. Keep any local control API local; do not expose it publicly.

If Pipeline is unavailable or unreliable, continue with Unity Editor C# automation and the installed `Unity.exe` batch interface, using appropriate `-projectPath`, `-batchmode`, `-executeMethod`, and `-logFile` arguments. Do not run conflicting Editor instances against the same project. Prefer Editor APIs to hand-editing complex scene/prefab YAML.

Useful official references:
- https://docs.unity.com/en-us/unity-cli/unity-cli-reference
- https://docs.unity.com/en-us/unity-cli/use-unity-cli
- https://docs.unity.com/en-us/unity-production-pipeline/local-tools-cli/unity-pipeline-package
- https://docs.unity3d.com/6000.6/Documentation/Manual/EditorCommandLineArguments.html

Target a local Windows executable first. Detect build support and compiler prerequisites. Prefer a working Windows Mono build initially when appropriate; do not require IL2CPP or install unnecessary platform modules just for this migration. Do not enroll in paid cloud services or purchase assets, subscriptions, or tools.

## 2. Asset acquisition: approved starting sources

Use these specific sources as a curated shortlist, not an instruction to download every file. Select compatible assets and verify each downloaded archive's actual contents and license. Research checked the creators' listings; these assets have not yet been imported and validated in this project.

For final gameplay art, use this preference order:
1. high-quality free licensed model/animation from an automation-friendly original source or public repository;
2. high-quality free licensed model requiring a normal manual handoff;
3. adaptation of an existing project asset if it can meet the visual target;
4. agent-authored/procedural geometry only when the object is inherently geometric, generated terrain/road/water is appropriate, or no suitable licensed asset exists.

Do not let option 4 become the default for recognizable animals, vehicles, buildings, vegetation, coral, props, or hero objects. Record why a procedural replacement was necessary when it remains visible in final gameplay.

Prefer original creator downloads and public repositories. Download only free assets. Use a free/name-your-price download path when offered; do not select paid upgrades. Respect site terms, rate limits, authentication, and published download mechanisms. If a site blocks downloading, do not bypass it or spend the whole task fighting it. Use another authorized source or give the user one consolidated list of specific manual downloads.

Stage downloads outside Unity's `Assets` directory, for example in `_asset_downloads/`. Check `_asset_inbox/` at the discovered repository root for archives the user has already supplied. Do not assume a hard-coded absolute path; first determine the Git/project root with `git rev-parse --show-toplevel`, the solution/project layout, and the location of `KeyLearner.csproj`. Import only selected meshes, textures, animation data, and required supporting files. Do not import or execute arbitrary scripts/installers from asset archives without examining their purpose.

### Shared outdoor scenery: bird flight and roadside environments

**Primary nature: Quaternius Stylized Nature MegaKit, free Standard edition.**
The Standard edition has 68 models. Do not mistake the full kit's advertised model count or paid engine/shader setup for free included content. Build the required URP materials yourself when necessary.
- Creator: https://quaternius.com/packs/stylizednaturemegakit.html
- Creator-uploaded public archive page: https://opengameart.org/content/stylized-nature-megakit
- Creator's alternative download page: https://quaternius.itch.io/stylized-nature-megakit
- Listed license: CC0.

**Supplemental or simpler nature: Kenney Nature Kit.**
Useful for a broad selection of trees, foliage, rocks, and distant scenery. Avoid mixing visibly incompatible nature styles merely to increase model counts.
- https://kenney.nl/assets/nature-kit
- Listed license: CC0.

**Town buildings: Kenney City Kit Suburban and Commercial.**
Use different building silhouettes and purposeful neighborhoods, not one house repeated forever.
- https://kenney.nl/assets/city-kit-suburban
- https://kenney.nl/assets/city-kit-commercial
- Listed licenses: CC0.

**Rural landmarks: Quaternius Farm Buildings.**
Use suitable included farm structures as landmarks visible from the air or rural roads.
- https://quaternius.com/packs/farmbuildings.html
- Listed license: CC0.

### Racing-specific assets

**Kenney City Kit Roads:** modular roads and roadside traffic props.
- https://kenney.nl/assets/city-kit-roads

**Kenney Car Kit:** replace crude vehicle stand-ins with suitable modeled vehicles.
- https://kenney.nl/assets/car-kit

**Kenney Racing Kit:** optional track/racing assets where they suit the existing minigame better than the City roads.
- https://kenney.nl/assets/racing-kit

These Kenney packs are listed as CC0. Do not change the existing driving rules or track difficulty merely because a downloaded kit includes different road pieces.

**Alternative town style and directly clonable repository: KayKit City Builder Bits.**
- https://github.com/KayKit-Game-Assets/KayKit-City-Builder-Bits-1.0
- Creator page: https://kaylousberg.itch.io/city-builder-bits
- Listed license: CC0.

This is an alternative building family, not a requirement to mix another visual style into the game. A shallow public clone is sufficient; record the commit used. Import the actual model/texture files, not any Godot-specific wrapper or scripts. Preserve the palette/gradient texture where the models use it.

### Dolphin/ocean-swimming assets

**Primary large marine animals: Quaternius Animated Fish Pack.**
The creator's pack includes animated marine animals, including dolphin, whale, shark, and ray models. Obtain the original animated files and verify the rigs/clips rather than assuming a static mirror preserves animation.
- https://quaternius.com/packs/animatedfish.html
- https://quaternius.itch.io/lowpoly-animated-fish
- Listed license: CC0.

**Colorful fish variety: Quaternius Animated Cute Fish Pack.**
Use this for varied bright fish and schools. Inspect the archive rather than treating every listed model/variant as a distinct species.
- https://quaternius.com/packs/cutefish.html
- Listed license: CC0.

**Curated reef decoration, manual handoff by default: MiniPoly Coral Reef Kit.**
- https://poly.pizza/bundle/Coral-Reef-Kit-ghN8EmbYa6
- The bundle lists mixed CC0 and CC-BY licenses. Preserve and implement the actual attribution requirements for each selected model; do not label the whole bundle CC0.

**Additional manual-handoff decoration: Mohabins Seaweed.**
- https://poly.pizza/m/oYxUdpyc4u
- Listed license: CC0.

Poly Pizza's site terms restrict automated access and downloading. Do not scrape it or reverse-engineer private download endpoints. Use these files from `_asset_inbox/` after the user downloads them through the normal website, or use an explicitly permitted published API only after verifying authorization and applicable terms. Missing manual reef assets must be reported, not quietly replaced with final-quality claims about primitive placeholders. Continue independent migration work while awaiting them.
- Terms: https://poly.pizza/docs/tos

This shortlist does not verify a ready-to-use animated hero bird. Inspect whether the existing bird can be reused adequately; otherwise find an additional creator-hosted, suitably licensed model under the same rules and verify its animation needs. Do not claim a scenery pack contains a rigged bird without checking.

For Poly Pizza assets:
- Do not scrape, crawl, script, or programmatically download from Poly Pizza.
- I may place manually downloaded Poly Pizza assets in `_asset_inbox`.
- Automatically inspect, import, convert, optimize, attribute, and integrate anything found there.
- If a desired Poly Pizza asset is missing, report its page URL and exact asset name in a short MANUAL_DOWNLOADS.md file, then continue working on everything else.
- Prefer equivalent CC0/public assets from automation-friendly sources when practical rather than blocking progress.

### Basic educational shapes

Use Unity's built-in spheres, cubes, cylinders, capsules, planes, and quads where those are the actual intended objects. Procedural meshes remain appropriate for mathematical shapes, ground, continuous roads/water, and helper geometry. Do not download a model pack just to obtain a sphere.
- https://docs.unity3d.com/6000.6/Documentation/Manual/PrimitiveObjects.html

## 3. Import, licensing, and maintainability

Prefer FBX files that preserve needed rigs/animations; use OBJ only for appropriate static assets. If using glTF/GLB, install a compatible, maintained importer rather than assuming native support. Do not depend on `.blend` imports unless the required local Blender workflow is actually present and tested.

Verify scale, axes, normals, pivots, material slots, texture paths, transparency, animation clips, and collider behavior. Preserve useful UVs, palette atlases, textures, and vertex colors. Do not replace every imported material with a single untextured color and discard the qualities that made the model worth importing.

Keep original third-party assets separate from game-specific prefabs/material adaptations. Preserve Unity `.meta` files and stable GUIDs. Use repeatable, idempotent Editor setup/import scripts where useful. Prevent a rerun from duplicating every asset or destroying manual edits.

Maintain `THIRD_PARTY_ASSETS.md` with creator, pack/model title, source URL, downloaded version/date or Git commit, exact license and license URL, archive hash where practical, imported paths, modifications, and any required credit. Keep supplied license files and include required attribution with the distributed build and accessible game credits where appropriate.

Prefer CC0; CC-BY is acceptable with proper attribution. Do not use unlicensed, noncommercial-only, no-derivatives, editorial-only, ripped, or branded fan assets. A free download is not itself a license. Treat this as game-asset use, not permission to harvest a website for model training.

All gameplay assets must be present locally in the build. Do not make children depend on live asset downloads or third-party services to play.

## 4. Visual direction and environment composition

Create a coherent bright, welcoming stylized world, not a collection of unrelated asset demonstrations. Establish a shared palette, scale convention, lighting approach, and restrained toon shading. Different minigames may have distinct environments while still belonging to the same suite.

Improve actual models and scene composition before layering on post-processing. Configure appropriate URP lighting, shadows, anti-aliasing, ambient/environment lighting, and restrained color grading/bloom. Avoid motion blur, excessive depth of field, harsh outlines on everything, and effects that obscure educational targets. Preserve leaf/plant cutouts and appropriate material/shadow behavior.

Do not treat a toon shader as a substitute for recognizable silhouettes, good animation, or varied models.

### Bird flight

Build scenery for the actual gameplay camera and flying height. Use readable landscape composition: woodland groves, clearings, fields, small settlements, and selected rural landmarks, with coastline/river/hills where appropriate to the existing world.

Use multiple genuinely different tree and building meshes, not only random colors on one model. Arrange them in designed clusters with clearings, transitions, and recognizable landmarks. Spend detail where the camera can see it; distant scenery can be simpler.

Preserve the existing flight behavior and objectives. Ensure the bird's presentation and wing movement read clearly against the environment. Do not turn decoration into new obstacles or collision hazards unless that was already part of the gameplay.

### Car racing

Create distinct roadside stretches using compatible buildings, vegetation, fences, signs, lamps, and other appropriate included props. Keep vehicle models and road scale coherent. Use town/rural/coastal variation only where it fits the existing course and gameplay.

Decorate beyond the playable road boundary. Keep the route, goals, and controls readable. Do not add traffic hazards, collisions, or difficulty changes as an incidental consequence of importing assets.

### Dolphin/ocean swimming

The target is a colorful aquarium-like reef, not an empty blue plane with an occasional fish. Compose reef gardens from multiple coral forms, seaweed/kelp clusters, sand patches, rock formations, and swimming fish at different depths. Make the scenery interesting close to the actual player route, not only far away.

Use real swim animation where available. Add varied fish schools and occasional larger marine animals, including whale, shark, and ray when suitable assets are available. Keep encounters calm and non-hostile unless existing gameplay explicitly requires otherwise. Do not invent attacks or frightening predator behavior.

Use gentle plant sway, bubbles, depth cues, and modest underwater lighting/caustic effects where achievable on the target laptop. Preserve contrast and visibility; dense fog or bloom is not an acceptable substitute for scenery.

### Variety and performance

Use reusable authored prop clusters and biome/segment rules rather than uniform random scattering. Avoid obvious adjacent duplicates. Vary scale, orientation, spacing, and approved palette variants, but also use genuinely different meshes. Keep generation deterministic when reproducibility benefits tests.

As visual review targets across representative routes, aim for several distinct tree silhouettes and building silhouettes on land, and several distinct coral forms, plant groupings, and fish silhouettes underwater. These are quality checks, not quotas that justify clutter or unnecessary downloads.

Use pooling, sensible visibility distances, instancing/batching where supported, and suitable detail levels. Measure performance on the actual machine. Aim for smooth 60-fps play when its hardware permits, and report measured conditions rather than promising an untested frame rate.

## 5. Implement, verify, and finish

First establish a working Unity shell and one representative minigame to prove the asset/material/input/build workflow. Then continue through the entire suite; do not stop permanently at the first demonstration. Preserve behavior while replacing MonoGame rendering, content loading, input, UI, audio, particles, and scene plumbing with appropriate Unity equivalents.

Use the simpler minigames' intentionally clean presentation rather than forcing detailed scenery into every learning activity. Keep pure game logic independently testable where practical.

Build a Windows executable and launch it. Exercise minigame selection, every minigame, controls, scoring/progression, restart/exit, audio, and mode selection where present. Inspect Editor and player logs and fix compilation/runtime errors, missing materials, broken animation, and missing assets. A successful compilation alone is not completion.

Capture representative screenshots from actual gameplay cameras for bird flight, racing, and underwater play, plus the simpler games. Inspect them when image-viewing tools are available, and iterate on sparse scenery, repetition, incorrect materials, poor framing, or obstructed targets. Do not use only an attractive Editor scene view as proof of gameplay quality. If visual or interactive inspection is unavailable, save evidence and explicitly distinguish what was and was not verified.

Deliver the Unity project, runnable Windows build, reproducible build/run instructions, asset attribution, test results, representative screenshots, and a brief list of remaining blockers or unresolved visual gaps. Also leave the project in a state where adding a new minigame means implementing/registering the minigame and its content, not modifying unrelated engine plumbing. Report actual download/import success separately from advertised pack contents. Do not silently substitute crude placeholders and call the visual upgrade complete.

Proceed with implementation and make reasonable reversible decisions. Ask only for genuinely necessary user actions such as authentication, a required manual download, a paid choice, or a destructive change. Continue independent work when a single asset or optional tool is blocked.


### Authorized license exception — 2026-09-14
The user explicitly approved WildMesh 3D’s ULTIMATE ANIMAL PACK under CC BY-NC 4.0 for noncommercial builds. Track every imported file and replacement point in `NONCOMMERCIAL_ASSETS.md`; commercial release requires a suitable separate license or removal/replacement. This exception does not waive attribution or authorize other restricted packs.
