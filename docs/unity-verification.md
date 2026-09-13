# Unity Windows verification - 2026-09-13

The Unity project and Windows release player contain all twelve minigames, the parent interface, licensed offline art/audio and the shared save/learning rules. This record separates observed acceptance from remaining engine and hardware risks. [Actual gameplay gallery](unity-gallery.md) and [reproducible commands](../UnityPort/README.md) accompany it.

## Build and rule checks

| Check | Observed result | Evidence |
| --- | --- | --- |
| Preserved MonoGame Release build | Passed, 0 warnings/errors | `artifacts/unity-migration/monogame-preservation-build.log` |
| Original regression harness | 1,193 passed | `artifacts/unity-migration/monogame-model-regression.log` |
| Shared .NET Standard domain | The same 1,193 regression checks passed against the shared DLL | `scripts/build-unity-domain.ps1` |
| Actual Unity bundled Mono and BCL | 36 passed: JSON identity/atomic backups, malformed data fallback, external image/WAV paths, first-run CSV content, catalog, math and physical/touch state | `artifacts/unity-migration/mono-domain/runtime-smoke.log` |
| Unity runtime + Editor C# | 0 warnings/errors against actual installed Editor/URP references; Editor recompile completed without errors | `scripts/test-unity-scripts.ps1`, `artifacts/unity-migration/editor.log` |
| Unity Windows build | Succeeded, 0 errors, 260,091,692 bytes; build summary 24.94 seconds | `artifacts/unity-migration/build-result.txt` |
| Source asset integrity | 396 of 396 manifest hashes match | `artifacts/unity-migration/source-asset-integrity.json` |
| Authored content overrides in Unity | 7 passed: replacement, append, malformed rows, metadata, rebuild preservation, cache invalidation and missing legacy overlay | `artifacts/unity-asset-research/authored-overrides-verification.txt` |
| Native disarmed platform/speech probe | Passed foreground/desktop callbacks, empty disarmed state, two offline voices, zero-volume completion and clean shutdown | `tools/KeyLearner.PlatformProbe` |

The final build uses the locally discovered Unity **6000.6.0f1**, URP **17.6.0**, Windows x64 **Mono**, **Release** managed code and `BuildOptions.None`. Pipeline is disabled in player builds. There is no development watermark or runtime Editor command server. Shader build/cache warnings from intermediate experiments are retained in the historical Editor log; they are not silently removed to make a clean-looking report.

## Actual Windows player coverage

The comprehensive runner renders all twelve modes and 21 deterministic behavior/effect scenarios. It checks active gameplay duration, exit status, screenshots, runtime error collection, scoring/progression and log errors. A late corrected fixture uses `qqqzqqq` to test repeated-key balloons: the earlier `aaabaaa` fixture legitimately recognized the default word `baa` and cleared balloons. Recognition behavior was kept; the misleading test fixture was changed.

Final full-suite acceptance: **33 of 33 cases accepted** on the same final Windows binary. The complete run passed 32; the repeated-letter case then passed in an isolated fixture rerun. Its `Q -> Z` jump had also legitimately triggered Sweep, so only that disposable profile now sets `GestureEffects=false`; dedicated gesture tests remain enabled. `artifacts/unity-acceptance/consolidated-results.json` retains the original failure, corrected result, source paths and matching runtime assembly hashes. Neither production rules nor the binary changed for the fixture correction.

| Area | Additional observed checks |
| --- | --- |
| Smash Garden | Rapid exact word recognition; external picture fixtures; repeated-key balloons and popping; broad/cluster gestures; left-first cannon hit and bounded miss; right-button local blast; transparent glass; fused liquid; source fire animation. |
| Word Adventure | Word balloons, all-clear score 100, guided completion, patient delayed prefix resolution, reusable camera celebration. |
| Counting Celebration | Partial-number sequencing through 25; bounded scheduled rocket rewards. |
| Sky / Racer / Ocean | Shared course/collection and score behavior; actual right-arrow steering projection; forest, lakes, mountain, town, city and underwater camera inspection; source animation and streaming. |
| Dot Pop | Correct/retry progression to stage 2, portrait/landscape hit mapping, right-button rejection and delayed narration/reveal phases. |
| All five math activities | Correct completion to stage 2, retries, frame/number-line layouts, actual pointer answers and Make the Number rapid-second-press suppression. |
| Parent/picker/focus | 24 assertions and 15 screenshots passed in `artifacts/unity-ux-final`: all nine parent tabs, credits, picker filters/pages/keyboard choice, isolated settings persistence, native minimize/restore, unfinished-edit cancellation, Escape reminder/Back contracts. |

Final native pointer acceptance passed all **7 cases**, including forced report locks in both Dot Pop aspect ratios (`artifacts/unity-pointer-acceptance/results.json`). Final native steering passed with 14 acknowledged held-key samples, **463.96 pixels right** relative to the road center and acknowledged release (`artifacts/unity-steering-acceptance/result.json`). The diagnostic lock test deliberately denies report replacement for 500 ms while a real mouse answer arrives. The game must reach reward and continue without an exception after the reader releases the file. Preview report IO errors are caught and retried; the written-state signature advances only after a successful atomic replacement. These diagnostic paths are unavailable to normal protected play.

Additional completed visual/behavior variants: early glass at 2 seconds (16 transparent panes); half-resolution liquid field (614 x 384 versus 1229 x 768); glass-shader disabled; monochrome Dot Pop; three sound-enabled word/math cases. Sound-enabled capture verifies actual playback/synthesis and absence of errors, but is not a human listening evaluation of every voice/recording.

Every runner uses a unique disposable profile and compares hashes of the five real parent save files before and after. The mouse/arrow helpers act only while their own preview window is foreground, release held input and restore the cursor. They never synthesize parent authorization or arm protected hooks.

## Visual and performance review

Seven final explorer captures passed and were visually inspected: Sky forest, Sky city, Sky mountains, Racer forest, Racer city, Racer town and Ocean. See `artifacts/unity-final-explorers`, `unity-final-mountains`, `unity-final-city`, `unity-final-town` and `unity-final-sky-city`. A separate lake route was also inspected. The gallery includes each educational game.

Rendering was iterated after actual camera inspection. Corrections included skinned bounds/axis normalization, preserved source atlas colors and alpha cutouts, complete road-furniture categories, driveway width scaling, bird/fish locomotion, grounded coral/outcrops, proper garden-fence height, neighborhood paving, mixed canopy bands, readable gold targets, UI panel tint, transparent glass, liquid resolution, filled source-fire interiors and bounded atmosphere effects. Models, silhouettes and composition were improved directly before finishing color/effects.

Measured explorer conditions: Windows 11 session, NVIDIA Quadro RTX 4000 (8 GB), Direct3D 11, 1366 x 768, render scale 1, terrain detail 64, MSAA/SMAA enabled, VSync requested, Gentle Motion off and Flight Assist on. Final explorer 95th-percentile frame intervals were approximately **4.9-6.72 ms** over eight active seconds. The final accepted cases lasting at least three seconds ranged **3.784-10.497 ms**. The two-second repeated-letter fixture is excluded from this range because the timing sampler omits startup.

Unity reported unreliable VSync/frame-statistics timestamps in this desktop session and used CPU timestamps. These are observed short-run frame intervals with rendering headroom, not a promise of physical refresh rate, a long-duration memory soak or tested performance on the children's laptop. Animated offscreen content is culled/limited, and streamed scenery is released with its parent; lower detail and Gentle Motion remain available.

## Native crash investigation

One final-QA Dot Pop landscape process crashed in native Unity before answering. Its dump records `0xC0000005`, reading null address `0x0`, in `UnityPlayer.dll+0x109CD3F`. The exact installed nondevelopment Mono PDB resolves the chain to `core::vector<LocalKeywordInfo,...>::assign` -> `ShaderRuntime::Variants::VariantUploadData` -> `ShaderVariantMonitor::FrameFinished` -> frame-render completion. The symbol identity matches the dump. This is separate from the managed preview report-lock exception, which was reproduced and fixed.

Evidence: `artifacts/unity-pointer-final/mode-6-1366x768/player.log`, `artifacts/unity-asset-research/native-crash-symbols.txt`, `native-crash-dump.json`, and `native-shader-monitor-investigation.md`. The Windows crash folder is recorded in those notes. Automatic collection tracing is already `None`, cache-miss tracing is off, the player uses Release managed code, and game source does not explicitly warm or unload shaders. The saved Send to Editor flag is inactive with tracing disabled, as described in [Unity's Graphics Settings reference](https://docs.unity3d.com/6000.6/Documentation/Manual/class-GraphicsSettings.html).

No documented global native monitor disable or matching confirmed fix was found. No speculative Graphics Jobs/driver/antivirus changes were made. **All 10 subsequent serial Dot Pop landscape launches passed without another native crash** (`artifacts/unity-dot-stability/run-01` through `run-10`), as did both final native pointer orientations. Each stability run includes 18 active gameplay seconds, a real mouse answer, captured diagnostics and checked exit/log state. Clean repeats do not establish that the native engine defect has been fixed. Installed CLI release discovery on 2026-09-13 lists no newer stable 6000.6 patch; the raw result is retained at `artifacts/unity-asset-research/stable-unity-releases-2026-09-13.json`. Retain this as a release risk when evaluating the build on the target laptop or a future Unity patch.

## Installer and remaining hardware acceptance

Final package/upgrade acceptance: **passed MonoGame 2.0.27 -> Unity 3.0.0**, with one registration/same directory, correct real-profile Parent Studio shortcut, all **300** obsolete files removed, unknown custom content preserved, all five real profiles unchanged and successful scoped uninstall (`artifacts/unity-installer-acceptance-final/result.json`). The baseline installer hardcodes its Start menu folder; the initial test assumed `/GROUP` was honored. That failed test and its successful cleanup are retained separately, and the corrected test checks ownership by exact shortcut target before using either preflight-absent folder. Packaging uses version 3.0.0, the existing per-user AppId/install path and real save directory, an explicit real-profile Parent Studio shortcut, offline runtime/art/audio notices, and no private dictionary/debug payload. The isolated test uses MonoGame 2.0.27 as baseline, verifies obsolete exact files are removed while unknown custom content and real profiles survive, then uninstalls only its own installation.

The **actual Unity accessibility lease and packaged watchdog passed** normal disposal and forced-owner-exit restoration (`artifacts/unity-accessibility-acceptance/result.json`). The runner compared full native accessibility structures, verified that only shortcut/confirmation mask `0x0c` changed, observed the exact packaged guardian, and confirmed final structures and real parent profiles were unchanged. It used early headless diagnostics with no keyboard hook. Physical parent chords, full keyboard/touchpad mashing and secure desktop recovery still cannot be inferred from these diagnostics or previews. Those target-device acceptance limits remain explicit in [the platform guide](unity-platform.md). The guard is not an OS kiosk. No outstanding paid choice, authentication or manual art download is needed for the delivered content.

The final installer is **81,628,898 bytes** with SHA-256 `9a2c5a580d9c78209fa25c5d56819a7740ba810430fade4c3d3a465ec897bab9`. The [portable acceptance record](verification/unity-2026-09-13.json) preserves the case results, evidence provenance and runtime assembly identities. Raw logs/dumps remain local under `artifacts`; no external issue report or remote publication was sent.

## CI protected-input correction (September 13, 2026)

[Run 26](https://github.com/faradaysage/Key-Learner/actions/runs/34774413628) passed all four hosted jobs for runtime source `1f84889`: 40 EditMode and 8 PlayMode tests, original/shared regressions, Windows Mono build and same-run MonoGame-to-Unity upgrade/uninstall. Installer acceptance includes fullscreen Play and windowed Parent Studio shortcuts. The pipeline summary identifies the single INSTALL artifact needed for installation; Portable is optional and the CI artifacts support packaging and tests.

The downloaded CI player passed three actual protected minimize/restore cycles, cursor/accessibility release, resumed capture, inactive close and unchanged parent files (`artifacts/github-ci-34774413628/protected-focus-v2/result.json`). An initial cold-start check timed out before the player finished starting; the corrected runner allows 30 seconds for startup independently of its six-second transition waits. Neither this native focus check nor the two passing focus-race domain fixtures establishes physical keyboard/parent-chord acceptance. The user-reported hardware input failure still needs confirmation on their device, and the native engine risk above remains open.
