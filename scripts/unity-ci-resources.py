"""Record numeric CI host resource counters without reading logs, processes or secrets."""
import argparse
import datetime
import json
import pathlib
import shutil


def counters(path, names):
    values = {}
    for line in pathlib.Path(path).read_text(encoding="ascii").splitlines():
        parts = line.replace(":", "").split()
        if len(parts) >= 2 and parts[0] in names and parts[1].isdigit():
            values[parts[0]] = int(parts[1])
    return values


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("stage", choices=("before-tests", "after-tests", "after-build"))
    args = parser.parse_args()
    disk = shutil.disk_usage(".")
    report = {
        "stage": args.stage,
        "atUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "memoryKiB": counters("/proc/meminfo", {"MemTotal", "MemAvailable", "SwapTotal", "SwapFree"}),
        "kernelCounters": counters("/proc/vmstat", {"oom_kill"}),
        "diskFreeBytes": disk.free,
    }
    output = pathlib.Path("artifacts/unity-ci-resources")
    output.mkdir(parents=True, exist_ok=True)
    baseline = output / "before-tests.json"
    if args.stage != "before-tests" and baseline.exists():
        before = json.loads(baseline.read_text(encoding="utf-8"))
        report["hostOomKillDelta"] = report["kernelCounters"].get("oom_kill", 0) - before["kernelCounters"].get("oom_kill", 0)
    encoded = json.dumps(report, indent=2) + "\n"
    (output / (args.stage + ".json")).write_text(encoded, encoding="utf-8")
    print(encoded, end="")


if __name__ == "__main__":
    main()
