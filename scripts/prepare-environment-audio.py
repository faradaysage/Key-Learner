"""Explicit developer preparation for the additional wave and learning-music recordings.
Requires requests/numpy/scipy/soundfile in a local developer environment. Never called by builds.
The committed source ledger supplies immutable source hashes and licenses.
"""
from pathlib import Path
from urllib.parse import urlsplit
from math import gcd
import hashlib,json,shutil
import numpy as np,requests,soundfile as sf
from scipy.signal import resample_poly
ROOT=Path(__file__).resolve().parents[1]
ledger=ROOT/"Content/Sounds/IMMERSION_SOURCES.json"
entries=json.loads(ledger.read_text(encoding="utf-8"))
cache=ROOT/"_asset_downloads/EnvironmentAudio";cache.mkdir(parents=True,exist_ok=True)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
for name in ["ocean-wave-1","ocean-wave-2","ocean-wave-3","ocean-wave-4","learning-home"]:
 row=entries[name];recipe=dict(schema=1,rate=24000,format="PCM_16",channels=1,peak=.65,fadeSeconds=.12 if name=="learning-home" else .1)
 output=ROOT/"Content/Sounds"/(name+".wav")
 if output.exists() and row.get("preparation")==recipe and sha(output)==row["outputSha256"]:
  print("Unchanged: "+name);continue
 url=row["download"]["url"];filename=Path(urlsplit(url).path).name;source=cache/filename
 if not source.exists():
  for folder in ["HomeMusic","OceanWaves"]:
   existing=ROOT/"_asset_downloads"/folder/filename
   if existing.exists():shutil.copyfile(existing,source);break
 if not source.exists():
  response=requests.get(url,timeout=150);response.raise_for_status();data=response.content
  if hashlib.sha256(data).hexdigest()!=row["download"]["sha256"]:raise ValueError("Downloaded source hash changed: "+name)
  source.write_bytes(data)
 if sha(source)!=row["download"]["sha256"]:raise ValueError("Cached source hash changed: "+name)
 audio,rate=sf.read(source,always_2d=True);audio=audio.mean(axis=1);factor=gcd(rate,24000)
 audio=resample_poly(audio,24000//factor,rate//factor);audio*=.65/max(float(np.max(np.abs(audio))),.001)
 fade=min(round(24000*recipe["fadeSeconds"]),len(audio)//4)
 audio[:fade]*=np.linspace(0,1,fade);audio[-fade:]*=np.linspace(1,0,fade)
 sf.write(output,audio,24000,subtype="PCM_16")
 row["preparation"]=recipe;row["outputSha256"]=sha(output);row["seconds"]=round(len(audio)/24000,3)
 print("Prepared: "+name)
ledger.write_text(json.dumps(entries,indent=2)+"\n",encoding="utf-8")
