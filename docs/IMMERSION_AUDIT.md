# Immersion review, 2026-09-14

Review evidence: the existing twelve-game Windows captures in artifacts/immersion-all-games, the interactive acceptance reports for painting/bonus/hop/steering/breaching, current presentation and domain code, and the first Dinosaur Speller launch. This is a working director review; automated audio/counter inspection does not establish subjective sound quality. No redesign of learning rules is intended.

| Game | What already works | Highest-impact remaining gap / chosen treatment |
| --- | --- | --- |
| Smash Garden | Immediate varied keyboard effects, large uncluttered canvas | Idle introduction is static and sparse. Add quiet peripheral drift and inviting, reversible hover/press response; keep free creation central. |
| Word Adventure | Prominent target, patient typing and balloon feedback | Waiting feels frozen; give balloons restrained buoyancy and a soft, varied completion flourish. Never rush the child. |
| Counting Stars | Large number and individual rocket counts; new 100 finale | Empty idle sky and abrupt final burst. Review paced rising trails and distinct finale silhouettes, with count labels readable and short. |
| Sky Speller | Terrain, settlements, source animated bird, clouds | Repeated evenly distributed trees and fauna on sine loops. Habitat-aware pauses, flight avoidance, companion encounters, layered ambient one-shots and composed landmarks. |
| Letter Racer | Curved markings, wheel steering, speed-sensitive motor, forgiving pickups | Wide sterile road edges; traffic reverses abruptly. Use forward route traffic, verge reactions/dust, occasional localized spectator events, and biome-specific audio. |
| Ocean Speller | Source fish/coral, caustics, assisted route, ballistic surface breach | Sparse middle distance, mechanically looping fish and sudden surface audio. School avoidance, predator anticipation/retreat, bubbles, visible splash/wake and underwater light shafts. |
| Dot Pop | Clear groups, touch paint, earned pearl rounds | Static waiting and identical touch response. Small local spring response on touched balls; quiet peripheral motion without moving count positions. |
| How Many Now? | Joining/leaving balls and clear choices | Transitions should feel physical while preserving exact counts. Local settling, gentle answer acknowledgement and distinct add/remove audio. |
| What's Hiding? | Occluder supports the concept | Box feels like a raw panel. Subtle reveal anticipation/settle, no distracting animation that reveals the answer early. |
| Make the Number | Direct frame interaction, large targets | Placement feels abrupt. Soft settling and per-placement tonal variation; completed frame gets brief cohesive feedback. |
| Dot Duel | Strong separation and comparison targets | Static columns and bare buttons. Restrained chosen-side feedback, no moving/reordering the dots during comparison. |
| Cannon Hop | Corrected keyboard selection including ten; native-input case passed | Needs stronger visible launch/landing physicality and landing sound; retain nonpunishing retry and readable number line. |
| Dinosaur Speller | Shared spelling course and new grounded model, heavy steps/roar | First launch found runtime shader stripped in incremental build. Fix first, then inspect first-person composition, source gait, reactive herds, fern canopy, cliffs/peaks/volcano and distant life. |

Shared priorities: movement/feedback first, then audio and actor reaction, then animation/ambient life/effects, then transitions and rare events. Add only assets with a deliberate placement and role. Keep school targets stationary/readable during recognition. Bound actors, audio voices and particles; use shared materials and source animation phase variation. Respect mute, speech ducking, focus safety and gentle motion. World actors pause with suspended gameplay.

Do not infer completion from this document: each changed behavior needs an actual Windows run, logs and representative visual review. Pending download/licensing entries remain explicit in MANUAL_DOWNLOADS.md and NONCOMMERCIAL_ASSETS.md.

## Current visual acceptance

- Build 19: native F1 presses cycled Close → Far → First person → Close. A held F1 produced only one switch. All three screenshots showed the intended framing; third-person T-rex and first-person shadow visibility are separate. Terrain clearance remained above the tested minimum, with word progress preserved.
- Build 20: the bird visitor approaches beside the player without overlapping silhouettes; relative arrival is damped. The source shark is visibly swimming across the reef, preceded by measured fish-scatter events. The dolphin's exit capture shows a clear spray/wake and distant source boat on the horizon. Both movement-driven encounter tests passed with sound enabled.
- Licensed art integrity: all 488 source-file byte counts and SHA-256 hashes match the provenance manifest. The tree-fern fossil remains local and is not presented as living canopy. Commercial restrictions remain explicit.
- The shader-stripping issue was corrected; later dinosaur captures include working ground detail, foliage, geology and flying source pterosaurs. A licensed authored volcano and distinct eagle are still optional manual acquisitions; no crude final substitute was installed.
- Prepared speech now passes complete recipe, file, WAV and shared-runtime resolution checks for all 4,897 entries. Contextual generation repaired short-word onset issues. Independent transcription retains ambiguous short-word and proper-name flags; this is not a listening-quality certification.

- Build 23/25: native acceleration, braking and Ctrl selected authored Run, Idle and Roar states; walking resumed afterward. Build 25 captured all four blended poses. The camera remained behind the visible T-rex, with a distance-driven gait tied to the shared stride/footfall counter. No attack/damage behavior was added.

- Build 26: the source neck/head roar is layered over the ongoing gait, preserving visible leg motion during travel. Native camera/motion checks and eight PlayMode lifecycle tests passed.
