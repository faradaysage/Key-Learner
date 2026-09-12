# Visual math games

The suite has five new entries under Numbers: How Many Now?, What's Hiding?, Make the Number, Dot Duel, and Cannon Hop. Each starts gently and adapts independently after four accurate rounds. Two assisted rounds in the last three ease difficulty by one step; stages are never removed. Answers are untimed. The number track unlocks after three joining and three separating completions in How Many Now?.

Use large pointer targets, with no dragging or rapid input. Make the Number completes on reaching the target; occupied slots can be removed. Games/back and unmodified Escape return to the existing picker. The parent chords and global sound settings remain available. Math progress is local in `math-progress.json`; word and gesture learning are unchanged.

`DotMath.cs` contains the deterministic constrained generator, eight difficulty configurations, progress and update-driven round state. `StudioGame.Math.cs` handles pointer mapping, narration and presentation. `MathBallRenderer.cs` uses a single cached sphere mesh and the existing Toon shader. Correct impacts use Canvas's existing mote particles. No background effects run while evaluating a math question.

Errors replay the same transformation more slowly. A second error reveals/counts objects, including the hidden part, before another attempt. Counting is not an answer timeout. At advanced levels visual support can be hidden after a stable observation interval; errors restore that support. Equation-first joining/separating asks before the animation, then confirms the transformation on success.

## Reproducible previews

Build with `dotnet build -c Release`. All debug overrides require `--preview`, which leaves keyboard protection disabled and uses an isolated profile with `--data`:

```
KeyLearner.exe --preview --math HowManyNow --math-level 4 --math-a 5 --math-b 2 --math-operation subtract --mute --scenario math-round --seconds 5 --data artifacts/math-debug --screenshot artifacts/subtract.png
```

- `--math`: HowManyNow, Hiding, MakeNumber, Duel, CannonHop.
- `--math-level`: 1–8; clamped to valid bounds.
- `--math-a`, `--math-b`: force valid operands. Invalid combinations fail explicitly.
- `--math-operation`: add or subtract for joining/separating and hops.
- `--math-state`: correct, incorrect, count, or a MathPhase name. Applied when the initial question becomes ready, so the setup remains visible first.
- `--width 1366 --height 768`, `--render-scale .5`: exercise laptop resolutions and reduced rendering quality.
- `--mute`: silence preview narration and effects only in the supplied profile.
- `--scenario math-complete`: exercise the same pointer hit targets used in play, answer correctly, and verify one stage is earned.

`scripts/test-math-visuals.ps1` runs the five activities, success volleys, corrective counting, subtraction to zero, advanced equations, and reduced render resolution. It requires a connected, unlocked interactive Windows session with an available display. A disconnected Remote Desktop session can make SDL report `No displays available`, which MonoGame 3.8.2 obscures with an OpenGL exception. Reconnect the session before changing GPU drivers or antivirus exclusions.

Model tests cover thousands of generated rounds at every activity/difficulty, correct arithmetic, nonnegative results, distinct answer choices, frame capacity, deterministic seeds, progression bounds, retries, focus return, and local save restoration.


For native pointer verification, run `scripts/test-math-pointer.ps1 -Interactive` in an unlocked desktop. This opt-in check shows a temporary preview, waits until its question is ready and the window is active, clicks the answer twice, then clicks Games. It asserts exactly one earned stage and return to the picker, restores the pointer position, and never installs the keyboard guard. Preview state/coordinate logs are written only for this explicit debug scenario.

Verified locally: all 1,193 model checks; all five pointer-target completion replays; thirteen 1366×768 graphics scenarios including reduced resolution, hidden-part counting and equations; real Windows pointer input with double-click suppression and back navigation; and the existing suite's visual regression scenarios. The spoken-round trace completed Ready, Set, Go, quantity, operation, and question in order with peak overlap 1 and no overflow.
