# Protection boundary and acceptance

## Current implementation

The preserved MonoGame game and the Unity Windows port use the same physical-key and parent-control rules. The Unity adapter is in `UnityPort/Assets/KeyLearner/Platform`; the engine-independent models remain in `Studio/` and are compiled by `Shared/KeyLearner.Domain.csproj`. `PROJECT_HISTORY.md` records the earlier fixes and current source defines implemented behavior.

Normal protected play installs `WH_KEYBOARD_LL` on a dedicated message-pump thread, initially disarmed. Every callback checks the actual game HWND against the native foreground window and verifies the desktop's `UOI_IO` flag **before reading key data**. A failed check passes the event to Windows without retaining or suppressing it. Background parent shortcuts also pass through. Focus/desktop events disarm the hook; only the visible active game loop can rearm it. The pump replaces the hook with overlap approximately once per second to recover from silent removal.

While authorized and foreground, the callback records physical transitions and consumes ordinary keyboard messages, including injected messages. Injected downs do not authorize parent actions. The callback never synthesizes replacement Windows keystrokes. The independent live snapshot is maintained by the interceptor; zero results from ordinary Windows polling are not treated as invented releases.

Unity requests a fullscreen window, topmost placement and a visible confined cursor while the protected game owns foreground. The in-game Parent Studio retains protection and pointer controls. Disarming releases cursor confinement and topmost placement before returning input to Windows. MonoGame uses its SDL confinement path. Application close requests are canceled until an authorized exit; cleanup releases the hook and session resources. Standalone Unity `--studio` is unprotected and uses the real parent profile. Ordinary `--preview` and Editor play are unprotected isolated sessions unless `--data` explicitly selects a profile.

Protected activation temporarily clears only the Sticky/Filter/Toggle Keys shortcut and confirmation bits (`0x0c`). Enabled accessibility features and unrelated preference bits are preserved. A separate restoration helper is started before capture is armed; failure prevents activation. Focus loss/exit restores the changed bits, and the independent watchdog restores them after an owner-process crash. The watchdog identifies the owner by PID and start time. This is a session lease, not a permanent Windows accessibility configuration.

## Parent controls and clean resume

Exact Ctrl+Alt+O or Ctrl+Shift+O opens/toggles options after two seconds; exact Ctrl+Alt+Escape exits after two seconds. No release sequence is required to trigger the first action. Extra physical keys restart the hold; toggle indicators do not count. After an action, release all keys before another action. A frame gap over 250 ms restarts the hold timer.

Ten complete O taps independently open options; ten complete Escape taps exit. Other key-downs reset a sequence, and repeats count once. G, G within 1.2 seconds, without modifiers, returns to the picker. One Escape shows a seven-second parent-control reminder and keeps Canvas/explorer play running. Dot Pop remains in its game; visual math retains its Escape/Back route to the picker. Parent text editing uses Escape to cancel the edit.

Key-up is matched to the original scan code plus extended flag while retaining its original virtual-key label. Pause/Break are pulses; overrun and synthetic extended Shift packets cannot remain held. A repaired injected release rebuilds state and cannot authorize a strict parent chord. No idle timeout invents a release for a genuinely held key. Diagnostic counters do not record raw key histories.

Focus, desktop, visibility and minimize/restore/maximize changes clear the native ledger, event buffer, game-held state, shortcut timers, gesture/word/count fragments, pointer edges, speech, sound effects and transient visuals together. Calibration and benchmarks stop; quantity/math questions restart appropriately while earned progress is preserved. Gameplay and parent shortcuts do not consume background keys. Returning starts a fresh input session. Events older than 250 ms cannot become deferred effects or speech, and the game does not repeatedly raise its window to continue background play.

## What it cannot guarantee

This is application-level containment. Secure desktops, OS dialogs, touchpad gestures, another process changing focus, hardware sleep/lid behavior and process termination remain outside its guarantee. Ctrl+Alt+Delete cannot be intercepted by an ordinary application hook. Windows can silently remove a timed-out low-level hook; a dedicated fast callback and renewal reduce that exposure but do not turn it into an OS filter. See Microsoft's [low-level hook documentation](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc).

Hardware rollover/ghosting may omit keys before the application receives anything. A confined pointer is not a desktop security boundary. Test the actual target laptop and any external keyboard or touchpad configuration.

An earlier development snapshot reported Windows Pro for Workstations. Recheck the selected deployment device rather than assuming that snapshot describes it. No migration step applies a kiosk policy or changes accounts, startup configuration or Windows edition.

## Optional OS deployment path

A separately chosen child/test account and eligible Windows device can use OS-level filtering and a kiosk shell. Microsoft lists Enterprise, Education and IoT Enterprise editions for Keyboard Filter; its documented limitations include remote desktop, Safe Mode and the Sleep key. The default five-press left-Windows-key breakout must be reviewed for a keyboard-smashing app. Parent chord keys must still reach the game. Configuration remains a separate target-device decision. See [Microsoft Keyboard Filter](https://learn.microsoft.com/en-us/windows/configuration/keyboard-filter/).

## Physical acceptance matrix — target hardware still required

Use a separate child/test account with no valuable applications open. Verify the adult escape routes before testing containment.

| Attempt | Expected behavior while protected and foreground |
|---|---|
| Alt+Tab, Alt+Escape, Alt+F4, Ctrl+Escape, Ctrl+Shift+Escape | Ordinary reported key messages consumed |
| Win, Win+D/L/R/Tab, browser/media keys | Ordinary reported key messages consumed; verify actual device |
| Exact parent chord held two seconds | Requested options/exit action |
| Correct chord plus another physical key | No parent action; hold restarts |
| Extra key pressed and released during a hold | Hold timer restarts |
| Ten complete O / Escape taps | Independent options / exit route |
| Held keys at launch, repeats, mash, unplug | No false authorization; verify recovery |
| Focus/desktop change, minimize/restore/maximize | Capture disarms; background keys pass through; transient state resets |
| Mouse corners and secondary monitor | Check visible confinement while active and release on disarm |
| Touchpad three/four-finger gestures | Device-specific; configure separately if needed |
| Sticky/Filter/Toggle shortcut sequences | Physical shortcut sequences still need device testing; normal and forced-exit lease restoration passed native diagnostics |
| Ctrl+Alt+Delete, sleep, lid, task termination, shutdown | Outside application guarantee |

The shared automated suite exercises reported physical streams and progression rules. The disarmed native probe installs/releases callbacks without capturing keys. Unity preview acceptance has exercised real minimize/restore callbacks, pointer answers, right-button rejection and a bounded right-arrow hold. These do not establish physical parent-chord authorization, keyboard rollover, secure-desktop containment or touchpad isolation. A separate actual Unity accessibility test passed normal lease disposal and packaged-watchdog restoration after its diagnostic owner was terminated. It verified that only `0x0c` shortcut/confirmation bits changed and all original flags/timing values were restored. See `docs/unity-platform.md` and `artifacts/unity-accessibility-acceptance/result.json` for evidence.

## Historical notes

Versions 2.0.22 and 2.0.23 introduced scan identity repairs, ten-O recovery and whole-session resets. Later foreground/desktop fixes superseded those versions' background-suppression behavior. Earlier text saying that the hook continued blocking or processing parent shortcuts while inactive was obsolete and must not be used to reintroduce that behavior. The old MonoGame `native-recovery` and `ten-o` preview scenarios remain historical regression tools; the Unity preview interface has its own guarded runners.
