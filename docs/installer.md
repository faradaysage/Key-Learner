# Windows installer

The Unity Windows package is built with the locally installed Unity Editor and `scripts/build-unity-installer.ps1`, as described below. The existing GitHub Actions workflow builds the preserved MonoGame package.

## Preserved MonoGame packaging

GitHub Actions: `.github/workflows/windows-installer.yml`. Runs on main, pull requests, version tags, and manual dispatch. Download the **KeyLearner-Windows-installer** artifact from a successful run, unzip, and copy the setup EXE to the laptop. The package targets Windows 10/11 x64 and bundles the latest .NET 8 runtime patch selected from Microsoft release metadata, fonts, icons, and 70 offline speech clips. Other words use Windows speech unless a parent adds recordings or optional Piper.

Build locally:

    ./scripts/build-installer.ps1 -Version 2.0.1 -Compiler 'path/to/ISCC.exe'

The workflow pins Inno Setup 7.1.0 and verifies its SHA-256 before installing the compiler. Ordinary runs use 2.0.<run number>; tags must be vMAJOR.MINOR.PATCH. Keep versions increasing for releases. Setup is currently unsigned, so Windows may show an unknown-publisher/SmartScreen prompt.

The permanent AppId is `{D5C654D0-1B14-4479-B771-20BD264731A8}`. Never change it to create a new version. Install location defaults to `%LOCALAPPDATA%\Programs\KeyLearner`, with the same Start menu shortcuts, taskbar identity, and uninstall entry across upgrades. Setup reuses the previous directory and can ask to close a running game. It never auto-launches protected play after installation.

Parent data remains in `%LOCALAPPDATA%\KeyLearner`, outside the application directory. Neither updates nor uninstall delete it. The private source CSV is explicitly excluded from publish. Add family words through the parent dictionary on the target laptop.

Run `scripts/test-installer.ps1 -Installer <setup.exe> -UpgradeInstaller <newer-setup.exe>` to exercise a temporary installation and upgrade, assert one registration/same directory/unchanged parent settings, then uninstall it. The test refuses to run if this AppId is already installed.

This installs the current session keyboard guard; it does not configure a Windows OS kiosk. See protection.md for its existing limits.

## Unity Windows package

Download the tested [Unity 3.0.0 Windows prerelease](https://github.com/faradaysage/Key-Learner/releases/tag/unity-v3.0.0-preview.1). Its installer/checksum are uploaded from the locally verified Unity build. The separately named `KeyLearner-MonoGame-Windows-installer` Actions artifact continues to validate the preserved engine; CI also runs the shared domain regression suite. No Unity CI license is assumed.

The Unity port uses the same installer identity, installation location, executable name, taskbar ID, and parent profile. After `KeyLearner.Unity.Editor.ProjectSetup.BuildWindows` builds `artifacts/unity-windows/KeyLearner.exe`, package it with:

```powershell
./scripts/build-unity-installer.ps1 -Version 3.0.0 -Compiler 'path/to/ISCC.exe'
```

Before the Unity build, `scripts/build-unity-platform.ps1` publishes the standalone Windows speech/accessibility helper and its .NET runtime into `UnityPort/Assets/StreamingAssets/Platform`, and stages the existing licensed audio and icons. It uses the highest installed .NET 8 runtime patch; `-RuntimeVersion` can pin a release build. This generated runtime is rebuilt rather than maintained as source.

The Parent Studio shortcut and optional post-install action pass `--preview --studio --data "%LOCALAPPDATA%\KeyLearner"` (Inno resolves the actual local data directory). This deliberately opens the real parent profile without a keyboard hook. Ordinary `--preview` remains an isolated disposable profile; QA must not reuse the shortcut's real-profile argument. Unity also accepts `--studio` alone for an unprotected real-profile studio.

`tools/KeyLearner.PlatformProbe` checks the native hook in disarmed pass-through mode and can verify the local speech helper with zero-volume playback. It never arms keyboard capture or changes accessibility flags. Physical keyboard/touchpad and lock-screen acceptance remains a separate test on the target laptop.

## Isolated Unity installer acceptance

`test-unity-installer.ps1 -PreflightOnly` checks the permanent AppId in per-user and machine uninstall views, the normal application directory and the normal Start menu group. Existing installation evidence causes a documented skip; the test never replaces a real installation for convenience. The local 2026-09-13 read-only check found none (`artifacts/unity-installer-preflight/preflight.json`). Inno Setup 7.1.0 was discovered at `.local/installer-tools/compiler/ISCC.exe`.

After the final release player is built and packaged, the preserved MonoGame 2.0.27 installer can be used for a real engine-upgrade test:

```powershell
./scripts/test-unity-installer.ps1 `
  -Installer artifacts/installer/KeyLearner-2.0.27-win-x64-setup.exe `
  -UpgradeInstaller artifacts/installer/KeyLearner-3.0.0-win-x64-setup.exe `
  -BaselineIsMonoGame
```

The script uses a new directory under this repository's `artifacts` and the unchanged application identity. It requests a unique test Start menu group; the preserved installer disables the group page and therefore uses its normal KeyLearner group. Both allowed locations must be absent before testing, and each shortcut must target the exact temporary executable. Silent installation never launches the game. It verifies the Unity payload and corrected Parent Studio shortcut after upgrade, compares all five real parent profile hashes, then uninstalls only after checking the registered directory still equals the intended test directory. A preflight result is not an installation result; see the later `result.json` and install/upgrade/uninstall logs for the actual outcome.

The Unity installer preflight requires the .NET runtime/System.Speech licenses and third-party notices, in addition to bundled audio/art notices. These exact texts are staged from the restored package cache under `StreamingAssets/Platform/Licenses`; the source package versions and cache roots are discovered by the platform build script.

Unity builds define `UnityPort` for the shared Inno script. Only this build includes `installer/LegacyMonoGameFiles.iss`: an audited list of 300 obsolete files from the preserved 2.0.27 publish directory. Upgrade deletes those exact old runtime, MonoGame and packaged content files before installing Unity. The list excludes the shared executable, all `data` dictionaries, diagnostic output and unknown custom files. It contains no directory deletions or wildcards. MonoGame installer builds retain their original behavior.

The isolated upgrade test also verifies every listed file actually present in the baseline is removed and an unlisted custom sentinel survives the upgrade. Installer waits are bounded to five minutes; a still-running installation prevents concurrent uninstall. The final result records removed-file count, custom-file preservation and real-profile hashes separately.
Unity packaging also excludes Unity build backup/Burst debugging directories and PDB symbols; these remain available locally for diagnosis. The upgrade acceptance checks they are absent from the installed release.

The first installation acceptance attempt was safely uninstalled after its validation expected the requested unique group. The baseline correctly used its normal group: Inno explicitly ignores `/GROUP` with `DisableProgramGroupPage=yes` ([Inno command-line reference](https://jrsoftware.org/ishelp/topic_setupcmdline.htm)). The corrected acceptance validates only preflight-absent allowed groups and exact temporary shortcut targets, then verifies both groups disappear on uninstall. Original failure evidence remains under `artifacts/unity-installer-acceptance`; the full corrected upgrade run has its own output directory.
The final actual upgrade passed in `artifacts/unity-installer-acceptance-final/result.json`: MonoGame 2.0.27 → Unity 3.0.0, one registration and the same temporary directory, corrected real-profile Parent Studio shortcut, all 300 obsolete files removed, unlisted custom content preserved, Unity dependencies/notices present, and all five real profile hashes unchanged. Scoped uninstall removed the temporary application and its shortcuts without reboot. `artifacts/unity-installer-after-check/preflight.json` independently confirms no registration, normal install directory or Start menu residue remained.

Final package: `artifacts/installer/KeyLearner-3.0.0-win-x64-setup.exe` (81,628,898 bytes), with SHA-256 `9a2c5a580d9c78209fa25c5d56819a7740ba810430fade4c3d3a465ec897bab9`; the adjacent `.sha256` file contains the same digest. The build log is `artifacts/unity-installer-preflight/build-final.log`. This setup contains the final preview-report retry fix and excludes Unity backup/debug artifacts.
