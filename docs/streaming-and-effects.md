# Streaming recognition and living effects

Exact words in Smash Garden are now independent of typing-speed labels. The old deliberate-only gate discarded ordinary fast typing. Physical clusters of three or more held keys still reset the spelling buffer; gesture confidence only limits fuzzy correction.

Each complete prefix has a persisted evidence score (0-8). It starts at zero:
- mom -> says mom immediately, but retains its prefix.
- Continuing to mommy says mommy immediately.
- Ending that episode as mommy adds one point to mom's continuation score.
- Ending an episode as mom subtracts one point.
- Evidence times observed typing cadence determines a wait, capped by Prefix Pause.
- Reaching momm removes the pending exact mom candidate.
- An incompatible next letter resolves mom immediately and starts a new word.
- Space, Enter, a new incompatible word, or a long pause finishes the learning episode.
- After a spoken prefix, the buffer remains available for extension until at least three seconds of inactivity (Prefix Pause + one second if larger).

No additional neural network is involved. The existing optional gesture network remains separate. Turning off Adaptive Learning makes all exact matches immediate.

Word Adventure only announces the displayed target and completes on its final letter, without a prefix wait. The next target appears after a short success animation, or immediately on the next keypress.

## Keys and effects

Key icons includes all 1,402 Font Awesome Free 6.7.2 solid icons. Child-friendly choices appear first. F1 defaults to a smiley; all other non-alphanumeric keys have deterministic defaults. Choose a key with the keyboard, search/select an icon, then Save & return. Space remains reserved for fire. Parent chords retain their original behavior. The font and license are bundled under Content/.

Space starts a short flame pulse; holding it feeds a continuous themed fireplace. Releasing it lets the fire die down. Additive textured flame particles curl upward, cool through the palette, and shed rising embers. The MIT flame atlas from yomotsu/three-particle-fire is bundled with attribution. Gentle Motion reduces emission.

Ordinary particle bursts are now droplets. An implicit density field joins outer halos and core surfaces. Nearby droplets attract; sufficiently slow contact conserves area/momentum when it fuses. Strong impacts can split larger droplets. Particle budgets are shared between droplets and embers. This is a stylized 2D surface-tension approximation, not a full fluid solver.

The old default confetti word celebration migrates to upward embers. Explicit alternate effects and custom recipes remain available. Ember shape is value 4 in recipe JSON; prior shape values keep their meanings.

Smash Garden hides its title, instructions, keyboard illustration and diagnostics after the first play input. Discovered words remain visible. Escape can still reveal the parent-chord reminder.

Icons appear at scattered positions while their key-to-icon mappings remain deterministic. Repeating a visible glyph inflates it with a short squeeze, pressure impulse, and damped settling, with bounded growth. Droplets have contrasting rims and shaded cores. Plasma and Vortex backdrops are original harmonic effects responsive to input; Aurora remains available. Black And White disables decorative background fields.


## Balloon rewards and space backdrops

Only the current consecutive key run owns an inflatable glyph. Any different key, including Space, retires that glyph. Returning to its key creates a new one; separated repeated letters are therefore independent. A retired balloon cannot pop or reinflate.

Each tap adds 0.28 size units. While active, one such step leaks away over Balloon Deflate Seconds (default 3). Retired balloons lose all remaining pressure over that interval. A damped spring preserves the squeeze/inflate/settle movement; extra volume supplies upward lift and a soft ceiling stop. Inflated glyphs remain visible while shrinking. Balloon Pop Size defaults to 3 times normal, configurable from 1.5 to 6. Popping reserves room for a themed ember/confetti reward and expanding ring.

Starfield uses forward perspective flight; Rotating Stars uses a slowly rotating 3D cloud. Star Count and Star Speed live under Developer. Gentle Motion slows travel. All star artwork and implementation are original; the references provide motion inspiration:
- https://samme.github.io/phaser-examples-mirror/demoscene/starfield.html
- https://mkhj.github.io/Demoscene-effects/effects/starfield/

Primary Colors and Starfield are the defaults. A versioned migration updates old Aurora/Plasma defaults once, while retaining other existing themes/backdrops and preserving all choices made after migration.
