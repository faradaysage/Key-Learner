"""Offline, incremental transcription spot checks. Flags require review; no auto-rewriting."""
import argparse,hashlib,json,os,re
ap=argparse.ArgumentParser()
ap.add_argument("--all-words",action="store_true")
ap.add_argument("--model",default="base.en",choices=["base.en","small.en"])
ap.add_argument("--ids",nargs="+")
ap.add_argument("--device",choices=["cpu","cuda"],default="cpu")
args=ap.parse_args()
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
os.environ["HF_HUB_DISABLE_IMPLICIT_TOKEN"]="1"
os.environ["HF_HOME"]=str(ROOT/".local/speech/huggingface")
if args.device=="cuda" and os.name=="nt":
    import torch
    cuda_dlls=os.add_dll_directory(str(Path(torch.__file__).parent/"lib"))
from faster_whisper import WhisperModel
from build_manifest import number
out=ROOT/(".local/speech/catalog-transcriptions"+("-"+args.model if args.model!="base.en" else "")+".json")
records=json.loads(out.read_text()) if out.exists() else {}
entries=json.loads((ROOT/"tools/speech/manifest.json").read_text(encoding="utf-8"))["entries"]
index=json.loads((ROOT/"Content/Voice/catalog.json").read_text(encoding="utf-8"))["clips"]
model=None
words=0
for e in entries:
    if args.ids and e["id"] not in args.ids:continue
    if e["id"].startswith("word-"):words+=1
    c=index.get(e["id"])
    if not c:continue
    if not args.all_words and not args.ids and e["id"].startswith("word-") and words%25 and not (len(e["text"])>=4 and c["seconds"]<.32):continue
    if records.get(e["id"],{}).get("sha256")==c["sha256"]:continue
    if model is None:model=WhisperModel(args.model,device=args.device,compute_type="float16" if args.device=="cuda" else "int8",cpu_threads=3,download_root=str(ROOT/".local/speech/audit-models"))
    segments,_=model.transcribe(str(ROOT/"Content/Voice"/c["file"]),beam_size=3,language="en",condition_on_previous_text=False,vad_filter=False,max_new_tokens=16 if len(e["text"].split())<3 else 64)
    transcript=" ".join(s.text.strip() for s in segments)
    def normalize(text):
        text=re.sub(r"[^a-z0-9 ]"," ",text.lower());text=" ".join(text.split()).replace("takeaway","take away")
        for n in range(100,-1,-1):text=re.sub(r"\b"+number(n)+r"\b",str(n),text)
        return text
    matched=normalize(transcript) in {normalize(a) for a in e["aliases"]}
    records[e["id"]]=dict(expected=e["text"],transcribed=transcript,sha256=c["sha256"],matched=matched)
    temp=out.with_suffix(".tmp.json");temp.write_text(json.dumps(records,indent=2)+"\n",encoding="utf-8");temp.replace(out)
    print(json.dumps(dict(id=e["id"],expected=e["text"],transcribed=transcript,matched=matched)),flush=True)
print(f"{len(records)} incremental spot checks recorded. Mismatches are review flags, not proof of bad speech.")
