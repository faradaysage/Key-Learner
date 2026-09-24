# LeapPad development and validation

Status: a successful build or CI artifact is a candidate, not physical LeapPad
verification. After targeted local checks pass, push a dedicated branch and open
a draft PR. Verify the Windows and ARMv7 artifacts from its successful CI run;
use that CI APK for the physical Model 6022 test. Do not merge or mark ready before
the user reports the physical result.

## Shared project and build

Use the repository's Unity **6000.6.0f1** project in `UnityPort`. Install Android
Build Support with its SDK, NDK, OpenJDK and CMake child modules. Keep the existing
Windows Mono project/build and installer commands.

```powershell
unity install-modules -e 6000.6.0f1 -m android --child-modules --accept-eula --yes
./scripts/build-unity-android.ps1 -Version 3.1.0 -VersionCode 1
```

Pass `-UnityCli` if `unity` is not on PATH. Python 3 and the existing .NET SDK are
required for staging and the shared domain build. Use the same semantic version
as the Windows artifact. Increase Android VersionCode for published updates.

The wrapper rebuilds the shared domain, verifies every selected narration WAV
against its catalog SHA-256 and mono/24 kHz/PCM16 contract, temporarily stages
Android assets, invokes `KeyLearner.Unity.Editor.AndroidBuild.Build`, then restores
Windows StreamingAssets in a finally block. It never modifies `Content/Voice`.
An interrupted wrapper can be recovered before another build with:

```powershell
python scripts/stage-unity-android.py restore
```

Staging lives under ignored `UnityPort/Assets/KeyLearnerAndroidBuild` and a journal
under `UnityPort/Library/KeyLearnerAndroidStage`. Do not commit staging, SDKs,
AVDs, keystores, generated APKs or build reports. Do not run Windows and Android
builds concurrently against this checkout while staging is active.

## Voice selection and memory

Blake (`kyutai-Blake`) is mandatory and is the Android default and fallback.
`-Voices` accepts a comma-separated list of completed manifest voice IDs. Unknown
or unfinished voices are rejected; Butter and Lake are not included by directory
scanning. Adding another completed voice does not require a project fork.

Android copies are Unity-imported with Vorbis quality 0.65, mono, 24 kHz,
CompressedInMemory, preloadAudioData=false and loadInBackground=true. This is the
initial quality setting and must be judged using the actual build/audio test.
Canonical WAVs and the Windows manual-WAV playback path are preserved.

The registry reads only JSON TextAssets and a string resource-path index at
startup. Narration uses `Resources.LoadAsync<AudioClip>` for the queue heads,
waits for audio data, and unloads imported speech after its final playing lane
finishes or playback is cancelled. Metadata holds no AudioClip references.
Effects use a separate, small session cache. No Addressables or AssetBundles are
required for this implementation. Check cancellation, overlapping playback and
repeated activity transitions under the memory test; compilation alone does not
prove these guarantees.

## Required runtime gate

Use API 30 Google APIs x86_64 revision 16 with ARM translation. The current local
AVD is `KeyLearner_API30_ARMTranslation`, serial `emulator-5556`. Leave unrelated
emulators alone. The initial API 29 ARM attempt cannot run on this Intel host;
physical Model 6022 testing replaces that separate gate by user instruction.

Validated emulator startup configuration:

```powershell
$env:ANDROID_AVD_HOME = Join-Path $PWD '.local/android-avd'
$env:ANDROID_SDK_ROOT = Join-Path $PWD '.local/android-sdk'
$env:ANDROID_HOME = $env:ANDROID_SDK_ROOT
& .local/android-emulator-probe/emulator/emulator.exe `
  -avd KeyLearner_API30_ARMTranslation -port 5556 -no-window -no-snapshot `
  -no-boot-anim -memory 1024 -gpu host -feature GLESDynamicVersion -qemu -m 1024
```

The guest, not just config.ini, must report API 30, ARMv7 in its ABI list,
libndk_translation.so, 1024x600, roughly 1 GB RAM and usable GLES 3.1 or later.
The emulator otherwise raises RAM to 2 GB on this image. This host's measured
MemTotal was 997976 kB with the QEMU override. The host GPU differs from the
LeapPad GPU; no emulator result substitutes for physical graphics/audio testing.

Install the **actual ARMv7 APK**, not an Intel substitute. Use adb touch taps
and swipes for the entire child/parent flow. The original acceptance scope
completed a round in all 13 activities on an intermediate APK,
including a word in Smash. By the user's revised instruction, final system-bar
and static-font fixes receive targeted regression checks; do not restart every
lengthy all-game scenario unless they expose a new functional problem.
The broader acceptance evidence covers rapid taps, menu/resume, restart, game switching,
parent hold, settings persistence, parent exit, background/resume and process
termination/relaunch. Record screenshots, logcat and repeated `dumpsys meminfo`
samples. Report peak PSS, changes across repeated switches, GC/decode errors,
stalls, OOM/LMK events and crashes. Investigate translation limitations before
changing production behavior to accommodate the emulator.

## APK inspection and reporting

```powershell
python scripts/inspect-leappad-apk.py artifacts/leappad/KeyLearner-LeapPad-3.1.0.apk
```

`--aapt` can point to the SDK build-tools executable. The build report lists
Unity-attributed packed asset bytes and the inspection reports real ZIP entry
sizes, native ARM ELF payloads, manifest, permissions and SHA-256. Archive
uncompressed size is not installed size: measure the installed APK, extracted
libraries/dex and app data on the emulator, with physical app-storage observation
when available. Report each included pack separately and the largest overall
contributors. Preserve Blake and investigate unexpectedly large results.

The workflow's Android job uses the existing Unity preflight/version and successful
Windows test/build job, a pinned Android editor image, and the same shared-domain
input artifact. Workflow syntax checks do not establish that CI has executed.
APK signatures must be verified before handing off updates: the package and
certificate must match the earlier installed candidate. Keep signing material
out of git and never paste passwords into logs or reports.

After targeted emulator/static/Windows checks pass, push and open a draft PR.
Verify that GitHub Actions produces both Windows and ARMv7 artifacts, then supply
the CI APK's exact run/artifact name, filename, checksum and download instructions.
Stop for the physical Model 6022 result; keep the PR in draft and do not merge.
A local APK is engineering evidence, not the physical handoff candidate.


## Shared navigation and pause contract

`Suite.OpenGameMenu()` is the semantic navigation action for Escape, Android
Back (when exposed), and the visible Android Menu button. All activities share
one overlay. Choose Another Game uses the existing picker in the same process.
Restart discards the current transient activity session and re-enters it;
learned profile progress is retained.

The overlay freezes the gameplay clock and `Time.timeScale`, clears held input,
and pauses audio without discarding queued narration. Resume restores playback
and continues the round. Android background/focus loss opens the same overlay;
input focus resets must not call the destructive activity Suspend operation.
Switching activities calls Exit, cancels audio, and releases per-game objects.
The imported speech cache releases clips once neither queues nor playing lanes
reference them; completed abandoned asynchronous requests are unloaded as well.

Parent Options requires a three-second single-finger hold inside the large
button. Leaving it, adding another finger or losing focus cancels the hold.
The parent view provides narrator/sound, learning options, about/licenses,
confirmed word-habit reset, Save and return, and Exit KeyLearner.

No runtime check is satisfied by the desktop `--touch-mode` preview. That flag
is useful for layout development only; the required navigation and narration
tests must run through injected touch events on the actual production ARMv7
APK in the API 30 emulator and then on the physical Model 6022.


## Android font atlases

Android stages separate copies of Fredoka and Baloo and imports a fixed character
set at 96 pixels with font data excluded. Windows keeps its original dynamic
fonts. The character lists are checked into
`UnityPort/Assets/KeyLearner/Editor/android-font-characters.json`; include any
new supported characters there before shipping translated UI. The build checks
that both generated fonts are static and reports their atlas dimensions.

The UI draws atlas glyphs directly at each label size with a bounded layout cache;
Unity 6.6 IMGUI otherwise attempts to recreate a runtime TextCore face even
for a static font. The build also repacks all UI glyphs upright into a white-RGB, alpha-preserving
atlas on the CPU, so headless CI does not need a GPU. A small generated UV index
allows direct destination-rectangle drawing without per-character GUI matrix
changes. Glyphs use a dedicated `KeyLearner/BitmapUi` material through
`Graphics.DrawTexture`, with explicit alpha blending, depth writes disabled and
`ZTest Always`. This isolates UI text from game rendering state. Validate it under
animated effects; the earlier shared IMGUI material intermittently lost captions
in the ARM-translation emulator. Packing uses a 1024-pixel width and a power-of-two height, capped at
4096; the actual dimensions and packed size are reported by each build. The
RGBA atlas has no retained CPU copy at runtime. 3D text uses
the original imported font atlas through TextMesh. This avoids runtime glyph
rasterization for normal Android labels and bounds atlas memory. It is being
validated against the API 30 ARM translation crash observed in Unity's
`gray_convert_glyph_inner` path; a successful build alone does not establish
that the workaround resolves the runtime gate. Inspect small labels, large
letters, credits and 3D text on the final APK and physical device.


Runtime primitive creation must retain `MeshFilter`, `MeshRenderer`, and the
sphere/box/mesh collider types in `link.xml`. `CreatePrimitive` requests colliders
internally even where the caller immediately removes them. Editor tests alone
cannot catch native type stripping; verify production logcat during all games.

Android credits use fixed-height text pages and large Previous/Next buttons.
This bounds per-frame glyph work and avoids rotated font-atlas glyph transforms
inside IMGUI scroll clipping groups. Windows credits retain their scroll view.

Android builds explicitly hide system-bar insets and use
`ShowTransientBarsBySwipe` (Unity 6.6). This leaves all bottom controls accessible
while allowing an edge swipe to reveal Home/Back temporarily on firmware that
provides them. Normal child navigation still uses KeyLearner's Menu control.
See [Unity system-bar documentation](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/developing/manage-system-bars).

`AndroidSystemUi.Apply` also reapplies this behavior on Android's UI thread after
startup and resume. The API 30 emulator otherwise consumed the first touch to
reveal a persistent navigation bar despite the packaged settings. Android 10
uses immersive-sticky view flags; API 30+ additionally uses WindowInsetsController.
This is lifecycle-driven, not a per-frame JNI call. Edge-swipe system navigation
remains available and must be included in the targeted emulator checks.
