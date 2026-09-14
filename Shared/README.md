# Shared gameplay domain

`KeyLearner.Domain.csproj` compiles the authoritative model files in `Studio/` into a .NET Standard 2.1 assembly. The MonoGame game and original model harness still compile those same files. There is no second copy of learning rules to drift during the renderer migration. Public APIs retain the `KeyLearner.Studio` namespace.

Run `../scripts/build-unity-domain.ps1` from PowerShell to build, run all 1,193 existing checks against the actual shared DLL, and update `UnityPort/Assets/Plugins/KeyLearner/KeyLearner.Domain.dll`. The modern .NET compiler handles records and collection expressions outside Unity's source compiler. `Compatibility.cs` supplies only record compiler support and integer population counts. Equivalent older API calls in linked sources preserve behavior; the Unity save path uses atomic `File.Replace` because .NET Standard has no overwrite `File.Move` overload.

The Unity 6000.6 editor's BCL extensions already supply System.Text.Json 8 and its dependencies. The domain pins NuGet System.Text.Json 8.0.6 (assembly identity 8.0.0.0) and intentionally does not copy any framework DLL into Assets. When upgrading Unity, verify JSON persistence in the actual Editor/player again before changing this dependency.

| Model | Presentation contract |
| --- | --- |
| `Store(string root = null, string contentRoot = null)` | Settings, Words, Profile, Gestures, MathLearning. Default remains `%LOCALAPPDATA%/KeyLearner`. `Save()` keeps temporary replacement and `.bak`; callers use isolated roots in preview. |
| `GuidedSpelling` | Set `Timed`, `Start(word)`, `Add(char, now)`, `Update(now)`. Correct prefix survives stray letters; `Score`, `Completed`, `Progress`, `FeedbackUntil`, `Remaining(now)` drive UI. |
| `WordRecognizer(Store)` | Smash streaming recognition: `Add(char,now,InputContext)`, `Update(now)`, `Flush()`. Returned `WordEntry` triggers narration/reward. Keep separate from guided spelling. |
| `CountingRecognizer` | `Add(digit,now)` returns completed number or null, preserving multi-digit pending input. `Expected`, `Pending`, `Update(now)`, `Reset()`. |
| `FireworkSchedule` | `Add(number,now)` schedules number rockets across four seconds. `Due(now)` returns launches this frame. |
| `BalloonReward` | `Start(difficulty)`, `Pop()`; last pop returns true and scores all-clear bonus. `Remaining`, `Score`. |
| `BalloonMotion` | Consecutive-key pressure: `Inflate()`, retire via `Release()`, `Step(dt,deflateSeconds,popSize)`. `Size`, `Squeeze`, `Popped`. |
| `FlightModel` | `Configure(ExplorerKind)`, `SetWord(word)`, `Step(dt,turn,pitch,boost,assist)` returns any collected letter. `Position`, `Forward`, `Up`, `Gate`, `Score`, `Collected`, `Completed`, `RewardRemaining`; `TapTurn` and `Signal` keep roll/call rules. Vectors are System.Numerics; adapter owns coordinate conversion. |
| `SubitizingGame(seed, mastery)` | `Step(dt)`, `Answer(number)`, `RestartRound()`. `Mask`, `Quantity`, `DotsVisible`, `Phase`, `CanAnswer`, `Popped`, `Stage`, `Mastery`. Layout/hit mapping remains `DotLayout`. |
| `MathGame(activity,progress,seed,level?,a?,b?,subtract?)` | `Step(dt,narrationBusy)`, `Answer(value)`, `Cell(index)` for builders, `Restart()` for resume. State includes `Phase`, `Round`, `Difficulty`, `Motion`, `Counting`, `Popped`, `CanAnswer`, `Revision`. Save when revision changes. `MathLayout` is shared 1440x900 geometry. |
| `GameCatalog` | Data-only metadata for persisted `PlayMode` IDs 0..11; Unity registry attaches lifecycle/presentation constructors. `GameShortcut` preserves completed unmodified GG taps. |
| Input models | `KeyEvent`, `KeySnapshot`, `ParentHold`, `PhysicalKeyboard`, `KeyTransitionBuffer`, `InputFocus`, independent emergency tap routes. Presentation must consume held snapshots independently from queued gameplay events. |

The regression runner at `Tests/KeyLearner.Domain.Regression.csproj` references the netstandard binary and links the original full test program. It covers all previous learning, physical-ledger, safe-input, explorer boundary, progression, save, retry, focus and narration regressions.

`../scripts/test-unity-domain.ps1` discovers the installed Editor matching the Unity project and runs a second executable inside that Editor's bundled Windows Mono runtime, loading only its BCL extensions. Its 36 checks validate JSON records/properties/numeric enum keys, atomic overwrite/backups, malformed-source preservation, external family paths, full physical snapshots, all five math completions, guided/counting behavior, vector-based explorer simulation, and touch gesture latching, emulated-mouse exclusion and focus rearming. Pass `-UnityEditor` with either the discovered Editor directory or its `Unity.exe` path to override discovery. Test profiles and logs stay under `artifacts/unity-migration/mono-domain`. The [extension guide](../docs/unity-domain.md) describes lifecycle, input and service boundaries for future games.
