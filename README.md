# KeyLearner: a little world of discovery

A Windows native, offline keyboard playground inspired by Scott Hanselman's Baby Smash. This revision replaces the active application shell with a MonoGame studio, gesture-aware learning, controlled speech, and a parent-only settings interface. The original source is retained for reference; only Studio/**/*.cs is compiled. No sibling Bepu checkout is required.

## Install on a laptop

The Windows installer workflow produces a self-contained x64 setup EXE and SHA-256 checksum under its **KeyLearner-Windows-installer** artifact. Download, unzip, and run setup. Subsequent installers update the same per-user installation and retain the parent profile. No .NET installation is required. See [installer builds and upgrades](docs/installer.md).

## Try it

Build with .NET 8 or newer:

    dotnet build -c Release
    dotnet run --project tests/KeyLearner.Tests.csproj -c Release

Inspect without capturing the keyboard:

    dotnet run -c Release -- --preview
    dotnet run -c Release -- --preview --studio

Normal play uses fullscreen and a session keyboard guard:

    dotnet run -c Release

Or launch bin/Release/net8.0-windows/KeyLearner.exe.

**The normal session guard is not an OS kiosk. Ctrl+Alt+Delete and other OS/hardware paths remain possible. Do not treat this build as an absolute desktop isolation boundary.** See [Protection](docs/protection.md) before a child uses it.

## Parent controls

Hold exactly one Ctrl key, one Alt key, and:
- **Esc** to close the game.
- **O** to open or close the parent studio.

Hold all three for at least **0.7 seconds**, then release every key. Left or right modifiers work. Extra Shift, a second Ctrl/Alt, Windows keys, or any other key invalidates the whole attempt, even if released before the chord. Release everything and start again. An extra key while releasing also invalidates it. Auto-repeat does not authorize actions.

Escape alone shows a small reminder. It does not leave play. The studio supports mouse input, Tab to switch sections, arrows to select/adjust settings, and Enter to edit. The key-to-text mapping in text fields currently assumes US QWERTY. Chord timing is intentionally not a child-adjustable preference.

See [streaming recognition and living effects](docs/streaming-and-effects.md) for the updated prefix-learning behavior, deterministic icons, fireplace and merging droplets.

## Play and learning

- **Smash Garden:** key geography becomes screen position. Gentle letters, color bursts, broad showers and directional swirls respond to the input context.
- **Word Adventure:** a parent-selected set of dictionary words becomes a rotating letter-copying invitation. Enable “Adventure word” on dictionary entries to add family names or favorite objects.
- **Counting:** recognizes an ascending sequence through 100, then loops. After 9, “1” remains pending until “0”; it does not say “one” early. Counting also works in Smash Garden.
- **Spelling:** exact words start immediate; only learned continuation habits introduce a prefix wait. Typo recovery waits for a pause or space/Enter. “mom” can become “mommy.” An incompatible next letter resolves a completed word and starts the next. A unique one-edit correction can recover “miolk” → “milk” or “mlik” → “milk.” Ambiguous corrections are rejected.
- Completed longer words increase per-prefix waiting; standalone uses decrease it. Observed typing cadence scales the wait. This is transparent statistical adaptation.
- The gesture analyzer uses a bounded 1.4-second history: rate, concurrent keys, horizontal spread and path straightness. It estimates deliberate input, rapid typing, clusters, broad mashing and sweeps. It cannot prove which hands were used.
- Optional parent calibration trains a tiny 6-input / 8-hidden / 5-output neural network locally. Give balanced examples of each pattern. Predictions are used only after 40 samples and sufficient confidence; physical overlap can override an implausible “deliberate” prediction. No raw keystroke history is written to disk.

This is a working playground foundation, not a validated developmental assessment. Hardware rollover can hide keys; see the protection notes.

## Voice

Speech uses reusable, capped overlapping channels: four for key feedback and two reserved for words by default. New input never cancels audio already playing. Parent Voice settings can adjust the caps. Short queues absorb bursts; sustained overload is bounded.

This checkout also has a project-local **Piper 1.4.2** installation and **LJ Speech** neural model under .local/. Seventy common letters, numbers and words have been prepared as local clips. New profiles automatically discover this installation. Existing profiles can select the executable and model under Voice:

- Executable: .local/piper/Scripts/piper.exe (use its absolute path)
- Model: .local/voices/en_US-ljspeech-high.onnx (use its absolute path)

See [voice setup and licensing](docs/voice.md) to reproduce the installation. The 70 common clips ship with the game. The optional full model/runtime stays local and is not included in the installer.

Playback priority: a word's WAV recording → cached offline Piper → bundled common clips → Windows speech. Uncached neural speech is prepared in the background for later use; it never blocks the current announcement. All playback honors the app volume. Windows speech rate affects Windows synthesis; pre-recorded and neural clips retain their natural pace. The Sound option mutes all speech.

For the most familiar voice at zero cost, attach a family recording to each favorite word in Dictionary. You can also customize the spoken phrase.

## Parent studio and effects

Experience controls modes, coordinated themes, fonts, gentle motion, scale and keyboard display. Primary Colors is the new-profile default. Black And White removes decorative background fields for a high-contrast monochrome scene. Fredoka and Baloo Bhai 2 from Google Fonts are bundled with their OFL licenses. Voice exposes installed voices, volume, Windows speech rate and Piper paths. Learning exposes typo handling, adaptive timing and calibration. Developer exposes physics/effect budgets and diagnostics.

Dictionary entries can be added, renamed, disabled, included in Word Adventure, and assigned a spoken phrase, WAV, image and celebration. Disabled entries replace destructive deletion. CSV dictionaries are imported on first profile creation; subsequent edits use the parent's saved JSON dictionary. Original CSV files are not modified. The legacy CSV importer supports simple unquoted word,imagePath,wavPath records; the in-game editor handles paths containing commas through JSON.

Effect plugins are bounded JSON recipes loaded from the profile's effects directory. See [effect recipes](docs/effects.md). The animation uses procedural light, harmonic curtains, curl forces, trails and bounces; it is not a full fluid solver or FFT simulation.

## Local data and verification

Settings, dictionary, learned word counts and neural weights live under %LOCALAPPDATA%/KeyLearner. Writes use temporary files, replacement and a .bak copy. A --data <directory> argument selects an isolated profile, useful for testing and separate children. Profile selection within the studio is not implemented.

    .\scripts\verify.ps1

Tests cover the chord state machine, typing mistakes and prefixes, count transitions, neural learning, persistence, and deterministic game replay. Preview scenarios never install the keyboard hook. Physical OS shortcut testing is a separate, unfinished acceptance step described in the protection document.

Generated previews and test profiles go in artifacts/ and are ignored by Git. No publishing, system policy changes, or commits are performed by the build.
