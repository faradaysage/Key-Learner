# Voice-pack integration

## Current phase

The source generation job was active at preflight (2026-09-14): Blake was approximately 1,900/4,897 valid clips, Butter/Lake incomplete. No finalist was ready. Its manifests, output, worker and generation tooling are outside this task's mutation scope while running. No partial production pack may be imported or staged.

## Architecture

`Store` owns one engine-independent voice-pack registry. Both audio adapters resolve semantic IDs through it, preserve family-recording priority, and keep custom-text synthesis as the existing fallback for text outside the required corpus. Existing textual game calls adapt to semantic IDs centrally; minigames never construct paths or name voices.

The runtime registry consumes `Content/Voice/voice-packs.json`, an explicitly finalized projection of the generator manifest. The manifest declares `defaultVoiceId`, corpus, ready packs and relative pack/catalog paths. The existing narrator is the safe compatibility dataset while finalized packs are unavailable. Final import will require an explicit stable default ID (planned Blake, unless the user chooses another), not filesystem ordering.

Catalogs must expose identical IDs/text/aliases. Runtime file validation is lazy and cached by length/write time; an unavailable or invalid individual clip falls back to the same key in the manifest default. Bounded diagnostics report bad pack/key cases once. Unknown keys fail quietly; new family text retains its existing fallback.

Settings persist only `VoicePackId`. Existing Options apply changes immediately and Save & Return persists them. The selector uses registry display names; preview uses the normal `cue-voice-preview` semantic ID. Pack selection is data, not an enum or per-game branch.

Finalized runtime WAVs are copied once into `Content/Voice/Packs/`; Unity stages them recursively and MonoGame copies the same canonical data. WAV is already the runtime format, so no second encoded set or float masters are shipped. Production speech WAVs use Git LFS; manifests/scripts remain ordinary text. CI must fetch LFS before validation. No history rewrite.

## Remaining asset gate

The supplied job must report complete and every finalist must be ready with a full validating corpus before final import. Counts alone are insufficient. Development output (`artifacts/voice-packs`, `.work`, references, caches and environments) remains ignored. Implementation can be tested with isolated small fixture packs while production generation continues.

## Commands and production layout

Normal builds use .NET and Unity only. After cloning, run `git lfs pull` before local verification (CI checks out LFS automatically).

```powershell
# Read-only checks; fails on any bad production pack, even if runtime could fall back.
dotnet run --project tools/KeyLearner.SpeechVerify -c Release -- .
dotnet run --project tools/KeyLearner.VoicePacks -c Release -- verify .
# Isolated PCM fixtures; no speech generation or production writes.
dotnet run --project tools/KeyLearner.VoicePacks -c Release -- self-test .

# Only after the existing job reports complete and every finalist is ready:
dotnet run --project tools/KeyLearner.VoicePacks -c Release -- import . artifacts/voice-packs --default kyutai-Blake
dotnet run --project tools/KeyLearner.VoicePacks -c Release -- verify . --require-packs
```

The importer reads but never modifies the generation directory. It refuses running/incomplete worker or pack state before any copy. It compares the snapshotted corpus with the current game manifest; validates every exact ID/text/alias, recipe, seed/model identity, SHA-256, encoding, minimum duration and absence of extras; checks generator validation reports; validates its staged copy through the runtime resolver; and rechecks source completion before promotion. It accepts all manifest voices together, not a partially completed subset. Paths must stay inside their roots and cannot pass through reparse points. Existing production packs are moved to a transaction backup under `artifacts/voice-pack-import/`, with rollback on promotion failure. Retain that backup until the replacement build is verified. Run imports with the game/builds closed.

The projected runtime manifest is `Content/Voice/voice-packs.json`. The top-level `defaultVoiceId` is explicit; `corpusCatalog` is `catalog.json`. Each voice retains source/license/model/generation metadata, has `readyForIntegration: true`, and references `Packs/<stable-id>/Voice/catalog.json`. Production copies include runtime WAVs/catalog, `voice.json`, the Chatterbox license and text source provenance. Reference audio and `.work` masters are deliberately excluded. The original narration remains available for old installations and invalid-manifest recovery.

Both games show **Parent Studio → Voice → Narrator** using manifest display names. Arrow/click selection applies immediately; **Try this voice** or Space uses `cue-voice-preview`. Save & Return writes `VoicePackId` to the existing `settings.json`. `PreviewVoice(candidate, settings)` itself never changes that setting. Selecting a new narrator cancels pending/playing narration so the next request uses the new pack; Unity music and effects continue. Family recordings retain priority. Unknown custom family text can still use existing Windows/Piper fallback; missing known speech keys never synthesize an unrelated substitute.

Release diagnostics live in the profile's `voice-pack-diagnostics.log`, capped at approximately 64 KiB, with up to 128 distinct registry warnings per session. They contain pack/key identifiers, not spoken text or family recordings.

## Git scope

`.gitattributes` tracks only `Content/Voice/speech-*.wav` and `Content/Voice/Packs/**/*.wav` through LFS. JSON, provenance, scripts and configuration stay regular Git text. Existing finalized narration is converted in the new commit only; old history is untouched. The initial conversion indexed 4,897 files. The indexed `speech-word-apple.wav` is a real LFS pointer (`oid sha256:0dfc94b87eb7d36f3714cb920b8d8b3ab3a1a9adcb049c02abdc3a87bc36fab2`, size 28,844), not a WAV blob.

Ignored development/staging locations include `.local/` (venv/model/reference caches), `artifacts/` (generation output, auditions, `.work`, verification fixtures), `__pycache__/`, `*.pyc`, Unity Library/Temp/Logs/Builds, and generated Unity StreamingAssets narration copies. Generator/audition source belonging to the parallel generation task is not staged by this integration task.

## Validation boundary

The implementation is tested with independent small PCM fixture packs, not unfinished finalists. These establish discovery, persistence/reordering, immediate selection, distinct Unity decoded clips, candidate preview isolation, same-key fallback, invalid metadata, bad encoding/hashes and strict import rejection. They do not establish narration quality. Real multi-voice listening/gameplay acceptance, final default selection and final-pack LFS staging remain pending until all three supplied voices finish. `verify --require-packs` intentionally fails before then.
