"""Inventory shipped speech only. Never reads private/family profiles."""
import csv, json, re, string
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
NUMBER_WORDS = "zero one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen".split()
TENS = "zero ten twenty thirty forty fifty sixty seventy eighty ninety".split()
def number(n):
    return NUMBER_WORDS[n] if n < 20 else "one hundred" if n == 100 else TENS[n // 10] + (" " + NUMBER_WORDS[n % 10] if n % 10 else "")
def normalize(text):
    return " ".join(text.lower().split()).rstrip(".!?")
def build():
    entries, aliases = {}, {}
    def add(id, text, source, *extra):
        key = normalize(text)
        if key in aliases:
            item = entries[aliases[key]]
            if source not in item["sources"]: item["sources"].append(source)
        else:
            if id in entries: raise ValueError("ID collision: " + id)
            item = entries[id] = dict(id=id, text=text, aliases=[], sources=[source])
        for alias in (text, *extra):
            key = normalize(alias)
            if key in aliases and aliases[key] != item["id"]: raise ValueError("Alias collision: " + alias)
            aliases[key] = item["id"]
            if key not in item["aliases"]: item["aliases"].append(key)
    letter_names = ["Ay", "Bee", "See", "Dee", "Ee", "Eff", "Gee", "Aitch", "Eye", "Jay", "Kay", "El", "Em", "En", "Oh", "Pee", "Cue", "Are", "Ess", "Tee", "You", "Vee", "Double you", "Ex", "Why", "Zee"]
    for c,spoken in zip(string.ascii_lowercase,letter_names):
        add("letter-" + c, c.upper(), "Keyboard letter names")
        entries["letter-"+c]["generationText"]="The letter. "+c.upper()+"."
        entries["letter-"+c]["trimPrefix"]="The letter"
        if c=="e":entries["letter-"+c]["seedOffset"]=17
        if c in "knv":
            entries["letter-"+c]["generationText"]="The letter. "+spoken+"."
            entries["letter-"+c]["seedOffset"]=23
    for n in range(101): add("number-" + str(n).zfill(3), number(n), "Counting and quantity games", str(n))
    fixed = {
        "ready":"Ready", "set":"Set", "go":"Go", "how-many":"How many?", "how-many-now":"How many now?",
        "hiding":"How many are hiding?", "fewer":"Which has fewer?", "more":"Which has more?",
        "count-together":"Let's count together.", "try-again":"Let's try that again.", "take-your-time":"Take your time. You can do it.",
        "well-done":"Well done!", "great-counting":"Great counting!", "bonus":"Bonus round! Triple points!",
        "bonus-correct":"Wonderful! Triple points!", "hundred":"Congratulations! You counted to one hundred!",
        "locked-hop":"First, play How Many Now. Practice joining and taking away.",
        "voice-preview":"Hello little explorer. Milk. Mommy. Let's play.",
        "ocean":"Follow the letters through the reef. Use the arrow keys to swim.",
        "bubble":"Pop a bubble for bonus points.", "racer":"Follow the road and collect the letters. Treasure chests change your car!",
        "bird":"Follow the letters through the sky. Use the arrow keys to fly.",
        "hop-help":"Watch the ball. Count the hops. Choose the number where it stops."
    }
    for id,text in fixed.items(): add("cue-"+id,text,"Game instructions and feedback")
    for n in range(11):
        word=number(n)
        for id,text in [("make", "Make "+word), ("add",word+" more"), ("subtract","Take away "+word),
                        ("hop-add",word+" more. Where will it land?"), ("hop-subtract","Take away "+word+". Where will it land?")]:
            add(id+"-"+str(n).zfill(2),text,"VisualMathGame / StudioGame.Math")
    source=(ROOT/"UnityPort/Assets/KeyLearner/Runtime/Games/CanvasGame.cs").read_text(encoding="utf-8-sig")
    icons=re.search(r"string\[\] Friendly = \{(.*?)\}",source).group(1)
    for icon in re.findall(r'"([^"]+)"', icons):
        add("icon-"+icon,icon.replace("face-","").replace("-"," "),"Default keyboard icon names")
    for name in ["app_dictionary.csv", "CPB_dictionary.csv", "custom_dictionary.csv"]:
        for row in csv.DictReader((ROOT/"data"/name).open(encoding="utf-8-sig")):
            word=row["word"].strip().lower()
            if word: add("word-"+re.sub(r"[^a-z0-9]+","-",word),word,"data/"+name)
    samples=["number-000","number-001","number-007","number-012","number-042","number-100","letter-a","letter-w",
             "cue-how-many","make-05","hop-subtract-02","cue-well-done","cue-great-counting","cue-try-again",
             "cue-take-your-time","cue-hundred","cue-voice-preview","cue-locked-hop","cue-ocean","cue-hop-help"]
    for id in samples: entries[id]["sample"]=True
    override_path=ROOT/"tools/speech/pronunciation_overrides.json"
    if override_path.exists():
        for id,change in json.loads(override_path.read_text(encoding="utf-8")).items():
            if id not in entries or set(change)-{"generationText","trimPrefix","seedOffset","trimAligner"}:raise ValueError("Invalid pronunciation override: "+id)
            entries[id].update(change)
    return dict(schema=1, entries=sorted(entries.values(),key=lambda x:x["id"]))
if __name__ == "__main__":
    manifest=build()
    (ROOT/"tools/speech/manifest.json").write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    print(f"Inventoried {len(manifest['entries'])} unique clips; 20 representative samples.")
