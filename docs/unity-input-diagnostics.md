# Diagnosing installed Unity keyboard input

The player automatically writes a small, shareable session log on every launch. No command-line option or working keyboard is required. The KeyLearner splash follows Unity's splash for four seconds and shows the player version plus the first twelve characters of Unity's unique build GUID. The same identity remains at the bottom of the game picker. This identity comes from the running player, not the installer filename.

## Send a useful report

1. Launch the newly installed **KeyLearner** Play shortcut. Note or photograph the version/build on the splash or picker.
2. On the picker, release all keys, then press/release Right Arrow a few times and Enter. Allow about five seconds for the periodic record. Describe whether a card moves or a game opens.
3. Click **Open diagnostics & close** at the bottom right of the picker. This explicitly releases capture, opens the folder in Explorer and closes KeyLearner. It also exists on the managed-error screen.
4. Send the newest `input-session-*.jsonl` file. If the button cannot be used, close the game using the working taskbar route and open `%LOCALAPPDATA%\KeyLearner\diagnostics` manually.

Mention whether the keys came from the laptop keyboard, an attached keyboard, Remote Desktop, or remapping/accessibility software. Do not send passwords, dictionaries, saved profiles, or the entire application-data folder. The normal player log can contain file paths and other application output; this purpose-built JSONL file does not copy it.

## What the log records

- Version, full build GUID, Unity version, compiled input backend, OS/GPU descriptions, protected/preview/studio mode, and an RDP-session boolean. No machine name, hardware serial, device identifier or session name.
- One flushed heartbeat per second during the first minute, then every five seconds, including when the game receives no keys or has no focus.
- The bound and foreground window handles/process IDs (no titles or executable paths), Unity focus, visibility/minimize/maximize state, native desktop query success, and whether that desktop receives input.
- Hook callback and focus-rejection counts; physical versus synthetic packets **only after the ownership gates**; held-key count, discarded malformed packets, repair/reset counts, hook renewals/errors, and the message-pump heartbeat age.
- Events dispatched by the input adapter, discarded stale events and maximum queue age; aggregate event counts at the picker, Parent Studio and game; Unity GUI key-event counts; splash completion, focus/pause transitions and cleanup.

There are no key codes, scan codes, held-key identities, typed characters/words, profile contents, command-line arguments, window titles or authentication values. Native callbacks only increment counters; file serialization happens in the main loop. Every record is flushed so recent evidence survives a forced exit. Each file is capped at 2 MiB, and a launch retains up to five preceding owned session files. A write failure disables logging without stopping play or cleanup.

## Interpreting the evidence

- `unityFocused=false`, a foreground HWND different from the bound HWND, minimized/hidden state, or a failed/inactive desktop gate explains why capture must pass through to Windows.
- If those gates are good but callbacks remain zero during reported physical presses, inspect hook delivery/installation and competing input routing. A growing pump age, stopped thread or renewal error distinguishes a stalled hook pump from a healthy one receiving no callbacks.
- Growing `syntheticPackets` with no physical packets means the input was tagged injected by Windows. Protected play retains its existing rejection of synthetic presses; this update does not authorize parent controls through automation.
- Physical packets followed by increasing `staleEvents`/large event age indicate queue freshness rejection. Dispatched events with no picker/game increments point farther downstream.
- A passing preview keyboard test verifies Unity's unprotected input path only. It does not establish protected physical-key acceptance.

The user's 3.0.31 device log confirmed the same failing stage as local reproduction: Unity GUI received keyboard events while our hook received zero callbacks, despite healthy ownership and pump state. A controlled build changing only Active Input Handling from Both to Input Manager (Old) restored hook delivery and passed the formerly failing native check. This project uses UnityEngine.Input for presentation and preview; the unused new backend is now disabled, while the protected physical-key path is unchanged. Builds and EditMode tests reject an incompatible configured or compiled backend. New session logs must report inputBackend=legacy-only. Physical keyboard gameplay on the target device still needs confirmation; synthetic delivery tests do not establish it.

## Reproducible local splash check

Preview replays normally skip the introduction so existing gameplay timing stays unchanged. `--preview --show-intro` enables the actual introduction for visual QA. Use an isolated `--data` directory for all previews. The ordinary protected launch has no skip flag and advances from the introduction without keyboard or mouse input.

`scripts/verify-unity-startup.ps1` captures and verifies the actual splash/picker and matching diagnostic identity. `scripts/test-unity-protected-focus.ps1 -VerifyDiagnostics` additionally requires the protected hook to acknowledge an OS-tagged synthetic press/release while rejecting it from gameplay. The initial Both-backends build failed this assertion. The legacy-only comparison passed without weakening synthetic-input rejection. The check now sends three bounded press/release pairs across hook renewals and requires all six packets; the original failed evidence is retained alongside subsequent results.
