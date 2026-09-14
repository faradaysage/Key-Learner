"""Explicit offline asset generation. No game build imports this module."""
import argparse, hashlib, importlib.metadata, json, os, random, sys, time, wave
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
LOCAL=ROOT/".local/speech"
os.environ.setdefault("HF_HOME",str(LOCAL/"huggingface"))
os.environ.setdefault("HF_HUB_DISABLE_TELEMETRY","1")
os.environ.setdefault("HF_HUB_DISABLE_IMPLICIT_TOKEN","1")
MANIFEST=ROOT/"tools/speech/manifest.json"
CONFIG=ROOT/"tools/speech/voice.json"
OUTPUT=ROOT/"Content/Voice"
INDEX=OUTPUT/"catalog.json"
MODEL_FILES=["ve.safetensors","t3_cfg.safetensors","s3gen.safetensors","tokenizer.json","conds.pt"]
def digest(data): return hashlib.sha256(data).hexdigest()
def canonical(value): return json.dumps(value,sort_keys=True,separators=(",",":"),ensure_ascii=False).encode()
def file_hash(path):
    h=hashlib.sha256()
    with path.open("rb") as f:
        for b in iter(lambda:f.read(1024*1024),b""):h.update(b)
    return h.hexdigest()
def save(path,data):
    path.parent.mkdir(parents=True,exist_ok=True)
    temp=path.with_suffix(path.suffix+".tmp")
    temp.write_text(json.dumps(data,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    for attempt in range(20):
        try:temp.replace(path);break
        except PermissionError:
            if attempt==19:raise
            time.sleep(.05)
def recipe(entry,config):
    data=dict(text=entry["text"],generationText=entry.get("generationText",entry["text"]),trimPrefix=entry.get("trimPrefix"),voice=config,generator=2)
    if entry.get("seedOffset"):data["seedOffset"]=entry["seedOffset"]
    if entry.get("trimAligner"):data["trimAligner"]=entry["trimAligner"]
    return digest(canonical(data))
def verify(entries,config,index):
    errors=[]
    for e in entries:
        item=index.get(e["id"],{});path=OUTPUT/("speech-"+e["id"]+".wav")
        if item.get("recipe")!=recipe(e,config) or not path.is_file() or item.get("sha256")!=file_hash(path):
            errors.append(e["id"]);continue
        with wave.open(str(path),"rb") as w:
            if w.getnchannels()!=1 or w.getsampwidth()!=2 or w.getnframes()==0:errors.append(e["id"])
    if errors:raise SystemExit(f"Missing/stale/invalid speech assets ({len(errors)}): "+", ".join(errors[:20]))
    print(f"Verified {len(entries)} speech assets and their recipe/file hashes.")
def sample_reel(entries,records):
    # This review artifact can be refreshed after targeted repairs without loading TTS.
    import numpy as np
    import soundfile as sf
    ordered=[e for e in entries if e["id"] in records]
    pieces=[]
    for e in ordered:
        a,sr=sf.read(OUTPUT/records[e["id"]]["file"]);pieces.extend([a,np.zeros(int(sr*.45))])
    if not pieces:return
    sf.write(LOCAL/"sample-reel.wav",np.concatenate(pieces),sr,subtype="PCM_16")
    save(LOCAL/"sample-order.json",[dict(id=e["id"],text=e["text"]) for e in ordered])
    print("Sample reel: "+str(LOCAL/"sample-reel.wav"))

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("--samples",action="store_true")
    ap.add_argument("--ids",nargs="+")
    ap.add_argument("--verify",action="store_true")
    ap.add_argument("--device",choices=["auto","cuda","cpu"],default="auto")
    ap.add_argument("--limit",type=int)
    ap.add_argument("--keep-going",action="store_true")
    args=ap.parse_args()
    if not args.verify:
        from filelock import FileLock, Timeout
        import atexit
        LOCAL.mkdir(parents=True,exist_ok=True)
        lock=FileLock(str(LOCAL/"generation.lock"))
        try:lock.acquire(timeout=0)
        except Timeout:raise SystemExit("Another speech generator owns this catalog. Wait for it to finish or pause it between clips.")
        atexit.register(lock.release)
    config=json.loads(CONFIG.read_text(encoding="utf-8"));all_entries=json.loads(MANIFEST.read_text(encoding="utf-8"))["entries"]
    entries=[e for e in all_entries if (not args.samples or e.get("sample")) and (not args.ids or e["id"] in args.ids)]
    if args.ids and set(args.ids)-{e["id"] for e in entries}:raise SystemExit("Unknown or filtered speech ID")
    index=json.loads(INDEX.read_text(encoding="utf-8")) if INDEX.exists() else dict(schema=1,clips={})
    records=index["clips"]
    if args.verify:verify(entries,config,records);return
    metadata_changed=False
    for e in entries:
        if e["id"] in records and records[e["id"]].get("aliases")!=e["aliases"]:
            records[e["id"]]["aliases"]=e["aliases"];metadata_changed=True
    if metadata_changed:save(INDEX,index)
    pending=[e for e in entries if records.get(e["id"],{}).get("recipe")!=recipe(e,config) or not (OUTPUT/("speech-"+e["id"]+".wav")).is_file() or records.get(e["id"],{}).get("sha256")!=file_hash(OUTPUT/("speech-"+e["id"]+".wav"))]
    if args.limit is not None:pending=pending[:args.limit]
    if not pending:
        print("No missing or changed speech. No model loaded.")
        if args.samples:sample_reel(entries,records)
        return
    if not args.samples:
        approval=LOCAL/"sample-review.json"
        if not approval.exists() or json.loads(approval.read_text())["configHash"]!=digest(canonical(config)):
            raise SystemExit("Review the representative samples first; record the configuration hash in .local/speech/sample-review.json (see README).")
    import numpy as np
    import soundfile as sf
    import torch
    from huggingface_hub import snapshot_download
    from chatterbox.tts import ChatterboxTTS
    installed=importlib.metadata.version("chatterbox-tts")
    if config["package"]!="chatterbox-tts=="+installed:raise SystemExit("Pinned Chatterbox package mismatch")
    device="cuda" if args.device=="auto" and torch.cuda.is_available() else "cpu" if args.device=="auto" else args.device
    if device=="cuda" and not torch.cuda.is_available():raise SystemExit("CUDA was requested but torch.cuda.is_available() is false")
    torch.set_num_threads(4)
    print(json.dumps(dict(python=sys.version.split()[0],torch=torch.__version__,cudaAvailable=torch.cuda.is_available(),device=device,gpu=torch.cuda.get_device_name(0) if device=="cuda" else None)),flush=True)
    location=snapshot_download(config["model"],revision=config["revision"],allow_patterns=MODEL_FILES+["LICENSE"],token=False)
    model_hashes={f:file_hash(Path(location)/f) for f in MODEL_FILES}
    save(LOCAL/"model-lock.json",dict(repository=config["model"],revision=config["revision"],files=model_hashes))
    model=ChatterboxTTS.from_local(location,device=device)
    OUTPUT.mkdir(parents=True,exist_ok=True);masters=LOCAL/"masters";masters.mkdir(parents=True,exist_ok=True)
    start=time.monotonic()
    aligners={}
    cuda_dlls=os.add_dll_directory(str(Path(torch.__file__).parent/"lib")) if device=="cuda" and os.name=="nt" else None
    failures=[]
    save(LOCAL/"generation-failures.json",failures)
    for i,e in enumerate(pending):
        if (LOCAL/"pause-generation").exists():
            print("Paused between clips; completed assets retained.",flush=True);return
        try:
            seed=(config["seed"]+e.get("seedOffset",0)+int(digest(e["id"].encode())[:8],16))%2**32
            random.seed(seed);np.random.seed(seed);torch.manual_seed(seed)
            if device=="cuda":torch.cuda.manual_seed_all(seed)
            with torch.inference_mode(): audio=model.generate(e.get("generationText",e["text"]),**config["settings"]).detach().cpu().numpy().reshape(-1)
            if not np.isfinite(audio).all() or len(audio)<model.sr*.12 or len(audio)>model.sr*35 or np.max(np.abs(audio))<.003:
                raise RuntimeError("Invalid or suspicious audio for "+e["id"])
            if e.get("trimPrefix"):
                context=LOCAL/"context";context.mkdir(parents=True,exist_ok=True)
                sf.write(context/(e["id"]+".wav"),audio,model.sr,subtype="FLOAT")
                alignment_model=e.get("trimAligner","base.en")
                if alignment_model not in aligners:
                    from faster_whisper import WhisperModel
                    if device=="cuda":torch.cuda.empty_cache()
                    aligners[alignment_model]=WhisperModel(alignment_model,device=device,compute_type="float16" if device=="cuda" else "int8",cpu_threads=3,download_root=str(LOCAL/"audit-models"))
                aligner=aligners[alignment_model]
                from scipy.signal import resample_poly
                import re
                segments,_=aligner.transcribe(resample_poly(audio,2,3).astype(np.float32),language="en",word_timestamps=True,condition_on_previous_text=False,beam_size=3,max_new_tokens=64)
                words=[word for segment in segments for word in segment.words]
                names=[re.sub(r"[^a-z]","",w.word.lower()) for w in words]
                marker=e["trimPrefix"].split()[-1].lower()
                if marker not in names or names.index(marker)+1>=len(words):
                    raise RuntimeError("Cannot safely trim speech context for "+e["id"]+": "+str(names))
                save(context/(e["id"]+".json"),[dict(text=w.word,start=w.start,end=w.end,probability=w.probability) for w in words])
                prefix=words[names.index(marker)];following=words[names.index(marker)+1]
                begin=max(prefix.end,(prefix.end+following.start)*.5)
                audio=audio[int(begin*model.sr):]
            if len(audio)<model.sr*.12 or not np.isfinite(audio).all() or np.max(np.abs(audio))<.003:
                raise RuntimeError("Invalid or empty audio after context trim for "+e["id"])
            # Preserve model output and embedded watermark. Runtime PCM is supported by both engines.
            sf.write(str(masters/(e["id"]+".wav")),audio,model.sr,subtype="FLOAT")
            path=OUTPUT/("speech-"+e["id"]+".wav");temporary=path.with_suffix(".tmp.wav")
            sf.write(str(temporary),audio,model.sr,subtype="PCM_16");temporary.replace(path)
            records[e["id"]]=dict(file=path.name,text=e["text"],aliases=e["aliases"],recipe=recipe(e,config),sha256=file_hash(path),seconds=round(len(audio)/model.sr,3),sampleRate=model.sr,seed=seed,modelFiles=model_hashes)
            save(INDEX,index)
            print(f"[{i+1}/{len(pending)}] {e['id']} {len(audio)/model.sr:.2f}s; elapsed {time.monotonic()-start:.1f}s",flush=True)
        except Exception as error:
            failures.append(dict(id=e["id"],error=str(error)))
            save(LOCAL/"generation-failures.json",failures)
            print("FAILED "+e["id"]+": "+str(error),flush=True)
            if not args.keep_going:raise
    if args.samples:
        sample_reel(entries,records)
        print("Configuration hash: "+digest(canonical(config)))
    save(LOCAL/"generation-failures.json",failures)
    if failures:raise SystemExit(f"Generated available clips; {len(failures)} phrases require review. See .local/speech/generation-failures.json")
if __name__=="__main__":main()
