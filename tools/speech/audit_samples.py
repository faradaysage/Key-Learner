"""Independent development-only transcription check; never part of builds."""
import json, os, sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
os.environ["HF_HUB_DISABLE_IMPLICIT_TOKEN"]="1"
os.environ["HF_HOME"]=str(ROOT/".local/speech/huggingface")
from faster_whisper import WhisperModel
model=WhisperModel("base.en",device="cpu",compute_type="int8",cpu_threads=4,download_root=str(ROOT/".local/speech/audit-models"))
manifest=json.loads((ROOT/"tools/speech/manifest.json").read_text())
results=[]
for entry in manifest["entries"]:
    if not entry.get("sample") and not ("--letters" in sys.argv and entry["id"].startswith("letter-")):continue
    path=ROOT/"Content/Voice"/("speech-"+entry["id"]+".wav")
    segments,info=model.transcribe(str(path),beam_size=5,language="en",condition_on_previous_text=False,vad_filter=False)
    spoken=" ".join(s.text.strip() for s in segments)
    result=dict(id=entry["id"],expected=entry["text"],transcribed=spoken)
    results.append(result);print(json.dumps(result),flush=True)
(ROOT/".local/speech/sample-transcriptions.json").write_text(json.dumps(results,indent=2))
