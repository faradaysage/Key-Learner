# Prepared speech (development only)

The game plays ordinary WAV assets. Neither engine invokes Python, Chatterbox, PyTorch, CUDA, Hugging Face, or this generator. Family recordings take priority; existing Windows/Piper fallback remains available only when a family adds text outside the prepared catalog. Voice selection in Parent Studio chooses Blake or Original narrator; custom Windows/Piper controls affect only unknown family text.

## Setup on Windows

Run `pwsh -File tools/setup-speech.ps1` explicitly. It installs verified uv 0.12.13, a managed Python 3.11.16 interpreter and `.local/speech-venv`, all inside this repository. It does not edit PATH, register Python globally, replace an existing interpreter or install global packages. The environment uses PyTorch/torchaudio 2.6.0 CUDA 12.4 wheels and a pinned dependency lock. `setuptools==80.9.0` supplies the legacy `pkg_resources` API required by PerTh 1.0.1; do not bypass the watermarker. CUDA was verified on the development Quadro RTX 4000. CPU generation is available on machines without CUDA.

- Samples: `pwsh -File tools/generate-speech.ps1 -Samples -Device cuda`
- Full catalog: `pwsh -File tools/generate-speech.ps1 -KeepGoing -Device cuda`
- Verify with no Python: `dotnet run --project tools/KeyLearner.SpeechVerify -c Release -- .`
- Verify with the developer environment: `pwsh -File tools/generate-speech.ps1 -Verify`

The generator loads no model at all when everything is unchanged, or when verifying hashes. Setup/generation are never normal build steps. The ordinary Unity staging script only copies committed files from `Content/Voice`.

## Inventory and voice

`manifest.json` has stable semantic IDs and aliases for 4,899 distinct clips: all 26 letters, 0–100, every mathematical instruction template, default keyboard icon names, feedback, parent voice preview, and the deduplicated public app/CPB/custom dictionaries. `build_manifest.py` explicitly reads only those public CSV files; it never reads `private_dictionary.csv` or parent profiles. Explicit shared additions such as Apollo and Athena live in `additional_words.json`; this does not import any private CSV or profile. Add future required phrases there or extend the manifest intentionally. Arbitrary future family text and custom icon labels cannot be enumerated in advance and keep the existing recording/fallback path.

`voice.json` pins model repository, immutable model revision, default speaker conditioning, seed, sampling parameters and WAV formats. No human reference voice has been copied or cloned. Any later voice-reference addition must have documented permission/license and include its content hash in the recipe before generation. The generator leaves Chatterbox's PerTh watermarker enabled; it does not explicitly remove watermarks. Letter-context trimming is recorded, and no watermark-detection claim is made for those short excerpts.

Twenty representative sample lines are marked in the manifest. `.local/speech/sample-reel.wav` is the listening reel, with `sample-order.json` and independent Whisper `sample-transcriptions.json` alongside it. Transcript checks are a content check, not a substitute for subjective listening. Isolated letters use a complete context sentence and a deliberate pause; the generator uses word alignment to remove the setup words and saves that context/alignment for inspection. Their runtime aliases remain the individual letters.

Review the samples before bulk generation. Record `.local/speech/sample-review.json` with a `configHash` equal to SHA-256 of sorted compact UTF-8 `voice.json` JSON and a description of the review. This protects against accidentally bulk-generating a new voice without sampling it. It is a developer review, not an extra end-user permission requirement.

## Incrementality and files

Each catalog entry stores a recipe hash of the spoken/generation text, context trim, voice/model revision, seed, generation settings and output settings, plus the output SHA-256 and actual model-file hashes. Only missing, corrupted or changed entries are regenerated. Writes use temporary files and atomic replacement; a stopped run can resume without discarding completed clips. IDs and runtime filenames do not depend on ordering.

- Commit `tools/speech/*`, the two PowerShell commands, `Content/Voice/catalog.json`, license, and generated `speech-*.wav` assets.
- `.local/speech/masters` contains lossless float WAV masters at the native 24 kHz sample rate. The game-ready WAV files are mono PCM16, directly supported by MonoGame `SoundEffect.FromStream` and the Unity WAV reader.
- `.local/speech/context` preserves untrimmed letter masters and alignment details. `.local/speech/huggingface` and `.local/speech/audit-models` contain downloaded models. All `.local` content is ignored and never packaged.
- Unity's generated StreamingAssets speech copies and their `.meta` files are ignored; CI stages the canonical committed `Content/Voice` files. No duplicate model or audio source repository is needed.

The runtime resolver is `Studio/PreparedSpeechCatalog.cs`, compiled into both engines. It normalizes whitespace/case and terminal punctuation; it rejects paths outside the voice directory. Explicit family recordings remain first. Unknown family text uses the pre-existing bounded playback/fallback queues.

## Attribution

Chatterbox by Resemble AI: https://github.com/resemble-ai/chatterbox (MIT). Model: https://huggingface.co/ResembleAI/chatterbox . Full license ships in `Content/Voice/CHATTERBOX_LICENSE.txt`. Original generated audio and any failed runs remain local until a valid output passes generation checks. Do not disable watermarking to work around dependency failures.

For a bounded repair, the Python command accepts `--ids letter-k letter-n`. Per-entry `seedOffset` and generation text are part of the recipe; changing one leaves all other recordings untouched. `--keep-going` records failures in `.local/speech/generation-failures.json` and continues other clips, then returns a failing exit code if any remain. Create `.local/speech/pause-generation` to pause between clips; remove that file before explicitly resuming. A process lock rejects a second generator against the same catalog. Do not run older tooling concurrently. `-Samples` also refreshes the local listening reel after targeted repairs, even when no clips need regeneration.

## Pronunciation review

`pronunciation_overrides.json` records bounded pronunciation/context repairs by stable ID. Text, per-phrase seed and alignment model are included in the recipe, so they cannot silently reuse stale output. The initial base recognizer missed some setup words; reviewed repairs use small.en alignment without supplying the expected transcript. A missing alignment marker fails safely and preserves the old runtime asset. Inspect `.local/speech/context` when a trim fails.

`python tools/speech/audit_catalog.py --ids <id> --model small.en` independently transcribes individual clips. `python tools/speech/audit_batches.py` checks many clips using word timestamps in local concatenated reels. Batching is faster but repeated sequences (especially ascending numbers) can confuse the recognizer; empty/mismatched transcripts require individual review, not automatic regeneration. All recognizer dependencies and audit audio remain development-only. Neither transcript agreement nor waveform checks establish subjective voice quality.
