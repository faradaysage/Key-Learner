"""Inspect the actual ARMv7 production APK and emit a measured size inventory."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("apk", type=Path)
parser.add_argument("--aapt")
parser.add_argument("--output", type=Path)
args = parser.parse_args()
aapt = args.aapt or shutil.which("aapt") or shutil.which("aapt.exe")
if not aapt:
    roots = [os.environ.get("ANDROID_HOME", ""), os.environ.get("ANDROID_SDK_ROOT", ""),
             r"C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK"]
    for root in filter(None, roots):
        candidates = sorted(Path(root).glob("build-tools/*/aapt*"), reverse=True)
        aapt = next((str(p) for p in candidates if p.name in ("aapt", "aapt.exe")), None)
        if aapt:
            break
if not aapt:
    raise SystemExit("Pass --aapt pointing to Android SDK build-tools/aapt")
badging = subprocess.check_output([aapt, "dump", "badging", str(args.apk)], text=True, encoding="utf-8")
manifest = subprocess.check_output([aapt, "dump", "xmltree", str(args.apk), "AndroidManifest.xml"], text=True, encoding="utf-8")
if "package: name='org.keylearner.app'" not in badging:
    raise SystemExit("Unexpected package ID")
if "sdkVersion:'29'" not in badging or "targetSdkVersion:'36'" not in badging:
    raise SystemExit("Unexpected minimum/target SDK")
if "native-code: 'armeabi-v7a'" not in badging:
    raise SystemExit("Expected ARMv7-only native code")
if "com.google.android.gms" in manifest or "com.android.vending" in manifest:
    raise SystemExit("Unexpected Google Play dependency")
permissions = re.findall(r"uses-permission: name='([^']+)'", badging)
if any(p in permissions for p in ("android.permission.INTERNET", "android.permission.WRITE_EXTERNAL_STORAGE", "android.permission.READ_EXTERNAL_STORAGE")):
    raise SystemExit("Unexpected network or shared-storage permission")
build_tools = Path(aapt).resolve().parent
signer = build_tools / "lib/apksigner.jar"
java = build_tools.parents[1].parent / "OpenJDK/bin/java.exe"
java = str(java) if java.exists() else shutil.which("java")
if not signer.exists() or not java:
    raise SystemExit("APK signature validation requires build-tools/lib/apksigner.jar and Java")
signature = subprocess.check_output([java, "-jar", str(signer), "verify", "--verbose", "--print-certs", str(args.apk)], text=True, encoding="utf-8")
zipalign = build_tools / ("zipalign.exe" if os.name == "nt" else "zipalign")
subprocess.run([str(zipalign), "-c", "-P", "16", "4", str(args.apk)], check=True)
with zipfile.ZipFile(args.apk) as archive:
    if archive.testzip():
        raise SystemExit("APK ZIP checksum failure")
    entries = archive.infolist()
    libraries = [e for e in entries if e.filename.startswith("lib/") and e.filename.endswith(".so")]
    if not libraries or not any(e.filename.endswith("/libunity.so") for e in libraries):
        raise SystemExit("Missing Unity native payload")
    for entry in libraries:
        if not entry.filename.startswith("lib/armeabi-v7a/"):
            raise SystemExit("Unexpected native ABI: " + entry.filename)
        header = archive.read(entry)[:20]
        if header[:4] != b"\x7fELF" or header[4:6] != b"\x01\x01" or struct.unpack_from("<H", header, 18)[0] != 40:
            raise SystemExit("Expected 32-bit little-endian ARM ELF: " + entry.filename)
    forbidden = [e.filename for e in entries if e.filename.lower().endswith(".wav") or
                 "/streamingassets/platform/" in e.filename.lower() or "/content/voice/" in e.filename.lower()]
    if forbidden:
        raise SystemExit("Raw audio/Windows staging unexpectedly packaged: " + repr(forbidden[:5]))
    report = {
        "apk": args.apk.name, "sha256": hashlib.sha256(args.apk.read_bytes()).hexdigest(),
        "apkBytes": args.apk.stat().st_size,
        "uncompressedArchiveBytes": sum(e.file_size for e in entries),
        "nativeLibraryBytes": sum(e.file_size for e in libraries),
        "permissions": permissions, "badging": badging, "manifest": manifest,
        "signatureVerification": signature, "zipAlignmentVerified": True,
        "largestEntries": [{"path": e.filename, "compressedBytes": e.compress_size, "uncompressedBytes": e.file_size}
                           for e in sorted(entries, key=lambda e: e.compress_size, reverse=True)[:40]],
        "nativeLibraries": [{"path": e.filename, "compressed": e.compress_type != zipfile.ZIP_STORED,
                            "uncompressedBytes": e.file_size} for e in libraries],
        "installedSizeNote": "Archive sizes are not installed size; measure APK/native/dex/app data on the test device."
    }
output = args.output or args.apk.with_suffix(".inspection.json")
output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(f"PASS ARMv7 APK structure: {report['apkBytes']:,} bytes; {report['sha256']}")
print(f"Inspection: {output}")
