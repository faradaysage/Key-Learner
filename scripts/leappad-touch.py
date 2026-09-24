"""Record touch-only emulator checks against an explicitly selected Android device.

This helper never injects keyboard events. Screenshots and a JSONL action journal
remain in the selected evidence directory. It does not label a runtime gate passed.
"""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import subprocess
import time

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--adb', required=True)
p.add_argument('--serial', required=True)
p.add_argument('--output', type=Path, default=Path('artifacts/leappad-runtime'))
actions = p.add_subparsers(dest='action', required=True)
tap = actions.add_parser('tap'); tap.add_argument('x', type=int); tap.add_argument('y', type=int)
swipe = actions.add_parser('swipe')
for name in ('x', 'y', 'end_x', 'end_y', 'milliseconds'): swipe.add_argument(name, type=int)
hold = actions.add_parser('hold'); hold.add_argument('x', type=int); hold.add_argument('y', type=int); hold.add_argument('milliseconds', type=int)
capture = actions.add_parser('capture'); capture.add_argument('name')
actions.add_parser('status')
a = p.parse_args()
a.output.mkdir(parents=True, exist_ok=True)

def adb(*args):
    deadline = time.monotonic() + 30
    while True:
        result = subprocess.run([a.adb, '-s', a.serial, *map(str, args)], capture_output=True, timeout=40)
        if result.returncode == 0: return result.stdout
        error = result.stderr.decode('utf-8', errors='replace')
        if time.monotonic() >= deadline or not any(s in error for s in ('offline', 'not found', 'daemon')):
            raise RuntimeError(error)
        time.sleep(1)

entry = {'utc': datetime.now(timezone.utc).isoformat(), 'serial': a.serial, 'action': a.action}
if a.action == 'tap':
    # Give the 30 fps player a realistic brief contact, rather than adb's
    # near-zero-duration down/up pair which can fall between frames.
    adb('shell', 'input', 'swipe', a.x, a.y, a.x, a.y, 120)
    entry.update(x=a.x, y=a.y, milliseconds=120)
elif a.action in ('hold', 'swipe'):
    if not 1 <= a.milliseconds <= 30000: p.error('Gesture duration must be 1..30000 milliseconds')
    end_x = a.x + 1 if a.action == 'hold' else a.end_x
    end_y = a.y if a.action == 'hold' else a.end_y
    adb('shell', 'input', 'swipe', a.x, a.y, end_x, end_y, a.milliseconds)
    entry.update(x=a.x, y=a.y, end_x=end_x, end_y=end_y, milliseconds=a.milliseconds)
elif a.action == 'capture':
    if Path(a.name).name != a.name or not a.name.endswith('.png'): p.error('Use a simple .png filename')
    target = a.output / a.name
    target.write_bytes(adb('exec-out', 'screencap', '-p'))
    entry['screenshot'] = str(target.resolve())
else:
    entry['api'] = adb('shell', 'getprop', 'ro.build.version.sdk').decode().strip()
    entry['abis'] = adb('shell', 'getprop', 'ro.product.cpu.abilist').decode().strip()
    entry['nativeBridge'] = adb('shell', 'getprop', 'ro.dalvik.vm.native.bridge').decode().strip()
    entry['display'] = adb('shell', 'wm', 'size').decode().strip()
    entry['memory'] = adb('shell', 'cat', '/proc/meminfo').decode().splitlines()[0]
    # pidof returns exit 1 for a stopped package; avoid treating that as a transport error.
    entry['process'] = adb('shell', 'ps', '-A').decode().splitlines()
    entry['process'] = [line for line in entry['process'] if 'org.keylearner.app' in line]
with (a.output / 'touch-actions.jsonl').open('a', encoding='utf-8') as journal:
    journal.write(json.dumps(entry) + '\n')
print(json.dumps(entry))
