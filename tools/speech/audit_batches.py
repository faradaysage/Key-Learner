"""Independent word-timestamp audit of short clips, batched to avoid 30s ASR padding per word.
Review flags are evidence for inspection, never automatic pronunciation replacements.
Run explicitly after generation; no Python or recognizer is used by builds or the game.
"""
import argparse,hashlib,json,os,re
from pathlib import Path
import numpy as np,soundfile as sf
from scipy.signal import resample_poly
from math import gcd
from build_manifest import number
ROOT=Path(__file__).resolve().parents[2]
LOCAL=ROOT/".local/speech"
os.environ["HF_HUB_DISABLE_IMPLICIT_TOKEN"]="1"
os.environ["HF_HOME"]=str(LOCAL/"huggingface")
ap=argparse.ArgumentParser();ap.add_argument("--model",default="small.en");ap.add_argument("--ids",nargs="+");ap.add_argument("--device",choices=["cpu","cuda"],default="cpu");args=ap.parse_args()
if args.device=="cuda" and os.name=="nt":
    import torch
    cuda_dlls=os.add_dll_directory(str(Path(torch.__file__).parent/"lib"))
from faster_whisper import WhisperModel
out=LOCAL/("batch-audit-"+args.model+".json")
records=json.loads(out.read_text()) if out.exists() else {}
entries=json.loads((ROOT/"tools/speech/manifest.json").read_text(encoding="utf-8"))["entries"]
index=json.loads((ROOT/"Content/Voice/catalog.json").read_text(encoding="utf-8"))["clips"]
pending=[e for e in entries if e["id"] in index and (not args.ids or e["id"] in args.ids) and records.get(e["id"],{}).get("sha256")!=index[e["id"]]["sha256"]]
if not pending:print("No new or changed speech to audit.");raise SystemExit(0)
model=WhisperModel(args.model,device=args.device,compute_type="float16" if args.device=="cuda" else "int8",cpu_threads=3,download_root=str(LOCAL/"audit-models"))
def normalize(t):
 t=" ".join(re.sub(r"[^a-z0-9 ]"," ",t.lower()).split())
 for n in range(100,-1,-1):t=re.sub(r"\b"+number(n)+r"\b",str(n),t)
 return t
folder=LOCAL/"audit-batches";folder.mkdir(exist_ok=True)
while pending:
 batch=[];pieces=[];length=0
 while pending:
  e=pending[0];c=index[e["id"]]
  if batch and length+c["seconds"]>23:break
  pending.pop(0)
  a,sr=sf.read(ROOT/"Content/Voice"/c["file"],dtype="float32");g=gcd(sr,16000);a=resample_poly(a,16000//g,sr//g)
  batch.append((e,length,length+len(a)/16000));pieces.extend([a,np.zeros(7200,dtype="float32")]);length+=len(a)/16000+.45
 audio=np.concatenate(pieces);key=hashlib.sha256("|".join(index[e["id"]]["sha256"] for e,_,_ in batch).encode()).hexdigest()[:16]
 sf.write(folder/(key+".wav"),audio,16000,subtype="PCM_16")
 segments,_=model.transcribe(audio,language="en",beam_size=3,word_timestamps=True,condition_on_previous_text=False,max_new_tokens=240)
 words=[w for seg in segments for w in seg.words]
 for e,start,end in batch:
  assigned=[w for w in words if start-.07<=(w.start+w.end)*.5<end+.22]
  transcript=" ".join(w.word.strip() for w in assigned)
  matched=normalize(transcript) in {normalize(a) for a in e["aliases"]}
  record=dict(expected=e["text"],transcribed=transcript,matched=matched,sha256=index[e["id"]]["sha256"],batch=key,start=round(start,3),end=round(end,3))
  records[e["id"]]=record
  print(json.dumps(dict(id=e["id"],**record)),flush=True)
 temp=out.with_suffix(".tmp.json");temp.write_text(json.dumps(records,indent=2)+"\n",encoding="utf-8");temp.replace(out)
print(str(len(records))+" clips audited. Review transcription mismatches; they are not proof of bad speech.")
