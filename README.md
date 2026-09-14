# KeyLearner: a little world of discovery

An offline Windows learning playground inspired by Scott Hanselman's Baby Smash, with thirteen Unity games (and twelve preserved MonoGame games), gesture-aware learning, bounded offline speech, and parent settings. The Unity URP implementation lives alongside the preserved MonoGame application. Both share tested C# learning, progression, scoring, movement, and JSON save contracts; no sibling Bepu checkout is required.

See [Unity build, run, and extension instructions](UnityPort/README.md), [migration evidence](docs/unity-migration.md), [gameplay gallery](docs/unity-gallery.md), and [asset credits](THIRD_PARTY_ASSETS.md). Local Unity Windows output is under artifacts/unity-windows/; the commands below still build and run the preserved MonoGame version.

## Install on a laptop

Download the **INSTALL-KeyLearner-Unity-Windows** artifact from the latest successful [Windows installer Actions run](https://github.com/faradaysage/Key-Learner/actions/workflows/windows-installer.yml) for the migration PR. Extract the ZIP and run its setup EXE; the portable player and CI artifacts are not needed for installation. [PR #8](https://github.com/faradaysage/Key-Learner/pull/8) links the current verified installer. The older 3.0.0 prerelease predates the current input and diagnostic work.

The package updates the existing per-user installation and retains the parent profile; no separate .NET installation is required. The KeyLearner splash and picker show the running version/build. If keys do not respond, use the mouse-accessible **Open diagnostics & close** button and follow [keyboard diagnostic instructions](docs/unity-input-diagnostics.md). The protected-hook delivery correction selects the single legacy Unity input backend; diagnostics report `inputBackend=legacy-only`. Target-device physical-keyboard acceptance remains pending.

[CI setup and artifacts](docs/unity-ci.md) explains Unity Personal activation, tests, caching and installer upgrade checks. [Verification](docs/unity-verification.md) documents remaining acceptance limits. See [installer builds and upgrades](docs/installer.md).

## Explorer worlds

Sky Speller, Letter Racer and Ocean Speller share the letter trail with distinct flying, driving and swimming movement. Wildlife reacts to nearby players; ambient and motion sounds follow the action. Dolphin breaches create entry/exit spray and sound, roadside pickups are forgiving, and occasional visitors give each journey surprises.

Unity's **Dinosaur Speller** adds a grounded T-rex adventure. It starts in a close third-person view. **F1** cycles close, far and first person; arrows steer/walk, Space runs, and Ctrl roars. Gentle Motion reduces step and roar camera effects. The view choice lasts for the current session.

Some scenery uses a noncommercial animal pack with the owner's explicit approval. See [the exact restricted assets and replacement/licensing checklist](NONCOMMERCIAL_ASSETS.md) before considering a commercial release.

## Visual math

Five pointer-first activities extend Dot Pop: **How Many Now?**, **What's Hiding?**, **Make the Number**, **Dot Duel**, and **Cannon Hop**. Find them under Numbers in the existing game picker. They start with concrete quantities, give patient visual explanations after mistakes, and gradually introduce arithmetic within 10. [Progression, controls, and reproducible visual tests](docs/visual-math.md).

## Try it

Build with .NET 8 or newer:

    dotnet build -c Release
    dotnet run --project tests/KeyLearner.Tests.csproj -c Release

Inspect without capturing the keyboard:

    dotnet run -c Release -- --preview
    dotnet run -c Release -- --studio

Normal play uses fullscreen and a session keyboard guard:

    dotnet run -c Release

Or launch bin/Release/net8.0-windows/KeyLearner.exe.

**The normal session guard is not an OS kiosk. Ctrl+Alt+Delete and other OS/hardware paths remain possible. Do not treat this build as an absolute desktop isolation boundary.** See [Protection](docs/protection.md) before a child uses it.

## Parent controls

Hold exactly one Ctrl key, one Alt key, and:
- **Esc** to close the game.
- **O** to open or close the parent studio.

Hold an exact combination for **two seconds**. No release is required. Left or right modifiers work. Additional physical keys restart the timer; Caps/Num/Scroll Lock indicators are ignored. Release all keys after an action before triggering another. Ctrl+Shift+O is also accepted for options.

One unmodified Escape shows the parent-control reminder without leaving Canvas/explorer play. Dot Pop stays in its game; visual math retains Escape as its Back route. Two completed, unmodified G taps within 1.2 seconds open the picker from any game. The studio supports mouse input, Tab to switch sections, arrows to select/adjust settings, and Enter to edit. The key-to-text mapping in text fields currently assumes US QWERTY. Chord timing is intentionally not a child-adjustable preference.

See [streaming recognition and living effects](docs/streaming-and-effects.md) for the updated prefix-learning behavior, deterministic icons, fireplace and merging droplets.

## Play and learning

- **Smash Garden:** key geography becomes screen position. Gentle letters, color bursts, broad showers and directional swirls respond to the input context.
- **Word Adventure:** a parent-selected set of dictionary words becomes a rotating letter-copying invitation. Enable “Adventure word” on dictionary entries to add family names or favorite objects.
- **Counting:** recognizes an ascending sequence through 100, then loops. After 9, “1” remains pending until “0”; it does not say “one” early. Counting also works in Smash Garden.
- **Spelling:** exact words start immediate; only learned continuation habits introduce a prefix wait. Typo recovery waits for a pause or space/Enter. “mom” can become “mommy.” An incompatible next letter resolves a completed word and starts the next. A unique one-edit correction can recover “miolk” → “milk” or “mlik” → “milk.” Ambiguous corrections are rejected.
- Completed longer words increase per-prefix waiting; standalone uses decrease it. Observed typing cadence scales the wait. This is transparent statistical adaptation.
- The gesture analyzer uses a bounded 1.4-second history: rate, concurrent keys, horizontal spread and path straightness. It estimates deliberate input, rapid typing, clusters, broad mashing and sweeps. It cannot prove which hands were used.
- Optional parent calibration trains a tiny 6-input / 8-hidden / 5-output neural network locally. Give balanced examples of each pattern. Predictions are used only after 40 samples and sufficient confidence; physical overlap overrides implausible typing predictions, and learned predictions cannot classify single-key typing as Cluster or BroadMash. No raw keystroke history is written to disk.

This is a working playground foundation, not a validated developmental assessment. Hardware rollover can hide keys; see the protection notes.

## Voice

Speech uses reusable, capped overlapping channels: four for key feedback and two reserved for words by default. New input never cancels audio already playing. Parent Voice settings can adjust the caps. Short queues absorb bursts; sustained overload is bounded.

Both engines now prefer committed Chatterbox recordings for the shipped vocabulary and instructions. These are ordinary mono PCM WAV files; the game and normal builds require no Python, PyTorch, CUDA, Chatterbox installation, or downloaded speech model. Family recordings still take priority. Prepared speech uses a fixed narrator; Parent Studio voice/rate choices control fallback synthesis for new family text.

Speech generation is an explicit developer operation: run `tools/setup-speech.ps1`, generate/review the twenty-line sample reel with `tools/generate-speech.ps1 -Samples`, then generate missing or changed clips with `tools/generate-speech.ps1 -KeepGoing`. See [prepared speech tooling](tools/speech/README.md) for the stable manifest, isolated Python 3.11 environment, CUDA verification, model/license pins, incremental hashes and lossless masters. CI verifies committed speech using the .NET verifier; it never generates speech.

Playback priority: a word's WAV recording → prepared Chatterbox catalog → existing cached/Piper or common clips → Windows speech. The older optional Piper setup remains compatible; see [voice setup and licensing](docs/voice.md). Runtime playback stays bounded, and Unity evicts unused decoded speech clips instead of retaining the entire vocabulary in memory. All playback honors app volume and mute.

For the most familiar voice at zero cost, attach a family recording to each favorite word in Dictionary. You can also customize the spoken phrase.

## Balloons and starfields

Consecutive presses inflate one balloon and lift it toward the ceiling. Reaching the configured pop size produces an expanding ring, glittering embers and confetti. The Balloons tab controls pop size (default 3 times normal) and deflation (default 3 seconds per inflation step). Steady toddler-paced taps can accumulate pressure. Changing keys retires the old balloon: it returns to normal size over the configured interval, and returning to its key creates a new glyph. Inflated glyphs finish shrinking before fading.

Experience offers Starfield (forward flight), Rotating Stars (a slowly turning star cloud), Aurora, Plasma and Vortex. Developer controls star count and speed; Gentle Motion slows both starfields. Both use original procedural star artwork and projection code, inspired by the referenced demoscene examples.

## Parent studio and effects

The child game picker controls modes. Experience controls coordinated themes, fonts, gentle motion, scale and keyboard display. Primary Colors and the forward-flight Starfield are the defaults. Older Aurora/Plasma default profiles migrate once; later parent choices persist. Black And White removes decorative background fields for a high-contrast monochrome scene. Fredoka and Baloo Bhai 2 from Google Fonts are bundled with their OFL licenses. Voice exposes installed voices, volume, Windows speech rate and Piper paths. Learning exposes typo handling, adaptive timing and calibration. Developer exposes physics/effect budgets and diagnostics.

Dictionary entries can be added, renamed, disabled, included in Word Adventure, and assigned a spoken phrase, WAV, image and celebration. Disabled entries replace destructive deletion. CSV dictionaries are imported on first profile creation; subsequent edits use the parent's saved JSON dictionary. Original CSV files are not modified. The legacy CSV importer supports simple unquoted word,imagePath,wavPath records; the in-game editor handles paths containing commas through JSON.

Effect plugins are bounded JSON recipes loaded from the profile's effects directory. See [effect recipes](docs/effects.md). The animation uses procedural light, harmonic curtains, curl forces, trails and bounces; it is not a full fluid solver or FFT simulation.

## Local data and verification

Settings, dictionary, learned word counts and neural weights live under %LOCALAPPDATA%/KeyLearner. Writes use temporary files, replacement and a .bak copy. A --data <directory> argument selects an isolated profile, useful for testing and separate children. Profile selection within the studio is not implemented.

    .\scripts\verify.ps1

Tests cover the chord state machine, typing mistakes and prefixes, count transitions, neural learning, persistence, and deterministic game replay. Preview scenarios never install the keyboard hook. Physical OS shortcut testing is a separate, unfinished acceptance step described in the protection document.

Generated previews and test profiles go in artifacts/ and are ignored by Git. No publishing, system policy changes, or commits are performed by the build.

## Gesture playground

Parent Studio > Play & safety toggles gesture effects, mouse play, growing fire, and the separate effects volume. Deliberate/rapid keys make letters and icons; overlapping clusters splat paint; broad mashing builds glass damage (quiet heals it); straight keyboard traces make larger deforming liquid drops. Gesture labels are estimates from timing, overlap and keyboard location, with optional parent-trained refinement.

Move the pointer to repel letters/icons and reward balloons. Left click fires the cannon in Smash Garden and Word Adventure; right click makes a local blast. Cannonballs travel until their first swept collision or until they leave the canvas. Effects particles are unaffected by repulsion. Holding Space grows the themed fireplace; assets in its depth ignite.

Counting launches one rocket per number over a four-second launch window, with approximately 0.75-second flights. Concurrent number shows overlap. Word Adventure releases 2–16 balloons based on word length and difficult letters. Each pop earns 10 points; clearing a round adds 5 points per balloon and a short screen shake (disabled by Gentle Motion). Finish the balloon round to get the next word. Scores last for the current game session.

### Windows session protection

Key transitions are deduplicated before queuing. If the bounded queue overflows, the game rebuilds held state instead of exiting or discarding releases. The keyboard hook renews periodically because Windows can silently remove a timed-out low-level hook. A single protected instance prevents competing hooks.

**Emergency exit:** tap and release Escape ten times, without pressing another key. Holding Escape does not count repeatedly. This works independently of stale modifier state. The exact Ctrl+Alt+Escape exit chord and Ctrl+Alt+O options chord each require a two-second hold.

During protected play, Sticky Keys, Filter Keys and Toggle Keys activation shortcuts and their confirmation dialogs are disabled for the session. Existing accessibility feature enablement is preserved. A separate helper restores the shortcut flags on normal exit or process termination. Preview mode does not change accessibility settings.

This is not Windows kiosk isolation: secure desktop and OS touchpad gestures cannot be blocked by this keyboard hook. Before play, set Windows three- and four-finger touchpad actions to Nothing (or disable the touchpad when using an external mouse). Parent Studio has a Touchpad setup & quit button. Use a dedicated child Windows account for containment; do not leave sensitive applications open behind protected play.

Effects recordings and their CC0 sources are documented in Content/Sounds/ATTRIBUTION.md. Effects playback is independent of speech and defaults to a quiet 30% level.

Additional replay scenarios: `cluster`, `glass`, `swipe`, `fireworks`, `word-balloons`, `word-pop`. Use `--preview --scenario word-pop --seconds 5 --screenshot <path>` for an integrated cannon/scoring test. `scripts/test-accessibility.ps1` explicitly tests real Windows shortcut flags and guardian restoration after killing its own probe process; it does not capture the keyboard.

## Preserved MonoGame graphics history

This section describes the earlier MonoGame renderer, retained alongside the new Unity implementation. The current Unity rendering and imported art are documented in [the migration report](docs/unity-migration.md). The MonoGame version uses its existing 3D GPU pipeline. The logical play area remains 1440 by 900, but it now renders to the display resolution at the selected render scale. Liquid density accumulation, surface lighting and wet paint shading run in GPU effects. Glass uses a connected convex partition: new fractures are clipped to an existing piece, pressure grows the network, and those exact polygons rotate and fall after the sheet is sufficiently divided. Refraction and reflection are screen-space approximations, not ray tracing. Letters and icons use cached, lit extrusions of the bundled font silhouettes.

**Graphics tab:** render scale, liquid resolution, terrain detail, 3D assets, toon shading, glass shading, multisample edge smoothing, VSync and flight assistance. Its six-second benchmark exercises a fixed native-resolution workload, restores the previous settings afterwards, includes update/physics time, synchronizes GPU work with readback, and reports a 95th-percentile frame time. It suggests High/Balanced/Performance settings; applying and saving remains a parent choice. This estimate is not a guarantee for every laptop or workload.

**Sky Speller (game picker):** the bird continuously flies over procedural hills, lakes and trees. Left/right turn; Up dives; Down climbs; Space accelerates; Ctrl makes a procedural squawk. Banking, pitch, speed and wind affect motion. Ground contact gently redirects flight. Collect one 3D letter at a time, earning 10 points each and a word-length bonus on completion; the completed word is spoken. Flight assistance gently steers toward the next letter and can be disabled. This implements the first proposed mode; distractor-letter and free-word modes remain future additions.

Hold exactly **Ctrl+Alt+O** (or **Ctrl+Shift+O**) for two seconds to open options. Hold exactly **Ctrl+Alt+Esc** for two seconds to exit. No release is required. Extra pressed keys restart the timer; lock indicators do not count as pressed keys. Ten O / Escape taps remain independent fallbacks.

Gesture calibration is supervised: the tiny network trains only while the parent labels a pattern, while spelling/prefix timing adapts during play. Learned predictions cannot invent a cluster or mash from single-key typing. The software keyboard now shows held keys independently of fading recent presses, plus the live and maximum held-key count. The event path is tested with twenty simultaneous keys; actual keyboard hardware may report fewer (rollover/ghosting).

Mouse trails again scatter radially with the original +/-150 velocity range and a longer fade; their size is adjustable in Play & safety. Asset collisions now reach the actual screen bottom rather than an invisible boundary 100 pixels above it.

Additional preview scenarios: `flight`, `quick-options`, `twenty-keys`, `fracture`, `benchmark` (use `--seconds 8`). Preview `--page graphics --studio` opens graphics controls. Shaders compile through the same Windows installer pipeline.

References: [MonoGame 3D rendering](https://docs.monogame.net/articles/getting_to_know/whatis/graphics/WhatIs_3DRendering.html), [custom GPU effects](https://docs.monogame.net/articles/getting_started/content_pipeline/custom_effects.html), [Microsoft keyboard ghosting explanation](https://www.microsoft.com/applied-sciences/projects/anti-ghosting).


## Preserved MonoGame release history

The following versioned sections describe the pre-port implementation. Shared behavior remains relevant, while Unity presentation and current verification are documented above.

### Input recovery and separate learning (2.0.22)

Key releases now match the physical scan code from the original press, even if Windows changes its virtual-key label after Num Lock or Shift changes. Pause/Break become complete taps; overrun packets and extended synthetic Shift do not become held keys. Main and keypad Enter retain independent physical references. An injected release that repairs state cancels chord authorization rather than completing it. Parent Studio displays repair counts without recording keystrokes.

Clusters and mashing require a fresh burst of overlapping distinct presses within 140 ms. Old held-key counts alone cannot turn paced typing into paint, and typing speed alone cannot trigger glass. Deliberate and rapid typing need no calibration. The optional network can refine a physically plausible multi-key pattern; it cannot override typing into a mash.

**Ten complete O taps open Parent Studio**, independently of the strict chord's held-key state. Any other press resets the sequence, and auto-repeat does not count. Ten Escape taps remain the exit fallback. The normal options shortcut is an exact two-second Ctrl+Alt+O (or Ctrl+Shift+O) hold; extra keys restart the timer.

**Learning** now offers separate Reset gesture training and Reset word learning buttons. The word model lives in `profile.json`; the calibration network lives in `gesture-training.json` and trains only during the twelve-second parent-labeled calibration sessions. In-game word and prefix learning never updates gesture weights. Legacy embedded gesture weights are retired on upgrade, preserving word counts and prefix habits. Calibration is off by default and can be enabled independently of word learning.

**Toon asset shading** is on by default in Graphics. Extruded letters, numbers, icons, and flight letter gates use banded lighting and dark silhouettes; turn it off to restore the previous lighting. It requires 3D assets in Smash Garden.

Regressions include translated key releases, Pause without key-up, strict options recovery, independent tap sequences, separate learning resets, and legacy profile migration. Integrated previews: `native-recovery`, `ten-o`. These do not replace acceptance testing on the laptop's physical keyboard and touchpad.

### Current key snapshots and clean resume (2.0.23)

The protected input layer publishes a 256-key snapshot independently of its bounded event queue. Parent controls check that snapshot every frame; speech, spelling and gesture recognition cannot consume or suppress it. Shortcuts no longer reconstruct a clean press/release sequence from gameplay history. Caps/Num/Scroll Lock count only while physically pressed, not while their indicator is on. Ten O taps also reset the input ledger to recover parent controls.

Startup, activation/deactivation, hide/show, minimize/restore, and maximize changes clear native held state, pending key events, speech queues/playback, effects sounds, particles, glass, paint, fireworks, word fragments and recent-key highlights. Inactive windows neither capture nor suppress any keys, including parent shortcuts. Speech/gameplay stop. Return to the game before using parent controls. Old events more than 250 ms late are discarded rather than replayed. Learned profiles are preserved. Calibration stops on a visibility change.

This snapshot is maintained by the native input interceptor, not claimed as a stateless hardware poll: ordinary Windows state readers can miss keys when their delivery is suppressed. The previous release-order dependency is gone, and return-to-game resets now clear the native state as well as the screen. Application-level keyboard protection still does not prevent all OS/secure-desktop/touchpad escape paths.

Validation scenarios: `options`, `shift-options`, `extra-key`, `quick-options` (short holds rejected), `native-recovery`, `window-resume` (actual SDL hide/show with queued speech and effects), `ten-o`, and `twenty-keys`.


### Explorer games and patient spelling (2.0.24)

The game picker includes **Sky Speller**, **Letter Racer** and **Ocean Speller**. All three collect letters in order, speak the completed word and award letter/word points, with score at the upper right. Existing parent controls and clean-resume behavior are unchanged.

- Bird Flight: Up dives, Down climbs continuously into full loops, Left/Right turn. Space accelerates toward a much higher boost limit. Double-tap either turn key within 320 ms for a barrel roll. Ctrl squawks and briefly reveals/attracts the next letter, with a cooldown.
- Racing: a perspective road with curves, lane markings, striped shoulders and a race car. Left/Right steer, Up accelerates, Down brakes, Space boosts. Ctrl calls the next letter closer. Assistance helps with collection; road shoulders keep young drivers nearby.
- Dolphin: an underwater camera, dolphin body/tail, coral, kelp and fish. Flight-style controls swim between the seafloor and surface; Ctrl activates the letter-attracting sonar. Double-tap turns roll the dolphin.

Bird/racing journeys move through forest, lakes, mountains, city, river, town and tundra regions. Terrain heights blend at boundaries; scenery includes dense trees, snowy rock, houses and taller buildings. These are procedural, stylized regions, not downloaded assets. Flight assistance remains optional. Learning includes response and boost-limit controls.

**Word Adventure** now has an independent sequential spelling model. The first eight completed words have no per-letter timeout. Incorrect letters do not erase the correct prefix. Words start at up to three letters and grow by one letter per five successes (using the shortest available dictionary entries when needed). Timed challenges, if enabled, begin at 45 seconds per letter and tighten slowly, with a 15-second floor. Expiry removes only the most recent correct letter, plays a quiet buzz and shakes the letter cards unless Gentle Motion is enabled. It waits for another correct press before restarting the timer, rather than repeatedly erasing progress. The next letter and its keyboard key are highlighted; the score at upper right combines spelling and balloon rewards. Turn timed spelling off in Learning to keep unlimited time.

**Cannon:** left click fires in both Smash Garden and Word Adventure; right click makes a local burst. The pointer chooses direction only. Cannonballs use swept collision tests, explode at the first asset they hit, and leave the screen quietly when they miss. Counting fireworks still burst at their scheduled destinations.

New replay scenarios: `racing`, `dolphin`, `flight-loop` (5 seconds), `region-City`, `region-Mountains`, `region-River`, `patient-red` (10 seconds), `spelling-timeout`, `cannon-miss`. The current development session could not initialize OpenGL for either this build or the previous installer, so this iteration's rendered scenes and GPU performance have not been visually validated here. Simulation tests and the Release/installer build are checked separately.


### Focus isolation and the game picker (2.0.25)

Launch opens a child-accessible game picker. During play, tap **G, then G** within 1.2 seconds (release between taps). Arrows select, Enter plays, Tab cycles All / Explore / Letters / Numbers, and cards support hover and click. The three explorer cards use in-engine previews; canvas games use illustrations. Mode selection is no longer buried in parent options. `GameCatalog` supplies each game's display name, description, suggested starting age, type, topics, and optional movement kind.

The keyboard guard starts disarmed. Every native callback verifies the game's own foreground window and the desktop's UOI_IO input flag before reading, retaining, or suppressing a key. A failed query passes through. Losing foreground or switching desktop revokes capture and clears the physical ledger; it is not re-enabled just because a key happens to arrive. The visible game loop clears pending audio/effects and re-arms it on return. Caps Lock toggle state is not a held key. Outside the game Windows receives Caps Lock normally. No background parent shortcuts or password keystrokes are collected. Accessibility shortcut settings are restored while inactive. This is still a session hook, not a Windows kiosk or secure-desktop lockout.

Explorer architecture separates `LetterCourse` (progress, score, completion interval), `IExplorerMovement` (spawn, physics, next gate), `ExplorerWorld` (terrain), `FlightRenderer` (presentation), and the catalog. Bird/dolphin share airborne movement with distinct height and speed policies; car movement owns shoulders and shoreline sweeps. Add a movement implementation and catalog entry for future explorer types, then supply its rendering assets.

With assistance enabled, releasing directional controls recovers upright treetop flight, including from inverted/high flight. Mountain terrain has two high banks around a winding valley and gates lead through it. Car terrain is tessellated outside the road using shared shoulder vertices; no ground triangle crosses the asphalt. Shoulders resist steering and reduce speed, tires kick up dirt off road, and shoreline collision prevents entering water. Letter meshes face the camera without horizontal mirroring. Green start rings and gold finish markers distinguish endpoints; completing a word releases an exploding word/particle reward, a synthesized power-up chime, a brief shake (disabled by Gentle Motion), and a score bonus before the next word appears.

Verification: regression tests cover missing key-up/focus changes, no background Caps/password history, repeated GG input, every mode's metadata, inverted high-flight recovery, valley banks, glyph orientation, shoulder tessellation, shoreline collision and one-time reward scoring. That historical MonoGame preview failed inside OpenGL initialization before game code. The Unity migration has since built and exercised the Windows Direct3D player; current evidence is in docs/unity-platform.md and docs/unity-migration.md. Physical lock-screen acceptance remains separate. No antivirus settings were changed.


### Dot Pop: toddler subitizing (2.0.26)

Choose **Dot Pop** from the game picker (second page, or the Numbers filter). No keyboard is needed to answer: click or touch one of ten large, fixed 0–9 buttons. A mute/unmute button is the only in-game control besides the answers. The stage number sits at the top right. The 720×1080 portrait composition fills the available height or width without stretching; landscape displays have quiet side margins. Monitor orientation is not changed.

READY → SET → GO → dots → HOW MANY? Each correct answer launches a rapid volley, pops precisely the occupied cells, and starts the next stage after a fixed 1.12-second reward, even for nine. Zero is an empty grid, with its own completion burst. Wrong answers cause only a small, silent shake and restore the same dots for an untimed retry. Multi-touch clusters, held clicks, and countdown/reward taps cannot submit a stream of answers. Ready/Set/Go are displayed; How many? is spoken when sound is enabled. Effects use the existing cannon/pop sounds. Gentle Motion removes the shake.

Difficulty increases only on correct first attempts: 12 successes with 0–3 grouped dots; 16 with 0–4 varied patterns; 16 with 5–6 grouped patterns; 16 with 7–9 grouped patterns; then 20 with 5–9 increasingly irregular patterns. These are untimed. At 80 first-attempt successes, dots display for four seconds, decreasing by 45 ms per further success to a 400 ms floor. The answer buttons never time out. Retries advance the stage but not difficulty. Stage and difficulty are session-local; restarting the application starts gently again.

`DotPatterns` represents all 512 3×3 masks, including zero. Selection balances quantities, favors the least-seen eligible masks, then less familiar rotation/reflection families, and avoids the immediately previous pattern. This prevents the many five-dot masks from crowding out zero and nine. `SubitizingGame` owns progression and timers, and `DotLayout` shares portrait geometry between rendering and hit testing. Visibility changes restart the current pattern from READY and cancel sound/volley effects without losing earned progression.

Preview scenarios: `dots-visible`, `dots-correct`, `dots-retry`. Use `--preview --mode dots --portrait` for a portrait window. Pure regression tests exercise the full pattern universe, difficulty tiers, retries, timers, zero/nine reward timing, duplicate answer rejection, focus reset and portrait/landscape hit mapping. Native touch hardware and the GPU presentation still require a working graphics session; no antivirus exceptions are needed for this mode.

## Unity immersion updates

Ocean Speller adds poppable bonus bubbles, fleeing small fish, surface breaches, and gentle water-particle response to the mouse. Letter Racer follows curved lane markings, turns its body/front wheels, and offers harmless slowing obstacles and treasure chests that change vehicle or color. Sky Speller adds occasional flocks, ground traffic/animals, and dolphin activity over lakes. The Arctic includes the supplied animated polar bear and a smaller cub.

Explorer ambience and motion sounds respond to movement and vehicle type and become quieter under speech. Counting Stars briefly labels individual fireworks and celebrates 100. Five correct subitizing rounds earn a pearl bonus question worth triple points; visible balls can be recolored without answering. Cannon Hop supports arrows and Enter, including answer 10, and explains its existing practice prerequisite when selected while locked.


**Noncommercial art notice:** builds containing the WildMesh wolf are restricted to noncommercial use until it is separately licensed or replaced. The source code remains MIT; third-party art keeps its own licenses. See [the exact restricted-asset register](NONCOMMERCIAL_ASSETS.md) before any commercial release.


## Narrator packs

Parent Studio's Voice tab now has a data-driven Narrator selector and **Try this voice** preview (Space). Selection applies immediately; Save & Return persists it. The original narrator remains the only production voice while the separate Blake/Butter/Lake generation job finishes. No incomplete speech packs are shipped. See [voice-pack integration and validation commands](docs/VOICE_PACK_INTEGRATION.md). Normal builds need Git LFS, .NET and Unity; they never run Python or speech generation.
