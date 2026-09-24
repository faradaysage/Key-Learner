"""Sample PSS for one package on an explicitly selected Android test device."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import time

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--adb',required=True)
p.add_argument('--serial',required=True)
p.add_argument('--package',default='org.keylearner.app')
p.add_argument('--seconds',type=int,default=600)
p.add_argument('--interval',type=float,default=2)
p.add_argument('--output',type=Path,required=True)
a=p.parse_args()
a.output.parent.mkdir(parents=True,exist_ok=True)
rows=[]
started=time.monotonic()
def adb(*args):
 return subprocess.run([a.adb,'-s',a.serial,*args],capture_output=True,text=True,timeout=15).stdout
try:
 with a.output.with_suffix('.raw.log').open('w',encoding='utf-8') as raw:
  while time.monotonic()-started<a.seconds:
   elapsed=round(time.monotonic()-started,3)
   pid=adb('shell','pidof',a.package).strip()
   report=adb('shell','dumpsys','meminfo',a.package) if pid else ''
   raw.write(f'\n=== {elapsed}s pid={pid} ===\n{report}');raw.flush()
   total=re.search(r'TOTAL PSS:\s*(\d+)',report) or re.search(r'^\s*TOTAL\s+(\d+)',report,re.M)
   native=re.search(r'^\s*Native Heap\s+(\d+)',report,re.M)
   rows.append({'seconds':elapsed,'pid':pid,'pssKiB':int(total[1]) if total else None,'nativeHeapPssKiB':int(native[1]) if native else None})
   summary={'serial':a.serial,'package':a.package,'samples':rows,'peakPssKiB':max((r['pssKiB'] or 0 for r in rows),default=0)}
   a.output.write_text(json.dumps(summary,indent=2),encoding='utf-8')
   time.sleep(max(.2,a.interval))
except KeyboardInterrupt:
 pass
print(f'Recorded {len(rows)} samples to {a.output}')
