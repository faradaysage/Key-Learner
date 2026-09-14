# Extending the Unity learning suite

The gameplay domain remains authoritative in `Studio/`. `Shared/KeyLearner.Domain.csproj` links those files into a .NET Standard 2.1 DLL; the preserved MonoGame project and original regression harness compile the same source. Unity-specific views use that DLL through the unchanged `KeyLearner.Studio` namespace. Modern C# records and collection expressions compile outside Unity, whose runtime and Editor sources use C# 9. Add pure rules to the shared source list rather than copying learning rules into a renderer.

## State and persistence

Keep answer generation, learning progression, scoring, spelling recognition, timing rules and retry behavior in models that can run without Unity. `GuidedSpelling`, `WordRecognizer`, `CountingRecognizer`, `FlightModel`, `SubitizingGame` and `MathGame` provide the current examples; [Shared/README.md](../Shared/README.md) documents their public APIs.

`Store` retains the existing JSON property names, numeric `PlayMode` values, dictionary keys, `.bak` files, malformed-file fallback, and atomic replacement. Normal play continues to use `%LOCALAPPDATA%/KeyLearner`. Preview runs use explicit disposable roots. Its optional `contentRoot` supplies first-run CSV defaults from the repository in the Editor or beside the built executable; saved custom image and recording paths remain unchanged. Do not replace this contract with PlayerPrefs or JsonUtility. Append new persisted game IDs without renumbering existing entries.

Unity uses the selected Editor's System.Text.Json 8 BCL extension, with no duplicate framework DLLs in Assets. The shared project pins an assembly-compatible dependency. Run the actual bundled-Mono persistence probe whenever upgrading Unity or this dependency.

## Presentation lifecycle

A game derives from `Minigame` in `Runtime/GameCore.cs`. `Enter(GameServices)` creates its presentation root; `Tick(dt)`, `Key(KeyEvent)`, `Pointer(logicalPosition, rightButton)` and `DrawUI()` drive it. `Suspend()` clears transient audio, gestures and effects when focus or parent controls interrupt play. `Exit()` releases owned objects and materials. Cache continuing domain state in `GameServices.Session` when it should survive a trip to the picker; do not retain stale narration or held-input effects with that state. Re-read relevant parent settings and validate selected content when resuming.

`GameServices.Now` is active gameplay time, advanced with the same bounded delta as the models. Use it for game deadlines and animation. The Windows protection layer independently uses real monotonic time; do not substitute gameplay time for input freshness or authorization. `DiagnosticState` should expose concise state that an isolated preview can verify. Optional preview reports must never control gameplay: replace reports atomically, tolerate temporary reader locks, and commit the last-reported state only after a successful write so the next tick can retry.

Shared services own audio, rewards, content, camera fitting and input dispatch. Use `Feedback.Pulse` for a brief celebration offset; it changes only presentation, clears on focus transitions and respects gentle motion. Use `CanvasCamera(width,height)` for educational layouts and `PointerFromScreen`/`ScreenFromLogical` for aspect-correct mapping. Dot Pop uses 720 x 1080 logical coordinates; math and the canvas use 1440 x 900. Maintain the host's UI transform when temporarily changing coordinates. Keep educational targets legible and scenery subordinate to the learning task. Explorer views can compose licensed assets more densely; see [the asset workflow](unity-assets.md).

## Input and narration rules

Consume the immutable held-key snapshot separately from ordinary event transitions. Parent escape routes and focus recovery must continue working when a minigame ignores gameplay keys or its event queue is full. Minigames never own native hooks, foreground authorization or accessibility flags; those boundaries are described in [the platform guide](unity-platform.md).

`PointerInputGate` uses the shared `PointerGestureSafety` model: one initial touch can answer, multiple touches latch until all fingers lift, and emulated mouse input is suppressed briefly afterward. Quantity games also enforce `CanAnswer`, reject right clicks and debounce accepted transitions. Use shared layout hit tests instead of separately guessing screen rectangles. Cancelling serialized math phases must stop obsolete narration; the model's bounded narration wait prevents an unavailable voice from freezing a round.

## Adding a game

1. Add the engine-independent model and meaningful regression cases. Persist only durable learning state; keep effect objects and renderer state transient.
2. Append a stable `PlayMode` ID and its `GameCatalog` metadata, then add one factory in `MinigameRegistry` in `Runtime/Suite.cs`.
3. Implement a focused `Minigame` view that consumes the existing services, and register licensed content through the content workflow. Add parent controls only when they change real behavior.
4. Add isolated preview scenarios for correct answers, retries, cancellation and representative visual states. Include a native pointer case when the game has pointer targets. Inspect the built player and screenshots at relevant aspect ratios before treating the game as complete.

## Verification

Run from the repository root:

```powershell
./scripts/build-unity-domain.ps1
./scripts/test-unity-domain.ps1
./scripts/test-unity-scripts.ps1
./scripts/verify-unity.ps1
./scripts/verify-unity-pointer.ps1
```

The two test scripts discover the Editor matching `UnityPort/ProjectSettings/ProjectVersion.txt`; either also accepts `-UnityEditor` with an Editor directory or `Unity.exe` path. Domain publication runs 1,193 original regression checks against the actual shared DLL. The separate bundled-Mono probe runs 36 checks covering save compatibility, real BCL loading, all math completions, physical snapshots and touch rearming. Script compilation validates Unity runtime and Editor APIs. The Windows replay runner exercises all twelve games plus learning/effect scenarios; native pointer acceptance verifies actual click mapping and rejected input. These checks complement visual inspection and the separate platform acceptance boundaries, rather than establishing protected laptop behavior by themselves. See [build setup](unity-build-environment.md) for the discovered toolchain and full build procedure.