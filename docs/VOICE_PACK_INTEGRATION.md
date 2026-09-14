# Voice-pack integration

## Current phase

Blake is a finalized production pack: **4,898 / 4,898 clips**, including the explicitly requested Apollo addition. Its stable ID `kyutai-Blake` is the default for new/unset/invalid selections. Original narrator (`builtin`) remains intact, selectable, and the same-key fallback. Existing explicit selections are preserved. Butter and Lake are intentionally on hold; do not generate or wait for them. Their future import path remains data-driven.

## Architecture

`Store` owns one engine-independent voice-pack registry. Both audio adapters resolve semantic IDs through it, preserve family-recording priority, and keep custom-text synthesis as the existing fallback for text outside the required corpus. Existing textual game calls adapt to semantic IDs centrally; minigames never construct paths or name voices.

The runtime registry consumes `Content/Voice/voice-packs.json`, an explicitly finalized projection of the generator manifest. The manifest declares independent `defaultVoiceId` and `fallbackVoiceId`, corpus, ready packs and relative pack/catalog paths. These are stable IDs, not filesystem ordering. Older manifests without `fallbackVoiceId` retain their previous default-as-fallback behavior. Invalid fallback declarations recover to Original narrator.

Catalogs must expose identical IDs/text/aliases. Runtime file validation is lazy and cached by length/write time; an unavailable or invalid individual clip falls back to the same key in the manifest fallback. Bounded diagnostics report bad pack/key cases once. Unknown keys fail quietly; new family text retains its existing fallback.

Settings persist only `VoicePackId`. Existing Options apply changes immediately and Save & Return persists them. The selector uses registry display names; preview uses the normal `cue-voice-preview` semantic ID. Pack selection is data, not an enum or per-game branch.

Finalized runtime WAVs are copied once into `Content/Voice/Packs/`; Unity stages them recursively and MonoGame copies the same canonical data. WAV is already the runtime format, so no second encoded set or float masters are shipped. Production speech WAVs use Git LFS; manifests/scripts remain ordinary text. CI must fetch LFS before validation. No history rewrite.

## Asset gate

Only explicitly selected, complete packs may be imported while others are on hold. A running worker is refused; each selected pack must pass full corpus, metadata, recipe and audio validation. Counts alone are insufficient. Development output (`artifacts/voice-packs`, `.work`, references, caches and environments) remains ignored. No generator command is run by import, verification, builds or CI.

## Commands and production layout

Normal builds use .NET and Unity only. After cloning, run `git lfs pull` before local verification (CI checks out LFS automatically).

```powershell
# Read-only checks; fails on any bad production pack, even if runtime could fall back.
dotnet run --project tools/KeyLearner.SpeechVerify -c Release -- .
dotnet run --project tools/KeyLearner.VoicePacks -c Release -- verify .
# Isolated PCM fixtures; no speech generation or production writes.
dotnet run --project tools/KeyLearner.VoicePacks -c Release -- self-test .

# Import only the finalized pack; other source packs may remain on hold:
dotnet run --project tools/KeyLearner.VoicePacks -c Release -- import . artifacts/voice-packs --default kyutai-Blake --voices kyutai-Blake --fallback builtin
dotnet run --project tools/KeyLearner.VoicePacks -c Release -- verify . --require-packs
```

The importer reads but never modifies the generation directory. It refuses running worker or incomplete selected pack state before any copy. Without `--voices`, the legacy all-packs-complete gate still applies. It compares the snapshotted corpus with the current game manifest; validates every exact ID/text/alias, recipe, seed/model identity, SHA-256, encoding, minimum duration and absence of extras; checks generator validation reports; validates its staged copy through the runtime resolver; and rechecks source manifest, worker, corpus and selected metadata fingerprints before promotion. Per-voice `repairSeedOffsets` are preserved and verified against each clip recipe/seed; the shared corpus is unchanged. Adding a selected pack retains already finalized production packs, so later Butter/Lake integration requires no gameplay edits. Paths must stay inside their roots and cannot pass through reparse points. Existing production packs are moved to a transaction backup under `artifacts/voice-pack-import/`, with rollback on promotion failure. Retain that backup until the replacement build is verified. Run imports with the game/builds closed.

The projected runtime manifest is `Content/Voice/voice-packs.json`. The top-level `defaultVoiceId` and `fallbackVoiceId` are explicit; `corpusCatalog` is `catalog.json`. Each voice retains source/license/model/generation metadata, has `readyForIntegration: true`, and references `Packs/<stable-id>/Voice/catalog.json`. Production copies include runtime WAVs/catalog, `voice.json`, the Chatterbox license and text source provenance. Reference audio and `.work` masters are deliberately excluded. The original narration remains available for old installations and invalid-manifest recovery.

Both games show **Parent Studio → Voice → Narrator** using manifest display names. Arrow/click selection applies immediately; **Try this voice** or Space uses `cue-voice-preview`. Save & Return writes `VoicePackId` to the existing `settings.json`. `PreviewVoice(candidate, settings)` itself never changes that setting. Selecting a new narrator cancels pending/playing narration so the next request uses the new pack; Unity music and effects continue. Family recordings retain priority. Unknown custom family text can still use existing Windows/Piper fallback; missing known speech keys never synthesize an unrelated substitute.

Release diagnostics live in the profile's `voice-pack-diagnostics.log`, capped at approximately 64 KiB, with up to 128 distinct registry warnings per session. They contain pack/key identifiers, not spoken text or family recordings.

## Git scope

`.gitattributes` tracks only `Content/Voice/speech-*.wav` and `Content/Voice/Packs/**/*.wav` through LFS. JSON, provenance, scripts and configuration stay regular Git text. Existing finalized narration is converted in the new commit only; old history is untouched. The initial conversion indexed 4,897 files. The indexed `speech-word-apple.wav` is a real LFS pointer (`oid sha256:0dfc94b87eb7d36f3714cb920b8d8b3ab3a1a9adcb049c02abdc3a87bc36fab2`, size 28,844), not a WAV blob.

Ignored development/staging locations include `.local/` (venv/model/reference caches), `artifacts/` (generation output, auditions, `.work`, verification fixtures), `__pycache__/`, `*.pyc`, Unity Library/Temp/Logs/Builds, and generated Unity StreamingAssets narration copies. Generator/audition source belonging to the parallel generation task is not staged by this integration task.

## Validation boundary

Strict verification checks both production corpora (9,796 WAVs total), including exact semantic contracts, hashes/PCM, repaired generation recipes and requested-pack resolution without masked fallback. Isolated regression fixtures cover additive import, held peers, corrupt selection refusal, default/fallback separation, preserved Original selection, persistence/reordering and decoder fallback. Actual-player verification uses isolated profiles and the normal Options/preview/audio paths; it does not alter parent saves or synthesize OS keys. Audio decode/completion evidence is not a subjective listening-quality claim.

```powershell
scripts/verify-unity-ux.ps1 -Output artifacts/blake-ux -SkipNativeFocus -VerifyVoices
```
