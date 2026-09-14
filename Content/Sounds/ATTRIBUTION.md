# Sound effects

All source recordings are CC0 (public domain dedication). No account, runtime download, or paid service is required.

| Packaged file | Source / creator | Processing |
| --- | --- | --- |
| crack.wav | [Impact Sounds](https://kenney.nl/assets/impact-sounds), Kenney, impactGlass_light_000 | Mono, 22.05 kHz, peak reduced |
| shatter.wav | Same pack, impactGlass_heavy_000 | Mono, 22.05 kHz, peak reduced |
| paint.wav | [Interface Sounds](https://kenney.nl/assets/interface-sounds), Kenney, drop_001 | Mono, 22.05 kHz, peak reduced |
| pop.wav | Same pack, drop_003 | Soft stylized balloon pop; mono, 22.05 kHz |
| cannon.wav | [Explosions](https://opengameart.org/content/explosions-4), EZduzziteh, explosion1_0 | First three seconds, mono, 22.05 kHz, peak reduced |
| fire.wav | [Fireplace Sound loop](https://opengameart.org/content/fireplace-sound-loop), PagDev, fire.wav | Eleven-second excerpt with crossfaded loop seam; mono, 22.05 kHz |

The effects bus defaults to 30% of the speech master level, with additional per-effect attenuation, eight simultaneous one-shots, and short duplicate suppression. Fire uses a separate quiet loop. Speech is independent and is never cancelled by effects.

License: https://creativecommons.org/publicdomain/zero/1.0/

`squawk.wav` is an original procedural bird chirp synthesized locally for this project from frequency-modulated sine waves and seeded noise. It contains no external recording.

## Explorer immersion and learning rewards

`IMMERSION_SOURCES.json` contains exact download URLs, archive members, source/output SHA-256 hashes and processing details for every new clip. All are CC0 1.0.

- Wind: **wind1**, Luke.RUSTLTD — https://opengameart.org/content/wind1
- Underwater ambience: **Underwater Ambient Pad**, isaiah658 — https://opengameart.org/content/underwater-ambient-pad
- Engine: **racing car engine sound loops**, domasx2 — https://opengameart.org/content/racing-car-engine-sound-loops (the author replaced the original upload with a public-domain source).
- Wing motion: **Large Wings Flap**, AntumDeluge, based on dave.des — https://opengameart.org/content/large-wings-flap
- Swimming, bubbles and surface splash: **Skippy Fish Water Sound Collection** — https://opengameart.org/content/skippy-fish-water-sound-collection ; creator recorded in the source ledger.
- Ball touch, treasure, soft bumps, braking accents and musical rewards: **Interface Sounds**, **Impact Sounds**, **Music Jingles**, Kenney — https://kenney.nl/assets/interface-sounds ; https://kenney.nl/assets/impact-sounds ; https://kenney.nl/assets/music-jingles

Runtime files are mono 24 kHz PCM WAV, normalized with headroom; loop seams are crossfaded. Explorer ambience uses three bounded sources, slow volume/pitch envelopes, mute handling and automatic attenuation during narration. No source archive, conversion tool or network connection is required at runtime.

Bonus-round music: **Happy**, Alex McCulloch (Pro Sensory), CC0 — https://opengameart.org/content/happy . A quiet 24-second excerpt with end fades, mixed down during narration.

Dinosaur calls: **T-rex Calls**, CaveboyTup, CC0 (created from CC0 alligator/lion/elk samples), https://opengameart.org/content/t-rex-calls . A bounded 4.4-second excerpt with fades. Heavy steps: **Fantozzi’s Footsteps (Grass/Sand & Stone)**, Fantozzi / qubodup, CC0, https://opengameart.org/content/fantozzis-footsteps-grasssand-stone . Two sand-footstep recordings lowered in pitch, normalized and faded. Exact processing/hashes are in IMMERSION_SOURCES.json.

- Forest ambience and three positional bird-call excerpts: **Ambient Bird Sounds**, isaiah658, CC0, https://opengameart.org/content/ambient-bird-sounds . Mono/24kHz PCM16, normalized and edge faded; short excerpts at 3/12/23 seconds. Exact source/output hashes in IMMERSION_SOURCES.json.
- Crowd cheer: **Cheers**, AuraVoice / Nocturnal_Vanguard, CC0, https://opengameart.org/content/cheers-0 . Mono/24kHz PCM16, normalized and edge faded. Used for occasional spectator reactions.

Beach Ocean Waves by jasinski (submitted by qubodup), CC0 1.0. https://opengameart.org/content/beach-ocean-waves . Four source recordings mixed to mono, resampled to 24kHz PCM16, normalized and faded at edges. Used for surface ambience and breach spray tails.

- **learning-home.wav**: “Children’s Game Music 3 - Home” by heartade, [OpenGameArt](https://opengameart.org/content/childrens-game-music-3-home), CC0 1.0. Piano/flute/strings at low gain beneath the learning canvas; speech ducking and mute respected. Mono/24kHz PCM16 adaptation and 120ms edge fades; source/output hashes in IMMERSION_SOURCES.json.
