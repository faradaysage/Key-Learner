# Streaming recognition and living effects

The learning contracts below are shared by the preserved MonoGame implementation and the Unity port. `PROJECT_HISTORY.md` records the regression fixes; current source is authoritative. Unity presentation is implemented by `CanvasGame`, its renderer helpers and shared audio/reward services, while recognition, balloon pressure, counting and progression remain engine-independent.

## Recognition and word rewards

Exact Smash Garden words are independent of gesture speed labels: the old deliberate-only gate discarded normal fast typing. Physical clusters of three or more held keys reset the spelling buffer; gesture confidence limits fuzzy correction. Optional supervised gesture calibration remains separate from adaptive word timing.

Each complete prefix has persisted evidence from 0 to 8. Initially, `mom` is spoken immediately and its prefix remains available for `mommy`. Ending the episode as `mommy` adds continuation evidence to `mom`; ending as `mom` subtracts it. Evidence and observed typing cadence determine a wait capped by Prefix Pause. Reaching `momm` removes the pending exact `mom`; an incompatible letter resolves the shorter word and begins the next one. Space, Enter, incompatible input or a long pause ends the learning episode. After a spoken prefix, extension remains possible for at least three seconds of inactivity, or Prefix Pause plus one second when larger. Turning off Adaptive Learning makes exact matches immediate.

Word Adventure announces only its displayed target and completes on the final required letter. Completion immediately speaks that word and releases colored reward balloons. Popping them adds points; the final balloon earns an all-clear bonus and advances the target. It does **not** skip directly to a new target after a short generic success animation or the next arbitrary keypress. Timed spelling remains a parent choice. Restart spelling resets the guided session's completed/score progression while keeping saved adaptive word habits.

## Keyboard and mouse effects

The 1,402 Font Awesome Free 6.7.2 solid icons retain deterministic key mappings and the original child-friendly ordering. F1 defaults to a smiley; non-alphanumeric keys have consistent defaults. Parent Studio supports key selection, search, assignment and restoring a key's default. Space remains reserved for fire. Unity stages the original catalog and license under `StreamingAssets/Content/Icons`; the original MonoGame content remains under `Content`.

Mouse play keeps its established mapping:

- Left click fires a projectile from the bottom-center cannon toward the pointer. A hit causes an impact/pop; a miss does not explode just because its lifetime expires.
- Right click causes the immediate local splash/blast.
- Pointer motion leaves overlapping additive radial-gradient trails and repels glyph assets. It does not repel effect particles.
- Mouse Play disables these canvas mouse interactions. Quantity/math games have their own answer controls, right-button rejection and rapid-press protection.

Space taps make a short flame pulse; holding feeds a themed fire that grows upward, with release allowing it to die down. Nearby assets can ignite. Unity uses the licensed flame atlas and colored flame/smoke/ember systems; Gentle Motion and effect limits reduce activity. The original MIT atlas attribution is preserved.

Keyboard clusters produce paint; broad mashes fracture connected glass panes; sweeps leave liquid trails. Distinct core/boundary colors, merging surfaces and irregular edges make the liquid legible. The Unity liquid renderer uses its own density/surface passes and honors Liquid Scale. This is a stylized effect, with the shared blob dynamics retained where practical. Glass geometry follows connected crack boundaries, not unrelated decorative shards.

The old default Confetti celebration migrates to upward embers once. Explicit alternate celebrations and data-only recipes remain available. Ember shape has value 4 in recipe JSON; earlier values keep their meanings.

Smash Garden hides its title, instructions, keyboard illustration and diagnostics after first play input while retaining discovered words. One Escape shows the parent-control reminder without abandoning Canvas/explorer play; G, G returns to the picker. Focus loss clears input fragments, pointer edges, speech and transient effects. Background input is never retained or suppressed; see `protection.md`.

## Consecutive balloon runs

Only the current consecutive key run owns an inflatable glyph. Any different key, including Space, retires it. Returning to the earlier key creates a new glyph, so separated repeated letters remain independent. A retired balloon cannot pop or reinflate.

Each tap adds 0.28 size units. While active, one step leaks away over Balloon Deflate Seconds (default 3). Retired balloons lose remaining pressure over that interval. A damped spring preserves squeeze/inflate/settle motion, with lift and a soft ceiling. Inflated glyphs remain visible while shrinking. Balloon Pop Size defaults to three times normal and is adjustable from 1.5 to 6. Popping makes room for a themed reward and expanding ring.

## Palette and backdrops

Primary Colors and forward Starfield remain defaults. Versioned migrations update earlier Aurora/Plasma defaults once while retaining later parent choices. Black And White suppresses decorative background fields. Aurora, Plasma, Vortex and rotating stars remain intentional parent options.

Starfield suggests forward perspective flight; Rotating Stars uses a rotating cloud. Star Count and Star Speed are in Developer; Gentle Motion slows movement. Original star artwork and harmonic backdrop code are retained/adapted independently of the third-party explorer models. Historical motion references are the [Phaser starfield example](https://samme.github.io/phaser-examples-mirror/demoscene/starfield.html) and [Demoscene starfield](https://mkhj.github.io/Demoscene-effects/effects/starfield/); they are references, not bundled assets.

## Historical renderer details and verification

The MonoGame renderer's implicit density field, area/momentum-preserving blob fusion and impact splitting remain useful implementation references in the preserved source. Unity has a distinct presentation pipeline; a visually similar shader does not prove physical or lifecycle parity. Preview replays must use active gameplay time, inspect diagnostic counts and actual frames, and inspect rendered screenshots.

The Unity acceptance runners cover rapid `milk`, prefix extension, consecutive/nonconsecutive balloon runs, paint, connected glass, swipes, held/tapped fire, sequential counting, Word Adventure's balloon/all-clear phase, misses, quantity/math answers and parent navigation. See `docs/unity-platform.md` and `docs/unity-migration.md` for current results and limitations. Earlier documentation that reversed the mouse buttons or omitted the Word Adventure balloon phase is superseded by this contract.
