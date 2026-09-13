# Unity installers from GitHub Actions

The **Windows installer** workflow builds the Unity Windows player and installer on GitHub-hosted runners. The preserved MonoGame build remains a compatibility check and supplies the same-run installer used to verify upgrades. No self-hosted runner, paid Unity service, remote build account, or local workstation agent is required.

## One-time Personal license setup

Follow the current [GameCI Personal activation instructions](https://game.ci/docs/github/activation/#personal-license). Activate a free Personal license through Unity Hub using the Unity account intended for CI. On Windows, the generated license is normally `C:\ProgramData\Unity\Unity_lic.ulf`. A Hub license card alone does not establish that the file exists; GameCI documents the explicit **Add → Get a free personal license** step.

In this repository's **Settings → Secrets and variables → Actions → Repository secrets**, enter these values yourself:

| Secret name | Value |
| --- | --- |
| `UNITY_LICENSE` | Complete contents of the `.ulf` created by the supported Hub activation flow. |
| `UNITY_EMAIL` | Email for the same Unity account. |
| `UNITY_PASSWORD` | Password for that Unity account. |

Do not paste these values into chat, an issue, a workflow, or Git. The workflow checks only whether each secret is present; GameCI verifies actual activation during tests and builds. If Hub cannot produce a usable license or Unity rejects it, resolve that normal activation flow using GameCI/Unity support. No hidden page controls, obsolete activation workaround, or copied browser session is part of this setup.

As of September 13, 2026, the user has configured all three repository secret names. Their values have not been read or printed by the agent. A successful license preflight confirms presence only; consult the actual Actions run for activation and build completion.

## Run and download

Open **Actions → Windows installer → Run workflow**, select the desired repository branch, and run it. Repository PRs and pushes to `main` also run the Unity path. Fork PRs run the non-Unity checks and show a visible notice that Unity tests and packaging were not performed; secrets are never supplied to forks. The workflow uses `pull_request`, not `pull_request_target`.

A successful run contains:

- `INSTALL-KeyLearner-Unity-Windows-<version>`: the laptop setup executable, SHA-256 file, and provenance/upgrade result.
- `Portable-Unity-Windows-player-<version>`: the complete portable Unity player and source/build provenance.
- `CI-Unity-test-results-<run>`: NUnit XML and the project build report.
- `CI-Unity-upgrade-test-results-<run>`: installer preflight and acceptance JSON.
- `CI-only-MonoGame-upgrade-baseline`: compatibility output, also used as the upgrade baseline.
- `CI-only-Unity-build-inputs`: staged domain DLL and Windows helper consumed by the Unity job.

Only the **INSTALL-KeyLearner-Unity-Windows** artifact is needed to install the game. Extract it, then run the setup executable. The run summary links directly to it. The portable player is optional; the CI artifacts are pipeline inputs and test evidence. The installed application keeps the existing per-user installation identity and parent profile. Artifacts are retained for 30 days. This workflow does not automatically create a GitHub Release; a release upload is a separate publication step.

Ordinary runs use `3.0.<workflow run number>`. Tags `unity-vMAJOR.MINOR.PATCH` and `unity-vMAJOR.MINOR.PATCH-preview.N` use the three-part version for both the Unity player and Inno installer. The preview suffix describes publication status, not the Windows numeric file version. Historical `vMAJOR.MINOR.PATCH` tags retain MonoGame packaging only.

## What the pipeline verifies

1. A hosted Windows job checks the licensed source asset hashes, runs the original and shared-domain regression suites, and builds the engine-independent DLL and self-contained Windows speech/accessibility helper. The staged helper includes its runtime and System.Speech notices.
2. A separate hosted preflight rejects missing activation secrets before the Unity image is downloaded. It verifies the project Editor version and determines one version for the player and installer.
3. A hosted Ubuntu job restores the prepared DLL/helper under the existing Unity `Assets` paths and caches `UnityPort/Library` by OS, Editor, packages, and source content. GameCI runs actual EditMode and PlayMode tests; missing, zero-test, failed, or skipped test results cannot count as success.
4. GameCI invokes `KeyLearner.Unity.Editor.ProjectSetup.BuildWindows` with `-keyLearnerVersion`. Unity cross-compiles `StandaloneWindows64` using the project's Mono backend and release settings. The build remains rooted at `artifacts/unity-windows` regardless of the container working directory. CI returns ownership of only that Docker-created output directory to the runner before writing provenance; GameCI's default output ownership handling does not cover the custom path.
5. A fresh hosted Windows job downloads that player and the same-run MonoGame baseline, builds the Inno installer, installs the baseline, upgrades to Unity, verifies one identity, preserved parent data/custom content and removed obsolete files, and uninstalls only the temporary test installation. A skipped preflight fails acceptance rather than producing a green result.

The final installer artifact is uploaded only after upgrade acceptance passes. The provenance records the actual source/PR merge SHA, original PR head SHA, run URL, version, player hash, installer hash, Unity build manifest, and upgrade result. Cache contents, activation files, credentials, raw licensing logs, private dictionaries, and diagnostic dumps are not publication artifacts.

## Pinned tools and maintenance

The project declares Unity `6000.6.0f1`. The workflow pins the available GameCI Linux Editor image containing Windows Mono support:

`unityci/editor:ubuntu-6000.6.0f1-windows-mono-3.2.2@sha256:88bc798f2912de3b7474a43d3b2da415d7466daeac11fbaa661543b87c57bd71`

A changed project Editor version fails preflight until the image/version check is deliberately updated. Library caches stay separate across Editor/package changes. Windows Mono can be cross-compiled on Linux; [GameCI's builder documentation](https://game.ci/docs/github/builder/#targetplatform) explains the host/backend distinction. This pipeline does not switch the application to IL2CPP.

The workflow pins [Unity Builder v4.8.2](https://github.com/game-ci/unity-builder/releases/tag/v4.8.2) and the maintained [Unity Test Runner v4.3.2 backport](https://github.com/game-ci/unity-test-runner/releases/tag/v4.3.2) by full commit SHA. Both include support for Unity 6.6's shared-memory requirement. The first hosted attempt exposed an argument incompatibility between the v4.4.0 test action and GameCI CLI v0.1.63: the action emitted `--no-coverageEnabled`, which the CLI rejected before activation or tests. The v4.3.2 backport executes Docker directly and avoids that CLI argument path. Its supported `coverageOptions` input is empty; the action still supplies optional Unity coverage flags, but this project does not install the Code Coverage package or require coverage reports. Actual EditMode and PlayMode NUnit results remain mandatory. The workflow uploads XML directly and does not request write access for GitHub Checks. The Inno Setup 7.1.0 installer remains checksum-pinned.

A green hosted build establishes compilation, imported asset references, Unity lifecycle tests, and packaging/upgrade checks. It does not establish the children's laptop frame rate, physical keyboard/touchpad containment, secure-desktop recovery, or resolution of the previously documented sporadic native Unity crash. Those acceptance limits remain in [verification](unity-verification.md).

### Hosted test resource diagnostics

The lifecycle/geometry PlayMode tests already disable their cameras and support headless execution. The GameCI test step explicitly uses `-nographics -job-worker-count 2 -gc-helper-count 2`, following [Unity's command-line options](https://docs.unity3d.com/6000.6/Documentation/Manual/EditorCommandLineArguments.html). The Windows player build and local rendering acceptance retain graphics. All EditMode and PlayMode cases remain required.

Run 27 exited 137 before PlayMode XML on two attempts after passing 40 EditMode cases; that exit alone does not establish the cause. The existing test-results artifact now also contains numeric host resource snapshots: memory, free disk and the kernel OOM-kill counter before/after tests and build. `scripts/unity-ci-resources.py` reads only the allowlisted numeric fields in `/proc/meminfo` and `/proc/vmstat` plus free disk, without logs, environment variables, process arguments or credential files.
