"""Development-only audit queue. Short outputs require review, not silent acceptance."""
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
clips=json.loads((ROOT/"Content/Voice/catalog.json").read_text(encoding="utf-8"))["clips"]
suspects=[dict(id=id,text=c["text"],seconds=c["seconds"],sha256=c["sha256"]) for id,c in clips.items()
    if (len(c["text"])>=4 and c["seconds"]<.32) or (len(c["text"])<20 and c["seconds"]>5)]
p=ROOT/".local/speech/short-clip-review.json";p.parent.mkdir(parents=True,exist_ok=True)
p.write_text(json.dumps(suspects,indent=2)+"\n",encoding="utf-8")
print(f"{len(suspects)} clips flagged for pronunciation/length review: {p}")
