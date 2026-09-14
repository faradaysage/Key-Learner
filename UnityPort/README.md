# KeyLearner Unity port

This Unity project sits alongside the preserved MonoGame implementation. The selected Editor version is recorded in `ProjectSettings/ProjectVersion.txt`; this checkout was created and compiled with the locally installed Unity 6000.6.0f1 and URP 17.6.0. Use the project version and the installed CLI's discovery output on another machine rather than assuming a drive or Editor path.

## GitHub Actions

The repository workflow [windows-installer.yml](../.github/workflows/windows-installer.yml) stages the shared DLL and Windows helper, runs actual Unity EditMode/PlayMode tests, builds the Windows Mono player with GameCI, and packages/tests the installer on GitHub-hosted runners. See [Unity CI setup](../docs/unity-ci.md) for Personal-license secrets, cache keys, versioning, downloadable artifacts, and fork behavior. Local verification and CI are complementary; a prior local build is never uploaded as the output of a new CI run.

## Build on Windows

From the repository root, prepare the shared models, platform helper, and original offline audio:

```powershell
./scripts/build-unity-domain.ps1
./scripts/build-unity-platform.ps1
```

The domain build runs all 1,193 original regression checks against the engine-independent DLL. The helper build uses the highest installed .NET 8 runtime patch, or accepts `-RuntimeVersion` to pin one. It stages a self-contained Windows speech/accessibility helper and locally bundled audio. Piper and its model remain optional parent-owned tools and are not distributed.

Find the CLI using `Get-Command unity`; if a fresh terminal has not picked up its PATH entry, the observed Unity Hub CLI location on this machine is `$env:LOCALAPPDATA\Unity\bin\unity.exe`. These are read-only discovery commands:

```powershell
$project = (Resolve-Path ./UnityPort).Path
$cli = (Get-Command unity.exe -ErrorAction SilentlyContinue).Source
if (!$cli) { $cli = Join-Path $env:LOCALAPPDATA 'Unity/bin/unity.exe' }
& $cli --version
& $cli editors --installed --format json
& $cli status --format json
& $cli pipeline list --format json
```

If this Unity project is already open, target its absolute path on every Editor command and keep using that Editor. Do not launch a batch Editor against the same project. The project ships `ProjectSetup.Configure`, `BuildWindows`, and `ConfigureAndBuild` Editor entry points. The first two also have **KeyLearner** menu items; `ConfigureAndBuild` runs both in order. `Configure` creates the initial scene when absent, preserves an existing authored scene, and refreshes generated content and project/build settings. Keep custom prefab/material variants outside generated folders; see [the asset pipeline](../docs/unity-assets.md). `BuildWindows` produces a normal release player, without the development watermark or a runtime Editor control server.

For a build with this project closed, select the installed version that matches the project, then use the official Editor batch interface:

```powershell
$project = (Resolve-Path ./UnityPort).Path
$version = (Get-Content ./UnityPort/ProjectSettings/ProjectVersion.txt |
    Select-String '^m_EditorVersion: (.+)$').Matches.Groups[1].Value
$installed = & $cli editors --installed --format json | ConvertFrom-Json
$editor = ($installed.data | Where-Object version -eq $version | Select-Object -First 1).location
if (!$editor) { throw "The project's Unity $version Editor is not installed." }
$logDirectory = Join-Path (Split-Path $project -Parent) 'artifacts/unity-migration'
New-Item -ItemType Directory -Force $logDirectory | Out-Null
$log = Join-Path $logDirectory 'windows-build.log'
& $editor -batchmode -projectPath $project -executeMethod KeyLearner.Unity.Editor.ProjectSetup.ConfigureAndBuild -quit -logFile $log
```

The Windows Mono player is written to `artifacts/unity-windows/KeyLearner.exe`. `artifacts/unity-migration/build-result.txt` records the actual Unity build result, error count, size, and elapsed time. Check both that report and the Editor log. A saved screenshot or old executable is not evidence that the latest build completed.

For the already-open project, discover the installed Pipeline's commands before using them:

```powershell
& $cli command --project-path $project --format json
& $cli command recompile --project-path $project --format json
& $cli command recompile_status --project-path $project --format json
```

Wait until recompile status is completed without errors, then use the **KeyLearner / Build Windows player** menu, or the discovered `eval` command with `--detach`. Long Editor work can continue after the experimental CLI's short evaluation timeout, so do not automatically retry and submit a second build. Observe the build result/log and the actual Editor operation. Existing Editor sessions and unrelated projects should be left open.

An optional PowerShell orchestration wrapper was attempted during migration. The local antivirus/AMSI rejected it at parsing, including its read-only discovery option. It was removed and replaced by the direct supported commands above. No antivirus exclusions, bypasses, or settings changes were made. See [the build environment record](../docs/unity-build-environment.md) for the concrete diagnostics.

## Run and verify

```powershell
# Windowed inspection; automatically creates a fresh disposable profile.
./artifacts/unity-windows/KeyLearner.exe --preview

# Actual parent profile, unprotected parent editor.
./artifacts/unity-windows/KeyLearner.exe --studio

# Normal protected fullscreen play.
./artifacts/unity-windows/KeyLearner.exe

# Static representative explorer screenshots and player logs.
./scripts/verify-unity.ps1 -StaticOnly -Modes @(3,4,5)

# All 12 modes plus deterministic canvas, quantity, and math replays.
./scripts/verify-unity.ps1

# A small selection for iteration.
./scripts/verify-unity.ps1 -SelectedCases @('word-pop','counting','dots-retry')
```

Preview never installs a keyboard hook or changes accessibility settings. `--data <directory>` explicitly selects a writable profile; use a disposable `artifacts` directory for QA. The verification runner hashes the five real parent save files before and after its run, captures actual player screenshots with diagnostic JSON, and checks player logs and progression/scoring assertions. Short screenshots are visual/behavioral checks, not frame-rate measurements.

`--probe-guard` remains a disarmed pass-through native diagnostic. The standalone platform probe also checks the real Windows speech helper at zero volume:

```powershell
dotnet run --project tools/KeyLearner.PlatformProbe/KeyLearner.PlatformProbe.csproj -c Release -- UnityPort/Assets/StreamingAssets/Platform/KeyLearner.PlatformHelper.exe
./scripts/test-unity-scripts.ps1 -UnityEditor '<installed Editor directory>'
```

Protected keyboard/touchpad mash and secure-desktop focus recovery still require the separate target-laptop acceptance procedure. The separate `scripts/test-unity-accessibility.ps1` runner has verified normal lease disposal and actual packaged-watchdog restoration after terminating its own early diagnostic process; it restores only owned shortcut bits and never arms keyboard capture. Preview screenshots and disarmed probes do not test those OS interactions.

## Packaging and profiles

The tested [Unity 3.0.0 prerelease](https://github.com/faradaysage/Key-Learner/releases/tag/unity-v3.0.0-preview.1) provides the Windows installer and checksum. That release predates the GameCI pipeline; new Windows player and installer artifacts come from successful runs of windows-installer.yml. Review the native engine and target-device acceptance limits in the release notes.

After the Windows build succeeds:

```powershell
./scripts/build-unity-installer.ps1 -Version 3.0.0 -Compiler '<installed Inno Setup ISCC.exe>'
```

The installer retains AppId `{D5C654D0-1B14-4479-B771-20BD264731A8}`, taskbar ID `KeyLearner.Desktop`, `%LOCALAPPDATA%\Programs\KeyLearner`, and `%LOCALAPPDATA%\KeyLearner` saves. Parent Studio shortcuts explicitly choose the real parent profile while remaining unprotected. Installers never auto-launch protected play. Private dictionaries and development tools/models are excluded.

## Adding a minigame

Implement `Minigame` in `Assets/KeyLearner/Runtime/Games`, add its factory in `MinigameRegistry`, and add catalog metadata in the shared `GameCatalog`. The base contract supplies lifecycle, key/pointer input, focus suspension, and diagnostics. `GameServices` supplies profile/settings, live physical keys, audio, rewards, camera, session progress, content lookup, and picker navigation. Keep rules and deterministic state in the [shared domain project](../Shared/KeyLearner.Domain.csproj), which compiles the existing engine-independent files in `Studio/` plus compatibility and safety helpers in `Shared/`. Add new shared source files to that project's explicit compile list; avoid moving learning logic into MonoBehaviours.

Explorer decoration uses the generated `ContentLibrary` and original third-party asset families. Preserve original textures, rigs, animations, and licenses; use Editor content-building/import code to make reusable adapted prefabs. Add curated segment/garden/neighborhood rules instead of copying the entire explorer implementation. Source assets remain separate from generated game materials and prefabs.

Full attribution is in [the asset ledger](../THIRD_PARTY_ASSETS.md), bundled license files, and Parent Studio's **Art & licenses** panel. [Manual downloads](../MANUAL_DOWNLOADS.md) records remaining optional/manual sources, if any. Platform contracts and verification limits are documented in [the platform guide](../docs/unity-platform.md); migration decisions and current evidence are in [the migration report](../docs/unity-migration.md).


Additional preview acceptance runners use fresh explicit profiles and verify the real five parent save hashes remain unchanged:

```powershell
./scripts/verify-unity-ux.ps1
./scripts/verify-unity-pointer.ps1
```

Run these sequentially with other player captures. The UX runner renders every Parent Studio tab and credits, verifies picker keyboard navigation and isolated save changes, then minimizes/restores only its launched preview window to verify actual Unity focus callbacks. `-SkipNativeFocus` runs only the deterministic UI checks. The pointer runner delivers mouse press/release only when its own launched preview window is still foreground, restores the cursor afterward, independently checks viewport coordinates, and confirms reward state for Dot Pop and all five math games. Dot Pop is checked in landscape and portrait; `-PortraitOnly` tests the selected modes in a portrait window. Neither runner synthesizes keyboard authorization or enables protected capture.

Explorer handedness regression:

```powershell
./scripts/verify-unity-steering.ps1
```

This launches an isolated visible Letter Racer preview, holds only the right-arrow key for a bounded 1.2 seconds, and samples the game's reported input and projection. Acceptance requires the car to move right relative to the projected road center, then acknowledge key release. The helper accepts no parent shortcut or modifier keys and releases the arrow if the preview loses foreground. Run it only when other player captures have released the desktop.

Current acceptance, actual gameplay screenshots and the retained native Unity crash risk are recorded in [verification](../docs/unity-verification.md) and the [gallery](../docs/unity-gallery.md).
