# Unity migration, 2026-09-13

The Unity implementation is a separate, runnable Windows application in [UnityPort](../UnityPort/README.md). All twelve minigames have Unity presentations over the existing C# learning and state models. The original MonoGame application remains buildable in place. [Gameplay gallery](unity-gallery.md), [verification record](unity-verification.md), and [asset credits](../THIRD_PARTY_ASSETS.md) describe the delivered result and its acceptance limits.

## Discovered baseline

The actual repository root is `D:/projects/KeyLearner/KeyLearner`, on `codex/visual-dot-math`, starting at `bb7e5fd`. Tracked source was initially clean; the handoffs and asset inbox were user-supplied. `PROJECT_HISTORY.md`, `README.md`, and `CODEX_UNITY_PORT.md` were read completely before changes. Current source resolved conflicting historical UX descriptions.

The installed tools were Unity 6000.6.0f1, Unity CLI 1.0.0-beta.8, Windows x64 Mono build support, .NET SDKs, and Inno Setup 7.1.0. CLI discovery, the existing license and a project-specific Pipeline connection were used. Unrelated open Unity projects were left alone. The new URP 17.6 project is pinned by its project version and package lock. No paid services or asset subscriptions were used.

## Architecture and extension boundaries

The migration was planned around shared rules and separate presentation before structural changes:

- `Shared/KeyLearner.Domain.csproj` links the original `Studio` models into .NET Standard 2.1. MonoGame and Unity compile the same learning, gesture, physical-key, movement, scoring and JSON contracts. Compatibility changes replace newer convenience APIs with equivalent operations; Unity uses its actual bundled System.Text.Json 8 runtime.
- `Minigame` defines lifecycle, key/pointer delivery, suspension, UI and diagnostics. `MinigameRegistry` explicitly registers all twelve factories. Canvas, explorer and quantity presentations live in focused classes. Add a model, stable catalog ID and presentation factory without changing unrelated engine plumbing; see [extension guidance](unity-domain.md).
- `GameServices` provides content, audio, rewards, camera fitting, settings, session state and a bounded active gameplay clock. Physical authorization has its own monotonic time. One aspect-fit coordinate mapping serves both drawing and pointer hit tests. Shared camera feedback and effects honor Gentle Motion.
- Unity's Windows adapter owns the real HWND, input desktop checks, immutable physical snapshots, stale-event limits, exact parent authorization, accessibility lease/watchdog and focus resets. Preview and standalone Parent Studio are unprotected. [Platform contracts](unity-platform.md) explain the boundary and remaining hardware acceptance.
- Original licensed meshes, textures and rigs stay under `Assets/ThirdParty`. Editor APIs build normalized reusable prefabs/materials with stable GUIDs. `ContentLibrary.AuthoredItems` allows persistent authored overrides, and Configure preserves an existing authored scene. [Asset authoring](unity-assets.md) identifies precisely which generated files are overwritten.

## Behavior retained

Numeric mode IDs, five JSON save files, first-run CSV import rules, external family images/WAVs, word-prefix learning, typo bounds, gesture calibration separation, scoring, math progression, retry timing, guided spelling and explorer physics remain shared. Ordinary Escape follows current source: a parent reminder in canvas/explorer/Dot Pop games, and the existing Back route in math. Parent holds/tap escapes and double-G are independent of minigame input consumption. Mouse buttons, repeated-key balloons, left-first cannon hits and right-button local blasts follow the implemented MonoGame behavior.

The installer keeps AppId `{D5C654D0-1B14-4479-B771-20BD264731A8}`, taskbar ID `KeyLearner.Desktop`, the per-user install location, and `%LOCALAPPDATA%/KeyLearner` saves. The Parent Studio shortcut now explicitly selects that real profile while staying unprotected, fixing the previous disposable-preview-profile regression. An exact legacy payload list cleans obsolete MonoGame dependencies during Unity upgrades without broad file deletion. Private dictionaries, local tools/models and diagnostic profiles are excluded.

## Visual implementation

Eleven selected licensed packs contribute 396 source files and 223 reusable adapted prefabs. Actual imports include a rigged bird, cars, several building and tree families, farm landmarks, nineteen marine models, 35 intact coral organisms extracted from six supplied display sets, and seaweed. Original palette atlases, foliage cutouts, coral textures, rigs and calm locomotion clips are preserved. All assets are local and attributed; no manual download remains outstanding. The supplied mushroom is the same geometry, UVs and texture as the already integrated creator CC0 source, and is deliberately deduplicated.

Sky Speller uses layered terrain, woodland edges, clearings, farms, water, settlements, mountain outcrops, animated bird flocks and soft clouds. Letter Racer uses region-specific frontages, connected paving, setbacks, driveways, garden fences, parked cars, roadside planting and distant skylines. Ocean Speller uses grounded coral gardens, sand openings, seaweed, fish schools at several depths, calm larger animals, caustics, bubbles and suspended motes. World-anchored streaming and deterministic composition keep these reusable without changing learning objectives or adding hazards.

URP uses linear color, HDR, MSAA/SMAA, soft cascaded shadows, depth, fog and restrained bloom/grading. Educational games retain clean layouts, readable fonts and quantities. The canvas has source-derived fire, fused liquid, transparent glass, word balloons, pictures and bounded celebration effects. Actual player screenshots drove successive corrections to scale, rig bounds, silhouettes, source palettes, ground contact, fence height, neighborhood composition, terrain, target size, UI tint and effect appearance.

## Acceptance and limits

All twelve games have been built and launched in the Windows player. Original and shared model checks, actual Unity Mono persistence checks, native pointer/focus/steering tests, UI flows, visual inspection and packaging evidence are tracked in [the verification record](unity-verification.md). Build output is `artifacts/unity-windows/KeyLearner.exe`; the installer output is `artifacts/installer/KeyLearner-3.0.0-win-x64-setup.exe`.

One isolated native Unity shader-variant-monitor crash was captured during final Dot Pop QA. Its exact stack and subsequent stability runs are retained; a passing replay is not presented as a confirmed engine fix. Actual normal and crash-time accessibility lease restoration passed against the packaged watchdog. Physical laptop keyboard/touchpad containment and secure desktop transitions remain separate hardware acceptance. The application is a Windows session guard, not an OS kiosk. See the verification record for current outcomes rather than treating this checklist or a compiling project as blanket acceptance.
