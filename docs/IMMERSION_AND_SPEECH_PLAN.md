# Immersion and prepared speech implementation plan

PR #8 was merged on 2026-09-14 (UTC), squash 0a44fcb96eb685c8779881f0e53f663d59465374. This work continues on codex/immersive-gameplay-audio.

## Boundaries

Keep learning, score, progression, navigation and reward rules in the engine-independent domain. Unity owns rendering, animated actors, pointer projection and bounded audio voices. Preserve save schemas, family recordings, protected input and the legacy-only Unity input backend. MonoGame remains buildable alongside Unity.

## Implementation sequence

1. Audit speech calls, navigation and quantity-game input; reproduce Cannon Hop and distinguish its progression lock from in-game failures.
2. Prepare a public, stable-ID speech manifest covering shipped dictionaries, letters, numbers, icons and all instruction templates. Use one text-to-asset resolver in both engines, with explicit family recordings first and existing fallback for unlisted family text.
3. Install a project-local Python 3.11 environment and pinned CUDA Chatterbox toolchain. Generate twenty representative samples before bulk generation. Pin model revision and voice/settings; retain watermarks. Hash recipes and generated files. Store float WAV masters outside the build, commit PCM WAV runtime assets. Normal builds never invoke Python or download models.
4. Repair assisted pursuit using actual gate altitude and distance-aware steering; allow dolphin surface breaches. Follow road curves with mesh-based markings and velocity-based body orientation, animate actual model wheels.
5. Introduce reusable bounded explorer encounters and an ambient/motion mixer: poppable bubbles, fish avoidance, flocks and ground activity, harmless road obstacles and vehicle/color rewards. Use licensed source models and sound recordings, with provenance. Respect mute, volume and gentle-motion settings; duck ambience under speech.
6. Add short firework-count labels and a 100-count celebration. Add domain-owned subitizing streak rewards and triple-score bonus rounds; use selectable ball colors and a dedicated pearl material. Diagnose and repair Cannon Hop with native pointer/keyboard acceptance coverage.
7. Validate incremental speech generation, catalog completeness, both engine builds and domain regressions. Exercise all affected games in the Windows player, inspect logs and screenshots, publish a PR, and test the GitHub-generated installer.

## Verification principles

Use actual player input paths and owned-process native acceptance scripts. Preview shortcuts complement tests but do not prove shipping input behavior. Keep generated diagnostics private and publish only implementation and test conclusions. Inspect final visuals and audio, including muted/low-volume behavior; never equate compilation with completion.


## Added user scope, September 14

The supplied animated CC BY polar bear replaces the static bear adaptation. Import the verified animated wolf from the WildMesh pack under the user-approved noncommercial exception and maintain `NONCOMMERCIAL_ASSETS.md/.json`. Inspect/import the supplied CC BY fishing-boat/shark vignette.

Add Dinosaur Speller as stable mode 12, leaving previous IDs unchanged and keeping the legacy picker at its existing twelve games. Use a separate ground-navigation domain model with the shared LetterCourse. Unity owns the prehistoric world (close chase by default; F1 cycles far chase and first person), source-animated dinosaur actors, heavy footstep audio/camera impulses, Ctrl roar and a bounded screen distortion; gentle-motion reduces those effects. Use the original CC0 Quaternius dinosaur pack: the supplied T-Rex describes itself as a Turok character and is not cleared for redistribution. Normal builds need no Blender/Python.

## Rich living environments

Prioritize visual and audio richness over install size. Wildlife needs sensing, avoidance, approach/escort and retreat states, bounded by habitat and safe player paths. Build occasional encounters with anticipation and aftermath (fish scatter before a shark, companion birds, roadside cheering). Add motion-linked dust, bubbles, water disturbance, lighting shafts and mist, with gentle-motion support. Keep all noncommercial asset entries traceable for replacement or future separate licensing.

Prehistoric scenery: prefer authored tree ferns, cycads and prehistoric vegetation; compatible tropical foliage at larger scale is an approved fallback. Compose cliffs, mountains, cloud banks and a volcanic landmark with occasional distant eruptions. Use an actual source-animated pterosaur for peak thermals when available. Keep learning targets readable.
