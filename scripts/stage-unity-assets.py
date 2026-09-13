"""Stage vetted meshes/textures/licenses, never executable archive content.

This deliberately uses a fixed list of creator-published URLs and selected file
extensions. Original archives remain outside Unity so import is reviewable.
"""
from pathlib import Path
import hashlib
import json
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / "_asset_downloads"
TARGET = ROOT / "UnityPort/Assets/ThirdParty"
SOURCES = {
    "kenney_car-kit.zip": "https://kenney.nl/media/pages/assets/car-kit/1a312ec241-1775131960/kenney_car-kit.zip",
    "kenney_city-kit-suburban_20.zip": "https://kenney.nl/media/pages/assets/city-kit-suburban/2c871b7af2-1745479373/kenney_city-kit-suburban_20.zip",
    "kenney_city-kit-commercial_2.1.zip": "https://kenney.nl/media/pages/assets/city-kit-commercial/a742d900eb-1753115042/kenney_city-kit-commercial_2.1.zip",
    "stylized_nature_megakitstandard.zip": "https://opengameart.org/sites/default/files/stylized_nature_megakitstandard.zip",
    "animated_fish_quaternius.zip": "https://opengameart.org/sites/default/files/Animated%20Fish%20Pack%20by%20%40Quaternius.zip",
    "BirdPantherOne.png": "https://opengameart.org/sites/default/files/Bird_0.png",
    "cute-fish-license.txt": "https://drive.google.com/uc?export=download&id=1waDUmVd9Cs2leBNGKpyHMamqGkIoob5Q",
    "farm-buildings-license.txt": "https://drive.google.com/uc?export=download&id=1O_kX6fCUCRUsBCKrxpeQU2E0GSJQ6GAf",
    "BirdGonzalez.blend": "https://opengameart.org/sites/default/files/bird_0.blend",
    "BirdPantherOne.blend": "https://opengameart.org/sites/default/files/Bird.blend",
    "bird_mess110.tar.gz": "https://opengameart.org/sites/default/files/bird.tar.gz",
    "kenney_city-kit-roads.zip": "https://kenney.nl/media/pages/assets/city-kit-roads/74288c9459-1787042796/kenney_city-kit-roads.zip",
}

def download(name, url):
    path = STAGE / name
    if not path.exists():
        request = urllib.request.Request(url, headers={"User-Agent": "KeyLearner-Asset-Import/1.0"})
        with urllib.request.urlopen(request, timeout=60) as response:
            path.write_bytes(response.read())
    print(name, path.stat().st_size, hashlib.sha256(path.read_bytes()).hexdigest())

def import_members(archive_name, category, prefixes):
    imported = []
    base = TARGET / category
    with zipfile.ZipFile(STAGE / archive_name) as archive:
        for item in archive.infolist():
            if item.is_dir():
                continue
            relative = None
            for source_prefix, destination_prefix in prefixes:
                if item.filename.startswith(source_prefix):
                    tail = item.filename[len(source_prefix):]
                    relative = Path(destination_prefix) / tail
                    break
            if relative is None or relative.suffix.lower() not in {".fbx", ".png", ".txt"}:
                continue
            if ".." in relative.parts or relative.is_absolute():
                raise ValueError("Unsafe archive member: " + item.filename)
            destination = base / relative
            if not destination.resolve().is_relative_to(base.resolve()):
                raise ValueError("Archive member escaped destination")
            data = archive.read(item)
            destination.parent.mkdir(parents=True, exist_ok=True)
            if not destination.exists() or destination.read_bytes() != data:
                destination.write_bytes(data)
            imported.append(str(destination.relative_to(ROOT)).replace("\\", "/"))
    return imported

def main():
    STAGE.mkdir(exist_ok=True)
    for name, url in SOURCES.items():
        download(name, url)
    manifest = {}
    packs = [
        ("stylized_nature_megakitstandard.zip", "QuaterniusNature", [("FBX (Unity)/", "Models"), ("Textures/", "Textures"), ("License_Standard.txt", "License_Standard.txt")]),
        ("animated_fish_quaternius.zip", "QuaterniusFish", [("Animated Fish Pack by @Quaternius/FBX/", "Models"), ("Animated Fish Pack by @Quaternius/License.txt", "License.txt")]),
        ("kenney_car-kit.zip", "KenneyCars", [("Models/FBX format/", "Models"), ("License.txt", "License.txt")]),
        ("kenney_city-kit-suburban_20.zip", "KenneySuburban", [("Models/FBX format/", "Models"), ("Models/Textures/", "PaletteVariants"), ("License.txt", "License.txt")]),
        ("kenney_city-kit-commercial_2.1.zip", "KenneyCommercial", [("Models/FBX format/", "Models"), ("Models/Textures/", "PaletteVariants"), ("License.txt", "License.txt")]),
        ("kenney_city-kit-roads.zip", "KenneyRoads", [("Models/FBX format/", "Models"), ("License.txt", "License.txt")]),
    ]
    for archive_name, category, prefixes in packs:
        manifest[category] = {"archive": archive_name, "sha256": hashlib.sha256((STAGE / archive_name).read_bytes()).hexdigest(), "imported": import_members(archive_name, category, prefixes)}
        print(category, len(manifest[category]["imported"]), "selected source files")
    (STAGE / "import-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")

if __name__ == "__main__":
    main()
