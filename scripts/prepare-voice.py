"""Prepare local neural clips once; subsequent game playback needs no synthesis."""
import argparse
import pathlib
import wave
from piper import PiperVoice

parser = argparse.ArgumentParser()
parser.add_argument("--model", required=True)
parser.add_argument("--sample")
args = parser.parse_args()
model = pathlib.Path(args.model).resolve()
voice = PiperVoice.load(str(model))
clips = pathlib.Path(str(model) + ".clips")
clips.mkdir(exist_ok=True)
words = list("abcdefghijklmnopqrstuvwxyz") + [str(n) for n in range(1, 21)] + "milk mom mommy dad daddy cat dog sun moon star rain fish bird bear tree apple happy love ball book blue red green yellow".split()
for index, text in enumerate(words):
    output = clips / (text + ".wav")
    if not output.exists():
        with wave.open(str(output), "wb") as audio:
            voice.synthesize_wav(text.upper() + "." if len(text) == 1 and text.isalpha() else text, audio)
    print(f"{index + 1}/{len(words)} {text}", flush=True)
if args.sample:
    with wave.open(args.sample, "wb") as audio:
        voice.synthesize_wav("Hello, little explorer. Milk. Mommy. A little touch. A little wonder. Let's play!", audio)
