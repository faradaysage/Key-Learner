# Protection boundary and acceptance

## Current implementation

Normal launch installs a Windows WH_KEYBOARD_LL hook on a dedicated message-pump thread. The callback queues physical transitions for the game and consumes ordinary keyboard messages, including injected input. Injected input cannot authorize parent actions. The callback does not synthesize replacement keystrokes into Windows.

The game is borderless fullscreen, disables MonoGame Alt+F4, requests topmost placement and mouse confinement with SDL, and cancels application close requests until the exact parent exit gesture. On focus loss it stops collecting game input, clears transient state, and keeps the blocking hook installed. The hook remains in place when the parent studio is open. Cleanup unhooks and releases SDL confinement. Crashes and forced process termination end the session protection.

Every frame checks the current 256-key snapshot. Exact Ctrl+Alt+O or Ctrl+Shift+O opens options after two seconds; exact Ctrl+Alt+Esc exits after two seconds. No release sequence is required. Extra physical keys restart the hold timer; toggle indicators do not count. After an action, release all keys before another action. A frame gap over 250 ms restarts the timer.

## What it cannot guarantee

An ordinary application cannot intercept the secure attention sequence, Ctrl+Alt+Delete. Windows can also silently remove a low-level hook that times out. Secure desktops, OS dialogs, touch/trackpad gestures, accessibility paths, another process changing focus, hardware sleep, power/lid behavior and a crash can defeat application-level confinement. A mouse grab is not a desktop security boundary.

Keyboards may have limited rollover or ghosting. If hardware never reports an extra key, no application can include that key in its exact-chord decision. Test the actual laptop, including an external keyboard if one is used.

This development PC reports ProfessionalWorkstation (Pro for Workstations). Microsoft Keyboard Filter is available on Enterprise, Education and IoT Enterprise editions, not this edition. Nothing in this revision changes Windows editions, policies, accounts, accessibility preferences or startup configuration.

## Strict deployment path (requires a chosen target)

For stronger child isolation, use a dedicated child account/device with no mail, documents or privileged sessions available, and an eligible Windows edition with Keyboard Filter enabled. Configure a kiosk shell and OS-level filtering for task switching, Windows shortcuts, accessibility breakouts and secure attention. Explicitly review the filter's default five-press Windows-key breakout: the default is inappropriate for keyboard smashing.

Do not blindly block the game's parent chords in the OS filter: the app must still receive those physical keys. Test both layers together. Even Keyboard Filter cannot block the hardware Sleep key. Safe mode and hardware servicing remain outside this application boundary.

A deployment policy has not been applied. The target machine and its Windows capabilities must be established first.

## Physical acceptance matrix — NOT yet executed

Use a separate child/test account with no valuable applications open. An adult must first verify both parent chords and power recovery.

| Attempt | Expected application guard behavior |
|---|---|
| Alt+Tab, Alt+Esc, Alt+F4, Ctrl+Esc, Ctrl+Shift+Esc | Consumed; remain in the game |
| Win, Win+D/L/R/Tab, browser/media keys | Ordinary reported key messages consumed |
| Esc | Reminder only |
| Ctrl+Alt+Esc, held 2 s | Game closes; normal keyboard restored |
| Ctrl+Alt+O, held 2 s | Studio toggles; guard remains installed |
| Correct chord plus Shift, both Ctrl keys, or random key | No parent action |
| Extra key pressed and released during a chord | Hold timer restarts |
| Release a target, press another before releasing modifiers | New exact combination must be held for two seconds |
| Hold keys at launch; repeat keys; mash many keys; unplug keyboard | No false parent action; verify recovery |
| Mouse corners, secondary monitor, touchpad 3/4-finger gestures | Check actual device; application confinement can be bypassed |
| Sticky/Filter/Toggle Keys activation sequences | Must be tested/configured at OS layer |
| Ctrl+Alt+Delete | OS security screen remains possible without OS Keyboard Filter |
| Sleep, lid closure, task/process termination, OS shutdown | Outside application guarantee |

Automated tests validate reported key streams, not OS enforcement or electrical keyboard rollover.

Sources:
- [Microsoft Keyboard Filter](https://learn.microsoft.com/en-us/windows/configuration/keyboard-filter/)
- [LowLevelKeyboardProc and timeout/removal behavior](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc)


### Physical key identity and fallback options (2.0.22)

The native hook matches key-up to the original scan code plus extended flag, retaining the original virtual-key label. Pause/Break are pulses; overrun and synthetic extended Shift packets cannot remain held. Repairs from injected releases rebuild state and cannot authorize a strict parent chord. No idle timeout invents releases for genuinely held keys.

Ten complete O taps independently open options, just as ten Escape taps exit. Other key-downs reset each sequence; repeats count once. The normal exact chord still rejects extra keys. Parent Studio shows scan repair/ignored-packet counters. Preview `native-recovery` exercises scan normalization through the game handler; `ten-o` verifies options recovery with poisoned held-key state. Physical hardware acceptance remains required.


### Clean resume (2.0.23)

The live snapshot is independent of buffered gameplay events. It is still maintained by the Windows interceptor: blocked Windows delivery can prevent ordinary polling APIs from reporting pressed keys. We do not substitute zero polling results for physical release events.

Focus/visibility transitions clear the native ledger, event queue, game-held state, shortcut timers, gesture history, recognition fragments, speech and effects together. Gameplay processing is paused while inactive; only parent holds and ten-tap fallbacks consume input, allowing an adult to close or restore the window. the hook continues suppressing Windows keyboard delivery. Returning starts a fresh input session. Ten O taps provide an independent input reset and options route. Key transitions older than 250 ms cannot become deferred effects or speech. The game no longer repeatedly restores/raises its window while continuing to play queued input in the background.

Synthetic and hide/show regressions are automated. The user's physical desktop escape remains unconfirmed on this development device; Windows touchpad/secure-desktop behavior remains outside this guard's guarantee.
