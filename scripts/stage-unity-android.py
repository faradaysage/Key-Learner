"""Reversible Android build staging; canonical WAVs and Windows assets stay intact.

Run prepare before starting Unity and restore in the build wrapper's finally block.
After an interrupted host process, run restore before any Windows build.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import wave

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "UnityPort"
ASSETS = PROJECT / "Assets"
STATE = PROJECT / "Library/KeyLearnerAndroidStage"
GENERATED = ASSETS / "KeyLearnerAndroidBuild"
STREAMING = ASSETS / "StreamingAssets"
VOICE = ROOT / "Content/Voice"
BLAKE = "kyutai-Blake"
META_CACHE = PROJECT / "Library/KeyLearnerAndroidImportMetadata.json"


def safe(path, parent):
    path.resolve().relative_to(parent.resolve())
    for entry in [path, *path.parents]:
        if entry.is_symlink() or (hasattr(entry, "is_junction") and entry.is_junction()):
            raise ValueError(f"Refusing staging through a link: {entry}")
        if entry == parent:
            break
    if path.exists() and path.is_dir():
        for entry in path.rglob("*"):
            if entry.is_symlink() or (hasattr(entry, "is_junction") and entry.is_junction()):
                raise ValueError(f"Refusing staging through a link: {entry}")
    return path


def copy(source, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)


def restore():
    journal = STATE / "state.json"
    if not journal.exists():
        print("No Android staging transaction to restore.")
        return
    data = json.loads(journal.read_text(encoding="utf-8"))
    safe(STATE, PROJECT / "Library")
    for path in (GENERATED, STREAMING):
        safe(path, ASSETS)
    # Only remove our replacement StreamingAssets if the original is safely backed up.
    backup = STATE / "StreamingAssets"
    if backup.exists() or not data["hadStreaming"]:
        if STREAMING.exists():
            shutil.rmtree(STREAMING)
        if backup.exists():
            backup.rename(STREAMING)
    meta_backup = STATE / "StreamingAssets.meta"
    meta = ASSETS / "StreamingAssets.meta"
    if meta_backup.exists():
        if meta.exists():
            meta.unlink()
        meta_backup.rename(meta)
    elif not data["hadStreamingMeta"] and meta.exists():
        meta.unlink()
    if GENERATED.exists():
        # Preserve Unity-generated GUID/import settings outside Assets for incremental builds.
        cache = {p.relative_to(GENERATED).as_posix(): p.read_text(encoding="utf-8-sig") for p in GENERATED.rglob("*.meta")}
        META_CACHE.write_text(json.dumps(cache), encoding="utf-8")
        shutil.rmtree(GENERATED)
    generated_meta = ASSETS / "KeyLearnerAndroidBuild.meta"
    if generated_meta.exists():
        generated_meta.unlink()
    shutil.rmtree(STATE)
    print("Restored Windows StreamingAssets; removed generated Android staging.")


def prepare(selected):
    if BLAKE not in selected:
        raise ValueError("Blake is required in every LeapPad build")
    if STATE.exists() or GENERATED.exists():
        raise ValueError("Existing Android staging detected. Run restore first.")
    manifest = json.loads((VOICE / "voice-packs.json").read_text(encoding="utf-8"))
    if manifest.get("schema") != 1 or manifest.get("status") != "complete":
        raise ValueError("Finalized voice manifest required")
    voices = [v for v in manifest["voices"] if v["voiceId"] in selected]
    if {v["voiceId"] for v in voices} != selected or any(not v.get("readyForIntegration") for v in voices):
        raise ValueError("Requested voice is unknown or unfinished")
    corpus = json.loads((VOICE / "catalog.json").read_text(encoding="utf-8"))["clips"]
    sources = []
    metadata = [VOICE / "catalog.json"]
    for voice in voices:
        folder = safe(VOICE / voice["packDirectory"], VOICE)
        catalog = safe(folder / voice["catalog"], folder)
        clips = json.loads(catalog.read_text(encoding="utf-8"))["clips"]
        if set(clips) != set(corpus):
            raise ValueError(f"Incomplete voice: {voice['voiceId']}")
        metadata.append(catalog)
        for key, clip in clips.items():
            if clip["file"] != f"speech-{key}.wav" or any(c in clip["file"] for c in "/\\:"):
                raise ValueError("Unsafe speech filename")
            if clip.get("text") != corpus[key].get("text") or set(clip["aliases"]) != set(corpus[key]["aliases"]):
                raise ValueError(f"Voice contract mismatch: {key}")
            source = safe(catalog.parent / clip["file"], VOICE)
            if hashlib.sha256(source.read_bytes()).hexdigest().lower() != clip["sha256"].lower():
                raise ValueError(f"Voice checksum mismatch: {source}")
            with wave.open(str(source), "rb") as audio:
                if (audio.getnchannels(), audio.getframerate(), audio.getsampwidth(), audio.getcomptype()) != (1, 24000, 2, "NONE"):
                    raise ValueError(f"Expected mono 24 kHz PCM16 narration: {source}")
            sources.append(source)
    safe(STREAMING, ASSETS)
    STATE.mkdir(parents=True)
    (STATE / "state.json").write_text(json.dumps({"hadStreaming": STREAMING.exists(), "hadStreamingMeta": (ASSETS / "StreamingAssets.meta").exists()}), encoding="utf-8")
    try:
        if STREAMING.exists():
            STREAMING.rename(STATE / "StreamingAssets")
        if (ASSETS / "StreamingAssets.meta").exists():
            (ASSETS / "StreamingAssets.meta").rename(STATE / "StreamingAssets.meta")
        STREAMING.mkdir()
        notices = STATE / "StreamingAssets/Notices"
        if notices.exists():
            shutil.copytree(notices, STREAMING / "Notices")
        resource = GENERATED / "Resources/AndroidContent"
        for source in metadata + sources:
            copy(source, resource / "Voice" / source.relative_to(VOICE))
        manifest.update(voices=voices, defaultVoiceId=BLAKE, fallbackVoiceId=BLAKE, corpusCatalog="catalog.json")
        (resource / "Voice/voice-packs.json").write_text(json.dumps(manifest), encoding="utf-8")
        index = ["AndroidContent/Voice/" + source.relative_to(VOICE).with_suffix("").as_posix() for source in sources]
        (resource / "voice-assets.json").write_text(json.dumps(index), encoding="utf-8")
        for source in (ROOT / "Content/Sounds").glob("*.wav"):
            copy(source, resource / "Sounds" / source.name)
        copy(ROOT / "Content/Icons/catalog.json", resource / "Icons/catalog.json")
        for voice in voices:
            for source in (VOICE / voice["packDirectory"]).glob("*LICENSE*"):
                copy(source, STREAMING / "Notices/Voices" / voice["voiceId"] / source.name)
        for name in ("Fredoka-Play", "BalooBhai2-Play"):
            copy(ASSETS / "KeyLearner/Resources/Fonts" / (name + ".ttf"), resource / "Fonts" / (name + ".ttf"))
        for source in (ROOT / "Content/Sounds").iterdir():
            if source.is_file() and source.suffix.lower() in (".md", ".txt", ".json"):
                copy(source, STREAMING / "Notices/Sounds" / source.name)
        copy(ROOT / "Content/Icons/FONT-AWESOME-LICENSE.txt", STREAMING / "Notices/Fonts/FONT-AWESOME-LICENSE.txt")
        if META_CACHE.exists():
            for relative, text in json.loads(META_CACHE.read_text(encoding="utf-8")).items():
                meta = safe(GENERATED / relative, GENERATED)
                if meta.suffix == ".meta" and Path(str(meta)[:-5]).exists():
                    meta.write_text(text, encoding="utf-8")
        print(f"Staged {len(sources)} verified narration clips; voices: {', '.join(sorted(selected))}")
    except BaseException:
        restore()
        raise


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["prepare", "restore"])
    parser.add_argument("--voices", default=BLAKE, help="Comma-separated completed voice IDs; Blake is mandatory")
    args = parser.parse_args()
    if args.action == "prepare":
        prepare(set(args.voices.split(",")))
    else:
        restore()
