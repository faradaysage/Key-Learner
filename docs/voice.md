# Free local voice

This checkout uses a separate Piper 1.4.2 Python environment and the en_US-ljspeech-high model. The model card identifies the LJ Speech dataset as public domain. Piper itself is GPL-3.0. Keep the engine/model notices and review redistribution obligations before packaging or distributing the runtime; KeyLearner's application license is unchanged.

Official sources:
- [Piper](https://github.com/OHF-Voice/piper1-gpl)
- [Piper CLI](https://github.com/OHF-Voice/piper1-gpl/blob/main/docs/CLI.md)
- [LJ Speech voice model card](https://huggingface.co/rhasspy/piper-voices/blob/main/en/en_US/ljspeech/high/MODEL_CARD)

Reproduce from the repository directory with a compatible Python:

    python -m venv .local/piper
    ./.local/piper/Scripts/python.exe -m pip install piper-tts==1.4.2
    ./.local/piper/Scripts/python.exe -m piper.download_voices en_US-ljspeech-high --data-dir .local/voices
    ./.local/piper/Scripts/python.exe scripts/prepare-voice.py --model .local/voices/en_US-ljspeech-high.onnx --sample artifacts/neural-voice-sample.wav

Create artifacts/ before requesting a sample. The prepared files sit beside the model in <model>.clips/. The app discovers this installation on first profile creation when run from this checkout. Otherwise enter both absolute paths in the parent studio. Bundled common clips also work without a Piper installation. No synthesis service or network request runs during play.

The app first checks per-word recordings and prepared clips, then bundled common clips. Uncached text speaks with Windows immediately while a bounded background queue prepares Piper audio for later use. Generation has a 15-second deadline, with argument-list escaping and redirected stdin. Successful clips are cached by model path, modification time and text, up to 500 files. Background completion never replays a stale phrase.

Reusable voice channels allow capped overlap, default four key channels and two reserved word channels. New words and keys never cancel existing playback. Queues hold up to eight keys and 32 words; requests beyond those limits are rejected, with overflow visible in replay diagnostics. Parent controls can adjust simultaneous key voices from one to five and word voices from one to three. Opening the parent studio deliberately stops playback. Recorded and synthesized WAV playback runs through MonoGame audio on the game thread, with adjustable volume. Windows speech rate does not change WAV playback speed.

Naturalness is subjective. Listen to artifacts/neural-voice-sample.wav and compare with the Windows voice preview; recordings by a parent remain an excellent free option.
