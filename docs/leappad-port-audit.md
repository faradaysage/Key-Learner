# LeapPad port audit and validation report

Updated 2026-09-24. One Unity 6000.6.0f1 project serves Windows and Android.
Final targeted Android validation passed. The authorized next step is a
**draft PR**, successful Windows/Android CI, and handoff of the **CI-produced APK**.
Physical LeapPad Academy Model 6022 verification remains pending; do not merge,
mark the PR ready, or describe the build as LeapPad-verified before that result.

## Architecture and platform isolation

Windows retains its x64 Mono player, platform helper, protected keyboard behavior,
original dynamic fonts, raw narration source/staging, and installer workflow.
Android uses IL2CPP, ARMv7 only, package `org.keylearner.app`, minimum SDK 29,
target SDK 36, landscape, GLES 3.1, and internal application storage. Profiles,
settings and progress use `Application.persistentDataPath` on Android. Native
Windows calls remain platform-guarded. No Google Play service or network is
required for normal play; the APK has no INTERNET or shared-storage permission.

`stage-unity-android.py` verifies selected source catalogs, SHA-256 hashes and
mono/24 kHz/PCM16 WAVs. It journals and temporarily replaces Windows staging with
Android-only imported assets, then restores it in the build wrapper's `finally`.
Canonical `Content/Voice` files are never modified. The Android build rejects
Windows helper/raw voice staging. `inspect-leappad-apk.py` checks the actual ZIP,
manifest, permissions, ARM ELF libraries, signature, alignment, raw-WAV exclusion,
size and checksum. This is one shared project, not an Android fork.

## Narration audit

The previous Windows pipeline copies WAVs into StreamingAssets and manually
loads PCM data through `AudioClip.Create`/`SetData`. Such files bypass AudioImporter
compression. Its bounded decoded cache and Windows playback path remain intact.
Android therefore stages separate importer-managed copies under Resources.

| Concern | Android implementation |
| --- | --- |
| Required/default narrator | Complete `kyutai-Blake`, 4,899 verified clips |
| Optional voices | Only explicitly selected completed manifest packs; unfinished Butter/Lake excluded |
| Compression | Unity Vorbis, quality 0.65, mono, 24,000 Hz; no source-rate increase |
| Load type | CompressedInMemory |
| Preload Audio Data | Disabled |
| Load In Background | Enabled |
| Startup metadata | JSON TextAssets and string paths; no AudioClip references |
| Narration loading | Asynchronous Resources load for queued speech, then audio-data readiness |
| Release | Clear completed AudioSource references; unload unneeded speech AudioClips/audio data |
| Cancellation | Abandoned asynchronous requests release their completed assets |
| Other audio | Small reusable effects cache; no thousands of decoded narration clips |
| Bundles/Addressables | Not used; selective build staging controls included packs |

Android serializes narration so accepted letters precede the completed word.
The large learning controls reject held/repeated input while speech is pending.
Windows retains its established speech channels and fallback behavior.

## Shared touch navigation and activities

Every game exposes the same large Menu control in the reserved upper-right area.
Escape, touch Menu, and Android Back feed `OpenGameMenu`. The overlay offers Resume,
Choose Another Game, Restart Current Game, and Parent Options. Game timers, domain
clock, animations, input and narration pause together. Switching uses the existing
picker in the same process and releases the previous activity's transient state.
Restart replaces the transient activity session without erasing learned progress.

Parent Options requires a deliberate three-second hold. Voice, volume, learning
settings, reset confirmation, application information and Exit are in that area.
Children can switch games without entering it. Parent exit saves settings and
cleans up before Unity destroys audio/visual owners; cleanup is idempotent.

| Activity | Touch input |
| --- | --- |
| Smash Garden | Large target advances one letter; touch reward balloons |
| Word Adventure | Large matching-letter choices; touch reward balloons |
| Counting Stars | Large target advances one count, through the 100 celebration |
| Sky Speller / Letter Racer / Ocean Speller | Steering pad, boost and signal controls |
| Dino Discovery | Steering, boost, roar and camera-view controls |
| Dot Pop | Large answer keypad |
| How Many Now / What's Hiding | Large answer choices |
| Make a Number | Touch objects to build the requested quantity |
| Number Duel | Touch a group or Same |
| Cannon Hop | Touch the landing position; normal learning unlock retained |

## Runtime compatibility fixes

The API 30 ARM translation layer crashed in Unity's native FreeType glyph
rasterization. Android-only static font atlases avoid runtime glyph generation;
Windows fonts are unchanged. IMGUI also attempted runtime TextCore conversion,
so Android UI labels draw baked atlas glyphs directly with a bounded layout cache.
The build repacks glyphs upright and uses a dedicated overlay shader with explicit
alpha blending and depth testing disabled. The prior shared-material renderer
intermittently lost captions; the replacement retained the complete Menu caption
across 12 Smash reward frames and 20 counting-effect frames. Credits use large,
paginated text controls. Static TextMesh font-size overrides were removed to avoid
repeated warning allocations.

Runtime `CreatePrimitive` needs native collider/renderer types even when callers
remove the collider immediately. `link.xml` preserves these types; stripped builds
otherwise failed during counting despite passing Editor tests.

The software-navigation emulator exposed a consumed first touch and persistent
bar covering bottom controls. The build requests hidden system bars with transient
edge-swipe behavior. Runtime startup/resume also reapplies immersive mode on the
Android UI thread, using Android 10 flags and the API 30+ insets controller.
Targeted testing confirmed first-touch entry, transient Android Back, Home/resume,
and unobstructed bottom controls. This is not a per-frame JNI loop.

## Validation evidence and scope

- API 30 x86_64 Google APIs revision 16, `libndk_translation.so`, actual ARMv7 APK,
  1024x600, measured MemTotal **997976 KiB**. A QEMU 1024 MB override is necessary
  because the emulator otherwise raises configured RAM. Requested small storage
  was clamped to a 6 GB data image (about 5.8 GB filesystem). Software Home/Back is
  enabled for touch lifecycle tests; normal game navigation uses KeyLearner Menu.
- All thirteen activities completed touch rounds on intermediate APK SHA-256
  `5677d9e8c7fb2f556e8987a52012a4ba165df6bdd62ed29e277c4e799740108f`,
  including full Smash/Word rewards, Counting 100, all four 3D activities and all
  six number activities. Evidence: `artifacts/leappad-credits-runtime`.
- Subsequent builds exercised touch pause/resume, restart, picker switching,
  three-second parent gate, parent return/exit, and offline launch. The upright
  build completed another full 100-touch counting round. Input diagnostics report
  zero host keyboard events. Returning from Android Home presents the pause menu.
- By the user's revised instruction, final system-bar/static-font changes receive
  targeted regression checks. The lengthy all-game sequence is not restarted
  unless a new functional problem requires it. Intermediate evidence is not
  mislabeled as testing a different APK.
- Windows: **72 EditMode and 15 PlayMode tests passed**; the installed player passed
  all 13 modes and five targeted gameplay cases. Install/upgrade/uninstall passed,
  real profiles were unchanged, and an unlisted custom file survived scoped cleanup.
  `artifacts/leappad-upright-installer-test/result.json` records that full run.
  The subsequent system-bar/static-font Windows build and 15 PlayMode tests also
  passed (`artifacts/leappad-final-*`). CI will validate the final pushed source.
- Final local candidate: first-touch game entry; Smash narration/hold debounce;
  pause/resume/restart; switch to Sky Speller and use steering/boost/call; readable
  3D letters; short Counting sequence; short-tap rejection and three-second parent
  gate; Blake preview/volume/save; Android Back/Home/resume; parent-only exit and
  touch relaunch with Blake/75% volume persisted. No host keyboard was used;
  Android Back correctly appears as an input event in the diagnostics.
  Evidence: `artifacts/leappad-runtime`, tied to the APK checksum below.
  Final source Windows build and **15/15 PlayMode tests passed**
  (`artifacts/leappad-immersive-*`).
- Final logcat contains **no fatal exception/signal, OOM, AndroidJavaException,
  audio decode error, or static-font-size warning**. Unity logs a nonfatal missing
  optional `AssetPackManager` class at startup; this standalone APK does not use
  Play Asset Delivery and continued through the complete targeted sequence offline.
  No continuing memory-growth trend or narration-loading stall was observed in
  the targeted sequence; this does not substitute for physical-device measurements.
- During an intermediate configuration experiment, disabling Google Play Services
  caused the emulator's **system_server** to crash in RoleControllerService binding,
  restarting the framework and killing all apps. The log identifies that failure
  before KeyLearner's SIGKILL; it was not a KeyLearner exception or OOM. That package
  change was not repeated. Normal offline tests use Wi-Fi/mobile data disabled.

## Measured storage and memory

The final local APK is **171,139,483 bytes (163.21 MiB)**, SHA-256
`8f30a5ec09164b1fe5243b68305429d2ba55a59c6c02974a5f303e6028098faa`.
The CI handoff must use the CI APK and its own inspection checksum.
These figures are measurements, not estimated compression ratios. The CI artifact
contains `build-report.json` and `.inspection.json` for its exact APK.

| Included voice payload | Unity packed bytes |
| --- | ---: |
| Blake compressed audio | 31,755,288 |
| Blake catalog | 6,571,348 |
| Blake pack subtotal | 38,326,636 |
| Shared corpus catalog | 4,414,040 |
| All voice paths | 42,744,336 |
| Butter / Lake | 0 (not included) |

Canonical Blake WAVs total **323,972,622 bytes**, preserved in the source pipeline.
The complete source pack including metadata is 330,554,750 bytes. Windows assets
were not reduced to obtain the Android size.

Largest Unity-attributed asset groups in the measured build:

| Group | Packed bytes |
| --- | ---: |
| Voice paths including shared metadata | 42,744,336 |
| Quaternius nature assets | 33,990,600 |
| Lasqueti boat/marine assets | 29,473,892 |
| Poly Haven scenery | 29,043,176 |
| Android font assets/atlases/index | 13,664,298 |
| Polar bear assets | 6,864,428 |
| Pteranodon assets | 6,813,348 |

APK ZIP size is distinct from Unity asset attribution. Its largest entries are
`assets/bin/Data/data.unity3d` (about 103.9 MB compressed),
`assets/bin/Data/resources.resource` (about 32.7 MB), ARM `libil2cpp.so` (about
13.7 MB compressed), `libunity.so` (about 8.9 MB compressed), and `classes.dex`
(about 6.4 MB). Report exact CI ZIP entries from its inspection JSON. Installed
size additionally includes extracted native libraries, dex artifacts and app data;
measure those on the final local/physical candidate rather than equating ZIP
uncompressed size with installed size.

The representative all-game intermediate run recorded **377,961 KiB peak PSS
(369.1 MiB)** across 908 samples. The later full-counting/offline session peaked
at 336,974 KiB and ended at 300,222 KiB; the larger upright-build session including
startup/3D play peaked at 369,757 KiB. These are sampled observations, not a hard
allocation ceiling, and ARM translation/host graphics differ from the LeapPad.
The final targeted run recorded **358,912 KiB peak PSS (350.5 MiB)**
across 261 samples; the final picker sample was 321,392 KiB.
Installed usage was **241,832 KiB (236.2 MiB)**: 229,424 KiB APK/native/dex install,
56 KiB private data and 12,352 KiB external app data. Prior test diagnostics are
included. These are emulator observations; the physical device may differ.
Memory sampling with `dumpsys meminfo` triggers explicit Java GC, so its GC log
entries must not be mistaken for application allocation pressure.

An intermediate ten-second emulator counting recording at system media volume 15/15 measured
**-4.3 dBFS peak, -30.9 dBFS mean**. Its capture mix is stereo/44.1 kHz, while
narration remains mono/24 kHz. The user authorized transfer for listening review,
but this agent runtime cannot process audio input; no subjective listening-quality
claim is made. The final candidate's ten-second Smash recording measured
**-7.1 dBFS peak, -33.2 dBFS mean**, confirming output during Blake narration.
Physical testing must confirm intelligibility and pleasant playback.

## CI and physical handoff

The Android job shares the Windows preflight/version and prepared-domain artifact,
uses the pinned Unity 6.6 Android image, stages only completed selected voices,
builds ARMv7, inspects the actual APK, restores staging and uploads the candidate.
The existing Windows artifact remains required. The draft PR is based on the
existing narration branch so it does not replace that open narration PR.

After both CI outputs succeed, download and inspect the CI APK, report the draft
PR URL, source commit, workflow run, artifact name, exact APK filename and SHA-256,
and supply `LEAPPAD-INSTALL-INSTRUCTIONS.md`. Stop for the user's authoritative
physical Model 6022 result. Do not merge or mark ready while that result is pending.
